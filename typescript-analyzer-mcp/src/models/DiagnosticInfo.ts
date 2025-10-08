export interface DiagnosticInfo {
  message: string;
  category: string;
  code: number;
  file?: string;
  line?: number;
  column?: number;
  length?: number;
}
