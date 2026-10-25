using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>One movement in a pocket's history, as the pocket sheet lists it.</summary>
    public sealed class PocketMovementRow(PocketMovement movement, string currencyCode)
    {
        public bool IsTakeOut => movement.AmountCents < 0;

        /// <summary>"Set aside" or "Take out".</summary>
        public string Title => IsTakeOut ? AppResources.TakeOut : AppResources.SetAside;

        /// <summary>"Oct 16, 2026", then the note when there is one.</summary>
        public string DetailText => string.IsNullOrWhiteSpace(movement.Note)
            ? DateFormatter.Format(movement.Date, DateStyle.Medium)
            : $"{DateFormatter.Format(movement.Date, DateStyle.Medium)} · {movement.Note}";

        /// <summary>"+$100.00" or "−$30.00".</summary>
        public string AmountText =>
            (IsTakeOut ? "−" : "+") + MoneyFormatter.Format(Math.Abs(movement.Amount), currencyCode);
    }
}
