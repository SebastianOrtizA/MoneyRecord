using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using System.ComponentModel;

namespace MoneyRecord.Helpers
{
    public class PeriodItem : INotifyPropertyChanged
    {
        public PeriodType Type { get; set; }

        public string DisplayName => Type switch
        {
            PeriodType.CalendarMonth => AppResources.CalendarMonth,
            PeriodType.CalendarYear => AppResources.CalendarYear,
            PeriodType.Today => AppResources.Today,
            PeriodType.LastWeek => AppResources.LastWeek,
            PeriodType.LastMonth => AppResources.LastMonth,
            PeriodType.LastYear => AppResources.LastYear,
            PeriodType.CustomPeriod => AppResources.CustomPeriod,
            _ => Type.ToString()
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        public override string ToString() => DisplayName;

        public override bool Equals(object? obj)
        {
            if (obj is PeriodItem other)
                return Type == other.Type;
            return false;
        }

        public override int GetHashCode() => Type.GetHashCode();
    }

    public static class PeriodHelper
    {
        public static List<PeriodItem> GetPeriods() =>
        [
            new PeriodItem { Type = PeriodType.CalendarMonth },
            new PeriodItem { Type = PeriodType.CalendarYear },
            new PeriodItem { Type = PeriodType.LastWeek },
            new PeriodItem { Type = PeriodType.LastMonth },
            new PeriodItem { Type = PeriodType.LastYear },
            new PeriodItem { Type = PeriodType.CustomPeriod }
        ];

        public static List<PeriodItem> GetReportPeriods() =>
        [
            new PeriodItem { Type = PeriodType.CalendarMonth },
            new PeriodItem { Type = PeriodType.CalendarYear },
            new PeriodItem { Type = PeriodType.Today },
            new PeriodItem { Type = PeriodType.LastWeek },
            new PeriodItem { Type = PeriodType.LastMonth },
            new PeriodItem { Type = PeriodType.LastYear },
            new PeriodItem { Type = PeriodType.CustomPeriod }
        ];

        public static PeriodItem GetDefaultPeriod() => new() { Type = PeriodType.CalendarMonth };
    }
}
