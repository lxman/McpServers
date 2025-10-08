export interface GetSymbolsRequest {
  code: string;
  fileName?: string;
  filter?: 'class' | 'interface' | 'function' | 'method' | 'property' | 'enum' | 'all';
}
