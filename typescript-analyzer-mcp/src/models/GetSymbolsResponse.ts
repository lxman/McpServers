import { SymbolInfo } from './SymbolInfo.js';

export interface GetSymbolsResponse {
  success: boolean;
  symbols: SymbolInfo[];
  count: number;
  error?: string;
}
