using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>One line of the forecast chart: a balance per day in one currency (<paramref name="IsApproximate"/> = converted).</summary>
    public sealed record ForecastView(IReadOnlyList<DateTime> Dates, IReadOnlyList<decimal> Values, string CurrencyCode, bool IsApproximate)
    {
        /// <summary>The lowest balance and its first day.</summary>
        public (DateTime Date, decimal Value) Lowest
        {
            get
            {
                var index = 0;
                for (var i = 1; i < Values.Count; i++)
                    if (Values[i] < Values[index])
                        index = i;
                return (Dates[index], Values[index]);
            }
        }

        /// <summary>The balance on the last day.</summary>
        public decimal End => Values[^1];

        public bool GoesNegative => Values.Any(v => v < 0);

        private string Money(decimal amount) => (IsApproximate ? MoneyFormatter.ApproximateMark : string.Empty) + MoneyFormatter.Format(amount, CurrencyCode);

        /// <summary>"Lowest: $40.00 on Nov 9": says in words what the colour shows.</summary>
        public string LowestText
        {
            get
            {
                var (date, value) = Lowest;
                return string.Format(AppResources.ForecastLowestLine, Money(value), DateFormatter.Format(date, DateStyle.MonthDay));
            }
        }

        /// <summary>"Balance on Dec 7: $1,200.00".</summary>
        public string EndText => string.Format(AppResources.ForecastEndLine, DateFormatter.Format(Dates[^1], DateStyle.MonthDay), Money(End));
    }

    /// <summary>One event behind the line: what moves, when, and in which account (its own currency, "~" when an estimate).</summary>
    public sealed record ForecastEventRow(ForecastEvent Event, string AccountName, string CurrencyCode)
    {
        public int? RecurringId => Event.RecurringId;

        public string Description => string.IsNullOrWhiteSpace(Event.Description) ? AccountName : Event.Description;

        public bool IsExpense => Event.Cents < 0;

        public TransactionType Type => IsExpense ? TransactionType.Expense : TransactionType.Income;

        public bool CanOpen => Event.RecurringId != null;

        /// <summary>"Nov 9 · Checking".</summary>
        public string DetailText => DateFormatter.Format(Event.Date, DateStyle.MonthDay) + " · " + AccountName;

        public string AmountText => (Event.IsEstimate ? "~" : string.Empty) + MoneyFormatter.FormatSigned(Money.FromCents(Event.Cents), CurrencyCode);

        /// <summary>A Material Design Icons glyph for where the event comes from.</summary>
        public string Icon => Event.Source switch
        {
            ForecastSource.Debt => "\U000F1218",
            ForecastSource.Scheduled => "\U000F00ED",
            _ => "\U000F0456"
        };
    }

    /// <summary>A horizon chip: "30 days".</summary>
    public sealed record ForecastDayChoice(int Days, bool IsSelected)
    {
        public string Text => string.Format(AppResources.ForecastDaysChip, Days);
    }

    /// <summary>The "All accounts" choice (<paramref name="AccountId"/> null) or one account of the shown currency.</summary>
    public sealed record ForecastAccountOption(int? AccountId, string Name);
}
