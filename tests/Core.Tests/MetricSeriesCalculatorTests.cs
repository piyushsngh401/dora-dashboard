using DoraDashboard.Core.Entities;
using DoraDashboard.Core.Metrics;
using Xunit;

namespace Core.Tests;

public class MetricSeriesCalculatorTests
{
    [Fact]
    public void GenerateSeries_SplitsWindowIntoEqualBuckets_WhenWindowIsAnExactMultiple()
    {
        var windowStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = windowStart.AddDays(21);

        var context = new MetricCalculationContext([], [], [], windowStart, windowEnd);
        var calculators = new IMetricCalculator[] { new DeploymentFrequencyCalculator() };

        var series = MetricSeriesCalculator.GenerateSeries(context, calculators, TimeSpan.FromDays(7));

        Assert.Equal(3, series.Count);
        Assert.Equal(windowStart, series[0].BucketStart);
        Assert.Equal(windowStart.AddDays(7), series[0].BucketEnd);
        Assert.Equal(windowEnd, series[^1].BucketEnd);
    }

    [Fact]
    public void GenerateSeries_ClipsFinalBucket_WhenWindowIsNotAnExactMultiple()
    {
        var windowStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = windowStart.AddDays(10);

        var context = new MetricCalculationContext([], [], [], windowStart, windowEnd);
        var calculators = new IMetricCalculator[] { new DeploymentFrequencyCalculator() };

        var series = MetricSeriesCalculator.GenerateSeries(context, calculators, TimeSpan.FromDays(7));

        Assert.Equal(2, series.Count);
        Assert.Equal(windowStart.AddDays(7), series[1].BucketStart);
        Assert.Equal(windowEnd, series[1].BucketEnd);
    }

    [Fact]
    public void GenerateSeries_OnlyCountsEventsFallingWithinEachBucket()
    {
        var windowStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = windowStart.AddDays(14);

        var deployments = new List<Deployment>
        {
            new() { DeployedAt = windowStart.AddDays(1) }, // bucket 1
            new() { DeployedAt = windowStart.AddDays(2) }, // bucket 1
            new() { DeployedAt = windowStart.AddDays(9) }, // bucket 2
        };

        var context = new MetricCalculationContext([], deployments, [], windowStart, windowEnd);
        var calculators = new IMetricCalculator[] { new DeploymentFrequencyCalculator() };

        var series = MetricSeriesCalculator.GenerateSeries(context, calculators, TimeSpan.FromDays(7));

        Assert.Equal(2, series.Count);
        Assert.Equal(2.0 / 7, series[0].Metrics[nameof(DoraMetricType.DeploymentFrequency)], precision: 3);
        Assert.Equal(1.0 / 7, series[1].Metrics[nameof(DoraMetricType.DeploymentFrequency)], precision: 3);
    }

    [Fact]
    public void GenerateSeries_ReturnsEmpty_WhenWindowStartEqualsWindowEnd()
    {
        var windowStart = DateTimeOffset.UtcNow;

        var context = new MetricCalculationContext([], [], [], windowStart, windowStart);
        var calculators = new IMetricCalculator[] { new DeploymentFrequencyCalculator() };

        var series = MetricSeriesCalculator.GenerateSeries(context, calculators, TimeSpan.FromDays(7));

        Assert.Empty(series);
    }

    [Fact]
    public void GenerateSeries_Throws_WhenBucketSizeIsNotPositive()
    {
        var windowStart = DateTimeOffset.UtcNow.AddDays(-7);
        var windowEnd = DateTimeOffset.UtcNow;

        var context = new MetricCalculationContext([], [], [], windowStart, windowEnd);
        var calculators = new IMetricCalculator[] { new DeploymentFrequencyCalculator() };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MetricSeriesCalculator.GenerateSeries(context, calculators, TimeSpan.Zero));
    }
}
