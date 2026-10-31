namespace MoneyRecord.Helpers
{
    /// <summary>
    /// The categorical chart colours (hex, in slot order) and a colour-vision check on them, so the palette lives
    /// where it can be tested; the app's <c>ChartPalette</c> turns them into <c>Color</c>s.
    /// </summary>
    public static class ChartColors
    {
        public static readonly string[] Light =
            ["#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#8a5a2b", "#6250d6", "#e34948"];

        public static readonly string[] Dark =
            ["#58a0f0", "#f08650", "#0f8a63", "#e0ae2c", "#e78aae", "#a06a3c", "#8465e0", "#e66767"];

        /// <summary>The smallest CIE76 colour difference between any two colours as a deuteranope sees them.</summary>
        public static double MinDeuteranopiaDistance(IReadOnlyList<string> hex)
        {
            var labs = hex.Select(h => ToLab(SimulateDeuteranopia(ParseLinear(h)))).ToArray();
            var min = double.MaxValue;
            for (var i = 0; i < labs.Length; i++)
                for (var j = i + 1; j < labs.Length; j++)
                    min = Math.Min(min, Distance(labs[i], labs[j]));
            return min;
        }

        private static double[] ParseLinear(string hex) =>
        [
            ToLinear(Convert.ToInt32(hex.Substring(1, 2), 16) / 255.0),
            ToLinear(Convert.ToInt32(hex.Substring(3, 2), 16) / 255.0),
            ToLinear(Convert.ToInt32(hex.Substring(5, 2), 16) / 255.0),
        ];

        private static double ToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        // Machado et al. (2009), severity 1.0, on linear RGB
        private static double[] SimulateDeuteranopia(double[] c) =>
        [
            0.367322 * c[0] + 0.860646 * c[1] - 0.227968 * c[2],
            0.280085 * c[0] + 0.672501 * c[1] + 0.047413 * c[2],
            -0.011820 * c[0] + 0.042940 * c[1] + 0.968881 * c[2],
        ];

        private static double[] ToLab(double[] rgb)
        {
            var r = Math.Clamp(rgb[0], 0, 1);
            var g = Math.Clamp(rgb[1], 0, 1);
            var b = Math.Clamp(rgb[2], 0, 1);
            var x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047;
            var y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            var z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883;
            double F(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116;
            return [116 * F(y) - 16, 500 * (F(x) - F(y)), 200 * (F(y) - F(z))];
        }

        private static double Distance(double[] a, double[] b) =>
            Math.Sqrt(Math.Pow(a[0] - b[0], 2) + Math.Pow(a[1] - b[1], 2) + Math.Pow(a[2] - b[2], 2));
    }
}
