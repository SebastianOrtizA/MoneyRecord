using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>SavingsChallengeService against a real temp SQLite file.</summary>
public sealed class SavingsChallengeServiceTests : IAsyncLifetime
{
    private static readonly DateTime Today = new(2026, 10, 25);

    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-challenges-{Guid.NewGuid():N}");
    private DatabaseInitializer _dbInitializer = null!;
    private SavingsChallengeService _sut = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    private static string Base => MoneyFormatter.BaseCurrency;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        _dbInitializer = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _dbInitializer.GetConnectionAsync();
        var clock = new FakeTimeProvider(new DateTimeOffset(Today.AddHours(12), TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _sut = new SavingsChallengeService(_dbInitializer, clock);
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private async Task<Account> AddAccountAsync(string name, string? currency = null)
    {
        var account = new Account { Name = name, CurrencyCode = currency ?? Base };
        await Db.InsertAsync(account);
        return account;
    }

    private static SavingsChallenge Challenge(int? from = null, int? to = null, ChallengeMode mode = ChallengeMode.Linear) => new()
    {
        Name = "Hucha", GoalCents = 46_500, CurrencyCode = Base, StartDate = Today, Days = 30, Mode = mode,
        FromAccountId = from, ToAccountId = to
    };

    [Fact]
    public async Task Days_marked_in_any_order_add_up_and_each_keeps_its_amount()
    {
        var challenge = Challenge();
        await _sut.SaveAsync(challenge);

        foreach (var day in new[] { 5, 2, 9 })
            await _sut.MarkDoneAsync(challenge.Id, day, recordTransfer: false);

        var summary = (await _sut.GetSummaryAsync(challenge.Id))!;
        summary.SavedCents.Should().Be((5 + 2 + 9) * 100);
        summary.TotalCents.Should().Be(46_500);
        summary.DoneCount.Should().Be(3);
        summary.Days.Where(d => d.IsDone).Select(d => d.DayNumber).Should().Equal(2, 5, 9);
        summary.Days[0].Date.Should().Be(Today);
        summary.DaysLeft.Should().Be(30);
    }

    [Fact]
    public async Task Marking_a_day_twice_or_out_of_range_is_refused()
    {
        var challenge = Challenge();
        await _sut.SaveAsync(challenge);
        await _sut.MarkDoneAsync(challenge.Id, 1, false);

        await _sut.Invoking(s => s.MarkDoneAsync(challenge.Id, 1, false)).Should().ThrowAsync<InvalidOperationException>();
        await _sut.Invoking(s => s.MarkDoneAsync(challenge.Id, 31, false)).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Marking_with_a_transfer_records_it_in_the_same_amounts_and_unmarking_can_delete_it()
    {
        var from = await AddAccountAsync("Bank");
        var to = await AddAccountAsync("Box");
        var challenge = Challenge(from.Id, to.Id);
        await _sut.SaveAsync(challenge);

        await _sut.MarkDoneAsync(challenge.Id, 3, recordTransfer: true);

        var transfer = (await Db.Table<Transfer>().ToListAsync()).Should().ContainSingle().Subject;
        (transfer.SourceAccountId, transfer.DestinationAccountId).Should().Be((from.Id, to.Id));
        (transfer.AmountCents, transfer.DestinationAmountCents).Should().Be((300, 300));
        (await _sut.GetSummaryAsync(challenge.Id))!.Days[2].TransferId.Should().Be(transfer.Id);

        await _sut.UnmarkAsync(challenge.Id, 3, deleteTransfer: true);

        (await Db.Table<Transfer>().CountAsync()).Should().Be(0);
        (await _sut.GetSummaryAsync(challenge.Id))!.SavedCents.Should().Be(0);
    }

    [Fact]
    public async Task Unmarking_without_deleting_keeps_the_transfer()
    {
        var from = await AddAccountAsync("Bank");
        var to = await AddAccountAsync("Box");
        var challenge = Challenge(from.Id, to.Id);
        await _sut.SaveAsync(challenge);
        await _sut.MarkDoneAsync(challenge.Id, 1, true);

        await _sut.UnmarkAsync(challenge.Id, 1, deleteTransfer: false);

        (await Db.Table<Transfer>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_transfer_without_accounts_or_in_another_currency_is_refused_and_nothing_is_marked()
    {
        var plain = Challenge();
        await _sut.SaveAsync(plain);
        await _sut.Invoking(s => s.MarkDoneAsync(plain.Id, 1, true)).Should().ThrowAsync<InvalidOperationException>();

        var from = await AddAccountAsync("Bank");
        var to = await AddAccountAsync("Dollars", "ZZZ");
        var mixed = Challenge(from.Id, to.Id);
        await _sut.Invoking(s => s.SaveAsync(mixed)).Should().ThrowAsync<InvalidOperationException>();

        (await Db.Table<SavingsChallengeDay>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task The_plan_is_locked_once_a_day_is_done_but_name_and_start_stay_editable()
    {
        var challenge = Challenge();
        await _sut.SaveAsync(challenge);
        await _sut.MarkDoneAsync(challenge.Id, 1, false);

        challenge.Name = "Renamed";
        challenge.StartDate = Today.AddDays(1);
        await _sut.SaveAsync(challenge);
        (await _sut.GetSummaryAsync(challenge.Id))!.Challenge.Name.Should().Be("Renamed");

        challenge.Days = 40;
        await _sut.Invoking(s => s.SaveAsync(challenge)).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Deleting_a_challenge_removes_its_days_but_keeps_its_transfers()
    {
        var from = await AddAccountAsync("Bank");
        var to = await AddAccountAsync("Box");
        var challenge = Challenge(from.Id, to.Id);
        await _sut.SaveAsync(challenge);
        await _sut.MarkDoneAsync(challenge.Id, 2, true);

        await _sut.DeleteAsync(challenge.Id);

        (await Db.Table<SavingsChallenge>().CountAsync()).Should().Be(0);
        (await Db.Table<SavingsChallengeDay>().CountAsync()).Should().Be(0);
        (await Db.Table<Transfer>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Open_challenges_come_before_finished_ones_and_days_left_stops_at_zero()
    {
        var done = Challenge();
        done.Name = "Done";
        done.Days = 2;
        done.GoalCents = 300;
        done.StartDate = Today.AddDays(-10);
        await _sut.SaveAsync(done);
        await _sut.MarkDoneAsync(done.Id, 1, false);
        await _sut.MarkDoneAsync(done.Id, 2, false);
        var open = Challenge();
        await _sut.SaveAsync(open);

        var all = await _sut.GetSummariesAsync();

        all.Select(s => s.Challenge.Name).Should().Equal("Hucha", "Done");
        all[1].IsFinished.Should().BeTrue();
        all[1].DaysLeft.Should().Be(0);
    }
}
