import { useState } from "react";
import { fetchTeamMetrics, triggerSync } from "./api/client";
import type { TeamMetricsResponse } from "./api/types";
import { MetricCard } from "./components/MetricCard";
import "./App.css";

function App() {
  const [teamName, setTeamName] = useState("platform");
  const [metrics, setMetrics] = useState<TeamMetricsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const loadMetrics = async () => {
    setIsLoading(true);
    setError(null);

    try {
      const result = await fetchTeamMetrics(teamName);
      setMetrics(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load metrics");
    } finally {
      setIsLoading(false);
    }
  };

  const handleSync = async () => {
    setError(null);
    try {
      await triggerSync();
      await loadMetrics();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Sync failed");
    }
  };

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
        <button type="button" onClick={loadMetrics} disabled={isLoading}>
          {isLoading ? "Loading…" : "Load metrics"}
        </button>
        <button type="button" onClick={handleSync}>
          Sync now
        </button>
      </div>

      {error && <p className="dashboard__error">{error}</p>}

      {metrics && (
        <section className="dashboard__grid">
          <MetricCard label="Deployment Frequency" value={metrics.metrics.DeploymentFrequency} unit="/ day" />
          <MetricCard label="Lead Time for Changes" value={metrics.metrics.LeadTimeForChanges} unit="hrs" />
          <MetricCard label="Change Failure Rate" value={metrics.metrics.ChangeFailureRate} unit="%" />
          <MetricCard label="Mean Time to Recovery" value={metrics.metrics.MeanTimeToRecovery} unit="hrs" />
        </section>
      )}
    </main>
  );
}

export default App;
