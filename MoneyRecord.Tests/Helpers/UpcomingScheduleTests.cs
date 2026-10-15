using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class UpcomingScheduleTests
{
    private static readonly DateTime Today = new(2026, 3, 15);

    private static RecurringTransaction Recurring(RecurrenceFrequency frequency, DateTime next, DateTime? end = null,
        bool active = true, int id = 1, string description = "Gym", int categoryId = 3, int accountId = 2) => new()
    {
        Id = id,
        Description = description,
        Amount = 20m,
        CategoryId = categoryId,
        AccountId = accountId,
        TransactionType = TransactionType.Expense,
        Frequency = frequency,
        StartDate = next,
        NextOccurrence = next,
        EndDate = end,
        IsActive = active
    };

    private static List<DateTime> Occurrences(RecurringTransaction recurring) =>
        UpcomingSchedule.Occurrences(recurring, Today, Today.AddDays(UpcomingSchedule.DefaultDays)).ToList();

    [Fact]
    public void Weekly_occurrences_cover_today_through_30_days_later()
    {
        Occurrences(Recurring(RecurrenceFrequency.Weekly, Today))
            .Should().Equal(Today, Today.AddDays(7), Today.AddDays(14), Today.AddDays(21), Today.AddDays(28));
    }

    [Fact]
    public void A_monthly_occurrence_on_the_31st_falls_on_the_last_day_of_a_shorter_month()
    {
        var recurring = Recurring(RecurrenceFrequency.Monthly, new DateTime(2026, 1, 31));
        recurring.NextOccurrence = new DateTime(2026, 3, 31);

        UpcomingSchedule.Occurrences(recurring, Today, new DateTime(2026, 5, 31))
            .Should().Equal(new DateTime(2026, 3, 31), new DateTime(2026, 4, 30), new DateTime(2026, 5, 31));
    }

    [Fact]
    public void Occurrences_already_due_are_skipped()
    {
        // Not created yet (the app creates them when it starts), so not upcoming either
        Occurrences(Recurring(RecurrenceFrequency.Daily, Today.AddDays(-3))).Should().HaveCount(31)
            .And.StartWith(Today);
    }

    [Fact]
    public void Occurrences_stop_at_the_end_date_including_it()
    {
        Occurrences(Recurring(RecurrenceFrequency.Weekly, Today, end: Today.AddDays(14)))
            .Should().Equal(Today, Today.AddDays(7), Today.AddDays(14));
    }

    [Fact]
    public void An_inactive_recurrence_has_no_occurrences()
    {
        Occurrences(Recurring(RecurrenceFrequency.Daily, Today, active: false)).Should().BeEmpty();
    }

    [Fact]
    public void Build_orders_by_date_and_fills_in_category_and_account()
    {
        var categories = new Dictionary<int, Category> { [3] = new() { Id = 3, Name = "Food", IconCode = "F025A" } };
        var accounts = new Dictionary<int, Account> { [2] = new() { Id = 2, Name = "Bank", CurrencyCode = "EUR" } };
        var rent = Recurring(RecurrenceFrequency.Monthly, Today.AddDays(10), id: 1, description: "Rent");
        var salary = Recurring(RecurrenceFrequency.Monthly, Today.AddDays(2), id: 2, description: "Salary",
            categoryId: 99, accountId: 99);
        salary.TransactionType = TransactionType.Income;

        var entries = UpcomingSchedule.Build([rent, salary], categories, accounts, Today.AddHours(10));

        entries.Select(e => e.Description).Should().Equal("Salary", "Rent");
        entries[0].DaysAway.Should().Be(2);
        entries[0].SignedAmount.Should().Be(20m);
        entries[0].CategoryName.Should().BeEmpty();
        entries[1].DaysAway.Should().Be(10);
        entries[1].SignedAmount.Should().Be(-20m);
        entries[1].CategoryName.Should().Be("Food");
        entries[1].CategoryIconCode.Should().Be("F025A");
        entries[1].AccountName.Should().Be("Bank");
        entries[1].CurrencyCode.Should().Be("EUR");
    }
}

[Collection(LanguageCollection.Name)]
public sealed class UpcomingEntryTextTests : LanguageTestBase
{
    [Theory]
    [InlineData(0, "Today")]
    [InlineData(1, "Tomorrow")]
    [InlineData(5, "In 5 days")]
    public void WhenText_says_today_tomorrow_or_in_n_days(int daysAway, string expected)
    {
        new UpcomingEntry { DaysAway = daysAway }.WhenText.Should().Be(expected);
    }
}
