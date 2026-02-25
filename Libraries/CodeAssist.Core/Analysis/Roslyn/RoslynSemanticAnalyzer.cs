using CodeAssist.Core.Models;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace CodeAssist.Core.Analysis.Roslyn;

/// <summary>
/// Roslyn-based semantic analyzer for C#.
/// Loads an MSBuild workspace, maintains a persistent compilation,
/// and enriches tree-sitter chunks with fully resolved type information.
/// </summary>
public sealed class RoslynSemanticAnalyzer : ISemanticAnalyzer, IDisposable
{
    private static readonly IReadOnlySet<string> Languages =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "csharp" };

    private static readonly Lock MsBuildLock = new();
    private static bool _msBuildRegistered;

    /// <summary>
    /// Display format for fully qualified names without "global::".
    /// </summary>
    private static readonly SymbolDisplayFormat QualifiedFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType);

    private readonly ILogger<RoslynSemanticAnalyzer> _logger;
    private MSBuildWorkspace? _workspace;
    private Solution? _solution;
    private readonly SemaphoreSlim _solutionLock = new(1, 1);

    public IReadOnlySet<string> SupportedLanguages => Languages;
    public bool IsReady { get; private set; }

    public RoslynSemanticAnalyzer(ILogger<RoslynSemanticAnalyzer> logger)
    {
        _logger = logger;
    }

    // ────────────────────────────────────────────────────────────────
    //  Initialization
    // ────────────────────────────────────────────────────────────────

    public async Task InitializeAsync(string projectPath, CancellationToken cancellationToken)
    {
        try
        {
            EnsureMsBuildRegistered();

            _workspace = MSBuildWorkspace.Create();
            _workspace.RegisterWorkspaceFailedHandler(e =>
                _logger.LogDebug("Workspace diagnostic: {Message}", e.Diagnostic.Message));

            string resolvedPath = ResolveProjectPath(projectPath);

            if (resolvedPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                _solution = await _workspace.OpenSolutionAsync(resolvedPath, cancellationToken: cancellationToken);
                _logger.LogInformation("Loaded solution {Path} with {Count} projects",
                    resolvedPath, _solution.Projects.Count());
            }
            else
            {
                Project project = await _workspace.OpenProjectAsync(resolvedPath, cancellationToken: cancellationToken);
                _solution = project.Solution;
                _logger.LogInformation("Loaded project {Path}", resolvedPath);
            }

            IsReady = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Roslyn workspace for {Path}", projectPath);
            IsReady = false;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Chunk Enrichment
    // ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<CodeChunk>> EnrichChunksAsync(
        string filePath,
        IReadOnlyList<CodeChunk> treeSitterChunks,
        CancellationToken cancellationToken)
    {
        if (_solution == null || !IsReady)
            return treeSitterChunks;

        Document? doc = FindDocument(filePath);
        if (doc == null)
            return treeSitterChunks;

        SemanticModel? semanticModel = await doc.GetSemanticModelAsync(cancellationToken);
        SyntaxNode? syntaxRoot = await doc.GetSyntaxRootAsync(cancellationToken);
        SourceText? sourceText = await doc.GetTextAsync(cancellationToken);

        if (semanticModel == null || syntaxRoot == null || sourceText == null)
            return treeSitterChunks;

        var enriched = new List<CodeChunk>(treeSitterChunks.Count);

        foreach (CodeChunk chunk in treeSitterChunks)
        {
            try
            {
                CodeChunk result = EnrichSingleChunk(chunk, semanticModel, syntaxRoot, sourceText);
                enriched.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to enrich chunk {Symbol} at line {Line}",
                    chunk.SymbolName, chunk.StartLine);
                enriched.Add(chunk);
            }
        }

        return enriched;
    }

    private CodeChunk EnrichSingleChunk(
        CodeChunk chunk, SemanticModel model, SyntaxNode root, SourceText sourceText)
    {
        if (chunk.StartLine < 1 || chunk.StartLine > sourceText.Lines.Count)
            return chunk;

        int start = sourceText.Lines[chunk.StartLine - 1].Start;
        int end = chunk.EndLine <= sourceText.Lines.Count
            ? sourceText.Lines[chunk.EndLine - 1].End
            : sourceText.Lines[^1].End;
        TextSpan span = TextSpan.FromBounds(start, end);

        SyntaxNode? declNode = FindDeclarationNode(root, span);
        if (declNode == null)
            return chunk;

        ISymbol? symbol = model.GetDeclaredSymbol(declNode);
        if (symbol == null)
            return chunk;

        string? qualifiedName = symbol.ToDisplayString(QualifiedFormat);
        string? ns = symbol.ContainingNamespace is { IsGlobalNamespace: false } cns
            ? cns.ToDisplayString()
            : null;
        string? parentSymbol = symbol.ContainingType?.Name;
        string? accessModifier = GetAccessibility(symbol);
        List<string>? modifiers = GetModifiers(symbol);
        List<string>? attributes = GetAttributes(symbol);

        string? returnType = chunk.ReturnType;
        string? baseType = chunk.BaseType;
        List<string>? interfaces = null;
        List<ParameterInfo>? parameters = null;

        switch (symbol)
        {
            case IMethodSymbol ms:
                returnType = ms.ReturnsVoid ? "void" : ms.ReturnType.ToDisplayString(QualifiedFormat);
                parameters = ms.Parameters.Select(ToParameterInfo).ToList();
                break;

            case INamedTypeSymbol ts:
                baseType = ts.BaseType is { SpecialType: not SpecialType.System_Object }
                    ? ts.BaseType.ToDisplayString(QualifiedFormat)
                    : null;
                interfaces = ts.Interfaces.Select(i => i.ToDisplayString(QualifiedFormat)).ToList();
                break;

            case IPropertySymbol ps:
                returnType = ps.Type.ToDisplayString(QualifiedFormat);
                break;

            case IFieldSymbol fs:
                returnType = fs.Type.ToDisplayString(QualifiedFormat);
                break;
        }

        IReadOnlyList<CallReference>? enrichedCalls = EnrichCallReferences(chunk.CallsOut, declNode, model);

        return chunk with
        {
            QualifiedName = qualifiedName,
            Namespace = ns ?? chunk.Namespace,
            ParentSymbol = parentSymbol ?? chunk.ParentSymbol,
            AccessModifier = accessModifier ?? chunk.AccessModifier,
            Modifiers = modifiers ?? chunk.Modifiers,
            ReturnType = returnType ?? chunk.ReturnType,
            BaseType = baseType ?? chunk.BaseType,
            ImplementedInterfaces = interfaces is { Count: > 0 } ? interfaces : chunk.ImplementedInterfaces,
            Parameters = parameters is { Count: > 0 } ? parameters : chunk.Parameters,
            CallsOut = enrichedCalls ?? chunk.CallsOut,
            Attributes = attributes is { Count: > 0 } ? attributes : chunk.Attributes
        };
    }

    private IReadOnlyList<CallReference>? EnrichCallReferences(
        IReadOnlyList<CallReference>? treeSitterCalls,
        SyntaxNode declNode,
        SemanticModel model)
    {
        if (treeSitterCalls is not { Count: > 0 })
            return null;

        // Build lookup of Roslyn-resolved calls by method name
        var resolvedByNameAndLine = new Dictionary<string, IMethodSymbol>();
        var resolvedByName = new Dictionary<string, IMethodSymbol>();

        foreach (InvocationExpressionSyntax inv in declNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            SymbolInfo symbolInfo = model.GetSymbolInfo(inv);
            if (symbolInfo.Symbol is not IMethodSymbol ms) continue;

            int line = inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            resolvedByNameAndLine[$"{ms.Name}:{line}"] = ms;
            resolvedByName.TryAdd(ms.Name, ms);
        }

        var enriched = new List<CallReference>(treeSitterCalls.Count);
        foreach (CallReference call in treeSitterCalls)
        {
            if (resolvedByNameAndLine.TryGetValue($"{call.MethodName}:{call.Line}", out IMethodSymbol? match)
                || resolvedByName.TryGetValue(call.MethodName, out match))
            {
                enriched.Add(call with
                {
                    ReceiverType = match.ContainingType?.ToDisplayString(QualifiedFormat),
                    QualifiedName = match.ToDisplayString(QualifiedFormat)
                });
            }
            else
            {
                enriched.Add(call);
            }
        }

        return enriched;
    }

    // ────────────────────────────────────────────────────────────────
    //  Dependency Mapping (DI Registration Extraction)
    // ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DependencyMapping>> ExtractDependencyMappingsAsync(
        CancellationToken cancellationToken)
    {
        if (_solution == null || !IsReady)
            return [];

        var mappings = new List<DependencyMapping>();

        foreach (Project project in _solution.Projects)
        {
            foreach (Document doc in project.Documents)
            {
                string? fileName = Path.GetFileName(doc.FilePath);
                if (fileName == null) continue;

                // Only scan DI registration files
                if (!fileName.Equals("Program.cs", StringComparison.OrdinalIgnoreCase) &&
                    !fileName.Equals("Startup.cs", StringComparison.OrdinalIgnoreCase) &&
                    !fileName.Contains("ServiceCollection", StringComparison.OrdinalIgnoreCase) &&
                    !fileName.Contains("DependencyInjection", StringComparison.OrdinalIgnoreCase))
                    continue;

                SemanticModel? model = await doc.GetSemanticModelAsync(cancellationToken);
                SyntaxNode? root = await doc.GetSyntaxRootAsync(cancellationToken);
                if (model == null || root == null) continue;

                foreach (InvocationExpressionSyntax inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    DependencyMapping? mapping = TryExtractDiMapping(inv, model);
                    if (mapping != null)
                        mappings.Add(mapping);
                }
            }
        }

        return mappings;
    }

    private DependencyMapping? TryExtractDiMapping(InvocationExpressionSyntax inv, SemanticModel model)
    {
        if (inv.Expression is not MemberAccessExpressionSyntax memberAccess)
            return null;

        string methodName = memberAccess.Name is GenericNameSyntax gns
            ? gns.Identifier.Text
            : memberAccess.Name.ToString();

        string? lifetime = methodName switch
        {
            "AddScoped" or "AddKeyedScoped" => "Scoped",
            "AddSingleton" or "AddKeyedSingleton" => "Singleton",
            "AddTransient" or "AddKeyedTransient" => "Transient",
            "AddHostedService" => "Singleton",
            _ => null
        };

        if (lifetime == null)
            return null;

        if (memberAccess.Name is GenericNameSyntax { TypeArgumentList.Arguments.Count: 2 } generic)
        {
            TypeSyntax interfaceType = generic.TypeArgumentList.Arguments[0];
            TypeSyntax concreteType = generic.TypeArgumentList.Arguments[1];

            ITypeSymbol? interfaceSymbol = model.GetTypeInfo(interfaceType).Type;
            ITypeSymbol? concreteSymbol = model.GetTypeInfo(concreteType).Type;

            return new DependencyMapping
            {
                InterfaceType = interfaceSymbol?.ToDisplayString(QualifiedFormat) ?? interfaceType.ToString(),
                ConcreteType = concreteSymbol?.ToDisplayString(QualifiedFormat) ?? concreteType.ToString(),
                Lifetime = lifetime
            };
        }

        return null;
    }

    // ────────────────────────────────────────────────────────────────
    //  HTTP Endpoint Extraction
    // ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<HttpEndpointInfo>> ExtractHttpEndpointsAsync(
        CancellationToken cancellationToken)
    {
        if (_solution == null || !IsReady)
            return [];

        var endpoints = new List<HttpEndpointInfo>();

        foreach (Project project in _solution.Projects)
        {
            foreach (Document doc in project.Documents)
            {
                SemanticModel? model = await doc.GetSemanticModelAsync(cancellationToken);
                SyntaxNode? root = await doc.GetSyntaxRootAsync(cancellationToken);
                if (model == null || root == null) continue;

                foreach (ClassDeclarationSyntax classDecl in
                    root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    INamedTypeSymbol? classSymbol = model.GetDeclaredSymbol(classDecl);
                    if (classSymbol == null || !IsControllerClass(classSymbol))
                        continue;

                    string? classRoute = GetRouteTemplateFromAttributes(classSymbol.GetAttributes());

                    foreach (MethodDeclarationSyntax method in
                        classDecl.Members.OfType<MethodDeclarationSyntax>())
                    {
                        IMethodSymbol? methodSymbol = model.GetDeclaredSymbol(method);
                        if (methodSymbol == null) continue;

                        foreach (AttributeData attr in methodSymbol.GetAttributes())
                        {
                            string? httpMethod = GetHttpMethodFromAttribute(attr);
                            if (httpMethod == null) continue;

                            string? methodRoute = GetRouteFromAttribute(attr);
                            string route = CombineRoutes(classRoute, methodRoute);

                            endpoints.Add(new HttpEndpointInfo
                            {
                                HttpMethod = httpMethod,
                                RouteTemplate = route,
                                Role = HttpEndpointRole.Server,
                                SymbolName = methodSymbol.Name,
                                QualifiedName = methodSymbol.ToDisplayString(QualifiedFormat),
                                FilePath = doc.FilePath,
                                Line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                            });
                        }
                    }
                }
            }
        }

        return endpoints;
    }

    // ────────────────────────────────────────────────────────────────
    //  Incremental File Update
    // ────────────────────────────────────────────────────────────────

    public async Task OnFileChangedAsync(string filePath, CancellationToken cancellationToken)
    {
        if (_solution == null || !IsReady)
            return;

        await _solutionLock.WaitAsync(cancellationToken);
        try
        {
            Document? doc = FindDocument(filePath);
            if (doc == null)
                return;

            string newContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            SourceText newText = SourceText.From(newContent);

            _solution = _solution.WithDocumentText(doc.Id, newText);
            _logger.LogDebug("Updated Roslyn solution for {File}", filePath);
        }
        finally
        {
            _solutionLock.Release();
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Helpers — Symbol Data Extraction
    // ────────────────────────────────────────────────────────────────

    private static ParameterInfo ToParameterInfo(IParameterSymbol p) => new()
    {
        Name = p.Name,
        Type = p.Type.ToDisplayString(QualifiedFormat),
        DefaultValue = p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null,
        IsOut = p.RefKind == RefKind.Out,
        IsRef = p.RefKind is RefKind.Ref or RefKind.In,
        IsParams = p.IsParams
    };

    private static string? GetAccessibility(ISymbol symbol) => symbol.DeclaredAccessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Private => "private",
        Accessibility.Protected => "protected",
        Accessibility.Internal => "internal",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        _ => null
    };

    private static List<string>? GetModifiers(ISymbol symbol)
    {
        var mods = new List<string>();
        if (symbol.IsStatic) mods.Add("static");
        if (symbol.IsAbstract) mods.Add("abstract");
        if (symbol.IsVirtual) mods.Add("virtual");
        if (symbol.IsOverride) mods.Add("override");
        if (symbol.IsSealed) mods.Add("sealed");

        if (symbol is IMethodSymbol ms)
        {
            if (ms.IsAsync) mods.Add("async");
            if (ms.IsExtern) mods.Add("extern");
        }

        if (symbol is IFieldSymbol fs)
        {
            if (fs.IsReadOnly) mods.Add("readonly");
            if (fs.IsVolatile) mods.Add("volatile");
        }

        if (symbol is INamedTypeSymbol ts && ts.IsRecord)
            mods.Add("record");

        return mods.Count > 0 ? mods : null;
    }

    private static List<string>? GetAttributes(ISymbol symbol)
    {
        var attrs = symbol.GetAttributes()
            .Where(a => a.AttributeClass != null)
            .Select(a => a.AttributeClass!.Name.EndsWith("Attribute")
                ? a.AttributeClass.Name[..^"Attribute".Length]
                : a.AttributeClass.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        return attrs.Count > 0 ? attrs : null;
    }

    // ────────────────────────────────────────────────────────────────
    //  Helpers — AST Navigation
    // ────────────────────────────────────────────────────────────────

    private static SyntaxNode? FindDeclarationNode(SyntaxNode root, TextSpan span)
    {
        SyntaxNode node = root.FindNode(span);

        // Walk up to find the nearest declaration
        SyntaxNode? current = node;
        while (current != null)
        {
            if (current is MemberDeclarationSyntax or BaseTypeDeclarationSyntax)
                return current;
            current = current.Parent;
        }

        return node is MemberDeclarationSyntax or BaseTypeDeclarationSyntax ? node : null;
    }

    private Document? FindDocument(string filePath)
    {
        if (_solution == null) return null;

        string normalized = Path.GetFullPath(filePath);
        foreach (Project project in _solution.Projects)
        {
            foreach (Document doc in project.Documents)
            {
                if (doc.FilePath != null &&
                    string.Equals(Path.GetFullPath(doc.FilePath), normalized, StringComparison.OrdinalIgnoreCase))
                    return doc;
            }
        }

        return null;
    }

    // ────────────────────────────────────────────────────────────────
    //  Helpers — HTTP Endpoint Detection
    // ────────────────────────────────────────────────────────────────

    private static bool IsControllerClass(INamedTypeSymbol classSymbol)
    {
        // Check for [ApiController] attribute
        if (classSymbol.GetAttributes().Any(a =>
            a.AttributeClass?.Name is "ApiControllerAttribute" or "ApiController"))
            return true;

        // Check inheritance from ControllerBase or Controller
        INamedTypeSymbol? baseType = classSymbol.BaseType;
        while (baseType != null)
        {
            string name = baseType.Name;
            if (name is "ControllerBase" or "Controller")
                return true;
            baseType = baseType.BaseType;
        }

        return false;
    }

    private static string? GetHttpMethodFromAttribute(AttributeData attr)
    {
        string? name = attr.AttributeClass?.Name;
        return name switch
        {
            "HttpGetAttribute" or "HttpGet" => "GET",
            "HttpPostAttribute" or "HttpPost" => "POST",
            "HttpPutAttribute" or "HttpPut" => "PUT",
            "HttpDeleteAttribute" or "HttpDelete" => "DELETE",
            "HttpPatchAttribute" or "HttpPatch" => "PATCH",
            "HttpHeadAttribute" or "HttpHead" => "HEAD",
            "HttpOptionsAttribute" or "HttpOptions" => "OPTIONS",
            _ => null
        };
    }

    private static string? GetRouteTemplateFromAttributes(
        System.Collections.Immutable.ImmutableArray<AttributeData> attributes)
    {
        foreach (AttributeData attr in attributes)
        {
            string? name = attr.AttributeClass?.Name;
            if (name is "RouteAttribute" or "Route" &&
                attr.ConstructorArguments.Length > 0 &&
                attr.ConstructorArguments[0].Value is string template)
            {
                return template;
            }
        }

        return null;
    }

    private static string? GetRouteFromAttribute(AttributeData attr)
    {
        if (attr.ConstructorArguments.Length > 0 &&
            attr.ConstructorArguments[0].Value is string template)
            return template;
        return null;
    }

    private static string CombineRoutes(string? classRoute, string? methodRoute)
    {
        if (string.IsNullOrEmpty(classRoute) && string.IsNullOrEmpty(methodRoute))
            return "/";

        if (string.IsNullOrEmpty(classRoute))
            return "/" + methodRoute!.TrimStart('/');

        if (string.IsNullOrEmpty(methodRoute))
            return "/" + classRoute.TrimStart('/');

        return "/" + classRoute.TrimStart('/').TrimEnd('/') + "/" + methodRoute.TrimStart('/');
    }

    // ────────────────────────────────────────────────────────────────
    //  Helpers — MSBuild & Project Resolution
    // ────────────────────────────────────────────────────────────────

    private static void EnsureMsBuildRegistered()
    {
        lock (MsBuildLock)
        {
            if (_msBuildRegistered) return;
            MSBuildLocator.RegisterDefaults();
            _msBuildRegistered = true;
        }
    }

    private static string ResolveProjectPath(string path)
    {
        if (File.Exists(path))
            return Path.GetFullPath(path);

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Project path not found: {path}");

        // Look for .sln first, then .csproj
        string[] slnFiles = Directory.GetFiles(path, "*.sln");
        if (slnFiles.Length == 1)
            return slnFiles[0];

        string[] csprojFiles = Directory.GetFiles(path, "*.csproj");
        if (csprojFiles.Length == 1)
            return csprojFiles[0];

        if (slnFiles.Length > 1)
            throw new InvalidOperationException(
                $"Multiple .sln files found in {path}. Specify the exact path.");

        if (csprojFiles.Length > 1)
            throw new InvalidOperationException(
                $"Multiple .csproj files found in {path}. Specify the exact path.");

        throw new FileNotFoundException(
            $"No .sln or .csproj file found in {path}");
    }

    // ────────────────────────────────────────────────────────────────
    //  Dispose
    // ────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _workspace?.Dispose();
        _solutionLock.Dispose();
    }
}
