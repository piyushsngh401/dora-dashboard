interface MetricCardProps {
  label: string;
  value: number;
  unit: string;
  precision?: number;
}

export function MetricCard({ label, value, unit, precision = 1 }: MetricCardProps) {
  return (
    <div className="metric-card">
      <span className="metric-card__label">{label}</span>
      <span className="metric-card__value">
        {value.toFixed(precision)}
        <span className="metric-card__unit">{unit}</span>
      </span>
    </div>
  );
}
