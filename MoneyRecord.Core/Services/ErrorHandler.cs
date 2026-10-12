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
            // Loads catch their own cancellation; one that gets here is not the user's error, but leave a trace
            if (ex is OperationCanceledException)
            {
                _logger.LogDebug(ex, "Cancelled: {UserMessage}", userMessage);
                return;
            }

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
