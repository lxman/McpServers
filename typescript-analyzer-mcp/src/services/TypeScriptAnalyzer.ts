import ts from 'typescript';
import { DiagnosticInfo } from '../models/DiagnosticInfo.js';
import { SymbolInfo } from '../models/SymbolInfo.js';
import { CodeMetrics } from '../models/CodeMetrics.js';

export class TypeScriptAnalyzer {
  private compilerOptions: ts.CompilerOptions;

  constructor() {
    this.compilerOptions = {
      target: ts.ScriptTarget.Latest,
      module: ts.ModuleKind.ESNext,
      strict: true,
      esModuleInterop: true,
      skipLibCheck: true,
      forceConsistentCasingInFileNames: true,
      moduleResolution: ts.ModuleResolutionKind.NodeNext,
    };
  }

  /**
   * Analyze TypeScript code and return diagnostics
   */
  analyzeCode(code: string, fileName: string = 'temp.ts'): DiagnosticInfo[] {
    const sourceFile = ts.createSourceFile(
      fileName,
      code,
      ts.ScriptTarget.Latest,
      true
    );

    // Get syntactic diagnostics from the source file
    const syntacticDiagnostics = ts.getPreEmitDiagnostics(
      ts.createProgram([fileName], this.compilerOptions, {
        getSourceFile: (name) => (name === fileName ? sourceFile : undefined),
        writeFile: () => {},
        getCurrentDirectory: () => '',
        getDirectories: () => [],
        fileExists: (name) => name === fileName,
        readFile: (name) => (name === fileName ? code : undefined),
        getCanonicalFileName: (name) => name,
        useCaseSensitiveFileNames: () => true,
        getNewLine: () => '\n',
        getDefaultLibFileName: () => 'lib.d.ts',
      }),
      sourceFile
    );


    // Create a program for semantic diagnostics
    const host: ts.CompilerHost = {
      getSourceFile: (name) => (name === fileName ? sourceFile : undefined),
      writeFile: () => {},
      getCurrentDirectory: () => '',
      getDirectories: () => [],
      fileExists: (name) => name === fileName,
      readFile: (name) => (name === fileName ? code : undefined),
      getCanonicalFileName: (name) => name,
      useCaseSensitiveFileNames: () => true,
      getNewLine: () => '\n',
      getDefaultLibFileName: () => 'lib.d.ts',
    };

    const program = ts.createProgram([fileName], this.compilerOptions, host);
    const semanticDiagnostics = program.getSemanticDiagnostics(sourceFile);

    // Combine all diagnostics
    const allDiagnostics = [...syntacticDiagnostics, ...semanticDiagnostics];

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