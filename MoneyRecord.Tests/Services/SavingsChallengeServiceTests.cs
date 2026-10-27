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
    private AccountRepository _accounts = null!;
    private SavingsChallengeService _sut = null!;
    private PocketService _pockets = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    private static string Base => MoneyFormatter.BaseCurrency;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        _dbInitializer = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _dbInitializer.GetConnectionAsync();
        _accounts = new AccountRepository(_dbInitializer);
        var clock = new FakeTimeProvider(new DateTimeOffset(Today.AddHours(12), TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        var balances = new BalanceService(_accounts, new TransactionRepository(_dbInitializer), new TransferRepository(_dbInitializer), clock);
        _sut = new SavingsChallengeService(_dbInitializer, balances, clock);
        _pockets = new PocketService(_dbInitializer, balances);
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private async Task<Account> AddAccountAsync(string name, decimal initial = 0, string? currency = null)
    {
        var account = new Account { Name = name, InitialBalance = initial, CurrencyCode = currency ?? Base, CreatedDate = new DateTime(2026, 1, 1) };
        await _accounts.SaveAsync(account);
        return account;
    }

    private async Task<Pocket> AddPocketAsync(Account account, string name, decimal held = 0)
    {
        var pocket = new Pocket { AccountId = account.Id, Name = name };
        await Db.InsertAsync(pocket);
        if (held != 0)
            await Db.InsertAsync(new PocketMovement { PocketId = pocket.Id, Amount = held, Date = new DateTime(2026, 10, 1) });
        return pocket;
    }

    private async Task<long> HeldAsync(Pocket pocket) =>
        await Db.ExecuteScalarAsync<long>("SELECT COALESCE(SUM(AmountCents), 0) FROM \"PocketMovement\" WHERE PocketId = ?", pocket.Id);

    private static SavingsChallenge Challenge(int? toAccount = null, int? toPocket = null, ChallengeMode mode = ChallengeMode.Linear) => new()
    {
        Name = "Hucha", GoalCents = 46_500, CurrencyCode = Base, StartDate = Today, Days = 30, Mode = mode,
        ToAccountId = toAccount, ToPocketId = toPocket
    };

    [Fact]
    public async Task Days_marked_in_any_order_add_up_and_each_keeps_its_amount()
    {
        var challenge = Challenge();
        await _sut.SaveAsync(challenge);

        foreach (var day in new[] { 5, 2, 9 })
            await _sut.MarkDoneAsync(challenge.Id, day, null);

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
        await _sut.MarkDoneAsync(challenge.Id, 1, null);

        await _sut.Invoking(s => s.MarkDoneAsync(challenge.Id, 1, null)).Should().ThrowAsync<InvalidOperationException>();
        await _sut.Invoking(s => s.MarkDoneAsync(challenge.Id, 31, null)).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task An_account_source_records_a_transfer_to_the_destination_and_unmarking_can_delete_it()
    {
        var bank = await AddAccountAsync("Bank", 1000m);
        var box = await AddAccountAsync("Box");
        var challenge = Challenge(toAccount: box.Id);
        await _sut.SaveAsync(challenge);

        await _sut.MarkDoneAsync(challenge.Id, 3, ChallengePlace.Account(bank.Id));

        var transfer = (await Db.Table<Transfer>().ToListAsync()).Should().ContainSingle().Subject;
        (transfer.SourceAccountId, transfer.DestinationAccountId).Should().Be((bank.Id, box.Id));
        (transfer.AmountCents, transfer.DestinationAmountCents).Should().Be((300, 300));
        (await _sut.GetSummaryAsync(challenge.Id))!.Days[2].MovedMoney.Should().BeTrue();

        await _sut.UnmarkAsync(challenge.Id, 3, undoMoney: true);

        (await Db.Table<Transfer>().CountAsync()).Should().Be(0);
        (await _sut.GetSummaryAsync(challenge.Id))!.SavedCents.Should().Be(0);
    }

    [Fact]
    public async Task Unmarking_without_undoing_keeps_the_money_moved()
    {
        var bank = await AddAccountAsync("Bank", 1000m);
        var box = await AddAccountAsync("Box");
        var challenge = Challenge(toAccount: box.Id);
        await _sut.SaveAsync(challenge);
        await _sut.MarkDoneAsync(challenge.Id, 1, ChallengePlace.Account(bank.Id));

        await _sut.UnmarkAsync(challenge.Id, 1, undoMoney: false);

        (await Db.Table<Transfer>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_pocket_source_takes_the_money_out_of_the_pocket_and_transfers_it()
    {
        var bank = await AddAccountAsync("Bank", 1000m);
        var travel = await AddPocketAsync(bank, "Travel", held: 50m);
        var box = await AddAccountAsync("Box");
        var challenge = Challenge(toAccount: box.Id);
        await _sut.SaveAsync(challenge);

        await _sut.MarkDoneAsync(challenge.Id, 10, ChallengePlace.Pocket(travel.Id));

        (await HeldAsync(travel)).Should().Be(5_000 - 1_000);
        var transfer = (await Db.Table<Transfer>().ToListAsync()).Should().ContainSingle().Subject;
        transfer.SourceAccountId.Should().Be(bank.Id);
        (await Db.Table<PocketMovement>().FirstAsync(m => m.AmountCents == -1_000)).TransferId.Should().Be(transfer.Id);

        await _sut.UnmarkAsync(challenge.Id, 10, undoMoney: true);

        (await HeldAsync(travel)).Should().Be(5_000);
        (await Db.Table<Transfer>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_source_pocket_that_holds_less_is_refused_and_nothing_is_written()
    {
        var bank = await AddAccountAsync("Bank", 1000m);
        var travel = await AddPocketAsync(bank, "Travel", held: 5m);
        var box = await AddAccountAsync("Box");
        var challenge = Challenge(toAccount: box.Id);
        await _sut.SaveAsync(challenge);

        var act = () => _sut.MarkDoneAsync(challenge.Id, 10, ChallengePlace.Pocket(travel.Id));

        (await act.Should().ThrowAsync<PocketProblemException>()).Which.Problem.Should().Be(PocketProblem.NotEnoughInPocket);
        (await Db.Table<Transfer>().CountAsync()).Should().Be(0);
        (await Db.Table<SavingsChallengeDay>().CountAsync()).Should().Be(0);
        (await HeldAsync(travel)).Should().Be(500);
    }

    [Fact]
    public async Task A_destination_pocket_gets_the_money_set_aside_after_the_transfer_arrives()
    {
        var bank = await AddAccountAsync("Bank", 1000m);
        var savings = await AddAccountAsync("Savings");
        var holiday = await AddPocketAsync(savings, "Holiday");
        var challenge = Challenge(toPocket: holiday.Id);
        await _sut.SaveAsync(challenge);

        await _sut.MarkDoneAsync(challenge.Id, 4, ChallengePlace.Account(bank.Id));

        challenge.ToAccountId.Should().Be(savings.Id, "a pocket decides the account");
        (await HeldAsync(holiday)).Should().Be(400);
        (await Db.Table<Transfer>().ToListAsync()).Should().ContainSingle().Which.DestinationAccountId.Should().Be(savings.Id);

        await _sut.UnmarkAsync(challenge.Id, 4, undoMoney: true);

        (await HeldAsync(holiday)).Should().Be(0);
        (await Db.Table<Transfer>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Within_one_account_money_moves_between_pockets_with_no_transfer()
    {
        var bank = await AddAccountAsync("Bank", 1000m);
        var food = await AddPocketAsync(bank, "Food", held: 50m);
        var holiday = await AddPocketAsync(bank, "Holiday");
        var challenge = Challenge(toPocket: holiday.Id);
        await _sut.SaveAsync(challenge);

        await _sut.MarkDoneAsync(challenge.Id, 10, ChallengePlace.Pocket(food.Id));

        (await Db.Table<Transfer>().CountAsync()).Should().Be(0);
        (await HeldAsync(food)).Should().Be(4_000);
        (await HeldAsync(holiday)).Should().Be(1_000);
    }

    [Fact]
    public async Task Setting_aside_from_the_accounts_own_available_money_needs_enough_of_it()
    {
        var bank = await AddAccountAsync("Bank", 20m);
        var holiday = await AddPocketAsync(bank, "Holiday");
        var challenge = Challenge(toPocket: holiday.Id);
        await _sut.SaveAsync(challenge);

        await _sut.MarkDoneAsync(challenge.Id, 10, ChallengePlace.Account(bank.Id));
        var act = () => _sut.MarkDoneAsync(challenge.Id, 20, ChallengePlace.Account(bank.Id));

        (await HeldAsync(holiday)).Should().Be(1_000);
        (await act.Should().ThrowAsync<PocketProblemException>()).Which.Problem.Should().Be(PocketProblem.NotEnoughAvailable);
        (await Db.Table<SavingsChallengeDay>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_route_that_moves_nothing_or_has_no_destination_is_refused()
    {
        var bank = await AddAccountAsync("Bank", 1000m);
        var pocket = await AddPocketAsync(bank, "Food", held: 50m);
        var toAccount = Challenge(toAccount: bank.Id);
        await _sut.SaveAsync(toAccount);
        var none = Challenge();
        await _sut.SaveAsync(none);

        await _sut.Invoking(s => s.MarkDoneAsync(toAccount.Id, 1, ChallengePlace.Account(bank.Id))).Should().ThrowAsync<InvalidOperationException>();
        await _sut.Invoking(s => s.MarkDoneAsync(toAccount.Id, 1, ChallengePlace.Pocket(pocket.Id))).Should().ThrowAsync<InvalidOperationException>();
        await _sut.Invoking(s => s.MarkDoneAsync(none.Id, 1, ChallengePlace.Account(bank.Id))).Should().ThrowAsync<InvalidOperationException>();
        (await Db.Table<SavingsChallengeDay>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_destination_or_source_in_another_currency_is_refused()
    {
        var bank = await AddAccountAsync("Bank", 1000m);
        var dollars = await AddAccountAsync("Dollars", 100m, "ZZZ");
        var mixed = Challenge(toAccount: dollars.Id);
        await _sut.Invoking(s => s.SaveAsync(mixed)).Should().ThrowAsync<InvalidOperationException>();

        var box = await AddAccountAsync("Box");
        var challenge = Challenge(toAccount: box.Id);
        await _sut.SaveAsync(challenge);
        await _sut.Invoking(s => s.MarkDoneAsync(challenge.Id, 1, ChallengePlace.Account(dollars.Id))).Should().ThrowAsync<InvalidOperationException>();
        (await Db.Table<SavingsChallengeDay>().CountAsync()).Should().Be(0);
        bank.Id.Should().NotBe(0);
    }

    [Fact]
    public async Task The_plan_is_locked_once_a_day_is_done_but_name_and_start_stay_editable()
    {
        var challenge = Challenge();
        await _sut.SaveAsync(challenge);
        await _sut.MarkDoneAsync(challenge.Id, 1, null);

        challenge.Name = "Renamed";
        challenge.StartDate = Today.AddDays(1);
        await _sut.SaveAsync(challenge);
        (await _sut.GetSummaryAsync(challenge.Id))!.Challenge.Name.Should().Be("Renamed");

        challenge.Days = 40;
        await _sut.Invoking(s => s.SaveAsync(challenge)).Should().ThrowAsync<InvalidOperationException>();

    }

    [Fact]
    public async Task A_daily_amount_is_locked_with_the_plan_once_a_day_is_done()
    {
        var challenge = Challenge(mode: ChallengeMode.Equal);
        challenge.DayAmountCents = 1_550;
        await _sut.SaveAsync(challenge);
        await _sut.MarkDoneAsync(challenge.Id, 1, null);

        // Consistent (15 days of 3,100 reach the goal), so it is the lock that refuses it
        challenge.DayAmountCents = 3_100;
        challenge.Days = 15;
        await _sut.Invoking(s => s.SaveAsync(challenge)).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task A_daily_amount_needs_the_equal_mode_and_the_days_it_works_out_to()
    {
        var linear = Challenge();
        linear.DayAmountCents = 1_550;
        await _sut.Invoking(s => s.SaveAsync(linear)).Should().ThrowAsync<InvalidOperationException>();

        var tooFewDays = Challenge(mode: ChallengeMode.Equal);
        tooFewDays.DayAmountCents = 1_000;
        await _sut.Invoking(s => s.SaveAsync(tooFewDays)).Should().ThrowAsync<InvalidOperationException>();

        var right = Challenge(mode: ChallengeMode.Equal);
        right.DayAmountCents = 1_000;
        right.Days = 47;
        await _sut.SaveAsync(right);
        (await _sut.GetSummaryAsync(right.Id))!.TotalCents.Should().Be(46_500);
    }

    [Fact]
    public async Task Deleting_a_challenge_removes_its_days_but_keeps_the_money_it_moved()
    {
        var bank = await AddAccountAsync("Bank", 1000m);
        var box = await AddAccountAsync("Box");
        var challenge = Challenge(toAccount: box.Id);
        await _sut.SaveAsync(challenge);
        await _sut.MarkDoneAsync(challenge.Id, 2, ChallengePlace.Account(bank.Id));

        await _sut.DeleteAsync(challenge.Id);

        (await Db.Table<SavingsChallenge>().CountAsync()).Should().Be(0);
        (await Db.Table<SavingsChallengeDay>().CountAsync()).Should().Be(0);
        (await Db.Table<Transfer>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Deleting_the_destination_pocket_sends_the_challenge_back_to_only_marking_days()
    {
        var bank = await AddAccountAsync("Bank", 1000m);
        var holiday = await AddPocketAsync(bank, "Holiday");
        var challenge = Challenge(toPocket: holiday.Id);
        await _sut.SaveAsync(challenge);

        await _pockets.DeleteAsync(holiday.Id);

        var saved = (await _sut.GetSummaryAsync(challenge.Id))!.Challenge;
        (saved.ToAccountId, saved.ToPocketId).Should().Be((null, null));
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
        await _sut.MarkDoneAsync(done.Id, 1, null);
        await _sut.MarkDoneAsync(done.Id, 2, null);
        var open = Challenge();
        await _sut.SaveAsync(open);

        var all = await _sut.GetSummariesAsync();

        all.Select(s => s.Challenge.Name).Should().Equal("Hucha", "Done");
        all[1].IsFinished.Should().BeTrue();
        all[1].DaysLeft.Should().Be(0);
    }
}
