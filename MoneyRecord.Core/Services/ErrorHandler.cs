using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class ErrorHandler : IErrorHandler
    {
        private readonly IDialogService _dialogService;

        public ErrorHandler(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public async Task HandleAsync(Exception ex, string userMessage)
        {
            System.Diagnostics.Debug.WriteLine($"[MoneyRecord Error] {userMessage}: {ex}");
            await _dialogService.AlertAsync(AppResources.Error, userMessage);
        }

        public async Task HandleAsync(string userMessage)
        {
            System.Diagnostics.Debug.WriteLine($"[MoneyRecord Error] {userMessage}");
            await _dialogService.AlertAsync(AppResources.Error, userMessage);
        }
    }
}
