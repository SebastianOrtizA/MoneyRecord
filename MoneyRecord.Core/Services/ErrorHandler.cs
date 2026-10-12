using Microsoft.Extensions.Logging;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class ErrorHandler : IErrorHandler
    {
        private readonly IDialogService _dialogService;
        private readonly ILogger<ErrorHandler> _logger;

        public ErrorHandler(IDialogService dialogService, ILogger<ErrorHandler> logger)
        {
            _dialogService = dialogService;
            _logger = logger;
        }

        public async Task HandleAsync(Exception ex, string userMessage)
        {
            // A load replaced by a newer one is not an error
            if (ex is OperationCanceledException)
                return;

            // The exception goes to the log only: the user sees the localized message, never ex.Message
            _logger.LogError(ex, "{UserMessage}", userMessage);
            await _dialogService.AlertAsync(AppResources.Error, userMessage);
        }

        public async Task HandleAsync(string userMessage)
        {
            _logger.LogDebug("{UserMessage}", userMessage);
            await _dialogService.AlertAsync(AppResources.Error, userMessage);
        }
    }
}
