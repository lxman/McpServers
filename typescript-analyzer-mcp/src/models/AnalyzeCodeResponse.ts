import { DiagnosticInfo } from './DiagnosticInfo.js';

export interface AnalyzeCodeResponse {
  success: boolean;
  diagnostics: DiagnosticInfo[];
  errorCount: number;
  warningCount: number;
  infoCount: number;
  error?: string;
}
