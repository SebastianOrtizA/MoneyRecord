using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class ErrorHandlerTests
{
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly RecordingLogger _logger = new();

    private ErrorHandler CreateHandler() => new(_dialogs, _logger);

    [Fact]
    public async Task Shows_the_user_message_and_logs_the_exception()
    {
        var ex = new InvalidOperationException("db locked");

        await CreateHandler().HandleAsync(ex, "Could not save");

        await _dialogs.Received(1).AlertAsync(AppResources.Error, "Could not save");
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Exception == ex);
    }

    [Fact]
    public async Task Shows_a_message_without_an_exception()
    {
        await CreateHandler().HandleAsync("Please enter a valid amount");

        await _dialogs.Received(1).AlertAsync(AppResources.Error, "Please enter a valid amount");
        _logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Ignores_a_cancelled_load()
    {
        await CreateHandler().HandleAsync(new OperationCanceledException(), "Could not load");

        await _dialogs.DidNotReceiveWithAnyArgs().AlertAsync(default!, default!);
        _logger.Entries.Should().BeEmpty();
    }

    private sealed class RecordingLogger : ILogger<ErrorHandler>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, exception));
    }
}
