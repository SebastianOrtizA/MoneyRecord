using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Helpers
{
    /// <summary>Count + noun in the current language: "1 transaction", "0 transactions" / "1 transacción".</summary>
    public static class Plurals
    {
        public static string Transactions(int count) =>
            string.Format(count == 1 ? AppResources.TransactionCountOne : AppResources.TransactionCountOther, count);
    }
}
