namespace MoneyRecord.Services.Interfaces
{
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

        /// <summary>Lets the user pick one of <paramref name="options"/>; null when the user cancels.</summary>
        Task<string?> ActionSheetAsync(string title, string cancel, IReadOnlyList<string> options);

        /// <summary>Shows a short message that disappears on its own.</summary>
        Task ToastAsync(string message);

        /// <summary>
        /// Shows a message with an Undo button for a few seconds. <paramref name="undo"/> runs when Undo is tapped;
        /// <paramref name="expired"/> when the message goes away without it (timeout, or another message replacing it).
        /// </summary>
        Task ToastWithUndoAsync(string message, Func<Task> undo, Func<Task>? expired = null);
    }
}
