using AwesomeAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers
{
    public class ChartPaletteTests
    {
        // The donut shows up to 7 slices (slots 0-6) next to each other
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Slices_StayApartForDeuteranopia(bool dark)
        {
            var palette = (dark ? ChartColors.Dark : ChartColors.Light).Take(7).ToArray();

            ChartColors.MinDeuteranopiaDistance(palette).Should().BeGreaterThan(MinDistance);
        }

        private const double MinDistance = 12;
    }
}
