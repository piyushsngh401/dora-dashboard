using DoraDashboard.Core.Entities;
using DoraDashboard.Core.Metrics;
using Xunit;

namespace Core.Tests;

public class ChangeFailureRateCalculatorTests
{
    [Fact]
    public void Calculate_CountsDeploymentsFollowedByIncidentWithinWindow()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var deployments = new List<Deployment>
        {
            new() { DeployedAt = start },              // followed by an incident within 24h
            new() { DeployedAt = start.AddDays(1) },    // clean
            new() { DeployedAt = start.AddDays(2) },    // clean
            new() { DeployedAt = start.AddDays(3) },    // clean
        };

        var incidents = new List<Incident>
        {
            new() { OpenedAt = start.AddHours(4) },
        };

        var context = new MetricCalculationContext([], deployments, incidents, start, start.AddDays(4));
        var calculator = new ChangeFailureRateCalculator();

        Assert.Equal(25, calculator.Calculate(context), precision: 3);
    }

    [Fact]
    public void Calculate_ReturnsZero_WhenNoDeployments()
    {
        var start = DateTimeOffset.UtcNow;
        var context = new MetricCalculationContext([], [], [], start, start.AddDays(1));
        var calculator = new ChangeFailureRateCalculator();

        Assert.Equal(0, calculator.Calculate(context));
    }
}
