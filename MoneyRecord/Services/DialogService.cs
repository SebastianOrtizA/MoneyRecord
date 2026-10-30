using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// <see cref="IDialogService"/> on top of the page dialogs and the CommunityToolkit toast.
    /// </summary>
    public sealed class DialogService(ILogger<DialogService> logger) : IDialogService
    {
        private static readonly TimeSpan UndoWindow = TimeSpan.FromSeconds(6);

        private PendingUndo? _pending;
        private ISnackbar? _snackbar;

        // The window's page: AppShell, or the first-run setup before the Shell exists
        private static Page CurrentPage =>
            Application.Current?.Windows.FirstOrDefault()?.Page
            ?? throw new InvalidOperationException("No window is open.");

        public Task AlertAsync(string title, string message, string? close = null) =>
            MainThread.InvokeOnMainThreadAsync(() =>
                CurrentPage.DisplayAlertAsync(title, message, close ?? AppResources.OK));

        public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
            MainThread.InvokeOnMainThreadAsync(() =>
                CurrentPage.DisplayAlertAsync(title, message, accept, cancel));

        public Task<string?> PromptAsync(string title, string message, string accept, string cancel,
            string? initialValue = null, bool numeric = false) =>
            MainThread.InvokeOnMainThreadAsync<string?>(() =>
                CurrentPage.DisplayPromptAsync(title, message, accept, cancel,
                    initialValue: initialValue ?? string.Empty,
                    keyboard: numeric ? Keyboard.Numeric : Keyboard.Default));

        public async Task<string?> PasswordAsync(string title, string message, bool confirm = false)
        {
            // The page is created on the UI thread too, like every dialog here
            var page = await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var created = new Views.PasswordPage(title, message, confirm);
                await CurrentPage.Navigation.PushModalAsync(created, animated: false);
                return created;
            });
            return await page.Result;
        }

        public async Task<string?> ActionSheetAsync(string title, string cancel, IReadOnlyList<string> options)
        {
            var choice = await MainThread.InvokeOnMainThreadAsync(() =>
                CurrentPage.DisplayActionSheetAsync(title, cancel, null, options.ToArray()));

            return choice == null || choice == cancel ? null : choice;
        }

        public Task ToastAsync(string message) =>
            MainThread.InvokeOnMainThreadAsync(() => Toast.Make(message).Show());

        public async Task ToastWithUndoAsync(string message, Func<Task> undo, Func<Task>? expired = null)
        {
            var pending = new PendingUndo(expired);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // One at a time: showing another makes the one on screen final
                await ExpireAsync(_pending);
                if (_snackbar != null && Snackbar.IsShown)
                    await _snackbar.Dismiss();

                var snackbar = Snackbar.Make(message, () =>
                {
                    if (pending.TrySettle())
                        _ = RunAsync(undo);
                }, AppResources.Undo, UndoWindow);
                _pending = pending;
                _snackbar = snackbar;
                await snackbar.Show();
            });

            // Not on the snackbar's own dismissal (a static event nothing ties to this one): a little after it goes away
            _ = Task.Delay(UndoWindow + TimeSpan.FromSeconds(1)).ContinueWith(_ => ExpireAsync(pending), TaskScheduler.Default).Unwrap();
        }

        private Task ExpireAsync(PendingUndo? pending) =>
            pending is { } p && p.TrySettle() && p.Expired != null ? RunAsync(p.Expired) : Task.CompletedTask;

        // The callbacks belong to the caller, which reports its own failures; this only keeps one from escaping a UI callback
        private async Task RunAsync(Func<Task> callback)
        {
            try
            {
                await callback();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "A snackbar callback failed");
            }
        }
        /// <summary>An undo offer that ends exactly once: by Undo or by expiring.</summary>
        private sealed class PendingUndo(Func<Task>? expired)
        {
            private int _settled;

            public Func<Task>? Expired { get; } = expired;

            public bool TrySettle() => Interlocked.Exchange(ref _settled, 1) == 0;
        }

    }
}
