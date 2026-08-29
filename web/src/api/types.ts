export type MetricName =
  | "DeploymentFrequency"
  | "LeadTimeForChanges"
  | "ChangeFailureRate"
  | "MeanTimeToRecovery";

export interface TeamMetricsResponse {
  team: string;
  windowStart: string;
  windowEnd: string;
  metrics: Record<MetricName, number>;
}

export interface MetricSeriesPoint {
  bucketStart: string;
  bucketEnd: string;
  metrics: Record<MetricName, number>;
}

export interface TeamMetricsSeriesResponse {
  team: string;
  windowStart: string;
  windowEnd: string;
  bucketDays: number;
  series: MetricSeriesPoint[];
}

export interface ApiError {
  status: number;
  message: string;
}
