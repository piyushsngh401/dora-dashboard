namespace DoraDashboard.Core.Metrics;

/// <summary>Average hours between an incident opening and its resolution, for incidents resolved in the window.</summary>
public sealed class MeanTimeToRecoveryCalculator : IMetricCalculator
{
    public DoraMetricType MetricType => DoraMetricType.MeanTimeToRecovery;

    public double Calculate(MetricCalculationContext context)
    {
        var resolvedIncidents = context.Incidents.Where(i => i.ResolvedAt is not null).ToList();
        if (resolvedIncidents.Count == 0)
        {
            return 0;
        }

        return resolvedIncidents.Average(i => (i.ResolvedAt!.Value - i.OpenedAt).TotalHours);
    }
}
