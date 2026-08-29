using DoraDashboard.Core.Entities;

namespace DoraDashboard.Core.Metrics;

/// <summary>
/// Buckets a MetricCalculationContext's window into fixed-size intervals (weekly by default) and
/// re-runs the existing IMetricCalculator strategies against each bucket. This is deliberately a
/// thin wrapper around the same calculators the point-in-time endpoints use — a trend is just the
/// same metric, computed repeatedly over smaller windows — so a new metric only has to be taught
/// to IMetricCalculator once and both the snapshot and series endpoints pick it up for free.
/// </summary>
public static class MetricSeriesCalculator
{
    public static readonly TimeSpan DefaultBucketSize = TimeSpan.FromDays(7);

    public static IReadOnlyList<MetricSeriesPoint> GenerateSeries(
        MetricCalculationContext context,
        IEnumerable<IMetricCalculator> calculators,
        TimeSpan? bucketSize = null)
    {
        var calculatorList = calculators as IReadOnlyList<IMetricCalculator> ?? calculators.ToList();
        var size = bucketSize ?? DefaultBucketSize;
        if (size <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketSize), "Bucket size must be positive.");
        }

        var points = new List<MetricSeriesPoint>();
        var bucketStart = context.WindowStart;

        while (bucketStart < context.WindowEnd)
        {
            // Clip the final bucket so it never extends past the requested window, even when the
            // window length isn't an exact multiple of the bucket size.
            var bucketEnd = bucketStart + size <= context.WindowEnd ? bucketStart + size : context.WindowEnd;

            var bucketContext = new MetricCalculationContext(
                FilterByMergedAt(context.PullRequests, bucketStart, bucketEnd),
                FilterByDeployedAt(context.Deployments, bucketStart, bucketEnd),
                FilterByOpenedAt(context.Incidents, bucketStart, bucketEnd),
                bucketStart,
                bucketEnd);

            var metrics = calculatorList.ToDictionary(
                c => c.MetricType.ToString(),
                c => c.Calculate(bucketContext));

            points.Add(new MetricSeriesPoint(bucketStart, bucketEnd, metrics));

            bucketStart = bucketEnd;
        }

        return points;
    }

    private static IReadOnlyList<PullRequest> FilterByMergedAt(
        IReadOnlyList<PullRequest> pullRequests, DateTimeOffset start, DateTimeOffset end) =>
        pullRequests.Where(p => p.MergedAt >= start && p.MergedAt < end).ToList();

    private static IReadOnlyList<Deployment> FilterByDeployedAt(
        IReadOnlyList<Deployment> deployments, DateTimeOffset start, DateTimeOffset end) =>
        deployments.Where(d => d.DeployedAt >= start && d.DeployedAt < end).ToList();

    private static IReadOnlyList<Incident> FilterByOpenedAt(
        IReadOnlyList<Incident> incidents, DateTimeOffset start, DateTimeOffset end) =>
        incidents.Where(i => i.OpenedAt >= start && i.OpenedAt < end).ToList();
}
