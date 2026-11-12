namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// What the conversion question shows when an account is deleted into the default account of another currency: the
    /// two accounts and currencies, how much moves, the net total of the transactions in the old currency and a rate to
    /// start from (the downloaded one, when there is one).
    /// </summary>
    public sealed record AccountConversionRequest(string AccountName, string DefaultAccountName, string FromCurrency,
        string ToCurrency, int Transactions, int RecurringRules, long NetCents, decimal? SuggestedRate);

    /// <summary>
    /// Alerts, questions and toasts, so ViewModels never touch <c>Shell.Current</c> and tests can answer them.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>Shows a message with a single button (<paramref name="close"/>, OK by default).</summary>
        Task AlertAsync(string title, string message, string? close = null);

        /// <summary>Asks a yes/no question; true when the user picks <paramref name="accept"/>.</summary>
        Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

        /// <summary>Asks for a line of text; null when the user cancels.</summary>
        Task<string?> PromptAsync(string title, string message, string accept, string cancel,
            string? initialValue = null, bool numeric = false);

        /// <summary>
        /// Asks for a password (typed hidden); with <paramref name="confirm"/> it is typed twice and must match. Null when
        /// the user cancels.
        /// </summary>
        Task<string?> PasswordAsync(string title, string message, bool confirm = false);

        /// <summary>
        /// Asks for the rate that converts an account's amounts into the default account's currency, as a rate or as the
        /// final amount the two are linked by; null when the user cancels. Confirming it is also confirming the delete.
        /// </summary>
        Task<decimal?> ConvertAccountAsync(AccountConversionRequest request);

        /// <summary>Lets the user pick one of <paramref name="options"/>; null when the user cancels.</summary>
        Task<string?> ActionSheetAsync(string title, string cancel, IReadOnlyList<string> options);

        /// <summary>Shows a short message that disappears on its own.</summary>
        Task ToastAsync(string message);

        /// <summary>
        /// Shows a message with an Undo button for a few seconds. <paramref name="undo"/> runs when Undo is tapped;
        /// <paramref name="expired"/> when the message goes away without it (timeout, or another message replacing it).
        /// </summary>
        Task ToastWithUndoAsync(string message, Func<Task> undo, Func<Task> expired);
    }
}
