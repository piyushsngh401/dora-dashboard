namespace DoraDashboard.Core.Metrics;

/// <summary>
/// Percentage of deployments followed by an incident opened within <see cref="_failureWindow"/>
/// of that deployment. The correlation is time-based rather than an explicit deployment-incident
/// link, which is the simplifying assumption to revisit first if the numbers look off — see ADR-0001.
/// </summary>
public sealed class ChangeFailureRateCalculator : IMetricCalculator
{
    private readonly TimeSpan _failureWindow;

    public ChangeFailureRateCalculator(TimeSpan? failureWindow = null)
    {
        _failureWindow = failureWindow ?? TimeSpan.FromHours(24);
    }

    public DoraMetricType MetricType => DoraMetricType.ChangeFailureRate;

    public double Calculate(MetricCalculationContext context)
    {
        if (context.Deployments.Count == 0)
        {
            return 0;
        }

        var failedDeployments = context.Deployments.Count(deployment =>
            context.Incidents.Any(incident =>
                incident.OpenedAt >= deployment.DeployedAt &&
                incident.OpenedAt <= deployment.DeployedAt + _failureWindow));

        return (double)failedDeployments / context.Deployments.Count * 100;
    }
}
