package analyzer

import (
	"fmt"
	"go/ast"
	"go/token"
)

// GetSymbolsInput represents the input for symbol extraction
type GetSymbolsInput struct {
	Code   string `json:"code" jsonschema:"Go source code to analyze"`
	Filter string `json:"filter,omitempty" jsonschema:"Optional filter: 'function', 'type', 'const', 'var', or 'all'"`
}

// GetSymbolsOutput represents the result of symbol extraction
type GetSymbolsOutput struct {
	Success bool     `json:"success"`
	Symbols []Symbol `json:"symbols"`
	Count   int      `json:"count"`
	Error   string   `json:"error,omitempty"`
}

// Symbol represents a symbol in Go code
type Symbol struct {
	Name       string `json:"name"`
	Kind       string `json:"kind"` // "function", "type", "const", "var", "method", "struct", "interface"
	Line       int    `json:"line"`
	Column     int    `json:"column"`
	Signature  string `json:"signature,omitempty"`
	Receiver   string `json:"receiver,omitempty"` // For methods
	TypeName   string `json:"type_name,omitempty"` // For methods, fields
}

// GetSymbols extracts all symbols from Go code
func GetSymbols(code, filter string) (*GetSymbolsOutput, error) {
	file, fset, err := ParseAST(code)
	if err != nil {
		return &GetSymbolsOutput{
			Success: false,
			Error:   err.Error(),
		}, nil
	}

	symbols := []Symbol{}

	// Walk the AST
	ast.Inspect(file, func(n ast.Node) bool {
		switch decl := n.(type) {
		case *ast.FuncDecl:
			if filter == "" || filter == "all" || filter == "function" {
				sym := extractFunctionSymbol(decl, fset)
				symbols = append(symbols, sym)
			}

		case *ast.GenDecl:
			// Handle type, const, var declarations
			for _, spec := range decl.Specs {
				switch s := spec.(type) {
				case *ast.TypeSpec:
					if filter == "" || filter == "all" || filter == "type" {
						sym := extractTypeSymbol(s, fset)
						symbols = append(symbols, sym)
					}

				case *ast.ValueSpec:
					kind := "var"
					if decl.Tok == token.CONST {
						kind = "const"
					}
					if filter == "" || filter == "all" || filter == kind {
						syms := extractValueSymbols(s, kind, fset)
						symbols = append(symbols, syms...)
					}
				}
			}
		}
		return true
	})

	return &GetSymbolsOutput{
		Success: true,
		Symbols: symbols,
		Count:   len(symbols),
	}, nil
}

func extractFunctionSymbol(decl *ast.FuncDecl, fset *token.FileSet) Symbol {
	pos := fset.Position(decl.Pos())
	
	sym := Symbol{
		Name:   decl.Name.Name,
		Kind:   "function",
		Line:   pos.Line,
		Column: pos.Column,
	}

	// Check if it's a method
	if decl.Recv != nil && len(decl.Recv.List) > 0 {
		sym.Kind = "method"
		// Extract receiver type
		if field := decl.Recv.List[0]; field.Type != nil {
			sym.Receiver = fmt.Sprintf("%s", field.Type)
		}
	}

	// Build signature
	sig := decl.Name.Name + "("
	if decl.Type.Params != nil {
		for i, param := range decl.Type.Params.List {
			if i > 0 {
				sig += ", "
			}
			for _, name := range param.Names {
				sig += name.Name + " "
			}
			sig += fmt.Sprintf("%s", param.Type)
		}
	}
	sig += ")"

	// Add return types
	if decl.Type.Results != nil && len(decl.Type.Results.List) > 0 {
		sig += " "
		if len(decl.Type.Results.List) > 1 {
			sig += "("
		}
		for i, result := range decl.Type.Results.List {
			if i > 0 {
				sig += ", "
			}
			sig += fmt.Sprintf("%s", result.Type)
		}
		if len(decl.Type.Results.List) > 1 {
			sig += ")"
		}
	}

	sym.Signature = sig
	return sym
}

func extractTypeSymbol(spec *ast.TypeSpec, fset *token.FileSet) Symbol {
	pos := fset.Position(spec.Pos())
	
	kind := "type"
	switch spec.Type.(type) {
	case *ast.StructType:
		kind = "struct"
	case *ast.InterfaceType:
		kind = "interface"
	}

	return Symbol{
		Name:   spec.Name.Name,
		Kind:   kind,
		Line:   pos.Line,
		Column: pos.Column,
	}
}

func extractValueSymbols(spec *ast.ValueSpec, kind string, fset *token.FileSet) []Symbol {
	symbols := []Symbol{}
	
	for _, name := range spec.Names {
		pos := fset.Position(name.Pos())
		sym := Symbol{
			Name:   name.Name,
			Kind:   kind,
			Line:   pos.Line,
			Column: pos.Column,
		}
		
		if spec.Type != nil {
			sym.TypeName = fmt.Sprintf("%s", spec.Type)
		}
		
		symbols = append(symbols, sym)
	}
	
	return symbols
}
