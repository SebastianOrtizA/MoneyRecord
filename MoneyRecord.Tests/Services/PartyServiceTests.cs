using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>PartyService against a real temp SQLite file.</summary>
public sealed class PartyServiceTests : IAsyncLifetime
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-party-{Guid.NewGuid():N}");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero));
    private SQLite.SQLiteAsyncConnection _db = null!;
    private PartyService _sut = null!;
    private Participant _ana = null!;
    private Participant _ben = null!;
    private Participant _cam = null!;
    private int _partyId;

    public async Task InitializeAsync()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        Directory.CreateDirectory(_appData);
        var initializer = new DatabaseInitializer(new AppPaths(_appData));
        _db = await initializer.GetConnectionAsync();
        _sut = new PartyService(initializer, _clock);
        _ana = await _sut.AddParticipantAsync("Ana");
        _ben = await _sut.AddParticipantAsync("Ben");
        _cam = await _sut.AddParticipantAsync("Cam");
        _partyId = await _sut.SaveAsync(new PartyEvent { Name = "Birthday", Budget = 100m }, [_ana.Id, _ben.Id, _cam.Id]);
    }

    public async Task DisposeAsync()
    {
        await _db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    [Fact]
    public async Task AddParticipant_SameNameIgnoringCase_ReturnsTheExistingOne()
    {
        var again = await _sut.AddParticipantAsync("  ANA ");

        again.Id.Should().Be(_ana.Id);
        (await _sut.GetParticipantsAsync()).Should().HaveCount(3);
    }

    [Fact]
    public async Task RenameParticipant_ToAnotherFriendsName_Throws()
    {
        var act = () => _sut.RenameParticipantAsync(_ben.Id, "ana");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeleteParticipant_InAParty_Throws()
    {
        var act = () => _sut.DeleteParticipantAsync(_ana.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Save_NewParty_KeepsMembersAndBudget()
    {
        var party = await _sut.GetAsync(_partyId);

        party!.Budget.Should().Be(100m);
        (await _sut.GetMembersAsync(_partyId)).Select(m => m.Name).Should().Equal("Ana", "Ben", "Cam");
    }

    [Fact]
    public async Task Save_TakingOutAPayer_Throws()
    {
        await _sut.SaveExpenseAsync(new PartyExpense { PartyId = _partyId, Amount = 30m, PaidByParticipantId = _cam.Id }, []);
        var party = (await _sut.GetAsync(_partyId))!;

        var act = () => _sut.SaveAsync(party, [_ana.Id, _ben.Id]);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await _sut.GetMembersAsync(_partyId)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Save_TakingOutAMember_DropsTheirExclusions()
    {
        var expenseId = await _sut.SaveExpenseAsync(new PartyExpense { PartyId = _partyId, Amount = 30m, PaidByParticipantId = _ana.Id }, [_cam.Id]);
        var party = (await _sut.GetAsync(_partyId))!;

        await _sut.SaveAsync(party, [_ana.Id, _ben.Id]);

        (await _sut.GetExclusionsAsync(_partyId)).Should().NotContainKey(expenseId);
    }

    [Fact]
    public async Task Save_TakingOutTheOnlySharerOfAnExpense_Throws()
    {
        await _sut.SaveExpenseAsync(new PartyExpense { PartyId = _partyId, Amount = 30m, PaidByParticipantId = _ana.Id }, [_ana.Id, _ben.Id]);
        var party = (await _sut.GetAsync(_partyId))!;

        var act = () => _sut.SaveAsync(party, [_ana.Id, _ben.Id]);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await _sut.GetMembersAsync(_partyId)).Should().HaveCount(3);
        (await _sut.GetSummaryAsync(_partyId)).TotalSpentCents.Should().Be(3000);
    }

    [Fact]
    public async Task SaveExpense_Invalid_Throws()
    {
        var zero = () => _sut.SaveExpenseAsync(new PartyExpense { PartyId = _partyId, AmountCents = 0, PaidByParticipantId = _ana.Id }, []);
        var stranger = () => _sut.SaveExpenseAsync(new PartyExpense { PartyId = _partyId, Amount = 5m, PaidByParticipantId = 999 }, []);
        var everyoneOut = () => _sut.SaveExpenseAsync(new PartyExpense { PartyId = _partyId, Amount = 5m }, [_ana.Id, _ben.Id, _cam.Id]);

        await zero.Should().ThrowAsync<ArgumentException>();
        await stranger.Should().ThrowAsync<ArgumentException>();
        await everyoneOut.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveExpense_Edit_ReplacesExclusions()
    {
        var expense = new PartyExpense { PartyId = _partyId, Amount = 30m, PaidByParticipantId = _ana.Id };
        await _sut.SaveExpenseAsync(expense, [_cam.Id]);

        await _sut.SaveExpenseAsync(expense, [_ben.Id]);

        (await _sut.GetExclusionsAsync(_partyId)).Single().Value.Should().BeEquivalentTo(new[] { _ben.Id });
    }

    [Fact]
    public async Task Summary_TotalBudgetBalancesAndPayments()
    {
        await _sut.SaveExpenseAsync(new PartyExpense { PartyId = _partyId, Amount = 60m, PaidByParticipantId = _ana.Id }, []);
        await _sut.SaveExpenseAsync(new PartyExpense { PartyId = _partyId, Amount = 30m, PaidByParticipantId = _ben.Id }, [_ana.Id]);
        await _sut.SaveContributionAsync(new PartyContribution { PartyId = _partyId, ParticipantId = _cam.Id, Amount = 10m });

        var summary = await _sut.GetSummaryAsync(_partyId);

        summary.TotalSpentCents.Should().Be(9000);
        summary.LeftCents.Should().Be(1000);
        summary.Balances.Sum(b => b.BalanceCents).Should().Be(0);
        // Ana 60 - 20, Ben 30 - 20 - 15, Cam 10 handed in - 20 - 15, the pot owes the 10 handed in back
        summary.Balances.Single(b => b.ParticipantId == _ana.Id).BalanceCents.Should().Be(4000);
        summary.Balances.Single(b => b.ParticipantId == _ben.Id).BalanceCents.Should().Be(-500);
        summary.Balances.Single(b => b.ParticipantId == _cam.Id).BalanceCents.Should().Be(-2500);
        summary.Balances.Single(b => b.ParticipantId == PartyLedger.PotId).BalanceCents.Should().Be(-1000);
        summary.Payments.Sum(p => p.AmountCents).Should().Be(4000);
    }

    [Fact]
    public async Task SaveContribution_FromANonMember_Throws()
    {
        var act = () => _sut.SaveContributionAsync(new PartyContribution { PartyId = _partyId, ParticipantId = 999, Amount = 5m });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Delete_RemovesThePartyAndEverythingInIt_FriendsStay()
    {
        var expenseId = await _sut.SaveExpenseAsync(new PartyExpense { PartyId = _partyId, Amount = 30m, PaidByParticipantId = _ana.Id }, [_cam.Id]);
        await _sut.SaveContributionAsync(new PartyContribution { PartyId = _partyId, ParticipantId = _ben.Id, Amount = 5m });

        await _sut.DeleteAsync(_partyId);

        (await _sut.GetPartiesAsync()).Should().BeEmpty();
        (await _sut.GetExpensesAsync(_partyId)).Should().BeEmpty();
        (await _sut.GetContributionsAsync(_partyId)).Should().BeEmpty();
        (await _sut.GetExclusionsAsync(_partyId)).Should().NotContainKey(expenseId);
        (await _sut.GetParticipantsAsync()).Should().HaveCount(3);
        await _sut.DeleteParticipantAsync(_ana.Id);
    }

    [Fact]
    public async Task Payments_CountInTheBalances_AndShrinkTheSettleUp()
    {
        await _sut.SaveExpenseAsync(new PartyExpense { PartyId = _partyId, Amount = 30m, PaidByParticipantId = _ana.Id }, []);

        var paymentId = await _sut.SavePaymentAsync(new PartyPayment { PartyId = _partyId, FromId = _ben.Id, ToId = _ana.Id, Amount = 10m });

        var summary = await _sut.GetSummaryAsync(_partyId);
        summary.Payments.Should().BeEquivalentTo([new SettlePayment(_cam.Id, _ana.Id, 1000)]);
        summary.Balances.Sum(b => b.BalanceCents).Should().Be(0);

        await _sut.DeletePaymentAsync(paymentId);
        (await _sut.GetSummaryAsync(_partyId)).Payments.Should().HaveCount(2);
    }

    [Fact]
    public async Task Payments_MustBeBetweenDifferentMembers_WithAPositiveAmount()
    {
        var stranger = await _sut.AddParticipantAsync("Zed");

        foreach (var payment in new[]
        {
            new PartyPayment { PartyId = _partyId, FromId = _ben.Id, ToId = _ben.Id, Amount = 5m },
            new PartyPayment { PartyId = _partyId, FromId = _ben.Id, ToId = _ana.Id, AmountCents = 0 },
            new PartyPayment { PartyId = _partyId, FromId = _ben.Id, ToId = stranger.Id, Amount = 5m }
        })
        {
            var act = () => _sut.SavePaymentAsync(payment);
            await act.Should().ThrowAsync<ArgumentException>();
        }
    }

    [Fact]
    public async Task AMemberWithPayments_CannotBeTakenOut()
    {
        await _sut.SavePaymentAsync(new PartyPayment { PartyId = _partyId, FromId = _cam.Id, ToId = _ana.Id, Amount = 5m });

        var party = (await _sut.GetAsync(_partyId))!;
        var act = () => _sut.SaveAsync(party, [_ana.Id, _ben.Id]);

        (await act.Should().ThrowAsync<PartyProblemException>()).Which.Problem.Should().Be(PartyProblem.MemberInUse);
    }

    [Fact]
    public async Task DeletingTheParty_RemovesItsPayments()
    {
        await _sut.SavePaymentAsync(new PartyPayment { PartyId = _partyId, FromId = _cam.Id, ToId = _ana.Id, Amount = 5m });

        await _sut.DeleteAsync(_partyId);

        (await _sut.GetPaymentsAsync(_partyId)).Should().BeEmpty();
    }

    private async Task<(int AccountId, int CategoryId)> AccountAndCategoryAsync() =>
        ((await _db.Table<Account>().FirstAsync()).Id, (await _db.Table<Category>().FirstAsync()).Id);

    [Fact]
    public async Task RecordOwnShare_MakesANormalExpenseOfTheShare_AndRemembersTheOwner()
    {
        await _sut.SaveExpenseAsync(new PartyExpense { PartyId = _partyId, Amount = 30m, PaidByParticipantId = _ana.Id }, []);
        var (accountId, categoryId) = await AccountAndCategoryAsync();

        var transaction = await _sut.RecordOwnShareAsync(_partyId, _ben.Id, accountId, categoryId);

        transaction.Id.Should().BeGreaterThan(0);
        var saved = await _db.FindAsync<Transaction>(transaction.Id);
        (saved.Type, saved.AmountCents, saved.Description, saved.AccountId, saved.CategoryId)
            .Should().Be((TransactionType.Expense, 1000L, "Birthday", accountId, categoryId));
        var party = (await _sut.GetAsync(_partyId))!;
        (party.MeParticipantId, party.OwnTransactionId).Should().Be((_ben.Id, transaction.Id));
    }

    [Fact]
    public async Task RecordOwnShare_Again_UpdatesTheSameExpense_OrMakesANewOneIfDeleted()
    {
        var expenseId = await _sut.SaveExpenseAsync(new PartyExpense { PartyId = _partyId, Amount = 30m, PaidByParticipantId = _ana.Id }, []);
        var (accountId, categoryId) = await AccountAndCategoryAsync();
        var first = await _sut.RecordOwnShareAsync(_partyId, _ben.Id, accountId, categoryId);

        await _sut.SaveExpenseAsync(new PartyExpense { Id = expenseId, PartyId = _partyId, Amount = 60m, PaidByParticipantId = _ana.Id }, []);
        var second = await _sut.RecordOwnShareAsync(_partyId, _ben.Id, accountId, categoryId);

        second.Id.Should().Be(first.Id);
        (await _db.FindAsync<Transaction>(first.Id)).AmountCents.Should().Be(2000);
        (await _db.Table<Transaction>().CountAsync(t => t.Description == "Birthday")).Should().Be(1);

        await _db.DeleteAsync<Transaction>(first.Id);
        var third = await _sut.RecordOwnShareAsync(_partyId, _ben.Id, accountId, categoryId);
        third.Id.Should().NotBe(first.Id);
        (await _sut.GetAsync(_partyId))!.OwnTransactionId.Should().Be(third.Id);
    }

    [Fact]
    public async Task RecordOwnShare_ZeroShare_AccountInAnotherCurrency_Refused()
    {
        var (accountId, categoryId) = await AccountAndCategoryAsync();
        var none = () => _sut.RecordOwnShareAsync(_partyId, _ben.Id, accountId, categoryId);
        (await none.Should().ThrowAsync<PartyProblemException>()).Which.Problem.Should().Be(PartyProblem.NoShare);

        await _sut.SaveExpenseAsync(new PartyExpense { PartyId = _partyId, Amount = 30m, PaidByParticipantId = _ana.Id }, []);
        var foreign = new Account { Name = "Yen", CurrencyCode = "JPY" };
        await _db.InsertAsync(foreign);
        var wrong = () => _sut.RecordOwnShareAsync(_partyId, _ben.Id, foreign.Id, categoryId);
        (await wrong.Should().ThrowAsync<PartyProblemException>()).Which.Problem.Should().Be(PartyProblem.AccountCurrency);
    }

    [Fact]
    public async Task EditingTheParty_KeepsTheOwnShareLink_UntilTheOwnerLeaves()
    {
        await _sut.SaveExpenseAsync(new PartyExpense { PartyId = _partyId, Amount = 30m, PaidByParticipantId = _ana.Id }, []);
        var (accountId, categoryId) = await AccountAndCategoryAsync();
        var transaction = await _sut.RecordOwnShareAsync(_partyId, _ben.Id, accountId, categoryId);

        // The edit form saves a fresh copy that knows nothing of the link
        await _sut.SaveAsync(new PartyEvent { Id = _partyId, Name = "Birthday 2", Budget = 100m }, [_ana.Id, _ben.Id, _cam.Id]);
        var kept = (await _sut.GetAsync(_partyId))!;
        (kept.MeParticipantId, kept.OwnTransactionId).Should().Be((_ben.Id, transaction.Id));

        await _sut.SaveAsync(new PartyEvent { Id = _partyId, Name = "Birthday 2", Budget = 100m }, [_ana.Id, _cam.Id]);
        (await _sut.GetAsync(_partyId))!.MeParticipantId.Should().BeNull();
    }

    [Fact]
    public async Task RecordOwnShare_WhenTheExpenseWasDeleted_WithoutPicks_ClearsTheLinkAndSaysSo()
    {
        await _sut.SaveExpenseAsync(new PartyExpense { PartyId = _partyId, Amount = 30m, PaidByParticipantId = _ana.Id }, []);
        var (accountId, categoryId) = await AccountAndCategoryAsync();
        var transaction = await _sut.RecordOwnShareAsync(_partyId, _ben.Id, accountId, categoryId);
        await _db.DeleteAsync<Transaction>(transaction.Id);

        var act = () => _sut.RecordOwnShareAsync(_partyId, _ben.Id, null, null);

        (await act.Should().ThrowAsync<PartyProblemException>()).Which.Problem.Should().Be(PartyProblem.ShareDeleted);
        (await _sut.GetAsync(_partyId))!.OwnTransactionId.Should().BeNull();
        (await _sut.RecordOwnShareAsync(_partyId, _ben.Id, accountId, categoryId)).AmountCents.Should().Be(1000);
    }
}
