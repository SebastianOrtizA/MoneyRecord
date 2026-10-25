using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>
/// RecurringTransactionService against a real temp SQLite file, with the clock fixed at 15 March 2026 10:00 (local time
/// is UTC in <see cref="FakeTimeProvider"/>). Occurrences are dates at 00:00.
/// </summary>
public sealed class RecurringTransactionServiceTests : IAsyncLifetime
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-recurring-{Guid.NewGuid():N}");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero));
    private DatabaseInitializer _dbInitializer = null!;
    private RecurringTransactionService _sut = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        _dbInitializer = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _dbInitializer.GetConnectionAsync();
        _sut = new RecurringTransactionService(new RecurringTransactionRepository(_dbInitializer),
            new CategoryRepository(_dbInitializer), new AccountRepository(_dbInitializer), _dbInitializer, _clock);
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private async Task<RecurringTransaction> AddAsync(RecurrenceFrequency frequency, DateTime start,
        DateTime? end = null, bool active = true, int? dayOfMonth = null)
    {
        var recurring = new RecurringTransaction
        {
            Amount = 25.50m,
            Description = "Gym",
            CategoryId = 3,
            AccountId = 2,
            TransactionType = TransactionType.Expense,
            Frequency = frequency,
            DayOfMonth = dayOfMonth,
            StartDate = start,
            NextOccurrence = start,
            EndDate = end,
            IsActive = active,
        };
        await Db.InsertAsync(recurring);
        return recurring;
    }

    private Task<List<Transaction>> TransactionsAsync() => Db.Table<Transaction>().OrderBy(t => t.Date).ToListAsync();

    private Task<RecurringTransaction> ReloadAsync(RecurringTransaction recurring) =>
        Db.GetAsync<RecurringTransaction>(recurring.Id);

    [Fact]
    public async Task Missed_months_are_caught_up_on_the_last_day_of_short_months()
    {
        var gym = await AddAsync(RecurrenceFrequency.Monthly, new DateTime(2026, 1, 31));

        (await _sut.ProcessDueTransactionsAsync()).Created.Should().Be(2);

        var created = await TransactionsAsync();
        created.Select(t => t.Date).Should().Equal(new DateTime(2026, 1, 31), new DateTime(2026, 2, 28));
        created.Should().AllSatisfy(t =>
        {
            t.Amount.Should().Be(25.50m);
            t.Description.Should().Be("Gym");
            t.CategoryId.Should().Be(3);
            t.AccountId.Should().Be(2);
            t.Type.Should().Be(TransactionType.Expense);
        });

        var saved = await ReloadAsync(gym);
        saved.NextOccurrence.Should().Be(new DateTime(2026, 3, 31));
        saved.LastProcessedDate.Should().Be(new DateTime(2026, 2, 28));
        saved.IsActive.Should().BeTrue();
    }

    private async Task<RecurringTransaction> AddVariableAsync(DateTime start, long lastCents = 0, DateTime? end = null)
    {
        var recurring = await AddAsync(RecurrenceFrequency.Monthly, start, end);
        recurring.IsVariableAmount = true;
        recurring.LastAmountCents = lastCents;
        await Db.UpdateAsync(recurring);
        return recurring;
    }

    private static Transaction Entry(DateTime date, decimal amount) => new()
    {
        Date = date, Amount = amount, Description = "Gym", CategoryId = 3, AccountId = 2, Type = TransactionType.Expense
    };

    [Fact]
    public async Task A_variable_amount_schedule_is_never_created_by_processing()
    {
        var gym = await AddVariableAsync(new DateTime(2026, 2, 10));

        (await _sut.ProcessDueTransactionsAsync()).Created.Should().Be(0);

        (await TransactionsAsync()).Should().BeEmpty();
        (await ReloadAsync(gym)).NextOccurrence.Should().Be(new DateTime(2026, 2, 10));
    }

    [Fact]
    public async Task Missed_variable_occurrences_wait_oldest_first()
    {
        await AddVariableAsync(new DateTime(2026, 2, 10), lastCents: 8500);

        var pending = await _sut.GetPendingAsync();

        pending.Select(e => e.Date).Should().Equal(new DateTime(2026, 2, 10), new DateTime(2026, 3, 10));
        pending.Should().AllSatisfy(e => { e.IsPending.Should().BeTrue(); e.Amount.Should().Be(85m); });
    }

    [Fact]
    public async Task A_future_variable_occurrence_is_upcoming_but_not_pending()
    {
        await AddVariableAsync(new DateTime(2026, 3, 20));

        (await _sut.GetPendingAsync()).Should().BeEmpty();
        (await _sut.GetUpcomingAsync()).Should().ContainSingle().Which.IsVariable.Should().BeTrue();
    }

    [Fact]
    public async Task Entering_an_occurrence_saves_it_remembers_the_amount_and_moves_on()
    {
        var power = await AddVariableAsync(new DateTime(2026, 2, 10));

        await _sut.EnterOccurrenceAsync(power.Id, Entry(new DateTime(2026, 2, 10), 85m));

        (await TransactionsAsync()).Should().ContainSingle().Which.Amount.Should().Be(85m);
        var saved = await ReloadAsync(power);
        saved.LastAmountCents.Should().Be(8500);
        saved.LastProcessedDate.Should().Be(new DateTime(2026, 2, 10));
        saved.NextOccurrence.Should().Be(new DateTime(2026, 3, 10));
        (await _sut.GetPendingAsync()).Should().ContainSingle().Which.Date.Should().Be(new DateTime(2026, 3, 10));
    }

    [Fact]
    public async Task Only_the_next_occurrence_can_be_entered()
    {
        var power = await AddVariableAsync(new DateTime(2026, 2, 10));

        var act = () => _sut.EnterOccurrenceAsync(power.Id, Entry(new DateTime(2026, 3, 10), 85m));

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await TransactionsAsync()).Should().BeEmpty();
        (await ReloadAsync(power)).NextOccurrence.Should().Be(new DateTime(2026, 2, 10));
    }

    [Fact]
    public async Task A_fixed_schedule_or_a_future_occurrence_cannot_be_entered()
    {
        var fixedGym = await AddAsync(RecurrenceFrequency.Monthly, new DateTime(2026, 2, 10));
        var later = await AddVariableAsync(new DateTime(2026, 3, 20));

        var enterFixed = () => _sut.EnterOccurrenceAsync(fixedGym.Id, Entry(new DateTime(2026, 2, 10), 1m));
        var skipLater = () => _sut.SkipOccurrenceAsync(later.Id);

        await enterFixed.Should().ThrowAsync<InvalidOperationException>();
        await skipLater.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Skipping_moves_on_without_creating_anything_or_changing_the_estimate()
    {
        var power = await AddVariableAsync(new DateTime(2026, 3, 10), lastCents: 8500);

        await _sut.SkipOccurrenceAsync(power.Id);

        (await TransactionsAsync()).Should().BeEmpty();
        var saved = await ReloadAsync(power);
        saved.NextOccurrence.Should().Be(new DateTime(2026, 4, 10));
        saved.LastAmountCents.Should().Be(8500);
        (await _sut.GetPendingAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Entering_the_last_occurrence_deactivates_the_schedule()
    {
        var power = await AddVariableAsync(new DateTime(2026, 3, 10), end: new DateTime(2026, 3, 31));

        await _sut.EnterOccurrenceAsync(power.Id, Entry(new DateTime(2026, 3, 10), 40m));

        (await ReloadAsync(power)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task An_occurrence_is_due_from_the_start_of_its_day()
    {
        await AddAsync(RecurrenceFrequency.Weekly, new DateTime(2026, 3, 15));
        await AddAsync(RecurrenceFrequency.Weekly, new DateTime(2026, 3, 16));

        (await _sut.ProcessDueTransactionsAsync()).Created.Should().Be(1);

        (await TransactionsAsync()).Single().Date.Should().Be(new DateTime(2026, 3, 15));
    }

    [Fact]
    public async Task Occurrences_get_the_payee_of_the_recurrence()
    {
        var recurring = await AddAsync(RecurrenceFrequency.Monthly, new DateTime(2026, 2, 15));
        recurring.PayeeId = 7;
        await Db.UpdateAsync(recurring);

        await _sut.ProcessDueTransactionsAsync();

        (await TransactionsAsync()).Select(t => t.PayeeId).Should().Equal(7, 7);
    }

    [Fact]
    public async Task Running_again_creates_nothing_new()
    {
        await AddAsync(RecurrenceFrequency.Daily, new DateTime(2026, 3, 10));

        (await _sut.ProcessDueTransactionsAsync()).Created.Should().Be(6);
        (await _sut.ProcessDueTransactionsAsync()).Created.Should().Be(0);

        _clock.Advance(TimeSpan.FromDays(1));
        (await _sut.ProcessDueTransactionsAsync()).Created.Should().Be(1);
        (await TransactionsAsync()).Should().HaveCount(7);
    }

    [Fact]
    public async Task Occurrences_stop_at_the_end_date_and_the_recurrence_is_deactivated()
    {
        // Regression: the deactivation used to be overwritten by the schedule save right after it
        var weekly = await AddAsync(RecurrenceFrequency.Weekly, new DateTime(2026, 3, 1), end: new DateTime(2026, 3, 10));

        (await _sut.ProcessDueTransactionsAsync()).Created.Should().Be(2);

        (await TransactionsAsync()).Select(t => t.Date)
            .Should().Equal(new DateTime(2026, 3, 1), new DateTime(2026, 3, 8));
        var saved = await ReloadAsync(weekly);
        saved.IsActive.Should().BeFalse();
        saved.NextOccurrence.Should().Be(new DateTime(2026, 3, 15));
    }

    [Fact]
    public async Task An_occurrence_on_the_end_date_is_still_created()
    {
        await AddAsync(RecurrenceFrequency.Weekly, new DateTime(2026, 3, 1), end: new DateTime(2026, 3, 8));

        (await _sut.ProcessDueTransactionsAsync()).Created.Should().Be(2);
    }

    [Fact]
    public async Task A_recurrence_already_past_its_end_date_only_gets_deactivated()
    {
        var ended = await AddAsync(RecurrenceFrequency.Monthly, new DateTime(2026, 3, 1), end: new DateTime(2026, 2, 28));

        (await _sut.ProcessDueTransactionsAsync()).Created.Should().Be(0);

        (await TransactionsAsync()).Should().BeEmpty();
        (await ReloadAsync(ended)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Inactive_recurrences_are_skipped()
    {
        await AddAsync(RecurrenceFrequency.Daily, new DateTime(2026, 3, 1), active: false);

        (await _sut.ProcessDueTransactionsAsync()).Created.Should().Be(0);

        (await TransactionsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Upcoming_lists_the_next_30_days_with_category_and_account()
    {
        var category = await Db.Table<Category>().FirstAsync(c => c.Type == CategoryType.Expense);
        var account = await Db.Table<Account>().FirstAsync();
        var rent = await AddAsync(RecurrenceFrequency.Monthly, new DateTime(2026, 3, 31));
        rent.CategoryId = category.Id;
        rent.AccountId = account.Id;
        await Db.UpdateAsync(rent);
        await AddAsync(RecurrenceFrequency.Weekly, new DateTime(2026, 3, 20), active: false);

        var upcoming = await _sut.GetUpcomingAsync();

        // 15 March + 30 days = 14 April: 31 March only (30 April is past the window)
        upcoming.Should().ContainSingle();
        upcoming[0].Date.Should().Be(new DateTime(2026, 3, 31));
        upcoming[0].DaysAway.Should().Be(16);
        upcoming[0].CategoryName.Should().Be(category.Name);
        upcoming[0].AccountName.Should().Be(account.Name);
        upcoming[0].SignedAmount.Should().Be(-25.50m);
    }

    [Fact]
    public async Task Upcoming_occurrences_are_the_ones_processing_creates_later()
    {
        await AddAsync(RecurrenceFrequency.Monthly, new DateTime(2026, 1, 31));
        await AddAsync(RecurrenceFrequency.Biweekly, new DateTime(2026, 3, 18), end: new DateTime(2026, 4, 10));
        await _sut.ProcessDueTransactionsAsync();
        var before = (await TransactionsAsync()).Count;

        var upcoming = await _sut.GetUpcomingAsync();
        _clock.Advance(TimeSpan.FromDays(30));
        await _sut.ProcessDueTransactionsAsync();

        (await TransactionsAsync()).Skip(before).Select(t => t.Date)
            .Should().Equal(upcoming.Select(u => u.Date));
    }

    [Fact]
    public async Task Runs_at_the_same_time_never_create_an_occurrence_twice()
    {
        // App start and the daily background job can overlap
        await AddAsync(RecurrenceFrequency.Daily, new DateTime(2026, 3, 1));

        var counts = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(_sut.ProcessDueTransactionsAsync)));

        counts.Sum(c => c.Created).Should().Be(15);
        (await TransactionsAsync()).Should().HaveCount(15);
    }

    [Fact]
    public async Task A_schedule_starting_tomorrow_records_nothing_until_its_date()
    {
        // Saved from the Recurring page: the first occurrence is the chosen date, nothing processed yet
        var rent = await AddAsync(RecurrenceFrequency.Monthly, new DateTime(2026, 3, 16));

        (await _sut.ProcessDueTransactionsAsync()).Created.Should().Be(0);
        (await TransactionsAsync()).Should().BeEmpty();

        _clock.Advance(TimeSpan.FromDays(1));

        (await _sut.ProcessDueTransactionsAsync()).Created.Should().Be(1);
        (await TransactionsAsync()).Select(t => t.Date).Should().Equal(new DateTime(2026, 3, 16));
        var saved = await ReloadAsync(rent);
        saved.LastProcessedDate.Should().Be(new DateTime(2026, 3, 16));
        saved.NextOccurrence.Should().Be(new DateTime(2026, 4, 16));
    }
}
