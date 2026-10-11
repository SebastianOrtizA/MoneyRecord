using CommunityToolkit.Maui.Alerts;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// <see cref="IDialogService"/> on top of the Shell dialogs and the CommunityToolkit toast.
    /// </summary>
    public sealed class DialogService : IDialogService
    {
        public Task AlertAsync(string title, string message, string? close = null) =>
            MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.DisplayAlertAsync(title, message, close ?? AppResources.OK));

        public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
            MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.DisplayAlertAsync(title, message, accept, cancel));

        public Task<string?> PromptAsync(string title, string message, string accept, string cancel,
            string? initialValue = null, bool numeric = false) =>
            MainThread.InvokeOnMainThreadAsync<string?>(() =>
                Shell.Current.DisplayPromptAsync(title, message, accept, cancel,
                    initialValue: initialValue ?? string.Empty,
                    keyboard: numeric ? Keyboard.Numeric : Keyboard.Default));

        public async Task<string?> ActionSheetAsync(string title, string cancel, IReadOnlyList<string> options)
        {
            var choice = await MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.DisplayActionSheetAsync(title, cancel, null, options.ToArray()));

            return choice == null || choice == cancel ? null : choice;
        }

        public Task ToastAsync(string message) =>
            MainThread.InvokeOnMainThreadAsync(() => Toast.Make(message).Show());
    }
}
