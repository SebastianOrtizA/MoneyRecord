namespace MoneyRecord.Controls
{
    public sealed class BarChartData
    {
        public string[] Labels { get; set; } = Array.Empty<string>();
        public BarChartSeries[] Series { get; set; } = Array.Empty<BarChartSeries>();
        public string? YLabelFormat { get; set; }
    }

    public sealed class BarChartSeries
    {
        public string Name { get; set; } = "";
        public decimal[] Values { get; set; } = Array.Empty<decimal>();
        public Color Color { get; set; } = Colors.CornflowerBlue;
    }

    public sealed class PieChartSlice
    {
        public string Name { get; set; } = "";
        public decimal Value { get; set; }
        public Color Color { get; set; } = Colors.Grey;
    }

    public sealed class BarChartDrawable : IDrawable
    {
        public BarChartData? Data { get; set; }
        public bool IsDarkMode { get; set; }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (Data is null || Data.Series.Length == 0 || Data.Labels.Length == 0)
                return;

            var textColor = IsDarkMode ? Colors.White : Color.FromArgb("#333333");
            var gridColor = IsDarkMode ? Color.FromArgb("#3A3A3A") : Color.FromArgb("#E0E0E0");

            float leftMargin = 52;
            float bottomMargin = 32;
            float topMargin = 8;
            float rightMargin = 8;

            float chartWidth = dirtyRect.Width - leftMargin - rightMargin;
            float chartHeight = dirtyRect.Height - topMargin - bottomMargin;

            if (chartWidth <= 0 || chartHeight <= 0) return;

            decimal maxVal = 0;
            foreach (var s in Data.Series)
                foreach (var v in s.Values)
                    if (v > maxVal) maxVal = v;

            if (maxVal == 0) maxVal = 1;
            decimal niceMax = CeilToNice(maxVal);

            canvas.FontSize = 10;
            canvas.FontColor = textColor;

            int gridLines = 4;
            for (int i = 0; i <= gridLines; i++)
            {
                float y = topMargin + chartHeight - (chartHeight * i / gridLines);
                decimal val = niceMax * i / gridLines;

                canvas.StrokeColor = gridColor;
                canvas.StrokeSize = 0.5f;
                canvas.DrawLine(leftMargin, y, dirtyRect.Width - rightMargin, y);

                string label = Data.YLabelFormat != null
                    ? string.Format(Data.YLabelFormat, val)
                    : $"${val:N0}";
                canvas.DrawString(label, 2, y - 7, leftMargin - 6, 14, HorizontalAlignment.Right, VerticalAlignment.Center);
            }

            int labelCount = Data.Labels.Length;
            int seriesCount = Data.Series.Length;
            float groupWidth = chartWidth / labelCount;
            float barGap = 4;
            float totalBarSpace = groupWidth - barGap * 2;
            float barWidth = totalBarSpace / seriesCount;
            barWidth = Math.Min(barWidth, 30);

            float totalGroupBarWidth = barWidth * seriesCount;
            float groupStartOffset = (groupWidth - totalGroupBarWidth) / 2;

            for (int li = 0; li < labelCount; li++)
            {
                float groupX = leftMargin + li * groupWidth;

                for (int si = 0; si < seriesCount; si++)
                {
                    if (si >= Data.Series.Length || li >= Data.Series[si].Values.Length)
                        continue;

                    decimal val = Data.Series[si].Values[li];
                    float barHeight = (float)(val / niceMax) * chartHeight;
                    float bx = groupX + groupStartOffset + si * barWidth;
                    float by = topMargin + chartHeight - barHeight;

                    canvas.FillColor = Data.Series[si].Color;
                    canvas.FillRoundedRectangle(bx, by, barWidth - 1, barHeight, 2);
                }

                canvas.FontSize = 10;
                canvas.FontColor = textColor;
                float labelX = groupX;
                float labelY = topMargin + chartHeight + 4;
                canvas.DrawString(Data.Labels[li], labelX, labelY, groupWidth, 24,
                    HorizontalAlignment.Center, VerticalAlignment.Top);
            }
        }

        private static decimal CeilToNice(decimal value)
        {
            if (value <= 0) return 1;
            decimal magnitude = 1;
            while (magnitude * 10 <= value) magnitude *= 10;
            decimal normalized = value / magnitude;
            if (normalized <= 1) return magnitude;
            if (normalized <= 2) return 2 * magnitude;
            if (normalized <= 5) return 5 * magnitude;
            return 10 * magnitude;
        }
    }

    public sealed class PieChartDrawable : IDrawable
    {
        public List<PieChartSlice>? Slices { get; set; }
        public bool IsDarkMode { get; set; }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (Slices is null || Slices.Count == 0)
                return;

            var textColor = IsDarkMode ? Colors.White : Color.FromArgb("#333333");

            decimal total = Slices.Sum(s => s.Value);
            if (total <= 0) return;

            float legendHeight = Slices.Count * 22 + 8;
            float pieAreaHeight = dirtyRect.Height - legendHeight;
            float diameter = Math.Min(dirtyRect.Width - 16, pieAreaHeight - 16);
            diameter = Math.Max(diameter, 60);
            float radius = diameter / 2;
            float cx = dirtyRect.Width / 2;
            float cy = pieAreaHeight / 2;

            float startAngle = -90;
            foreach (var slice in Slices)
            {
                float sweepAngle = (float)(slice.Value / total) * 360f;
                canvas.FillColor = slice.Color;

                var path = new PathF();
                path.MoveTo(cx, cy);
                path.AddArc(cx - radius, cy - radius, cx + radius, cy + radius,
                    startAngle, startAngle + sweepAngle, false);
                path.Close();
                canvas.FillPath(path);

                startAngle += sweepAngle;
            }

            float legendY = pieAreaHeight + 4;
            float legendX = 16;
            canvas.FontSize = 12;

            foreach (var slice in Slices)
            {
                canvas.FillColor = slice.Color;
                canvas.FillRoundedRectangle(legendX, legendY + 2, 12, 12, 2);

                decimal pct = total > 0 ? (slice.Value / total * 100) : 0;
                canvas.FontColor = textColor;
                canvas.DrawString($"{slice.Name} ({pct:N1}%)",
                    legendX + 18, legendY, dirtyRect.Width - legendX - 34, 20,
                    HorizontalAlignment.Left, VerticalAlignment.Center);

                legendY += 22;
            }
        }
    }
}
