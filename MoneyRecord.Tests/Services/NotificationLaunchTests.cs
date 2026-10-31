using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MoneyRecord.Tests.Services
{
    public class NotificationLaunchTests
    {
        private readonly INavigationService _navigation = Substitute.For<INavigationService>();
        private readonly RecordingLogger _logger = new();
        private readonly NotificationLaunch _launch;

        public NotificationLaunchTests()
        {
            _launch = new NotificationLaunch(_navigation, _logger);
        }

        [Fact]
        public async Task The_requested_page_opens_once()
        {
            _launch.Request("//BudgetPage");

            await _launch.OpenPendingAsync();
            await _launch.OpenPendingAsync();

            await _navigation.Received(1).NavigateToAsync("//BudgetPage");
        }

        [Fact]
        public async Task Nothing_opens_without_a_request()
        {
            await _launch.OpenPendingAsync();

            await _navigation.DidNotReceiveWithAnyArgs().NavigateToAsync(default!);
        }

        [Fact]
        public async Task A_route_that_cannot_be_opened_is_logged_as_an_error_not_thrown()
        {
            var ex = new ArgumentException("unable to figure out route");
            _navigation.NavigateToAsync("//Nowhere").ThrowsAsync(ex);
            _launch.Request("//Nowhere");

            var open = () => _launch.OpenPendingAsync();

            await open.Should().NotThrowAsync();
            _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Exception == ex);
        }

        [Fact]
        public async Task A_cancelled_navigation_is_not_an_error()
        {
            _navigation.NavigateToAsync("//BudgetPage").ThrowsAsync(new OperationCanceledException());
            _launch.Request("//BudgetPage");

            await _launch.OpenPendingAsync();

            _logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Warning);
        }

        private sealed class RecordingLogger : ILogger<NotificationLaunch>
        {
            public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, exception));
        }
    }
}
