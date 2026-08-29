import type { TeamMetricsResponse, TeamMetricsSeriesResponse } from "./types";

// Set VITE_API_BASE_URL in web/.env.local to point at a non-default API host.
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000";

export async function fetchTeamMetrics(teamName: string): Promise<TeamMetricsResponse> {
  const response = await fetch(`${API_BASE_URL}/api/teams/${encodeURIComponent(teamName)}/metrics`);

  if (!response.ok) {
    throw new Error(`Failed to load metrics for "${teamName}" (HTTP ${response.status})`);
  }

  return (await response.json()) as TeamMetricsResponse;
}

export async function fetchTeamMetricsSeries(
  teamName: string,
  bucketDays = 7,
): Promise<TeamMetricsSeriesResponse> {
  const response = await fetch(
    `${API_BASE_URL}/api/teams/${encodeURIComponent(teamName)}/metrics/series?bucketDays=${bucketDays}`,
  );

  if (!response.ok) {
    throw new Error(`Failed to load metric trends for "${teamName}" (HTTP ${response.status})`);
  }

  return (await response.json()) as TeamMetricsSeriesResponse;
}

export async function triggerSync(): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/sync`, { method: "POST" });

  if (!response.ok) {
    throw new Error(`Sync trigger failed (HTTP ${response.status})`);
  }
}
