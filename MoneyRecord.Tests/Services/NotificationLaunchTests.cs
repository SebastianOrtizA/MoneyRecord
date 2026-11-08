using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Models;
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
        private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 15, 9, 0, 0, TimeSpan.Zero));
        private readonly PrivacySettings _settings = new(new FakePreferences()) { LockEnabled = true, LockAfterMinutes = 5 };
        private readonly AppLock _appLock;
        private readonly NotificationLaunch _launch;

        public NotificationLaunchTests()
        {
            var deviceLock = Substitute.For<IDeviceLock>();
            deviceLock.IsAvailable.Returns(true);
            _appLock = new AppLock(_settings, deviceLock, _clock);
            _launch = new NotificationLaunch(_appLock, _navigation, _logger);
        }

        [Fact]
        public async Task A_page_requested_at_a_locked_start_waits_for_the_unlock_and_opens_once()
        {
            _appLock.LockOnStart();
            _launch.Request("AddTransactionPage?Type=Expense");

            // Home appears under the lock screen: nothing may open yet
            await _launch.OpenPendingAsync();
            await _navigation.DidNotReceiveWithAnyArgs().NavigateToAsync(default!);

            _appLock.Unlock();
            await _launch.OpenPendingAsync();
            await _launch.OpenPendingAsync();

            await _navigation.Received(1).NavigateToAsync("AddTransactionPage?Type=Expense");
        }

        [Fact]
        public async Task A_quick_entry_requested_at_a_locked_start_waits_for_the_unlock()
        {
            var ran = new List<int>();
            _launch.RunQuickEntryAsync = id => { ran.Add(id); return Task.CompletedTask; };
            _appLock.LockOnStart();
            _launch.Request(NotificationLaunch.QuickEntryRoute(3));

            await _launch.OpenPendingAsync();
            ran.Should().BeEmpty();

            _appLock.Unlock();
            await _launch.OpenPendingAsync();

            ran.Should().Equal(3);
        }

        [Fact]
        public async Task A_page_requested_while_the_app_is_away_waits_for_the_return_to_decide_the_lock()
        {
            // Android delivers the new intent before the app comes back and knows whether it locks
            _appLock.LockOnStart();
            _appLock.Unlock();
            _appLock.Backgrounded();
            _clock.Advance(TimeSpan.FromMinutes(10));
            _launch.Request("//BudgetPage");

            await _launch.OpenPendingAsync();
            await _navigation.DidNotReceiveWithAnyArgs().NavigateToAsync(default!);

            // Back after the delay: the lock screen is up, the page still waits
            _appLock.LockOnResume().Should().BeTrue();
            await _launch.OpenPendingAsync();
            await _navigation.DidNotReceiveWithAnyArgs().NavigateToAsync(default!);

            _appLock.Unlock();
            await _launch.OpenPendingAsync();

            await _navigation.Received(1).NavigateToAsync("//BudgetPage");
        }

        [Fact]
        public async Task A_page_requested_while_away_opens_on_a_return_that_does_not_lock()
        {
            _appLock.LockOnStart();
            _appLock.Unlock();
            _appLock.Backgrounded();
            _clock.Advance(TimeSpan.FromMinutes(1));
            _launch.Request("//BudgetPage");

            _appLock.LockOnResume().Should().BeFalse();
            await _launch.OpenPendingAsync();

            await _navigation.Received(1).NavigateToAsync("//BudgetPage");
        }

        [Fact]
        public async Task Without_the_app_lock_the_page_opens_at_once()
        {
            _settings.LockEnabled = false;
            _appLock.LockOnStart();
            _launch.Request("//BudgetPage");

            await _launch.OpenPendingAsync();

            await _navigation.Received(1).NavigateToAsync("//BudgetPage");
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
        public async Task A_quick_entry_route_runs_it_instead_of_opening_a_page()
        {
            var ran = new List<int>();
            _launch.RunQuickEntryAsync = id => { ran.Add(id); return Task.CompletedTask; };
            _launch.Request(NotificationLaunch.QuickEntryRoute(7));

            await _launch.OpenPendingAsync();

            ran.Should().Equal(7);
            await _navigation.DidNotReceiveWithAnyArgs().NavigateToAsync(default!);
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
