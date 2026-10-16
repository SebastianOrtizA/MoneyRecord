using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>
/// ReminderService with the clock at 15 March 2026 09:00, reminders on and notifications allowed (unless a test says
/// otherwise). Shown notifications are collected in <see cref="_shown"/>.
/// </summary>
[Collection(LanguageCollection.Name)]
public sealed class ReminderServiceTests : LanguageTestBase
{
    private static readonly DateTime Today = new(2026, 3, 15);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 15, 9, 0, 0, TimeSpan.Zero));
    private readonly IRecurringTransactionService _recurring = Substitute.For<IRecurringTransactionService>();
    private readonly IBudgetRepository _budgets = Substitute.For<IBudgetRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IConversionService _conversion = Substitute.For<IConversionService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly FakePreferences _preferences = new();
    private readonly ReminderSettings _settings;
    private readonly List<AppNotification> _shown = [];
    private readonly ReminderService _sut;

    public ReminderServiceTests()
    {
        _settings = new ReminderSettings(_preferences) { Enabled = true };
        _notifications.IsSupported.Returns(true);
        _notifications.AreAllowed.Returns(true);
        _notifications.When(n => n.Show(Arg.Any<AppNotification>())).Do(c => _shown.Add(c.Arg<AppNotification>()));
        _recurring.GetUpcomingAsync(Arg.Any<int>()).Returns([]);
        _budgets.GetActiveBudgetsAsync().Returns([]);
        _categories.GetAllAsync().Returns([new Category { Id = 7, Name = "Food" }]);

        _sut = new ReminderService(_recurring, _budgets, _categories, _conversion, _settings, _notifications, _dialogs,
            _preferences, _clock, Substitute.For<ILogger<ReminderService>>());
    }

    private static UpcomingEntry Bill(int recurringId, int daysAway, string description = "Rent",
        TransactionType type = TransactionType.Expense, DateTime? date = null) => new()
    {
        RecurringId = recurringId,
        Date = date ?? Today.AddDays(daysAway),
        DaysAway = daysAway,
        Description = description,
        Amount = 850m,
        Type = type,
        AccountName = "Bank",
        CurrencyCode = "USD"
    };

    private void Spent(decimal food) =>
        _conversion.GetExpensesByCategoryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(new Dictionary<int, BudgetSpent> { [7] = new(food, []) });

    [Fact]
    public async Task Bills_due_within_the_chosen_days_are_reminded_once()
    {
        _settings.BillDaysBefore = 1;
        _recurring.GetUpcomingAsync(1).Returns([Bill(1, 1), Bill(2, 1, "Salary", TransactionType.Income)]);

        await _sut.RunDailyAsync();
        await _sut.RunDailyAsync();

        var reminder = _shown.Should().ContainSingle().Subject;
        reminder.Id.Should().Be(ReminderService.BillIdBase + 1);
        reminder.Channel.Should().Be(ReminderChannel.Bills);
        reminder.Title.Should().Be("Bill due: Rent");
        reminder.Message.Should().Be($"Tomorrow · {MoneyFormatter.Format(850m, "USD")} · Bank");
        reminder.Route.Should().Be(ReminderService.UpcomingRoute);
    }

    [Fact]
    public async Task The_next_occurrence_of_the_same_bill_is_reminded_again()
    {
        _settings.BillDaysBefore = 0;
        _recurring.GetUpcomingAsync(0).Returns([Bill(1, 0)]);
        await _sut.RunDailyAsync();

        _clock.Advance(TimeSpan.FromDays(7));
        _recurring.GetUpcomingAsync(0).Returns([Bill(1, 0, date: Today.AddDays(7))]);
        await _sut.RunDailyAsync();

        _shown.Should().HaveCount(2);
        _shown[1].Message.Should().StartWith("Today");
    }

    [Fact]
    public async Task Overdue_occurrences_are_not_reminded()
    {
        _recurring.GetUpcomingAsync(Arg.Any<int>()).Returns([Bill(1, 0, date: Today.AddDays(-2))]);

        await _sut.RunDailyAsync();

        _shown.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, true, 1)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, ReminderSettings.BillsOff)]
    public async Task Nothing_is_shown_when_reminders_are_off_or_blocked_but_recurring_still_runs(bool enabled,
        bool allowed, int billDays)
    {
        _settings.Enabled = enabled;
        _settings.BillDaysBefore = billDays;
        _notifications.AreAllowed.Returns(allowed);
        _recurring.GetUpcomingAsync(Arg.Any<int>()).Returns([Bill(1, 0)]);

        await _sut.RunDailyAsync();

        _shown.Should().BeEmpty();
        await _recurring.Received(1).ProcessDueTransactionsAsync();
    }

    [Fact]
    public async Task Added_recurring_transactions_are_counted_in_one_notification()
    {
        _recurring.ProcessDueTransactionsAsync().Returns(3);

        await _sut.RunDailyAsync();

        var added = _shown.Should().ContainSingle().Subject;
        added.Id.Should().Be(ReminderService.RecurringAddedId);
        added.Message.Should().Be("3 recurring transactions were added");
        added.Route.Should().Be(ReminderService.TransactionsRoute);
    }

    [Fact]
    public async Task Nothing_added_means_no_notification()
    {
        await _sut.RunDailyAsync();

        _shown.Should().BeEmpty();
    }

    [Fact]
    public async Task A_budget_alerts_at_80_percent_then_once_more_when_it_goes_over()
    {
        _budgets.GetActiveBudgetsAsync().Returns([new Budget { Id = 4, CategoryId = 7, LimitAmount = 100m }]);

        Spent(70m);
        await _sut.CheckBudgetsAsync();
        Spent(85m);
        await _sut.CheckBudgetsAsync();
        await _sut.CheckBudgetsAsync();
        Spent(120m);
        await _sut.CheckBudgetsAsync();
        await _sut.CheckBudgetsAsync();

        _shown.Select(n => n.Title).Should().Equal("Food: budget near its limit", "Food: over budget");
        _shown.Should().AllSatisfy(n =>
        {
            n.Id.Should().Be(ReminderService.BudgetIdBase + 4);
            n.Channel.Should().Be(ReminderChannel.Budgets);
            n.Route.Should().Be(ReminderService.BudgetsRoute);
        });
        _shown[1].Message.Should().Be($"{MoneyFormatter.Format(120m)} of {MoneyFormatter.Format(100m)}");
    }

    [Fact]
    public async Task A_new_budget_period_alerts_again()
    {
        _budgets.GetActiveBudgetsAsync().Returns([new Budget { Id = 4, CategoryId = 7, LimitAmount = 100m }]);
        Spent(120m);
        await _sut.CheckBudgetsAsync();

        _clock.Advance(TimeSpan.FromDays(31));
        await _sut.CheckBudgetsAsync();

        _shown.Should().HaveCount(2);
    }

    [Fact]
    public async Task Changing_the_budget_period_starts_its_alerts_over()
    {
        // Month → Year mid-year: the year's window starts before the month's, and still alerts
        var food = new Budget { Id = 4, CategoryId = 7, LimitAmount = 100m, CreatedDate = Today.AddMonths(-2) };
        _budgets.GetActiveBudgetsAsync().Returns([food]);
        Spent(120m);
        await _sut.CheckBudgetsAsync();

        food.Period = BudgetPeriod.Year;
        await _sut.CheckBudgetsAsync();

        _shown.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_new_budget_with_a_reused_id_alerts_again()
    {
        Spent(120m);
        _budgets.GetActiveBudgetsAsync().Returns([new Budget { Id = 4, CategoryId = 7, LimitAmount = 100m, CreatedDate = Today.AddDays(-9) }]);
        await _sut.CheckBudgetsAsync();

        _budgets.GetActiveBudgetsAsync().Returns([new Budget { Id = 4, CategoryId = 7, LimitAmount = 100m, CreatedDate = Today }]);
        await _sut.CheckBudgetsAsync();

        _shown.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_failing_budget_check_or_offer_after_a_save_is_only_logged()
    {
        // The entry is saved already: the form must still close
        _budgets.GetActiveBudgetsAsync().Returns(Task.FromException<List<Budget>>(new InvalidOperationException()));
        await _sut.Invoking(s => s.CheckBudgetsAsync()).Should().NotThrowAsync();

        _settings.Enabled = false;
        _dialogs.ConfirmAsync(default!, default!, default!, default!)
            .ReturnsForAnyArgs(Task.FromException<bool>(new InvalidOperationException()));
        await _sut.Invoking(s => s.OfferAsync()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task Budget_alerts_can_be_turned_off()
    {
        _settings.BudgetAlerts = false;
        _budgets.GetActiveBudgetsAsync().Returns([new Budget { Id = 4, CategoryId = 7, LimitAmount = 100m }]);
        Spent(120m);

        await _sut.CheckBudgetsAsync();

        _shown.Should().BeEmpty();
    }

    [Fact]
    public async Task The_offer_is_shown_once_and_accepting_turns_reminders_on()
    {
        _settings.Enabled = false;
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        _notifications.RequestPermissionAsync().Returns(true);

        await _sut.OfferAsync();
        await _sut.OfferAsync();

        _settings.Enabled.Should().BeTrue();
        await _dialogs.ReceivedWithAnyArgs(1).ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Declining_the_offer_leaves_reminders_off_without_asking_the_system()
    {
        _settings.Enabled = false;

        await _sut.OfferAsync();

        _settings.Enabled.Should().BeFalse();
        _settings.Offered.Should().BeTrue();
        await _notifications.DidNotReceive().RequestPermissionAsync();
    }

    [Fact]
    public async Task No_offer_where_notifications_are_not_supported()
    {
        _settings.Enabled = false;
        _notifications.IsSupported.Returns(false);

        await _sut.OfferAsync();

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Blocked_notifications_keep_reminders_off_and_can_open_the_system_settings()
    {
        _settings.Enabled = false;
        _notifications.RequestPermissionAsync().Returns(false);
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);

        (await _sut.EnableAsync()).Should().BeFalse();

        _settings.Enabled.Should().BeFalse();
        _notifications.Received(1).OpenSystemSettings();
    }
}

[Collection(LanguageCollection.Name)]
public sealed class BillReminderOptionTests : LanguageTestBase
{
    [Fact]
    public void The_choices_are_named_in_the_current_language()
    {
        BillReminderOption.All().Select(o => o.Name).Should()
            .Equal("Off", "On the day", "1 day before", "2 days before", "3 days before", "7 days before");
    }

    [Fact]
    public void Bill_reminders_default_to_one_day_before_and_reminders_start_off()
    {
        var settings = new ReminderSettings(new FakePreferences());

        settings.Enabled.Should().BeFalse();
        settings.BillDaysBefore.Should().Be(1);
        settings.BudgetAlerts.Should().BeTrue();
        settings.RecurringAdded.Should().BeTrue();
    }
}
