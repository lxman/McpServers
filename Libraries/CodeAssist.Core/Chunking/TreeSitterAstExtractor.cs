using CodeAssist.Core.Models;
using TreeSitter;

namespace CodeAssist.Core.Chunking;

/// <summary>
/// Extracts enriched AST data from tree-sitter nodes across multiple languages.
/// Used by TreeSitterChunker to populate the extended CodeChunk fields (Phase 2).
/// </summary>
internal static class TreeSitterAstExtractor
{
    private static readonly HashSet<string> TypeDeclarationTypes =
    [
        // C#
        "class_declaration", "struct_declaration", "interface_declaration",
        "record_declaration", "enum_declaration",
        // Python
        "class_definition",
        // C/C++
        "class_specifier", "struct_specifier",
        // Rust
        "struct_item", "enum_item", "trait_item", "impl_item",
        // Ruby
        "class", "module",
        // Go
        "type_declaration"
    ];

    private static readonly HashSet<string> NamespaceNodeTypes =
    [
        "namespace_declaration", "file_scoped_namespace_declaration", // C#
        "package_declaration", // Java
        "namespace_definition", // PHP
        "mod_item" // Rust
    ];

    private static readonly HashSet<string> CallableNodeTypes =
    [
        "method_declaration", "constructor_declaration", "local_function_statement", // C#
        "function_definition", // Python
        "function_declaration", "method_definition", "arrow_function", // JS/TS
        "function_item", // Rust
        "method_declaration" // Go/Java share with C#
    ];

    private static readonly HashSet<string> AccessModifierKeywords =
        ["public", "private", "protected", "internal"];

    // ────────────────────────────────────────────────────────────────
    //  2g — Parent Symbol
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Walk up the tree to find the name of the enclosing type declaration.
    /// </summary>
    public static string? ExtractParentSymbolName(Node node)
    {
        Node? current = SafeParent(node);
        while (current != null)
        {
            if (TypeDeclarationTypes.Contains(current.Type))
            {
                Node? nameNode = SafeGetField(current, "name");
                if (nameNode != null)
                    return nameNode.Text;

                foreach (Node child in current.NamedChildren)
                {
                    if (child.Type is "identifier" or "type_identifier" or "name" or "constant")
                        return child.Text;
                }

                return null;
            }

            current = SafeParent(current);
        }

        return null;
    }

    // ────────────────────────────────────────────────────────────────
    //  2c — Namespace
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Walk up the tree to find the enclosing namespace or module.
    /// </summary>
    public static string? ExtractNamespace(Node node, string language)
    {
        Node? current = SafeParent(node);
        while (current != null)
        {
            if (NamespaceNodeTypes.Contains(current.Type))
            {
                Node? nameNode = SafeGetField(current, "name");
                if (nameNode != null)
                    return nameNode.Text;

                foreach (Node child in current.NamedChildren)
                {
                    if (child.Type is "identifier" or "qualified_name" or "name" or "scoped_identifier")
                        return child.Text;
                }
            }

            // At root level, check for sibling namespace declarations that don't
            // wrap their children (file-scoped namespaces in C#, package_clause in Go)
            if (current.Type is "compilation_unit" or "source_file")
            {
                foreach (Node child in current.NamedChildren)
                {
                    // C#: file_scoped_namespace_declaration is a sibling, not a parent
                    if (child.Type == "file_scoped_namespace_declaration")
                    {
                        Node? nameNode = SafeGetField(child, "name");
                        if (nameNode != null) return nameNode.Text;

                        foreach (Node gc in child.NamedChildren)
                        {
                            if (gc.Type is "identifier" or "qualified_name" or "name" or "scoped_identifier")
                                return gc.Text;
                        }
                    }

                    // Go: package_clause at the source_file level
                    if (language == "go" && child.Type == "package_clause")
                    {
                        Node? nameNode = SafeGetField(child, "name");
                        if (nameNode != null) return nameNode.Text;

                        foreach (Node gc in child.NamedChildren)
                        {
                            if (gc.Type == "package_identifier")
                                return gc.Text;
                        }
                    }
                }
            }

            current = SafeParent(current);
        }

        return null;
    }

    /// <summary>
    /// Build a qualified name from namespace, parent symbol, and symbol name.
    /// </summary>
    public static string? BuildQualifiedName(string? ns, string? parentSymbol, string? symbolName)
    {
        if (symbolName == null) return null;

        var parts = new List<string>(3);
        if (!string.IsNullOrEmpty(ns)) parts.Add(ns);
        if (!string.IsNullOrEmpty(parentSymbol)) parts.Add(parentSymbol);
        parts.Add(symbolName);

        return string.Join(".", parts);
    }

    // ────────────────────────────────────────────────────────────────
    //  2a — Parameters
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extract parameter info from a callable node (method, function, constructor).
    /// </summary>
    public static List<ParameterInfo>? ExtractParameters(Node node, string language)
    {
        if (!CallableNodeTypes.Contains(node.Type))
            return null;

        Node? paramList = SafeGetField(node, "parameters");
        if (paramList == null)
        {
            foreach (Node child in node.NamedChildren)
            {
                if (child.Type is "parameter_list" or "formal_parameters" or "parameters")
                {
                    paramList = child;
                    break;
                }
            }
        }

        if (paramList == null)
            return null;

        var parameters = new List<ParameterInfo>();

        foreach (Node paramNode in paramList.NamedChildren)
        {
            if (paramNode.Type is "parameter" or "formal_parameter" or "required_parameter"
                or "optional_parameter" or "rest_parameter" or "default_parameter"
                or "typed_parameter" or "typed_default_parameter"
                or "parameter_declaration" or "variadic_parameter")
            {
                ParameterInfo? p = ExtractSingleParameter(paramNode, language);
                if (p != null)
                    parameters.Add(p);
            }
            else if (paramNode.Type == "identifier" && language == "python")
            {
                parameters.Add(new ParameterInfo { Name = paramNode.Text });
            }
        }

        return parameters.Count > 0 ? parameters : null;
    }

    private static ParameterInfo? ExtractSingleParameter(Node paramNode, string language)
    {
        string? name = null;
        string? type = null;
        string? defaultValue = null;
        bool isOut = false, isRef = false, isParams = false;

        Node? nameNode = SafeGetField(paramNode, "name");
        Node? typeNode = SafeGetField(paramNode, "type");

        if (nameNode != null) name = nameNode.Text;
        if (typeNode != null) type = typeNode.Text;

        foreach (Node child in paramNode.NamedChildren)
        {
            if (name == null && child.Type is "identifier" or "name")
                name = child.Text;

            if (type == null && child.Type is "predefined_type" or "type_identifier" or "generic_name"
                or "nullable_type" or "array_type" or "type" or "type_annotation")
            {
                type = child.Text;
                if (child.Type == "type_annotation" && type.StartsWith(':'))
                    type = type[1..].Trim();
            }

            if (child.Type is "equals_value_clause" or "default_value")
                defaultValue = child.Text.TrimStart('=').Trim();
        }

        // Check non-named children for C# parameter modifiers
        if (language == "csharp")
        {
            foreach (Node child in paramNode.Children)
            {
                string text = child.Text;
                switch (text)
                {
                    case "out": isOut = true; break;
                    case "ref": isRef = true; break;
                    case "params": isParams = true; break;
                }
            }
        }

        return name == null
            ? null
            : new ParameterInfo
            {
                Name = name,
                Type = type,
                DefaultValue = defaultValue,
                IsOut = isOut,
                IsRef = isRef,
                IsParams = isParams
            };
    }

    // ────────────────────────────────────────────────────────────────
    //  2a — Return Type
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extract the return type from a callable node.
    /// </summary>
    public static string? ExtractReturnType(Node node, string language)
    {
        if (!CallableNodeTypes.Contains(node.Type))
            return null;

        if (node.Type is "constructor_declaration")
            return null;

        return language switch
        {
            "csharp" or "java" => ExtractReturnTypeCLike(node),
            "python" => ExtractReturnTypeAnnotation(node, "->"),
            "typescript" or "javascript" => ExtractReturnTypeAnnotation(node, ":"),
            "go" => SafeGetField(node, "result")?.Text,
            "rust" => ExtractReturnTypeAnnotation(node, "->"),
            _ => null
        };
    }

    private static string? ExtractReturnTypeCLike(Node node)
    {
        Node? typeNode = SafeGetField(node, "type");
        if (typeNode != null)
            return typeNode.Text;

        foreach (Node child in node.NamedChildren)
        {
            if (child.Type is "predefined_type" or "type_identifier" or "generic_name"
                or "nullable_type" or "array_type" or "void_keyword")
                return child.Text;

            if (child.Type is "identifier" or "name")
                break;
        }

        return null;
    }

    private static string? ExtractReturnTypeAnnotation(Node node, string prefix)
    {
        Node? rt = SafeGetField(node, "return_type");
        if (rt == null) return null;
        string text = rt.Text;
        return text.StartsWith(prefix) ? text[prefix.Length..].Trim() : text;
    }

    // ────────────────────────────────────────────────────────────────
    //  2b — Base Type
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extract the base type (superclass) from a type declaration node.
    /// </summary>
    public static string? ExtractBaseType(Node node, string language)
    {
        if (!TypeDeclarationTypes.Contains(node.Type))
            return null;

        return language switch
        {
            "csharp" => ExtractFirstBaseListItem(node),
            "java" => ExtractJavaSuperclass(node),
            "python" => ExtractPythonFirstSuperclass(node),
            "typescript" or "javascript" => ExtractTsExtendsClause(node),
            "rust" when node.Type == "impl_item" => SafeGetField(node, "trait")?.Text,
            _ => null
        };
    }

    private static string? ExtractFirstBaseListItem(Node node)
    {
        foreach (Node child in node.NamedChildren)
        {
            if (child.Type == "base_list")
            {
                foreach (Node bt in child.NamedChildren)
                {
                    if (bt.Type is "simple_base_type" or "identifier" or "type_identifier"
                        or "generic_name" or "qualified_name")
                        return bt.Text;
                }
            }
        }

        return null;
    }

    private static string? ExtractJavaSuperclass(Node node)
    {
        Node? sc = SafeGetField(node, "superclass");
        if (sc == null) return null;

        foreach (Node child in sc.NamedChildren)
        {
            if (child.Type is "type_identifier" or "generic_type" or "scoped_type_identifier")
                return child.Text;
        }

        return sc.Text;
    }

    private static string? ExtractPythonFirstSuperclass(Node node)
    {
        Node? sc = SafeGetField(node, "superclasses");
        if (sc != null && sc.NamedChildren.Count > 0)
            return sc.NamedChildren[0].Text;

        foreach (Node child in node.NamedChildren)
        {
            if (child.Type == "argument_list" && child.NamedChildren.Count > 0)
                return child.NamedChildren[0].Text;
        }

        return null;
    }

    private static string? ExtractTsExtendsClause(Node node)
    {
        foreach (Node child in node.NamedChildren)
        {
            if (child.Type == "class_heritage")
            {
                foreach (Node clause in child.NamedChildren)
                {
                    if (clause.Type == "extends_clause")
                    {
                        foreach (Node tn in clause.NamedChildren)
                        {
                            if (tn.Type is "identifier" or "type_identifier"
                                or "generic_type" or "member_expression")
                                return tn.Text;
                        }
                    }
                }
            }
        }

        return null;
    }

    // ────────────────────────────────────────────────────────────────
    //  2b — Implemented Interfaces
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extract implemented interfaces from a type declaration.
    /// For C# this returns all base_list items after the first (syntax-level heuristic).
    /// </summary>
    public static List<string>? ExtractImplementedInterfaces(Node node, string language)
    {
        if (!TypeDeclarationTypes.Contains(node.Type))
            return null;

        List<string> result = language switch
        {
            "csharp" => ExtractCSharpInterfaces(node),
            "java" => ExtractJavaInterfaces(node),
            "typescript" or "javascript" => ExtractTsImplements(node),
            "python" => ExtractPythonAdditionalBases(node),
            "rust" when node.Type == "impl_item" => ExtractRustTraitImpl(node),
            _ => []
        };

        return result.Count > 0 ? result : null;
    }

    private static List<string> ExtractCSharpInterfaces(Node node)
    {
        var result = new List<string>();
        foreach (Node child in node.NamedChildren)
        {
            if (child.Type == "base_list")
            {
                bool first = true;
                foreach (Node bt in child.NamedChildren)
                {
                    if (bt.Type is "simple_base_type" or "identifier" or "type_identifier"
                        or "generic_name" or "qualified_name")
                    {
                        if (first) { first = false; continue; }
                        result.Add(bt.Text);
                    }
                }
            }
        }

        return result;
    }

    private static List<string> ExtractJavaInterfaces(Node node)
    {
        var result = new List<string>();
        Node? ifaces = SafeGetField(node, "interfaces");
        if (ifaces == null) return result;

        foreach (Node child in ifaces.NamedChildren)
        {
            if (child.Type == "type_list")
            {
                foreach (Node tn in child.NamedChildren)
                    result.Add(tn.Text);
            }
            else if (child.Type is "type_identifier" or "generic_type" or "scoped_type_identifier")
            {
                result.Add(child.Text);
            }
        }

        return result;
    }

    private static List<string> ExtractTsImplements(Node node)
    {
        var result = new List<string>();
        foreach (Node child in node.NamedChildren)
        {
            if (child.Type == "class_heritage")
            {
                foreach (Node clause in child.NamedChildren)
                {
                    if (clause.Type == "implements_clause")
                    {
                        foreach (Node tn in clause.NamedChildren)
                        {
                            if (tn.Type is "identifier" or "type_identifier" or "generic_type")
                                result.Add(tn.Text);
                        }
                    }
                }
            }
        }

        return result;
    }

    private static List<string> ExtractPythonAdditionalBases(Node node)
    {
        var result = new List<string>();
        Node? sc = SafeGetField(node, "superclasses");
        IReadOnlyList<Node>? children = sc?.NamedChildren;
        if (children == null)
        {
            foreach (Node child in node.NamedChildren)
            {
                if (child.Type == "argument_list")
                {
                    children = child.NamedChildren;
                    break;
                }
            }
        }

        if (children != null)
        {
            for (int i = 1; i < children.Count; i++)
                result.Add(children[i].Text);
        }

        return result;
    }

    private static List<string> ExtractRustTraitImpl(Node node)
    {
        var result = new List<string>();
        Node? trait = SafeGetField(node, "trait");
        if (trait != null) result.Add(trait.Text);
        return result;
    }

    // ────────────────────────────────────────────────────────────────
    //  2f — Modifiers
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extract access modifier and other modifiers from a declaration node.
    /// </summary>
    public static (string? AccessModifier, List<string>? Modifiers) ExtractModifiers(
        Node node, string language)
    {
        string? access = null;
        var mods = new List<string>();

        switch (language)
        {
            case "csharp" or "java":
                ExtractModifiersCLike(node, ref access, mods);
                break;

            case "typescript" or "javascript":
                ExtractModifiersTs(node, ref access, mods);
                break;

            case "python":
                ExtractModifiersPython(node, mods);
                break;

            case "go":
                ExtractModifiersGo(node, ref access);
                break;

            case "rust":
                ExtractModifiersRust(node, ref access, mods);
                break;
        }

        return (access, mods.Count > 0 ? mods : null);
    }

    private static void ExtractModifiersCLike(Node node, ref string? access, List<string> mods)
    {
        foreach (Node child in node.Children)
        {
            if (child.Type is "modifier" or "modifiers")
            {
                if (child.Type == "modifiers")
                {
                    foreach (Node mod in child.NamedChildren)
                        ClassifyModifier(mod.Text, ref access, mods);
                }
                else
                {
                    ClassifyModifier(child.Text, ref access, mods);
                }
            }
        }
    }

    private static void ExtractModifiersTs(Node node, ref string? access, List<string> mods)
    {
        foreach (Node child in node.Children)
        {
            string text = child.Text;
            if (text is "export" or "default")
            {
                access ??= "public";
            }
            else if (text is "abstract" or "readonly" or "static" or "async" or "declare")
            {
                mods.Add(text);
            }

            if (child.Type == "accessibility_modifier")
                access = child.Text;
        }
    }

    private static void ExtractModifiersPython(Node node, List<string> mods)
    {
        foreach (Node child in node.Children)
        {
            if (child.Type == "decorator")
            {
                string text = child.Text.TrimStart('@');
                switch (text)
                {
                    case "staticmethod": mods.Add("static"); break;
                    case "classmethod": mods.Add("classmethod"); break;
                    case "abstractmethod": mods.Add("abstract"); break;
                    case "property": mods.Add("property"); break;
                }
            }
        }
    }

    private static void ExtractModifiersGo(Node node, ref string? access)
    {
        Node? nameNode = SafeGetField(node, "name");
        if (nameNode == null) return;
        string name = nameNode.Text;
        access = name.Length > 0 && char.IsUpper(name[0]) ? "public" : "private";
    }

    private static void ExtractModifiersRust(Node node, ref string? access, List<string> mods)
    {
        foreach (Node child in node.Children)
        {
            if (child.Type == "visibility_modifier")
                access = child.Text == "pub" ? "public" : child.Text;

            string text = child.Text;
            if (text is "async" or "unsafe" or "const")
                mods.Add(text);
        }
    }

    private static void ClassifyModifier(string text, ref string? access, List<string> mods)
    {
        if (AccessModifierKeywords.Contains(text))
            access = text;
        else if (text is "static" or "abstract" or "virtual" or "override" or "sealed"
                 or "readonly" or "async" or "extern" or "volatile" or "new" or "partial"
                 or "required" or "final" or "synchronized" or "native" or "transient"
                 or "default")
            mods.Add(text);
    }

    // ────────────────────────────────────────────────────────────────
    //  2f — Attributes / Decorators
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extract attributes or decorators from a declaration node.
    /// </summary>
    public static List<string>? ExtractAttributes(Node node, string language)
    {
        var attrs = new List<string>();

        switch (language)
        {
            case "csharp":
                ExtractAttributesCSharp(node, attrs);
                break;
            case "java":
                ExtractAttributesJava(node, attrs);
                break;
            case "python" or "typescript" or "javascript":
                ExtractDecorators(node, attrs);
                break;
            case "rust":
                ExtractAttributesRust(node, attrs);
                break;
        }

        return attrs.Count > 0 ? attrs : null;
    }

    private static void ExtractAttributesCSharp(Node node, List<string> attrs)
    {
        foreach (Node child in node.NamedChildren)
        {
            if (child.Type == "attribute_list")
            {
                foreach (Node attr in child.NamedChildren)
                {
                    if (attr.Type == "attribute")
                    {
                        Node? nameNode = SafeGetField(attr, "name");
                        attrs.Add(nameNode?.Text ?? attr.Text);
                    }
                }
            }
        }
    }

    private static void ExtractAttributesJava(Node node, List<string> attrs)
    {
        foreach (Node child in node.Children)
        {
            if (child.Type is "marker_annotation" or "annotation")
            {
                Node? nameNode = SafeGetField(child, "name");
                attrs.Add(nameNode?.Text ?? child.Text.TrimStart('@'));
            }

            if (child.Type == "modifiers")
            {
                foreach (Node mod in child.Children)
                {
                    if (mod.Type is "marker_annotation" or "annotation")
                    {
                        Node? nameNode = SafeGetField(mod, "name");
                        attrs.Add(nameNode?.Text ?? mod.Text.TrimStart('@'));
                    }
                }
            }
        }
    }

    private static void ExtractDecorators(Node node, List<string> attrs)
    {
        foreach (Node child in node.Children)
        {
            if (child.Type == "decorator")
            {
                string text = child.Text.TrimStart('@').Trim();
                int paren = text.IndexOf('(');
                if (paren > 0) text = text[..paren];
                attrs.Add(text);
            }
        }
    }

    private static void ExtractAttributesRust(Node node, List<string> attrs)
    {
        foreach (Node child in node.Children)
        {
            if (child.Type == "attribute_item")
            {
                string text = child.Text.TrimStart('#').Trim('[', ']');
                int paren = text.IndexOf('(');
                if (paren > 0) text = text[..paren];
                attrs.Add(text);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  2e — Field Access Tracking
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extract field/property accesses from a code node (syntax-level).
    /// Identifies member accesses in assignment contexts.
    /// </summary>
    public static List<FieldAccess>? ExtractFieldAccesses(Node node, string language)
    {
        var accesses = new List<FieldAccess>();
        var seen = new HashSet<string>();
        WalkForFieldAccesses(node, accesses, seen);
        return accesses.Count > 0 ? accesses : null;
    }

    private static void WalkForFieldAccesses(Node node, List<FieldAccess> accesses, HashSet<string> seen)
    {
        if (node.Type is "assignment_expression" or "augmented_assignment" or "compound_assignment_expr")
        {
            Node? left = node.FirstNamedChild;
            if (left != null && IsMemberAccess(left))
                AddFieldAccess(left, FieldAccessKind.Write, accesses, seen);
        }

        if (IsMemberAccess(node) && !IsCallTarget(node))
        {
            Node? parent = SafeParent(node);
            bool isWriteTarget = parent != null
                && parent.Type is "assignment_expression" or "augmented_assignment" or "compound_assignment_expr"
                && parent.FirstNamedChild?.Id == node.Id;

            if (!isWriteTarget)
                AddFieldAccess(node, FieldAccessKind.Read, accesses, seen);
        }

        foreach (Node child in node.NamedChildren)
            WalkForFieldAccesses(child, accesses, seen);
    }

    private static bool IsMemberAccess(Node node)
    {
        return node.Type is "member_access_expression" or "member_expression"
            or "field_expression" or "attribute";
    }

    private static bool IsCallTarget(Node node)
    {
        Node? parent = SafeParent(node);
        if (parent == null) return false;

        if (parent.Type is "invocation_expression" or "call_expression" or "call"
            or "method_invocation" or "method_call_expression" or "function_call_expression")
        {
            return parent.FirstNamedChild?.Id == node.Id;
        }

        return false;
    }

    private static void AddFieldAccess(
        Node memberNode, FieldAccessKind kind, List<FieldAccess> accesses, HashSet<string> seen)
    {
        Node? last = memberNode.LastNamedChild;
        if (last == null || last.Type is not ("identifier" or "property_identifier"
            or "field_identifier" or "name"))
            return;

        string fieldName = last.Text;
        Node? first = memberNode.FirstNamedChild;
        string? containingExpr = first != null && first.Id != last.Id ? first.Text : null;

        string key = $"{containingExpr}.{fieldName}:{kind}";
        if (!seen.Add(key)) return;

        accesses.Add(new FieldAccess
        {
            FieldName = fieldName,
            ContainingType = containingExpr,
            Kind = kind,
            Line = (int)memberNode.StartPosition.Row + 1
        });
    }

    // ────────────────────────────────────────────────────────────────
    //  2d — Receiver Expression for Calls
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extract the receiver expression from a call node.
    /// For "service.Process()" returns "service".
    /// </summary>
    public static string? ExtractReceiverExpression(Node callNode)
    {
        Node? firstChild = callNode.FirstNamedChild;
        if (firstChild == null || !IsMemberAccess(firstChild))
            return null;

        Node? receiver = firstChild.FirstNamedChild;
        return receiver?.Text;
    }

    // ────────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────────

    private static Node? SafeGetField(Node node, string fieldName)
    {
        try { return node.GetChildForField(fieldName); }
        catch { return null; }
    }

    private static Node? SafeParent(Node node)
    {
        try { return node.Parent; }
        catch { return null; }
    }
}
