import ts from 'typescript';
import { DiagnosticInfo } from '../models/DiagnosticInfo.js';
import { SymbolInfo } from '../models/SymbolInfo.js';
import { CodeMetrics } from '../models/CodeMetrics.js';
import * as path from 'path';
import * as fs from 'fs';

export class TypeScriptAnalyzer {
  private compilerOptions: ts.CompilerOptions;

  constructor() {
    this.compilerOptions = {
      target: ts.ScriptTarget.ES2020,
      module: ts.ModuleKind.ESNext,
      lib: ['es2020', 'dom'], // Include both ES2020 and DOM libraries
      strict: true,
      esModuleInterop: true,
      skipLibCheck: false,
      forceConsistentCasingInFileNames: true,
      moduleResolution: ts.ModuleResolutionKind.Bundler, // Changed from NodeNext
      noUnusedLocals: true, // Enable unused variable detection
      noUnusedParameters: true, // Enable unused parameter detection
    };
  }


  /**
   * Analyze TypeScript code and return diagnostics
   */

  /**
* Helper method to resolve lib file paths
* Handles cases like "dom" -> "lib.dom.d.ts"
*/
  private resolveLibFilePath(name: string): string {
    let resolvedPath = name;
    
    
    // Normalize path separators to handle both / and \
    const normalizedName = name.replace(/\\/g, '/');
    
    // Check if this is a lib file path (contains node_modules/typescript/lib)
    if (normalizedName.includes('node_modules/typescript/lib')) {
      const baseName = path.basename(name);
      // If the basename doesn't start with 'lib.', add it
      if (!baseName.startsWith('lib.')) {
        const libFileName = baseName.endsWith('.d.ts') 
          ? `lib.${baseName}` 
          : `lib.${baseName}.d.ts`;
        resolvedPath = path.join(path.dirname(name), libFileName);
      }
    } else if (!name.includes(path.sep) && !name.includes('/') && !name.startsWith('lib.') && !name.endsWith('.d.ts')) {
      // This is a short name like "dom" or "es2020"
      const libFileName = `lib.${name}.d.ts`;
      const tsLibPath = path.join(
        path.dirname(ts.getDefaultLibFilePath(this.compilerOptions)),
        libFileName
      );
      
      if (fs.existsSync(tsLibPath)) {
        resolvedPath = tsLibPath;
      }
    }
    
    return resolvedPath;
  }

  analyzeCode(code: string, fileName: string = 'temp.ts'): DiagnosticInfo[] {
    const sourceFile = ts.createSourceFile(
      fileName,
      code,
      ts.ScriptTarget.Latest,
      true
    );

    // Create a compiler host that can access lib files
    const host: ts.CompilerHost = {
      getSourceFile: (name, languageVersion) => {
        if (name === fileName) {
          return sourceFile;
        }
        
        // Resolve lib file paths
        const resolvedPath = this.resolveLibFilePath(name);
        
        // Try to read the file
        try {
          const libContent = ts.sys.readFile(resolvedPath);
          if (libContent) {
            return ts.createSourceFile(resolvedPath, libContent, languageVersion, true);
          }
        } catch (e) {
          // Ignore errors for lib files
        }
        return undefined;
      },
      writeFile: () => {},
      getCurrentDirectory: () => process.cwd(),
      getDirectories: ts.sys.getDirectories,
      fileExists: (name) => {
        const resolvedPath = this.resolveLibFilePath(name);
        return ts.sys.fileExists(resolvedPath);
      },
      readFile: (name) => {
        const resolvedPath = this.resolveLibFilePath(name);
        return ts.sys.readFile(resolvedPath);
      },
      getCanonicalFileName: (name) => name,
      useCaseSensitiveFileNames: () => true,
      getNewLine: () => '\n',
      getDefaultLibFileName: (options) => ts.getDefaultLibFilePath(options),
    };

    const program = ts.createProgram([fileName], this.compilerOptions, host);
    const allDiagnostics = ts.getPreEmitDiagnostics(program, sourceFile);

    return allDiagnostics.map((diagnostic) => this.formatDiagnostic(diagnostic, sourceFile));
  }


  /**
   * Get all symbols (classes, interfaces, functions, etc.) from the code
   */
  getSymbols(code: string, fileName: string = 'temp.ts', filter?: string): SymbolInfo[] {
    const sourceFile = ts.createSourceFile(
      fileName,
      code,
      ts.ScriptTarget.Latest,
      true
    );

    const symbols: SymbolInfo[] = [];
    
    const visit = (node: ts.Node, containerName?: string) => {
      let symbolInfo: SymbolInfo | null = null;

      if (ts.isClassDeclaration(node) && node.name) {
        if (!filter || filter === 'class' || filter === 'all') {
          symbolInfo = this.createSymbolInfo(node, node.name, 'class', sourceFile);
        }
        // Visit class members
        node.members.forEach(member => visit(member, node.name?.text));
      } else if (ts.isInterfaceDeclaration(node) && node.name) {
        if (!filter || filter === 'interface' || filter === 'all') {
          symbolInfo = this.createSymbolInfo(node, node.name, 'interface', sourceFile);
        }
        // Visit interface members
        node.members.forEach(member => visit(member, node.name?.text));
      } else if (ts.isFunctionDeclaration(node) && node.name) {
        if (!filter || filter === 'function' || filter === 'all') {
          symbolInfo = this.createSymbolInfo(node, node.name, 'function', sourceFile, containerName);
        }
      } else if (ts.isMethodDeclaration(node) && node.name && ts.isIdentifier(node.name)) {
        if (!filter || filter === 'method' || filter === 'all') {
          symbolInfo = this.createSymbolInfo(node, node.name, 'method', sourceFile, containerName);
        }
      } else if (ts.isPropertyDeclaration(node) && node.name && ts.isIdentifier(node.name)) {
        if (!filter || filter === 'property' || filter === 'all') {
          symbolInfo = this.createSymbolInfo(node, node.name, 'property', sourceFile, containerName);
        }
      } else if (ts.isEnumDeclaration(node) && node.name) {
        if (!filter || filter === 'enum' || filter === 'all') {
          symbolInfo = this.createSymbolInfo(node, node.name, 'enum', sourceFile);
        }
      }

      if (symbolInfo) {
        symbols.push(symbolInfo);
      }

      ts.forEachChild(node, (child) => visit(child, containerName));
    };

    visit(sourceFile);
    return symbols;
  }

  /**
   * Get type information at a specific position in the code
   */
  getTypeInfo(code: string, line: number, column: number, fileName: string = 'temp.ts'): {
    typeName?: string;
    typeString?: string;
    symbolKind?: string;
    documentation?: string;
  } {
    const sourceFile = ts.createSourceFile(
      fileName,
      code,
      ts.ScriptTarget.Latest,
      true
    );

    // Create a language service for type information
    const host: ts.LanguageServiceHost = {
      getCompilationSettings: () => this.compilerOptions,
      getScriptFileNames: () => [fileName],
      getScriptVersion: () => '0',
      getScriptSnapshot: (name) => {
        if (name === fileName) {
          return ts.ScriptSnapshot.fromString(code);
        }
        return undefined;
      },
      getCurrentDirectory: () => '',
      getDefaultLibFileName: (options) => ts.getDefaultLibFilePath(options),
      fileExists: ts.sys.fileExists,
      readFile: ts.sys.readFile,
      readDirectory: ts.sys.readDirectory,
      directoryExists: ts.sys.directoryExists,
      getDirectories: ts.sys.getDirectories,
    };

    const languageService = ts.createLanguageService(host);

    // Convert line/column to position (0-based)
    const position = this.getPositionFromLineColumn(code, line, column);

    // Get quick info at position
    const quickInfo = languageService.getQuickInfoAtPosition(fileName, position);
    
    if (!quickInfo) {
      return {};
    }

    const typeString = ts.displayPartsToString(quickInfo.displayParts);
    const documentation = ts.displayPartsToString(quickInfo.documentation);

    return {
      typeName: quickInfo.kind,
      typeString,
      symbolKind: quickInfo.kind,
      documentation,
    };
  }

  /**
   * Format TypeScript code
   */
  formatCode(code: string, fileName: string = 'temp.ts'): string {
    const sourceFile = ts.createSourceFile(
      fileName,
      code,
      ts.ScriptTarget.Latest,
      true
    );

    const formatSettings: ts.FormatCodeSettings = {
      indentSize: 2,
      tabSize: 2,
      newLineCharacter: '\n',
      convertTabsToSpaces: true,
      insertSpaceAfterCommaDelimiter: true,
      insertSpaceAfterSemicolonInForStatements: true,
      insertSpaceBeforeAndAfterBinaryOperators: true,
      insertSpaceAfterKeywordsInControlFlowStatements: true,
      insertSpaceAfterFunctionKeywordForAnonymousFunctions: true,
      insertSpaceAfterOpeningAndBeforeClosingNonemptyParenthesis: false,
      insertSpaceAfterOpeningAndBeforeClosingNonemptyBrackets: false,
      insertSpaceAfterOpeningAndBeforeClosingTemplateStringBraces: false,
      placeOpenBraceOnNewLineForFunctions: false,
      placeOpenBraceOnNewLineForControlBlocks: false,
    };

    const host: ts.LanguageServiceHost = {
      getCompilationSettings: () => this.compilerOptions,
      getScriptFileNames: () => [fileName],
      getScriptVersion: () => '0',
      getScriptSnapshot: (name) => {
        if (name === fileName) {
          return ts.ScriptSnapshot.fromString(code);
        }
        return undefined;
      },
      getCurrentDirectory: () => '',
      getDefaultLibFileName: (options) => ts.getDefaultLibFilePath(options),
      fileExists: ts.sys.fileExists,
      readFile: ts.sys.readFile,
      readDirectory: ts.sys.readDirectory,
      directoryExists: ts.sys.directoryExists,
      getDirectories: ts.sys.getDirectories,
    };

    const languageService = ts.createLanguageService(host);
    const edits = languageService.getFormattingEditsForDocument(fileName, formatSettings);

    // Apply edits in reverse order to maintain positions
    let formattedCode = code;
    edits
      .sort((a, b) => b.span.start - a.span.start)
      .forEach((edit) => {
        const head = formattedCode.slice(0, edit.span.start);
        const tail = formattedCode.slice(edit.span.start + edit.span.length);
        formattedCode = head + edit.newText + tail;
      });

    return formattedCode;
  }

  /**
   * Calculate code metrics
   */
  calculateMetrics(code: string, fileName: string = 'temp.ts'): CodeMetrics {
    const sourceFile = ts.createSourceFile(
      fileName,
      code,
      ts.ScriptTarget.Latest,
      true
    );

    const metrics: CodeMetrics = {
      linesOfCode: 0,
      commentLines: 0,
      blankLines: 0,
      totalLines: 0,
      cyclomaticComplexity: 1, // Base complexity
      functionCount: 0,
      classCount: 0,
      interfaceCount: 0,
    };

    // Count lines
    const lines = code.split('\n');
    metrics.totalLines = lines.length;

    lines.forEach((line) => {
      const trimmed = line.trim();
      if (trimmed === '') {
        metrics.blankLines++;
      } else if (trimmed.startsWith('//') || trimmed.startsWith('/*') || trimmed.startsWith('*')) {
        metrics.commentLines++;
      } else {
        metrics.linesOfCode++;
      }
    });

    // Count declarations and calculate complexity
    const visit = (node: ts.Node) => {
      if (ts.isClassDeclaration(node)) {
        metrics.classCount++;
      } else if (ts.isInterfaceDeclaration(node)) {
        metrics.interfaceCount++;
      } else if (ts.isFunctionDeclaration(node) || ts.isMethodDeclaration(node) || ts.isArrowFunction(node)) {
        metrics.functionCount++;
      }

      // Cyclomatic complexity: count decision points
      if (
        ts.isIfStatement(node) ||
        ts.isForStatement(node) ||
        ts.isForInStatement(node) ||
        ts.isForOfStatement(node) ||
        ts.isWhileStatement(node) ||
        ts.isDoStatement(node) ||
        ts.isCaseClause(node) ||
        ts.isConditionalExpression(node) ||
        (ts.isBinaryExpression(node) && (node.operatorToken.kind === ts.SyntaxKind.AmpersandAmpersandToken || 
                                         node.operatorToken.kind === ts.SyntaxKind.BarBarToken))
      ) {
        metrics.cyclomaticComplexity++;
      }

      ts.forEachChild(node, visit);
    };

    visit(sourceFile);

    return metrics;
  }


    /**
  * Remove unused imports from TypeScript code
  */
    removeUnusedImports(code: string, fileName: string = 'temp.ts'): string {
      const sourceFile = ts.createSourceFile(
        fileName,
        code,
        ts.ScriptTarget.Latest,
        true
      );

      // Get TypeScript lib directory path
      const typeScriptLibPath = path.dirname(ts.getDefaultLibFilePath(this.compilerOptions));

      const host: ts.LanguageServiceHost = {
        getCompilationSettings: () => this.compilerOptions,
        getScriptFileNames: () => [fileName],
        getScriptVersion: () => '0',
        getScriptSnapshot: (name) => {
          if (name === fileName) {
            return ts.ScriptSnapshot.fromString(code);
          }
          
          // Handle lib file references - TypeScript may request them by short name
          let resolvedPath = name;
          
          // Check if this is a lib file reference without the full path
          if (!name.includes(path.sep) && !name.startsWith('lib.') && !name.endsWith('.d.ts')) {
            // This might be a lib reference like "dom" or "es2020"
            const libFileName = `lib.${name}.d.ts`;
            resolvedPath = path.join(typeScriptLibPath, libFileName);
          } else if (name.includes('lib.') && name.endsWith('.d.ts')) {
            // Already has lib. prefix, just get the basename
            resolvedPath = path.join(typeScriptLibPath, path.basename(name));
          }
          
          // Try to read the file
          try {
            const content = ts.sys.readFile(resolvedPath);
            if (content) {
              return ts.ScriptSnapshot.fromString(content);
            }
          } catch (e) {
            // File not found, return undefined
          }
          return undefined;
        },
        getCurrentDirectory: () => process.cwd(),
        getDefaultLibFileName: (options) => {
          return path.join(typeScriptLibPath, ts.getDefaultLibFileName(options));
        },
        fileExists: (name) => {
          if (name === fileName) return true;
          
          // Handle lib file references - TypeScript may request them by short name
          let resolvedPath = name;
          
          // Check if this is a lib file reference without the full path
          if (!name.includes(path.sep) && !name.startsWith('lib.') && !name.endsWith('.d.ts')) {
            // This might be a lib reference like "dom" or "es2020"
            const libFileName = `lib.${name}.d.ts`;
            resolvedPath = path.join(typeScriptLibPath, libFileName);
          } else if (name.includes('lib.') && name.endsWith('.d.ts')) {
            // Already has lib. prefix, just get the basename
            resolvedPath = path.join(typeScriptLibPath, path.basename(name));
          }
          
          return ts.sys.fileExists(resolvedPath);
        },
        readFile: (name) => {
          if (name === fileName) return code;
          
          // Handle lib file references - TypeScript may request them by short name
          let resolvedPath = name;
          
          // Check if this is a lib file reference without the full path
          if (!name.includes(path.sep) && !name.startsWith('lib.') && !name.endsWith('.d.ts')) {
            // This might be a lib reference like "dom" or "es2020"
            const libFileName = `lib.${name}.d.ts`;
            resolvedPath = path.join(typeScriptLibPath, libFileName);
          } else if (name.includes('lib.') && name.endsWith('.d.ts')) {
            // Already has lib. prefix, just get the basename
            resolvedPath = path.join(typeScriptLibPath, path.basename(name));
          }
          
          return ts.sys.readFile(resolvedPath);
        },
        readDirectory: ts.sys.readDirectory,
        directoryExists: ts.sys.directoryExists,
        getDirectories: ts.sys.getDirectories,
      };

      const languageService = ts.createLanguageService(host);
    
      // Get semantic diagnostics which include unused import information
      const diagnostics = languageService.getSemanticDiagnostics(fileName);
    
      // Filter for unused import diagnostics (code 6133)
      const unusedImports = diagnostics.filter(d => 
        d.code === 6133 && // unused variable/import
        d.file?.fileName === fileName &&
        d.start !== undefined
      );

      if (unusedImports.length === 0) {
        return code;
      }

      // Apply changes in reverse order to maintain positions
      let modifiedCode = code;
      const edits: { start: number; end: number }[] = [];

      unusedImports.forEach(diagnostic => {
        if (diagnostic.start !== undefined && diagnostic.length !== undefined) {
          const start = diagnostic.start;
          const end = start + diagnostic.length;
        
          // Find the full import statement
          const lineStart = modifiedCode.lastIndexOf('\n', start) + 1;
          const lineEnd = modifiedCode.indexOf('\n', end);
          const line = modifiedCode.substring(lineStart, lineEnd === -1 ? undefined : lineEnd);
        
          // If this is part of an import statement, mark the entire line for removal
          if (line.trim().startsWith('import ')) {
            edits.push({ start: lineStart, end: lineEnd === -1 ? modifiedCode.length : lineEnd + 1 });
          }
        }
      });

      // Sort edits in reverse order and apply
      edits
        .sort((a, b) => b.start - a.start)
        .forEach(edit => {
          modifiedCode = modifiedCode.substring(0, edit.start) + modifiedCode.substring(edit.end);
        });

      return modifiedCode;
    }

  // Helper methods

  private formatDiagnostic(diagnostic: ts.Diagnostic, sourceFile?: ts.SourceFile): DiagnosticInfo {
    const message = ts.flattenDiagnosticMessageText(diagnostic.messageText, '\n');
    const category = ts.DiagnosticCategory[diagnostic.category];
    
    let file: string | undefined;
    let line: number | undefined;
    let column: number | undefined;
    let length: number | undefined;

    if (diagnostic.file && diagnostic.start !== undefined) {
      file = diagnostic.file.fileName;
      const { line: l, character: c } = diagnostic.file.getLineAndCharacterOfPosition(diagnostic.start);
      line = l + 1; // 1-based
      column = c + 1; // 1-based
      length = diagnostic.length;
    }

    return {
      message,
      category,
      code: diagnostic.code,
      file,
      line,
      column,
      length,
    };
  }

  private createSymbolInfo(
    node: ts.Node,
    name: ts.Identifier,
    kind: string,
    sourceFile: ts.SourceFile,
    containerName?: string
  ): SymbolInfo {
    const { line, character } = sourceFile.getLineAndCharacterOfPosition(node.getStart());
    
    // Try to get type information
    let type = 'any';
    if (ts.isPropertyDeclaration(node) || ts.isMethodDeclaration(node) || ts.isFunctionDeclaration(node)) {
      if (node.type) {
        type = node.type.getText(sourceFile);
      }
    }

    return {
      name: name.text,
      kind,
      type,
      line: line + 1, // 1-based
      column: character + 1, // 1-based
      containerName,
    };
  }

  private getPositionFromLineColumn(code: string, line: number, column: number): number {
    const lines = code.split('\n');
    let position = 0;

    for (let i = 0; i < line - 1 && i < lines.length; i++) {
      position += lines[i].length + 1; // +1 for newline
    }

    position += column - 1; // column is 1-based
    return position;
  }
}