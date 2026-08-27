using DoraDashboard.Core.Entities;
using DoraDashboard.Core.Metrics;
using Xunit;

namespace Core.Tests;

public class LeadTimeForChangesCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsAverageHours_FromMergeToNextDeployment()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var pullRequests = new List<PullRequest>
        {
            new() { MergedAt = start.AddHours(0) },  // deploys 6h later
            new() { MergedAt = start.AddHours(10) }, // deploys 2h later
        };

        var deployments = new List<Deployment>
        {
            new() { DeployedAt = start.AddHours(6) },
            new() { DeployedAt = start.AddHours(12) },
        };

        var context = new MetricCalculationContext(
            pullRequests, deployments, new List<Incident>(), start, start.AddDays(1));

        var calculator = new LeadTimeForChangesCalculator();

        // (6 + 2) / 2 = 4 hours average
        Assert.Equal(4, calculator.Calculate(context), precision: 3);
    }

    [Fact]
    public void Calculate_IgnoresUnmergedPullRequests()
    {
        var start = DateTimeOffset.UtcNow;
        var pullRequests = new List<PullRequest> { new() { MergedAt = null } };
        var deployments = new List<Deployment> { new() { DeployedAt = start } };

        var context = new MetricCalculationContext(pullRequests, deployments, [], start, start.AddDays(1));
        var calculator = new LeadTimeForChangesCalculator();

        Assert.Equal(0, calculator.Calculate(context));
    }
}
