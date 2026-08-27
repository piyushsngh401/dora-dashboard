using DoraDashboard.Core.Entities;

namespace DoraDashboard.Core.Metrics;

/// <summary>
/// Pre-filtered data for one team or repository over one time window. Calculators receive this
/// instead of a DbContext so they stay pure functions of their input and are trivial to unit test.
/// </summary>
public sealed record MetricCalculationContext(
    IReadOnlyList<PullRequest> PullRequests,
    IReadOnlyList<Deployment> Deployments,
    IReadOnlyList<Incident> Incidents,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd);
