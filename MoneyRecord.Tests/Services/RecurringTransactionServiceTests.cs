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
        _sut = new RecurringTransactionService(new RecurringTransactionRepository(_dbInitializer), _dbInitializer, _clock);
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

        (await _sut.ProcessDueTransactionsAsync()).Should().Be(2);

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

    [Fact]
    public async Task An_occurrence_is_due_from_the_start_of_its_day()
    {
        await AddAsync(RecurrenceFrequency.Weekly, new DateTime(2026, 3, 15));
        await AddAsync(RecurrenceFrequency.Weekly, new DateTime(2026, 3, 16));

        (await _sut.ProcessDueTransactionsAsync()).Should().Be(1);

        (await TransactionsAsync()).Single().Date.Should().Be(new DateTime(2026, 3, 15));
    }

    [Fact]
    public async Task Running_again_creates_nothing_new()
    {
        await AddAsync(RecurrenceFrequency.Daily, new DateTime(2026, 3, 10));

        (await _sut.ProcessDueTransactionsAsync()).Should().Be(6);
        (await _sut.ProcessDueTransactionsAsync()).Should().Be(0);

        _clock.Advance(TimeSpan.FromDays(1));
        (await _sut.ProcessDueTransactionsAsync()).Should().Be(1);
        (await TransactionsAsync()).Should().HaveCount(7);
    }

    [Fact]
    public async Task Occurrences_stop_at_the_end_date_and_the_recurrence_is_deactivated()
    {
        // Regression: the deactivation used to be overwritten by the schedule save right after it
        var weekly = await AddAsync(RecurrenceFrequency.Weekly, new DateTime(2026, 3, 1), end: new DateTime(2026, 3, 10));

        (await _sut.ProcessDueTransactionsAsync()).Should().Be(2);

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

        (await _sut.ProcessDueTransactionsAsync()).Should().Be(2);
    }

    [Fact]
    public async Task A_recurrence_already_past_its_end_date_only_gets_deactivated()
    {
        var ended = await AddAsync(RecurrenceFrequency.Monthly, new DateTime(2026, 3, 1), end: new DateTime(2026, 2, 28));

        (await _sut.ProcessDueTransactionsAsync()).Should().Be(0);

        (await TransactionsAsync()).Should().BeEmpty();
        (await ReloadAsync(ended)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Inactive_recurrences_are_skipped()
    {
        await AddAsync(RecurrenceFrequency.Daily, new DateTime(2026, 3, 1), active: false);

        (await _sut.ProcessDueTransactionsAsync()).Should().Be(0);

        (await TransactionsAsync()).Should().BeEmpty();
    }
}
