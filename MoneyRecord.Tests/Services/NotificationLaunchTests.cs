using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
        private readonly NotificationLaunch _launch;

        public NotificationLaunchTests()
        {
            _launch = new NotificationLaunch(_navigation, NullLogger<NotificationLaunch>.Instance);
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
        public async Task A_route_that_cannot_be_opened_is_logged_not_thrown()
        {
            _navigation.NavigateToAsync("//Nowhere").ThrowsAsync(new ArgumentException("unable to figure out route"));
            _launch.Request("//Nowhere");

            var open = () => _launch.OpenPendingAsync();

            await open.Should().NotThrowAsync();
        }
    }
}
