namespace DoraDashboard.Core.Metrics;

/// <summary>Deployments per day, averaged over the window.</summary>
public sealed class DeploymentFrequencyCalculator : IMetricCalculator
{
    public DoraMetricType MetricType => DoraMetricType.DeploymentFrequency;

    public double Calculate(MetricCalculationContext context)
    {
        var days = Math.Max((context.WindowEnd - context.WindowStart).TotalDays, 1);
        return context.Deployments.Count / days;
    }
}
