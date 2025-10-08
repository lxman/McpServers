import { CodeMetrics } from './CodeMetrics.js';

export interface CalculateMetricsResponse {
  success: boolean;
  metrics?: CodeMetrics;
  error?: string;
}
