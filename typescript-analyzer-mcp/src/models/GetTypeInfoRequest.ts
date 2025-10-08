export interface GetTypeInfoRequest {
  code: string;
  line: number;
  column: number;
  fileName?: string;
}
