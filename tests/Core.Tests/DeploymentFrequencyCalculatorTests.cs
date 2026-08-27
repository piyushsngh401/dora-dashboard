using DoraDashboard.Core.Entities;
using DoraDashboard.Core.Metrics;
using Xunit;

namespace Core.Tests;

public class DeploymentFrequencyCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsDeploymentsPerDay_OverTheWindow()
    {
        var windowStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = windowStart.AddDays(10);

        var deployments = new List<Deployment>
        {
            new() { DeployedAt = windowStart.AddDays(1) },
            new() { DeployedAt = windowStart.AddDays(3) },
            new() { DeployedAt = windowStart.AddDays(5) },
            new() { DeployedAt = windowStart.AddDays(8) },
            new() { DeployedAt = windowStart.AddDays(9) },
        };

        var context = new MetricCalculationContext(
            PullRequests: new List<PullRequest>(),
            Deployments: deployments,
            Incidents: new List<Incident>(),
            WindowStart: windowStart,
            WindowEnd: windowEnd);

        var calculator = new DeploymentFrequencyCalculator();

        Assert.Equal(0.5, calculator.Calculate(context), precision: 3);
    }

    [Fact]
    public void Calculate_ReturnsZero_WhenNoDeployments()
    {
        var windowStart = DateTimeOffset.UtcNow.AddDays(-30);
        var windowEnd = DateTimeOffset.UtcNow;

        var context = new MetricCalculationContext([], [], [], windowStart, windowEnd);
        var calculator = new DeploymentFrequencyCalculator();

        Assert.Equal(0, calculator.Calculate(context));
    }
}
