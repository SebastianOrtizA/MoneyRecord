using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

public sealed class BalanceWidgetTests
{
    private const int WidgetId = 7;

    private readonly Account _cash = new() { Id = 1, Name = "Cash", IsDefault = true, CurrencyCode = "USD" };
    private readonly Account _euros = new() { Id = 2, Name = "Euros", CurrencyCode = "EUR" };
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IBalanceService _balances = Substitute.For<IBalanceService>();
    private readonly PrivacySettings _privacy = new(new FakePreferences());
    private readonly IDeviceLock _deviceLock = Substitute.For<IDeviceLock>();
    private readonly BalanceWidget _sut;

    public BalanceWidgetTests()
    {
        _accounts.GetOwnAsync().Returns(_ => new List<Account> { _cash, _euros });
        _balances.GetAccountBalanceAsync(_cash.Id).Returns(1250m);
        _balances.GetAccountBalanceAsync(_euros.Id).Returns(80.5m);
        _deviceLock.IsAvailable.Returns(true);
        var appLock = new AppLock(_privacy, _deviceLock, new FakeTimeProvider());
        _sut = new BalanceWidget(_accounts, _balances, _privacy, appLock, new FakePreferences());
    }

    [Fact]
    public async Task A_widget_shows_its_accounts_balance_and_opens_the_forms_on_it()
    {
        _sut.SetAccount(WidgetId, _euros.Id);

        var content = await _sut.GetContentAsync(WidgetId);

        content.AccountId.Should().Be(_euros.Id);
        content.AccountName.Should().Be("Euros");
        content.BalanceText.Should().Be(MoneyFormatter.Format(80.5m, "EUR"));
        content.IsHidden.Should().BeFalse();
        content.ExpenseRoute.Should().Be("AddTransactionPage?Type=Expense&AccountId=2");
        content.IncomeRoute.Should().Be("AddTransactionPage?Type=Income&AccountId=2");
        content.TransferRoute.Should().Be("AddTransferPage?SourceAccountId=2");
    }

    [Fact]
    public async Task Each_widget_keeps_its_own_account()
    {
        _sut.SetAccount(WidgetId, _euros.Id);
        _sut.SetAccount(WidgetId + 1, _cash.Id);

        (await _sut.GetContentAsync(WidgetId)).AccountId.Should().Be(_euros.Id);
        (await _sut.GetContentAsync(WidgetId + 1)).AccountId.Should().Be(_cash.Id);
    }

    [Fact]
    public async Task A_deleted_or_never_picked_account_falls_back_to_the_default_one()
    {
        _sut.SetAccount(WidgetId, 99);

        (await _sut.GetContentAsync(WidgetId)).AccountId.Should().Be(_cash.Id);
        (await _sut.GetContentAsync(WidgetId + 1)).AccountId.Should().Be(_cash.Id);
    }

    [Fact]
    public async Task Without_a_default_account_the_first_one_is_shown()
    {
        _cash.IsDefault = false;
        _accounts.GetOwnAsync().Returns(_ => new List<Account> { _euros, _cash });

        (await _sut.GetContentAsync(WidgetId)).AccountId.Should().Be(_euros.Id);
    }

    [Fact]
    public async Task A_removed_widget_forgets_its_account()
    {
        _sut.SetAccount(WidgetId, _euros.Id);

        _sut.Forget(WidgetId);

        (await _sut.GetContentAsync(WidgetId)).AccountId.Should().Be(_cash.Id);
    }

    [Fact]
    public async Task Hide_balances_hides_the_widgets_balance()
    {
        _privacy.HideBalances = true;

        var content = await _sut.GetContentAsync(WidgetId);

        content.IsHidden.Should().BeTrue();
        content.BalanceText.Should().Be(BalanceWidget.HiddenAmount);
        await _balances.DidNotReceive().GetAccountBalanceAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task The_app_lock_hides_the_widgets_balance()
    {
        // The home screen is seen without passing the app lock
        _privacy.LockEnabled = true;

        (await _sut.GetContentAsync(WidgetId)).BalanceText.Should().Be(BalanceWidget.HiddenAmount);
    }

    [Fact]
    public async Task An_app_lock_that_cannot_lock_hides_nothing()
    {
        // Without a phone lock the app lock doesn't lock either
        _privacy.LockEnabled = true;
        _deviceLock.IsAvailable.Returns(false);

        (await _sut.GetContentAsync(WidgetId)).IsHidden.Should().BeFalse();
    }

    [Fact]
    public async Task Without_accounts_the_buttons_still_open_the_forms()
    {
        _accounts.GetOwnAsync().Returns(_ => new List<Account>());

        var content = await _sut.GetContentAsync(WidgetId);

        content.AccountId.Should().BeNull();
        content.AccountName.Should().BeEmpty();
        content.BalanceText.Should().BeEmpty();
        content.ExpenseRoute.Should().Be("AddTransactionPage?Type=Expense");
        content.TransferRoute.Should().Be("AddTransferPage");
    }
}
