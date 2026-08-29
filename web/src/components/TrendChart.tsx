import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { MetricSeriesPoint } from "../api/types";

interface TrendChartProps {
  title: string;
  series: MetricSeriesPoint[];
  metricKey: keyof MetricSeriesPoint["metrics"];
  unit: string;
  color?: string;
}

interface ChartRow {
  bucketLabel: string;
  value: number;
}

const formatBucketLabel = (isoDate: string): string =>
  new Date(isoDate).toLocaleDateString(undefined, { month: "short", day: "numeric" });

export function TrendChart({ title, series, metricKey, unit, color = "#2563eb" }: TrendChartProps) {
  const rows: ChartRow[] = series.map((point) => ({
    bucketLabel: formatBucketLabel(point.bucketStart),
    value: point.metrics[metricKey],
  }));

  return (
    <div className="trend-chart">
      <h3 className="trend-chart__title">
        {title} <span className="trend-chart__unit">({unit})</span>
      </h3>
      <ResponsiveContainer width="100%" height={220}>
        <LineChart data={rows} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#eee" />
          <XAxis dataKey="bucketLabel" tick={{ fontSize: 12 }} stroke="#999" />
          <YAxis tick={{ fontSize: 12 }} stroke="#999" width={40} />
          <Tooltip
            formatter={(value) => [typeof value === "number" ? value.toFixed(1) : String(value), unit]}
            labelFormatter={(label) => label}
          />
          <Legend />
          <Line type="monotone" dataKey="value" name={title} stroke={color} strokeWidth={2} dot={{ r: 3 }} />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
