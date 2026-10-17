using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>
    /// A debt with its amounts (the Debts page card): <see cref="TotalCents"/> = everything lent or borrowed (the start plus
    /// later additions), <see cref="PaidCents"/> = the payments, interest apart.
    /// </summary>
    public sealed class DebtSummary
    {
        /// <summary>Within this many days of the due date the card says "Due soon".</summary>
        public const int DueSoonDays = 7;

        public const string IconCode = "F1218"; // handshake

        public DebtSummary(Debt debt, string personName, string currencyCode, long totalCents, long paidCents,
            long interestCents, RecurringTransaction? schedule, DateTime today)
        {
            Debt = debt;
            PersonName = personName;
            CurrencyCode = currencyCode;
            TotalCents = totalCents;
            PaidCents = paidCents;
            InterestCents = interestCents;
            Schedule = schedule is { IsActive: true } ? schedule : null;
            Today = today.Date;
        }

        public Debt Debt { get; }

        public int DebtId => Debt.Id;

        public DebtDirection Direction => Debt.Direction;

        public string PersonName { get; }

        /// <summary>The debt's currency (its account's, resolved).</summary>
        public string CurrencyCode { get; }

        public long TotalCents { get; }

        public long PaidCents { get; }

        public long InterestCents { get; }

        /// <summary>The active payment schedule, if any.</summary>
        public RecurringTransaction? Schedule { get; }

        private DateTime Today { get; }

        public long OutstandingCents => Math.Max(0, TotalCents - PaidCents);

        public decimal Outstanding => Money.FromCents(OutstandingCents);

        public bool IsPaidOff => TotalCents > 0 && OutstandingCents == 0;

        public bool IsOverdue => !IsPaidOff && Debt.DueDate is { } due && due.Date < Today;

        public bool IsDueSoon => !IsPaidOff && !IsOverdue && Debt.DueDate is { } due && (due.Date - Today).Days <= DueSoonDays;

        public string DisplayIcon => IconHelper.GetCategoryDisplayIcon(IconCode);

        /// <summary>"Ana · Car repair".</summary>
        public string Title => Debt.Note.Length > 0 ? $"{PersonName} · {Debt.Note}" : PersonName;

        /// <summary>"Owes you" or "You owe".</summary>
        public string DirectionText => Direction == DebtDirection.Lent ? AppResources.DebtOwesYou : AppResources.DebtYouOwe;

        public string OutstandingText => MoneyFormatter.Format(Outstanding, CurrencyCode);

        /// <summary>"Paid back $40.00 of $100.00".</summary>
        public string PaidOfTotalText => string.Format(AppResources.DebtPaidOfTotal,
            MoneyFormatter.Format(Money.FromCents(PaidCents), CurrencyCode), MoneyFormatter.Format(Money.FromCents(TotalCents), CurrencyCode));

        public double ProgressPercentage => TotalCents > 0 ? Math.Min(100, 100.0 * PaidCents / TotalCents) : 0;

        /// <summary>"Due Oct 30, 2026"; empty without a due date or once paid.</summary>
        public string DueText => !IsPaidOff && Debt.DueDate is { } due
            ? string.Format(AppResources.DebtDueOn, DateFormatter.Format(due, DateStyle.Medium))
            : string.Empty;

        public bool HasDue => DueText.Length > 0;

        /// <summary>"Next payment $20.00 on Nov 1 · Monthly": the next scheduled one, capped at what is left.</summary>
        public string ScheduleText => Schedule is { } schedule && !IsPaidOff
            ? string.Format(AppResources.DebtNextPayment,
                MoneyFormatter.Format(Money.FromCents(Math.Min(schedule.AmountCents, OutstandingCents) + schedule.InterestCents), CurrencyCode),
                DateFormatter.Format(schedule.NextOccurrence, DateStyle.MonthDay),
                FrequencyName(schedule.Frequency))
            : string.Empty;

        public bool HasSchedule => ScheduleText.Length > 0;

        /// <summary>"Interest $12.00" (paid or received); empty without any.</summary>
        public string InterestText => InterestCents > 0
            ? string.Format(AppResources.DebtInterestTotal, MoneyFormatter.Format(Money.FromCents(InterestCents), CurrencyCode))
            : string.Empty;

        public bool HasInterest => InterestText.Length > 0;

        public bool HasStatus => IsPaidOff || IsOverdue || IsDueSoon;

        public string StatusText => IsPaidOff ? AppResources.DebtPaidOff
            : IsOverdue ? AppResources.DebtOverdue
            : IsDueSoon ? AppResources.DebtDueSoon
            : string.Empty;

        public string StatusIcon => IconHelper.GetCategoryDisplayIcon(IsPaidOff ? "F05E0" /* check-circle */ : "F00F0" /* calendar-clock */);

        /// <summary>The status colour (light/dark via BudgetLevelToColorConverter): paid = on track, due soon = high, overdue = exceeded.</summary>
        public BudgetLevel Level => IsOverdue ? BudgetLevel.Exceeded : IsDueSoon ? BudgetLevel.High : BudgetLevel.OnTrack;

        public static string FrequencyName(RecurrenceFrequency frequency) => frequency switch
        {
            RecurrenceFrequency.Daily => AppResources.FrequencyDaily,
            RecurrenceFrequency.Weekly => AppResources.FrequencyWeekly,
            RecurrenceFrequency.Biweekly => AppResources.FrequencyBiweekly,
            RecurrenceFrequency.Yearly => AppResources.FrequencyYearly,
            _ => AppResources.FrequencyMonthly
        };
    }

    /// <summary>What a debt movement was.</summary>
    public enum DebtMovementKind
    {
        /// <summary>Lent or borrowed: the start, or more later.</summary>
        Added,
        Payment,
        Interest
    }

    /// <summary>One line of a debt's history: a transfer (added / payment), the "already owed" start, or a payment's interest.</summary>
    public sealed class DebtMovement
    {
        public DebtMovementKind Kind { get; init; }

        public DebtDirection Direction { get; init; }

        public DateTime Date { get; init; }

        public long AmountCents { get; init; }

        public string CurrencyCode { get; init; } = string.Empty;

        /// <summary>The real account the money moved from or to; empty for the "already owed" start.</summary>
        public string AccountName { get; init; } = string.Empty;

        /// <summary>The transfer behind it (added / payment); null for the start and interest.</summary>
        public int? TransferId { get; init; }

        /// <summary>The interest transaction behind it.</summary>
        public int? TransactionId { get; init; }

        /// <summary>The "already owed" start (the hidden account's initial balance): it goes only with the whole debt.</summary>
        public bool CanDelete => TransferId != null || TransactionId != null;

        /// <summary>"Lent", "Borrowed", "Paid back", "Interest".</summary>
        public string KindText => Kind switch
        {
            DebtMovementKind.Payment => AppResources.DebtPaidBack,
            DebtMovementKind.Interest => AppResources.DebtInterest,
            _ => Direction == DebtDirection.Lent ? AppResources.DebtLent : AppResources.DebtBorrowed
        };

        /// <summary>"Oct 3, 2026 · Cash", or "Oct 3, 2026 · Already owed".</summary>
        public string DetailText => $"{DateFormatter.Format(Date, DateStyle.Medium)} · {(AccountName.Length > 0 ? AccountName : AppResources.DebtAlreadyOwed)}";

        public string AmountText => MoneyFormatter.Format(Money.FromCents(AmountCents), CurrencyCode);
    }
}
