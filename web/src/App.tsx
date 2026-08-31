import { useState } from "react";
import { fetchTeamMetrics, fetchTeamMetricsSeries, triggerSync } from "./api/client";
import type { TeamMetricsResponse, TeamMetricsSeriesResponse } from "./api/types";
import { MetricCard } from "./components/MetricCard";
import { TrendChart } from "./components/TrendChart";
import "./App.css";

function App() {
  const [teamName, setTeamName] = useState("platform");
  const [metrics, setMetrics] = useState<TeamMetricsResponse | null>(null);
  const [series, setSeries] = useState<TeamMetricsSeriesResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  // Separate from isLoading: syncing hits GitHub for every configured repo and can take anywhere
  // from a couple seconds to well over a minute (more on a cold-started free-tier instance) —
  // without its own indicator the button just sits there with no feedback, which is exactly what
  // made a real 90-second sync look "stalled" before this existed.
  const [isSyncing, setIsSyncing] = useState(false);

  const loadMetrics = async () => {
    setIsLoading(true);
    setError(null);

    try {
      const [metricsResult, seriesResult] = await Promise.all([
        fetchTeamMetrics(teamName),
        fetchTeamMetricsSeries(teamName),
      ]);
      setMetrics(metricsResult);
      setSeries(seriesResult);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load metrics");
    } finally {
      setIsLoading(false);
    }
  };

  const handleSync = async () => {
    setError(null);
    setIsSyncing(true);
    try {
      await triggerSync();
      await loadMetrics();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Sync failed");
    } finally {
      setIsSyncing(false);
    }
  };

  const isBusy = isLoading || isSyncing;

  return (
    <main className="dashboard">
      <header className="dashboard__header">
        <h1>DORA Metrics Dashboard</h1>
        <p>Deployment frequency, lead time, change failure rate, and MTTR, computed from GitHub activity.</p>
      </header>

      <div className="dashboard__controls">
        <input
          type="text"
          value={teamName}
          onChange={(e) => setTeamName(e.target.value)}
          placeholder="Team name (see dora.config.yaml)"
        />
        <button type="button" onClick={loadMetrics} disabled={isBusy}>
          {isLoading && <span className="spinner" aria-hidden="true" />}
          {isLoading ? "Loading…" : "Load metrics"}
        </button>
        <button type="button" onClick={handleSync} disabled={isBusy}>
          {isSyncing && <span className="spinner" aria-hidden="true" />}
          {isSyncing ? "Syncing…" : "Sync now"}
        </button>
      </div>

      {isSyncing && (
        <p className="dashboard__hint" role="status">
          Fetching from GitHub — this can take up to a minute or so, longer if the free-tier API
          instance just woke up from being idle.
        </p>
      )}

      {error && <p className="dashboard__error">{error}</p>}

      {metrics && (
        <section className="dashboard__grid">
          <MetricCard label="Deployment Frequency" value={metrics.metrics.DeploymentFrequency} unit="/ day" />
          <MetricCard label="Lead Time for Changes" value={metrics.metrics.LeadTimeForChanges} unit="hrs" />
          <MetricCard label="Change Failure Rate" value={metrics.metrics.ChangeFailureRate} unit="%" />
          <MetricCard label="Mean Time to Recovery" value={metrics.metrics.MeanTimeToRecovery} unit="hrs" />
        </section>
      )}

      {series && series.series.length > 0 && (
        <section className="dashboard__trends">
          <h2>Trends</h2>
          <div className="dashboard__trends-grid">
            <TrendChart
              title="Deployment Frequency"
              series={series.series}
              metricKey="DeploymentFrequency"
              unit="/ day"
              color="#2563eb"
            />
            <TrendChart
              title="Lead Time for Changes"
              series={series.series}
              metricKey="LeadTimeForChanges"
              unit="hrs"
              color="#7c3aed"
            />
            <TrendChart
              title="Change Failure Rate"
              series={series.series}
              metricKey="ChangeFailureRate"
              unit="%"
              color="#dc2626"
            />
            <TrendChart
              title="Mean Time to Recovery"
              series={series.series}
              metricKey="MeanTimeToRecovery"
              unit="hrs"
              color="#059669"
            />
          </div>
        </section>
      )}
    </main>
  );
}

export default App;
