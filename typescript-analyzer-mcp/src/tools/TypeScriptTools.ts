import { TypeScriptAnalyzer } from '../services/TypeScriptAnalyzer.js';
import { AnalyzeCodeRequest } from '../models/AnalyzeCodeRequest.js';
import { AnalyzeCodeResponse } from '../models/AnalyzeCodeResponse.js';
import { GetSymbolsRequest } from '../models/GetSymbolsRequest.js';
import { GetSymbolsResponse } from '../models/GetSymbolsResponse.js';
import { GetTypeInfoRequest } from '../models/GetTypeInfoRequest.js';
import { GetTypeInfoResponse } from '../models/GetTypeInfoResponse.js';
import { FormatCodeRequest } from '../models/FormatCodeRequest.js';
import { FormatCodeResponse } from '../models/FormatCodeResponse.js';
import { CalculateMetricsResponse } from '../models/CalculateMetricsResponse.js';

export class TypeScriptTools {
  private analyzer: TypeScriptAnalyzer;

  constructor() {
    this.analyzer = new TypeScriptAnalyzer();
  }

  /**
   * Analyze TypeScript code for errors, warnings, and diagnostics
   */
  analyzeCode(request: AnalyzeCodeRequest): AnalyzeCodeResponse {
    try {
      const diagnostics = this.analyzer.analyzeCode(
        request.code,
        request.fileName || 'temp.ts'
      );

      const errorCount = diagnostics.filter(d => d.category === 'Error').length;
      const warningCount = diagnostics.filter(d => d.category === 'Warning').length;
      const infoCount = diagnostics.filter(d => d.category === 'Message' || d.category === 'Suggestion').length;

      return {
        success: true,
        diagnostics,
        errorCount,
        warningCount,
        infoCount,
      };
    } catch (error) {
      return {
        success: false,
        diagnostics: [],
        errorCount: 0,
        warningCount: 0,
        infoCount: 0,
        error: error instanceof Error ? error.message : 'Unknown error occurred',
      };
    }
  }

  /**
   * Get all symbols (classes, interfaces, functions, etc.) from TypeScript code
   */
  getSymbols(request: GetSymbolsRequest): GetSymbolsResponse {
    try {
      const symbols = this.analyzer.getSymbols(
        request.code,
        request.fileName || 'temp.ts',
        request.filter
      );

      return {
        success: true,
        symbols,
        count: symbols.length,
      };
    } catch (error) {
      return {
        success: false,
        symbols: [],
        count: 0,
        error: error instanceof Error ? error.message : 'Unknown error occurred',
      };
    }
  }

  /**
   * Get type information at a specific position in the code
   */
  getTypeInfo(request: GetTypeInfoRequest): GetTypeInfoResponse {
    try {
      const typeInfo = this.analyzer.getTypeInfo(
        request.code,
        request.line,
        request.column,
        request.fileName || 'temp.ts'
      );

      return {
        success: true,
        ...typeInfo,
      };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Unknown error occurred',
      };
    }
  }

  /**
   * Format TypeScript code using the TypeScript formatter
   */
  formatCode(request: FormatCodeRequest): FormatCodeResponse {
    try {
      const formattedCode = this.analyzer.formatCode(
        request.code,
        request.fileName || 'temp.ts'
      );

      return {
        success: true,
        formattedCode,
      };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Unknown error occurred',
      };
    }
  }

  /**
   * Calculate code metrics including complexity, LOC, and more
   */
  calculateMetrics(request: AnalyzeCodeRequest): CalculateMetricsResponse {
    try {
      const metrics = this.analyzer.calculateMetrics(
        request.code,
        request.fileName || 'temp.ts'
      );

      return {
        success: true,
        metrics,
      };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Unknown error occurred',
      };
    }
  }


    /**
  * Remove unused imports from TypeScript code
  */
    removeUnusedImports(request: FormatCodeRequest): FormatCodeResponse {
      try {
        const cleanedCode = this.analyzer.removeUnusedImports(
          request.code,
          request.fileName || 'temp.ts'
        );

        return {
          success: true,
          formattedCode: cleanedCode,
        };
      } catch (error) {
        return {
          success: false,
          error: error instanceof Error ? error.message : 'Unknown error occurred',
        };
      }
    }
}