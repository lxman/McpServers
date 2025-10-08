export interface SymbolInfo {
  name: string;
  kind: string;
  type: string;
  line: number;
  column: number;
  containerName?: string;
}
