namespace DoraDashboard.Core.Metrics;

/// <summary>
/// One bucket's worth of metric values — e.g. "the four DORA metrics, computed only from data
/// in the week starting BucketStart." A series of these is what powers a trend chart.
/// </summary>
public sealed record MetricSeriesPoint(
    DateTimeOffset BucketStart,
    DateTimeOffset BucketEnd,
    IReadOnlyDictionary<string, double> Metrics);
