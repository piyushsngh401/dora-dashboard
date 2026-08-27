namespace DoraDashboard.Core.Metrics;

/// <summary>
/// Average hours between a pull request merging and the next deployment at or after that merge.
/// This is a v1 approximation: it assumes the next deployment after a PR merges is the one that
/// shipped it, which holds for most trunk-based, frequently-deployed repos but can overstate lead
/// time for repos that batch several PRs into one release. See ADR-0001 for the discussion.
/// </summary>
public sealed class LeadTimeForChangesCalculator : IMetricCalculator
{
    public DoraMetricType MetricType => DoraMetricType.LeadTimeForChanges;

    public double Calculate(MetricCalculationContext context)
    {
        var mergedPullRequests = context.PullRequests.Where(pr => pr.MergedAt is not null).ToList();
        if (mergedPullRequests.Count == 0 || context.Deployments.Count == 0)
        {
            return 0;
        }

        var orderedDeployments = context.Deployments.OrderBy(d => d.DeployedAt).ToList();
        var leadTimesHours = new List<double>();

        foreach (var pullRequest in mergedPullRequests)
        {
            var nextDeployment = orderedDeployments.FirstOrDefault(d => d.DeployedAt >= pullRequest.MergedAt);
            if (nextDeployment is null)
            {
                continue;
            }

            leadTimesHours.Add((nextDeployment.DeployedAt - pullRequest.MergedAt!.Value).TotalHours);
        }

        return leadTimesHours.Count == 0 ? 0 : leadTimesHours.Average();
    }
}
