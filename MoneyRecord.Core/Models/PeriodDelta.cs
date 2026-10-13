using MoneyRecord.Resources.Strings;
using System.Globalization;

namespace MoneyRecord.Models
{
    /// <summary>A headline number against the same number for the previous period: "▲ 12% vs previous period".</summary>
    public sealed record PeriodDelta(decimal Current, decimal Previous)
    {
        /// <summary>The largest change shown as a number (999%); anything bigger reads "&gt;999%".</summary>
        public const decimal MaxShownChange = 9.99m;

        /// <summary>The change as a fraction of the previous value; null when there was nothing before.</summary>
        public decimal? Change => Previous == 0 ? null : (Current - Previous) / Math.Abs(Previous);

        /// <summary>Arrow and percentage in words, so the direction isn't carried by colour; empty when both are zero.</summary>
        public string Text
        {
            get
            {
                if (Current == 0 && Previous == 0)
                    return string.Empty;
                if (Change is not { } change)
                    return AppResources.DeltaNothingBefore;
                if (change == 0)
                    return AppResources.DeltaNoChange;

                var arrow = change > 0 ? "▲" : "▼";
                // Past ten times the previous value the exact figure is noise ("▲ 2,000,000%")
                var percent = Math.Abs(change) >= MaxShownChange
                    ? ">" + MaxShownChange.ToString("P0", CultureInfo.CurrentCulture)
                    : Math.Abs(change).ToString("P0", CultureInfo.CurrentCulture);
                return string.Format(AppResources.DeltaVsPrevious, $"{arrow} {percent}");
            }
        }
    }
}
