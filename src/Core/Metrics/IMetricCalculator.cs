namespace DoraDashboard.Core.Metrics;

/// <summary>
/// Strategy interface for a single DORA metric. Implementations are registered in DI as
/// IMetricCalculator and resolved as a collection, so adding a fifth custom metric is a new
/// class plus one registration line — not a change to any existing calculator.
/// </summary>
public interface IMetricCalculator
{
    DoraMetricType MetricType { get; }

    double Calculate(MetricCalculationContext context);
}
