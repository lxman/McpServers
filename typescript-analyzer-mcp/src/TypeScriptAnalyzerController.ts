import { Body, Controller, Post, Route, Tags } from 'tsoa';
import { TypeScriptTools } from './tools/TypeScriptTools.js';

interface AnalyzeRequest {
  code: string;
  filePath?: string;
}

interface SymbolsRequest {
  code: string;
  filePath?: string;
  filter?: 'class' | 'interface' | 'function' | 'method' | 'property' | 'enum' | 'all';
}

interface TypeInfoRequest {
  code: string;
  line: number;
  column: number;
  filePath?: string;
}

interface FormatRequest {
  code: string;
  filePath?: string;
}

interface MetricsRequest {
  code: string;
  filePath?: string;
}

interface RemoveImportsRequest {
  code: string;
  filePath?: string;
}

type AnalyzeCodeResponse = any;
type GetSymbolsResponse = any;
type GetTypeInfoResponse = any;
type FormatCodeResponse = any;
type CalculateMetricsResponse = any;

@Route('api/typescript')
@Tags('TypeScript Analyzer')
export class TypeScriptAnalyzerController extends Controller {
  private tsTools = new TypeScriptTools();

  /**
   * Analyze TypeScript code for errors and warnings
   */
  @Post('analyze')
  public async analyzeCode(@Body() request: AnalyzeRequest): Promise<AnalyzeCodeResponse> {
    return this.tsTools.analyzeCode({
      code: request.code,
      fileName: request.filePath,
    });
  }

  /**
   * Extract symbols (classes, interfaces, functions, etc.) from TypeScript code
   */
  @Post('symbols')
  public async getSymbols(@Body() request: SymbolsRequest): Promise<GetSymbolsResponse> {
    return this.tsTools.getSymbols({
      code: request.code,
      fileName: request.filePath,
      filter: request.filter,
    });
  }

  /**
   * Get type information at a specific position in TypeScript code
   */
  @Post('type-info')
  public async getTypeInfo(@Body() request: TypeInfoRequest): Promise<GetTypeInfoResponse> {
    return this.tsTools.getTypeInfo({
      code: request.code,
      line: request.line,
      column: request.column,
      fileName: request.filePath,
    });
  }

  /**
   * Format TypeScript code
   */
  @Post('format')
  public async formatCode(@Body() request: FormatRequest): Promise<FormatCodeResponse> {
    return this.tsTools.formatCode({
      code: request.code,
      fileName: request.filePath,
    });
  }

  /**
   * Calculate code metrics including cyclomatic complexity
   */
  @Post('metrics')
  public async calculateMetrics(@Body() request: MetricsRequest): Promise<CalculateMetricsResponse> {
    return this.tsTools.calculateMetrics({
      code: request.code,
      fileName: request.filePath,
    });
  }

  /**
   * Remove unused import statements from TypeScript code
   */
  @Post('remove-unused-imports')
  public async removeUnusedImports(@Body() request: RemoveImportsRequest): Promise<FormatCodeResponse> {
    return this.tsTools.removeUnusedImports({
      code: request.code,
      fileName: request.filePath,
    });
  }
}
