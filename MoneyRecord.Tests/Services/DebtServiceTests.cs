using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>
/// DebtService and the scheduled debt payments of RecurringTransactionService against a real temp SQLite file, with the
/// clock at 15 March 2026 10:00 (UTC).
/// </summary>
[Collection(LanguageCollection.Name)]
public sealed class DebtServiceTests : LanguageTestBase, IAsyncLifetime
{
    private static readonly DateTime Today = new(2026, 3, 15);

    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-debts-{Guid.NewGuid():N}");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero));
    private DatabaseInitializer _dbInitializer = null!;
    private AccountRepository _accounts = null!;
    private BalanceService _balances = null!;
    private PayeeService _payees = null!;
    private RecurringTransactionService _recurring = null!;
    private DebtService _sut = null!;
    private Account _cash = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    private static string Base => MoneyFormatter.BaseCurrency;

    public async Task InitializeAsync()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        Directory.CreateDirectory(_appData);
        _dbInitializer = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _dbInitializer.GetConnectionAsync();
        _accounts = new AccountRepository(_dbInitializer);
        var categories = new CategoryRepository(_dbInitializer);
        _balances = new BalanceService(_accounts, new TransactionRepository(_dbInitializer), new TransferRepository(_dbInitializer));
        _payees = new PayeeService(_dbInitializer, _accounts);
        _recurring = new RecurringTransactionService(new RecurringTransactionRepository(_dbInitializer), categories, _accounts,
            _dbInitializer, _clock);
        _sut = new DebtService(_dbInitializer, _accounts, categories, new PayeeRepository(_dbInitializer), _payees, _clock);

        _cash = new Account { Name = "Bank", InitialBalance = 1000m, CurrencyCode = Base };
        await _accounts.SaveAsync(_cash);
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private Task<int> LendAsync(decimal amount, int? accountId, DebtScheduleDraft? schedule = null, DateTime? due = null) =>
        _sut.AddAsync(new DebtDraft(DebtDirection.Lent, "Ana", Base, amount, accountId, Today.AddDays(-60), "Car repair", due), schedule);

    private Task<int> BorrowAsync(decimal amount, int? accountId, DebtScheduleDraft? schedule = null) =>
        _sut.AddAsync(new DebtDraft(DebtDirection.Borrowed, "Bank loan", Base, amount, accountId, Today.AddDays(-60), "", null), schedule);

    private async Task<DebtSummary> SummaryAsync(int debtId) => (await _sut.GetAllAsync()).Single(d => d.DebtId == debtId);

    private async Task<decimal> BalanceAsync(int accountId) => await _balances.GetAccountBalanceAsync(accountId);

    [Fact]
    public async Task Lending_moves_money_into_the_debts_hidden_account()
    {
        var id = await LendAsync(300m, _cash.Id);

        var debt = await SummaryAsync(id);
        debt.PersonName.Should().Be("Ana");
        debt.Title.Should().Be("Ana · Car repair");
        debt.TotalCents.Should().Be(30000);
        debt.OutstandingCents.Should().Be(30000);
        debt.IsPaidOff.Should().BeFalse();
        (await BalanceAsync(_cash.Id)).Should().Be(700m);
        (await BalanceAsync(debt.Debt.AccountId)).Should().Be(300m);
        // Hidden from the user's own accounts, still there for names
        (await _accounts.GetOwnAsync()).Should().Contain(a => a.Id == _cash.Id).And.NotContain(a => a.IsDebt);
        (await _accounts.GetByIdAsync(debt.Debt.AccountId))!.Name.Should().Be("Ana");
    }

    [Fact]
    public async Task An_already_owed_debt_moves_no_money()
    {
        var id = await BorrowAsync(500m, accountId: null);

        var debt = await SummaryAsync(id);
        debt.OutstandingCents.Should().Be(50000);
        debt.DirectionText.Should().Be("You owe");
        (await BalanceAsync(_cash.Id)).Should().Be(1000m);
        (await Db.Table<Transfer>().CountAsync()).Should().Be(0);
        (await _sut.GetMovementsAsync(id)).Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Kind = DebtMovementKind.Added, AmountCents = 50000L, CanDelete = false });
    }

    [Fact]
    public async Task A_payment_with_interest_saves_the_interest_as_an_expense_in_the_interest_category()
    {
        var id = await BorrowAsync(500m, _cash.Id);

        var paidOff = await _sut.RecordPaymentAsync(id, _cash.Id, Today, 200m, 15m);

        paidOff.Should().BeFalse();
        var debt = await SummaryAsync(id);
        debt.PaidCents.Should().Be(20000);
        debt.OutstandingCents.Should().Be(30000);
        debt.InterestCents.Should().Be(1500);
        // +500 borrowed, -200 paid back, -15 interest
        (await BalanceAsync(_cash.Id)).Should().Be(1285m);
        var interest = (await Db.Table<Transaction>().ToListAsync()).Should().ContainSingle().Subject;
        interest.Type.Should().Be(TransactionType.Expense);
        interest.DebtId.Should().Be(id);
        interest.PayeeId.Should().Be(debt.Debt.PayeeId);
        var category = await Db.FindAsync<Category>(interest.CategoryId);
        category.Name.Should().Be("Interest");
        category.Type.Should().Be(CategoryType.Expense);

        // A second interest payment reuses the category
        await _sut.RecordPaymentAsync(id, _cash.Id, Today, 100m, 5m);
        (await Db.Table<Category>().CountAsync(c => c.Name == "Interest")).Should().Be(1);
    }

    [Fact]
    public async Task Paying_what_is_left_pays_the_debt_off_and_stops_its_schedule()
    {
        var id = await LendAsync(100m, _cash.Id,
            new DebtScheduleDraft(40m, 0, RecurrenceFrequency.Monthly, Today.AddMonths(1), _cash.Id));

        (await _sut.RecordPaymentAsync(id, _cash.Id, Today, 100m, 0)).Should().BeTrue();

        var debt = await SummaryAsync(id);
        debt.IsPaidOff.Should().BeTrue();
        debt.Schedule.Should().BeNull();
        (await Db.Table<RecurringTransaction>().FirstAsync()).IsActive.Should().BeFalse();
        (await BalanceAsync(_cash.Id)).Should().Be(1000m);
    }

    [Fact]
    public async Task A_payment_over_what_is_left_or_from_another_currency_is_refused()
    {
        var id = await LendAsync(100m, _cash.Id);
        var euros = new Account { Name = "Euros", CurrencyCode = Base == "EUR" ? "USD" : "EUR" };
        await _accounts.SaveAsync(euros);

        await FluentActions.Awaiting(() => _sut.RecordPaymentAsync(id, _cash.Id, Today, 100.01m, 0))
            .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => _sut.RecordPaymentAsync(id, euros.Id, Today, 10m, 0))
            .Should().ThrowAsync<InvalidOperationException>();
        (await SummaryAsync(id)).PaidCents.Should().Be(0);
    }

    [Fact]
    public async Task Scheduled_payments_are_capped_at_what_is_left_and_stop_when_it_is_paid()
    {
        // 100 lent, 40 a month from 15 December: Dec, Jan, Feb are due by 15 March, Mar too; the third pays the last 20
        var id = await LendAsync(100m, _cash.Id,
            new DebtScheduleDraft(40m, 0, RecurrenceFrequency.Monthly, new DateTime(2025, 12, 15), _cash.Id));

        var run = await _recurring.ProcessDueTransactionsAsync();

        run.Created.Should().Be(3);
        run.PaidOffDebtIds.Should().Equal(id);
        var payments = (await _sut.GetMovementsAsync(id)).Where(m => m.Kind == DebtMovementKind.Payment)
            .OrderBy(m => m.Date).Select(m => m.AmountCents);
        payments.Should().Equal(4000, 4000, 2000);
        (await SummaryAsync(id)).IsPaidOff.Should().BeTrue();
        (await BalanceAsync(_cash.Id)).Should().Be(1000m);
        (await Db.Table<RecurringTransaction>().FirstAsync()).IsActive.Should().BeFalse();

        // Nothing more later
        _clock.Advance(TimeSpan.FromDays(90));
        (await _recurring.ProcessDueTransactionsAsync()).Created.Should().Be(0);
    }

    [Fact]
    public async Task A_borrowed_schedule_pays_out_of_the_account_with_its_interest_and_shows_in_upcoming_capped()
    {
        var id = await BorrowAsync(100m, _cash.Id,
            new DebtScheduleDraft(30m, 2m, RecurrenceFrequency.Weekly, Today.AddDays(1), _cash.Id));

        var upcoming = await _recurring.GetUpcomingAsync();

        // 30, 30, 30, 10: the fourth is capped and nothing comes after it; each with its interest
        upcoming.Select(u => u.Amount).Should().Equal(32m, 32m, 32m, 12m);
        upcoming.Should().AllSatisfy(u =>
        {
            u.Type.Should().Be(TransactionType.Expense);
            u.Description.Should().Be("Payment to Bank loan");
            u.CategoryIconCode.Should().Be(DebtSummary.IconCode);
        });

        _clock.Advance(TimeSpan.FromDays(8));
        (await _recurring.ProcessDueTransactionsAsync()).Created.Should().Be(2);
        var debt = await SummaryAsync(id);
        debt.PaidCents.Should().Be(6000);
        debt.InterestCents.Should().Be(400);
        // +100 borrowed, -60 paid back, -4 interest
        (await BalanceAsync(_cash.Id)).Should().Be(1036m);
    }

    [Fact]
    public async Task Editing_renames_the_hidden_account_and_replaces_the_schedule()
    {
        var id = await LendAsync(100m, _cash.Id,
            new DebtScheduleDraft(40m, 0, RecurrenceFrequency.Monthly, Today.AddMonths(1), _cash.Id));

        await _sut.UpdateAsync(id, "Ana María", "", Today.AddMonths(2),
            new DebtScheduleDraft(25m, 0, RecurrenceFrequency.Weekly, Today.AddDays(3), _cash.Id));

        var debt = await SummaryAsync(id);
        debt.PersonName.Should().Be("Ana María");
        debt.Debt.DueDate.Should().Be(Today.AddMonths(2));
        (await _accounts.GetByIdAsync(debt.Debt.AccountId))!.Name.Should().Be("Ana María");
        var rule = (await Db.Table<RecurringTransaction>().ToListAsync()).Should().ContainSingle().Subject;
        rule.AmountCents.Should().Be(2500);
        rule.Frequency.Should().Be(RecurrenceFrequency.Weekly);
        rule.NextOccurrence.Should().Be(Today.AddDays(3));
        rule.Description.Should().Be("Payment from Ana María");

        await _sut.UpdateAsync(id, "Ana María", "", null, schedule: null);
        (await Db.Table<RecurringTransaction>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Renaming_or_merging_the_payee_renames_the_debt_and_deleting_it_is_refused()
    {
        var id = await LendAsync(100m, _cash.Id,
            new DebtScheduleDraft(40m, 0, RecurrenceFrequency.Monthly, Today.AddMonths(1), _cash.Id));
        var payeeId = (await SummaryAsync(id)).Debt.PayeeId;

        await _payees.RenameAsync(payeeId, "Anita");

        (await SummaryAsync(id)).PersonName.Should().Be("Anita");
        (await _accounts.GetByIdAsync((await SummaryAsync(id)).Debt.AccountId))!.Name.Should().Be("Anita");
        (await Db.Table<RecurringTransaction>().FirstAsync()).Description.Should().Be("Payment from Anita");
        (await _payees.GetAllAsync()).Single().UsesText.Should().Be("0 transactions, 1 debt");
        await FluentActions.Awaiting(() => _payees.DeleteAsync(payeeId)).Should().ThrowAsync<InvalidOperationException>();

        var other = (await _payees.ResolveAsync("Ana R."))!.Value;
        await _payees.MergeAsync(payeeId, other);
        (await SummaryAsync(id)).PersonName.Should().Be("Ana R.");
        (await Db.Table<RecurringTransaction>().FirstAsync()).Description.Should().Be("Payment from Ana R.");
    }

    [Fact]
    public async Task The_history_lists_every_movement_and_one_can_be_deleted()
    {
        var id = await LendAsync(100m, _cash.Id);
        await _sut.AddToDebtAsync(id, _cash.Id, Today.AddDays(-10), 50m);
        await _sut.RecordPaymentAsync(id, _cash.Id, Today, 30m, 3m);

        var movements = await _sut.GetMovementsAsync(id);

        movements.Select(m => (m.Kind, m.AmountCents)).Should().Equal(
            (DebtMovementKind.Interest, 300L), (DebtMovementKind.Payment, 3000L),
            (DebtMovementKind.Added, 5000L), (DebtMovementKind.Added, 10000L));
        movements.Should().AllSatisfy(m => m.AccountName.Should().Be("Bank"));
        (await SummaryAsync(id)).OutstandingCents.Should().Be(12000);

        await _sut.DeleteMovementAsync(movements[1]);
        (await SummaryAsync(id)).OutstandingCents.Should().Be(15000);
    }

    [Fact]
    public async Task Deleting_a_debt_removes_its_money_history_schedule_and_hidden_account()
    {
        var id = await BorrowAsync(500m, _cash.Id,
            new DebtScheduleDraft(40m, 0, RecurrenceFrequency.Monthly, Today.AddMonths(1), _cash.Id));
        await _sut.RecordPaymentAsync(id, _cash.Id, Today, 100m, 10m);
        var ledgerId = (await SummaryAsync(id)).Debt.AccountId;

        await _sut.DeleteAsync(id);

        (await _sut.GetAllAsync()).Should().BeEmpty();
        (await Db.Table<Transfer>().CountAsync()).Should().Be(0);
        (await Db.Table<Transaction>().CountAsync()).Should().Be(0);
        (await Db.Table<RecurringTransaction>().CountAsync()).Should().Be(0);
        (await _accounts.GetByIdAsync(ledgerId)).Should().BeNull();
        (await BalanceAsync(_cash.Id)).Should().Be(1000m);
    }

    [Fact]
    public async Task Open_debts_come_first_by_due_date_and_paid_ones_last()
    {
        var paid = await LendAsync(10m, _cash.Id);
        await _sut.RecordPaymentAsync(paid, _cash.Id, Today, 10m, 0);
        var later = await LendAsync(10m, _cash.Id, due: Today.AddDays(30));
        var overdue = await LendAsync(10m, _cash.Id, due: Today.AddDays(-1));
        var none = await LendAsync(10m, _cash.Id);

        var debts = await _sut.GetAllAsync();

        debts.Select(d => d.DebtId).Should().Equal(overdue, later, none, paid);
        debts[0].IsOverdue.Should().BeTrue();
        debts[0].StatusText.Should().Be("Overdue");
        debts[1].IsDueSoon.Should().BeFalse();
        debts[3].StatusText.Should().Be("Paid off");
    }
    private Task<RecurringTransaction> AddPlainRecurringAsync(DateTime start) =>
        InsertAsync(new RecurringTransaction
        {
            Amount = 20m, Description = "Gym", CategoryId = 1, AccountId = _cash.Id, TransactionType = TransactionType.Expense,
            Frequency = RecurrenceFrequency.Monthly, StartDate = start, NextOccurrence = start
        });

    private async Task<T> InsertAsync<T>(T row)
    {
        await Db.InsertAsync(row);
        return row;
    }

    [Fact]
    public async Task A_schedule_whose_account_changed_currency_stops_without_blocking_other_recurring_transactions()
    {
        var id = await LendAsync(100m, _cash.Id,
            new DebtScheduleDraft(40m, 0, RecurrenceFrequency.Monthly, Today.AddDays(-1), _cash.Id));
        await AddPlainRecurringAsync(Today.AddDays(-1));
        _cash.CurrencyCode = Base == "EUR" ? "USD" : "EUR";
        await _accounts.SaveAsync(_cash);

        var run = await _recurring.ProcessDueTransactionsAsync();

        run.Created.Should().Be(1);
        (await Db.Table<Transaction>().ToListAsync()).Should().ContainSingle().Which.Description.Should().Be("Gym");
        (await Db.Table<RecurringTransaction>().FirstAsync(r => r.DebtId == id)).IsActive.Should().BeFalse();
        (await SummaryAsync(id)).PaidCents.Should().Be(0);
    }

    [Fact]
    public async Task A_recurrence_that_fails_is_logged_and_the_others_are_still_processed()
    {
        var id = await LendAsync(100m, _cash.Id,
            new DebtScheduleDraft(40m, 0, RecurrenceFrequency.Monthly, Today.AddDays(-1), _cash.Id));
        await AddPlainRecurringAsync(Today.AddDays(-1));
        // A debt whose hidden account is gone can't be read: its schedule throws
        await Db.ExecuteAsync("DELETE FROM \"Account\" WHERE IsDebt = 1");

        var run = await _recurring.ProcessDueTransactionsAsync();

        run.Created.Should().Be(1);
        run.PaidOffDebtIds.Should().BeEmpty();
        (await Db.Table<RecurringTransaction>().FirstAsync(r => r.DebtId == id)).IsActive.Should().BeTrue("it is tried again next time");
    }

    [Fact]
    public async Task Deleting_what_was_lent_below_what_was_paid_back_is_refused()
    {
        var id = await LendAsync(100m, _cash.Id);
        await _sut.AddToDebtAsync(id, _cash.Id, Today, 50m);
        await _sut.RecordPaymentAsync(id, _cash.Id, Today, 120m, 0);
        var debt = await SummaryAsync(id);
        var movements = await _sut.GetMovementsAsync(id);
        var first = movements.Single(m => m.AmountCents == 10000);
        var more = movements.Single(m => m.AmountCents == 5000);

        debt.CanDelete(first).Should().BeFalse();
        debt.CanDelete(more).Should().BeFalse();
        await FluentActions.Awaiting(() => _sut.DeleteMovementAsync(first)).Should().ThrowAsync<InvalidOperationException>();
        (await SummaryAsync(id)).TotalCents.Should().Be(15000);

        // Once the payment is smaller, the 50 can go
        await _sut.DeleteMovementAsync(movements.Single(m => m.Kind == DebtMovementKind.Payment));
        await _sut.RecordPaymentAsync(id, _cash.Id, Today, 90m, 0);
        (await SummaryAsync(id)).CanDelete(more).Should().BeTrue();
        await _sut.DeleteMovementAsync(more);
        (await SummaryAsync(id)).OutstandingCents.Should().Be(1000);
    }

    [Fact]
    public async Task A_debt_overpaid_from_elsewhere_shows_as_overpaid_not_paid_off()
    {
        var id = await LendAsync(100m, _cash.Id);
        await _sut.RecordPaymentAsync(id, _cash.Id, Today, 60m, 0);
        // The Transactions list can delete the lent transfer on its own
        var lent = (await _sut.GetMovementsAsync(id)).Single(m => m.Kind == DebtMovementKind.Added);
        await Db.DeleteAsync<Transfer>(lent.TransferId!.Value);

        var debt = await SummaryAsync(id);

        debt.IsOverpaid.Should().BeTrue();
        debt.IsPaidOff.Should().BeFalse();
        debt.CanRecordPayment.Should().BeFalse();
        debt.StatusText.Should().Be($"Overpaid by {MoneyFormatter.Format(60m, Base)}");
        debt.Level.Should().Be(BudgetLevel.Exceeded);
    }

    [Fact]
    public async Task The_interest_category_is_found_by_its_mark_not_by_its_translated_name()
    {
        // A category of the user's own with the same name is left alone
        var own = await InsertAsync(new Category { Name = "Interest", Type = CategoryType.Expense });
        var id = await BorrowAsync(500m, _cash.Id);
        await _sut.RecordPaymentAsync(id, _cash.Id, Today, 100m, 5m);
        LocalizationService.Instance.SetLanguage("es", new FakePreferences());
        await _sut.RecordPaymentAsync(id, _cash.Id, Today, 100m, 5m);

        var categoryIds = (await Db.Table<Transaction>().ToListAsync()).Select(t => t.CategoryId).Distinct().ToList();

        categoryIds.Should().ContainSingle().Which.Should().NotBe(own.Id);
        var category = await Db.FindAsync<Category>(categoryIds[0]);
        category.IsDebtInterest.Should().BeTrue();
        category.Name.Should().Be("Interest", "named once, in the language it was created in");
    }
}
