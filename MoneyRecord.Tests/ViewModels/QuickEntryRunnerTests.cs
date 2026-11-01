using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.ViewModels;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.ViewModels;

public sealed class QuickEntryRunnerTests
{
    private readonly IQuickEntryService _quickEntries = Substitute.For<IQuickEntryService>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IBalanceService _balances = Substitute.For<IBalanceService>();
    private readonly IPocketService _pockets = Substitute.For<IPocketService>();
    private readonly IReminderService _reminders = Substitute.For<IReminderService>();
    private readonly ILedgerService _ledger = Substitute.For<ILedgerService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IErrorHandler _errors = Substitute.For<IErrorHandler>();
    private readonly QuickEntryRunner _sut;
    private readonly Account _bank = new() { Id = 3, Name = "Bank", CurrencyCode = "EUR" };
    private int _changed;

    public QuickEntryRunnerTests()
    {
        var pocketCover = new PocketCoverPrompt(_dialogs, _pockets, _errors);
        _sut = new QuickEntryRunner(_quickEntries, _accounts, _balances, pocketCover, _reminders, _ledger, _dialogs, _errors,
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero)));
        _accounts.GetByIdAsync(3).Returns(_bank);
        _balances.GetAccountBalanceAsync(3).Returns(100m);
        _quickEntries.UseAsync(7, Arg.Any<long?>()).Returns(ci => new Transaction
        {
            Id = 41, Date = new DateTime(2026, 11, 2, 9, 0, 0), AmountCents = ci.Arg<long?>() ?? 350, AccountId = 3,
            Type = TransactionType.Expense
        });
    }

    private Task Changed() { _changed++; return Task.CompletedTask; }

    private static QuickEntryItem Item(long cents = 350, string account = "Bank", TransactionType type = TransactionType.Expense) =>
        new(new QuickEntry { Id = 7, Name = "Coffee", AmountCents = cents, CategoryId = 1, AccountId = 3, Type = type }, "Food", "F025A", account, "EUR");

    [Fact]
    public async Task Saves_reloads_checks_budgets_and_offers_undo()
    {
        var saved = await _sut.RunAsync(Item(), null, Changed);

        saved.Should().BeTrue();
        await _quickEntries.Received(1).UseAsync(7, 350);
        await _reminders.Received(1).CheckBudgetsAsync();
        _changed.Should().Be(1);
        await _dialogs.Received(1).ToastWithUndoAsync(Arg.Any<string>(), Arg.Any<Func<Task>>(), Arg.Any<Func<Task>>());
    }

    [Fact]
    public async Task Undo_deletes_the_new_transaction_for_good_and_reloads()
    {
        Func<Task>? undo = null;
        await _dialogs.ToastWithUndoAsync(Arg.Any<string>(), Arg.Do<Func<Task>>(f => undo = f), Arg.Any<Func<Task>>());
        var deleted = new DeletedEntry([new Transaction { Id = 41 }], null);
        _ledger.DeleteAsync(Arg.Is<LedgerEntry>(e => e.SourceId == 41 && e.Kind == LedgerEntryKind.Expense)).Returns(deleted);
        await _sut.RunAsync(Item(), null, Changed);

        await undo!();

        _ledger.Received(1).Discard(deleted);
        _changed.Should().Be(2);
    }

    [Fact]
    public async Task An_asking_entry_prompts_and_saves_the_typed_amount()
    {
        _dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>()).Returns("12.75");

        var saved = await _sut.RunWithPromptAsync(Item(0), Changed);

        saved.Should().BeTrue();
        await _quickEntries.Received(1).UseAsync(7, 1_275);
    }

    [Fact]
    public async Task A_cancelled_or_invalid_prompt_saves_nothing()
    {
        _dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>()).Returns((string?)null, "0");

        (await _sut.RunWithPromptAsync(Item(0), Changed)).Should().BeFalse();
        (await _sut.RunWithPromptAsync(Item(0), Changed)).Should().BeFalse();

        await _quickEntries.DidNotReceiveWithAnyArgs().UseAsync(default, default);
    }

    [Fact]
    public async Task An_amount_typed_for_an_asking_entry_is_saved()
    {
        var saved = await _sut.RunAsync(Item(0), 1_275, Changed);

        saved.Should().BeTrue();
        await _quickEntries.Received(1).UseAsync(7, 1_275);
    }

    [Fact]
    public async Task Without_an_amount_nothing_is_saved()
    {
        var saved = await _sut.RunAsync(Item(0), null, Changed);

        saved.Should().BeFalse();
        await _quickEntries.DidNotReceiveWithAnyArgs().UseAsync(default, default);
    }

    [Fact]
    public async Task A_broken_entry_is_not_saved()
    {
        var saved = await _sut.RunAsync(Item(account: ""), null, Changed);

        saved.Should().BeFalse();
        await _quickEntries.DidNotReceiveWithAnyArgs().UseAsync(default, default);
    }

    [Fact]
    public async Task An_expense_past_the_balance_of_an_account_that_cannot_go_negative_is_refused()
    {
        _balances.GetAccountBalanceAsync(3).Returns(2m);

        var saved = await _sut.RunAsync(Item(), null, Changed);

        saved.Should().BeFalse();
        await _errors.Received(1).HandleAsync(Arg.Is<string>(m => m.Contains("Bank")));
        await _quickEntries.DidNotReceiveWithAnyArgs().UseAsync(default, default);
    }

    [Fact]
    public async Task An_account_that_allows_a_negative_balance_saves_it()
    {
        _bank.AllowNegativeBalance = true;
        _balances.GetAccountBalanceAsync(3).Returns(2m);

        (await _sut.RunAsync(Item(), null, Changed)).Should().BeTrue();
    }

    [Fact]
    public async Task An_income_skips_the_balance_check_and_the_budget_alerts()
    {
        _balances.GetAccountBalanceAsync(3).Returns(0m);
        _quickEntries.UseAsync(7, Arg.Any<long?>()).Returns(new Transaction { Id = 41, AccountId = 3, AmountCents = 350, Type = TransactionType.Income });

        (await _sut.RunAsync(Item(type: TransactionType.Income), null, Changed)).Should().BeTrue();

        await _reminders.DidNotReceive().CheckBudgetsAsync();
    }

    [Fact]
    public async Task Cancelling_the_pocket_question_saves_nothing()
    {
        _pockets.GetShortfallAsync(3, Arg.Any<decimal>(), Arg.Any<DateTime>()).Returns(3.5m);
        _pockets.GetPocketsAsync(3).Returns([new PocketSummary(new Pocket { Id = 1, Name = "Holiday" }, _bank, 5_000)]);
        _dialogs.ActionSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns((string?)null);

        var saved = await _sut.RunAsync(Item(), null, Changed);

        saved.Should().BeFalse();
        await _quickEntries.DidNotReceiveWithAnyArgs().UseAsync(default, default);
    }

    [Fact]
    public async Task A_failure_is_reported_and_not_thrown()
    {
        _quickEntries.UseAsync(7, Arg.Any<long?>()).Returns<Transaction>(_ => throw new InvalidOperationException("gone"));

        var saved = await _sut.RunAsync(Item(), null, Changed);

        saved.Should().BeFalse();
        await _errors.Received(1).HandleAsync(Arg.Any<Exception>(), AppResources.FailedToSaveQuickEntry);
    }
}
