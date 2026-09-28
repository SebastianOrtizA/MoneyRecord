using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class ErrorHandler : IErrorHandler
    {
        public async Task HandleAsync(Exception ex, string userMessage)
        {
            System.Diagnostics.Debug.WriteLine($"[MoneyRecord Error] {userMessage}: {ex}");
            await ShowErrorAsync(userMessage);
        }

        public async Task HandleAsync(string userMessage)
        {
            System.Diagnostics.Debug.WriteLine($"[MoneyRecord Error] {userMessage}");
            await ShowErrorAsync(userMessage);
        }

        private static async Task ShowErrorAsync(string message)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, message, AppResources.OK);
            });
        }
    }
}
