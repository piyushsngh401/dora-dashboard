using DoraDashboard.Core.Entities;
using DoraDashboard.Core.Metrics;
using Xunit;

namespace Core.Tests;

public class MeanTimeToRecoveryCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsAverageResolutionHours()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var incidents = new List<Incident>
        {
            new() { OpenedAt = start, ResolvedAt = start.AddHours(2) },
            new() { OpenedAt = start, ResolvedAt = start.AddHours(6) },
            new() { OpenedAt = start, ResolvedAt = null }, // still open, excluded
        };

        var context = new MetricCalculationContext([], [], incidents, start, start.AddDays(1));
        var calculator = new MeanTimeToRecoveryCalculator();

        Assert.Equal(4, calculator.Calculate(context), precision: 3);
    }

    [Fact]
    public void Calculate_ReturnsZero_WhenNoIncidentsResolved()
    {
        var start = DateTimeOffset.UtcNow;
        var context = new MetricCalculationContext([], [], [], start, start.AddDays(1));
        var calculator = new MeanTimeToRecoveryCalculator();

        Assert.Equal(0, calculator.Calculate(context));
    }
}
