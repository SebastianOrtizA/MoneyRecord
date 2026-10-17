using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// What a receipt reading changes in the add form: its total fills an empty amount and, for a new entry, its date the
    /// date (keeping the time of day). A total that differs from the amount already typed is only mentioned
    /// (<see cref="Differs"/>), and the date is left alone then: the photo may be of another purchase.
    /// </summary>
    public sealed record ReceiptFill(decimal? Amount, DateTime? Date, decimal? Differs)
    {
        public bool ChangesNothing => Amount == null && Date == null;

        /// <param name="typedCents">The amount in the form; null (or zero) while it is empty.</param>
        public static ReceiptFill Apply(ReceiptReading reading, long? typedCents, bool isNewEntry, DateTime currentDate)
        {
            decimal? amount = null;
            if (reading.Amount is { } total)
            {
                if (typedCents is null or 0)
                    amount = total;
                else if (typedCents != Money.ToCents(total))
                    return new ReceiptFill(null, null, total);
            }

            DateTime? date = reading.Date is { } day && isNewEntry && day.Date != currentDate.Date
                ? day.Date + currentDate.TimeOfDay
                : null;
            return new ReceiptFill(amount, date, null);
        }
    }
}
