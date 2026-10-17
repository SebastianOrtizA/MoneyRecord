namespace MoneyRecord.Models
{
    /// <summary>What a home-screen balance widget shows: one account's balance and the routes of its add buttons.</summary>
    public sealed class WidgetContent
    {
        /// <summary>Empty when the user has no account (the buttons still open the forms).</summary>
        public string AccountName { get; init; } = string.Empty;

        /// <summary>The formatted balance, "••••••" while balances are hidden, empty without an account.</summary>
        public string BalanceText { get; init; } = string.Empty;

        public string ExpenseRoute { get; init; } = string.Empty;

        public string IncomeRoute { get; init; } = string.Empty;

        public string TransferRoute { get; init; } = string.Empty;
    }
}
