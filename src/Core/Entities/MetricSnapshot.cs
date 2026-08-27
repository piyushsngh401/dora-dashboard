using DoraDashboard.Core.Metrics;

namespace DoraDashboard.Core.Entities;

/// <summary>
/// A pre-computed metric value for a team or repository over a time window. The API reads from
/// snapshots rather than recomputing on every request; the sync worker (re)writes them after ingest.
/// Not yet written to by Phase 0 — computation currently happens on-demand in the API endpoints.
/// </summary>
public class MetricSnapshot
{
    public int Id { get; set; }

    public int? TeamId { get; set; }
    public int? RepositoryId { get; set; }

    public DoraMetricType MetricType { get; set; }
    public DateOnly WindowStart { get; set; }
    public DateOnly WindowEnd { get; set; }
    public double Value { get; set; }
    public DateTimeOffset ComputedAt { get; set; }
}
