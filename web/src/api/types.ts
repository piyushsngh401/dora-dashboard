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

export interface ApiError {
  status: number;
  message: string;
}
