using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.ViewModels;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.ViewModels;

public class SetupViewModelTests
{
    private readonly ISetupService _setup = Substitute.For<ISetupService>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly SetupViewModel _sut;
    private SetupChoices? _applied;

    public SetupViewModelTests()
    {
        _setup.ApplyAsync(Arg.Do<SetupChoices>(c => _applied = c)).Returns(Task.CompletedTask);
        _sut = new SetupViewModel(_setup, new CurrencyService(new FakePreferences()), _categories, new FakePreferences(),
            LocalizationService.Instance, _dialogs, Substitute.For<IErrorHandler>());
    }

    private async Task FinishAsync()
    {
        while (!_sut.IsLastStep)
            await _sut.NextCommand.ExecuteAsync(null);
        await _sut.NextCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task First_run_starts_with_Cash_and_every_default_category()
    {
        var completed = false;
        _sut.Completed += (_, _) => completed = true;
        await _sut.InitializeAsync(isFirstRun: true);

        _sut.Accounts.Should().ContainSingle().Which.Name.Should().Be(AppResources.DefaultAccountCash);
        _sut.Accounts[0].Balance = "250";
        await FinishAsync();

        completed.Should().BeTrue();
        _applied!.ReplaceStartingData.Should().BeTrue();
        _applied.Accounts.Should().Equal(new SetupAccount(AppResources.DefaultAccountCash, 250m, Account.CashIconCode));
        _applied.Categories.Should().BeEquivalentTo(DefaultCategory.All);
    }

    [Fact]
    public async Task Skip_finishes_with_what_the_steps_show()
    {
        await _sut.InitializeAsync(isFirstRun: true);

        await _sut.SkipCommand.ExecuteAsync(null);

        _applied!.Accounts.Should().ContainSingle();
        _applied.Categories.Should().HaveCount(DefaultCategory.All.Count);
    }

    [Fact]
    public async Task Unchecked_categories_and_unnamed_accounts_are_left_out()
    {
        await _sut.InitializeAsync(isFirstRun: true);
        _sut.AddAccountCommand.Execute(null);
        _sut.AddAccountCommand.Execute(null);
        _sut.Accounts[1].Name = " Bank ";
        _sut.ToggleCategoryCommand.Execute(_sut.ExpenseCategories[0]);

        await FinishAsync();

        _applied!.Accounts.Select(a => a.Name).Should().Equal(AppResources.DefaultAccountCash, "Bank");
        _applied.Categories.Should().NotContain(_sut.ExpenseCategories[0].Category)
            .And.HaveCount(DefaultCategory.All.Count - 1);
    }

    [Fact]
    public async Task First_run_needs_an_account()
    {
        await _sut.InitializeAsync(isFirstRun: true);
        _sut.RemoveAccountCommand.Execute(_sut.Accounts[0]);

        await FinishAsync();

        await _setup.DidNotReceive().ApplyAsync(Arg.Any<SetupChoices>());
        await _dialogs.Received().AlertAsync(Arg.Any<string>(), AppResources.SetupNeedsAccount);
        _sut.IsAccountsStep.Should().BeTrue("the user is taken back to the accounts");
    }

    [Fact]
    public async Task From_settings_existing_categories_stay_checked_and_nothing_is_added_by_default()
    {
        var food = DefaultCategory.All.First(c => c.NameKey == "DefaultCategoryFood");
        _categories.GetAllAsync().Returns([new Category { Id = 1, Name = food.Name, Type = food.Type }]);
        await _sut.InitializeAsync(isFirstRun: false);

        var foodOption = _sut.ExpenseCategories.Single(o => o.Category == food);
        foodOption.IsSelected.Should().BeTrue();
        foodOption.CanToggle.Should().BeFalse();
        _sut.ExpenseCategories.Concat(_sut.IncomeCategories).Count(o => o.IsSelected).Should().Be(1);
        _sut.Accounts.Should().BeEmpty();

        await FinishAsync();

        _applied!.ReplaceStartingData.Should().BeFalse();
        _applied.Accounts.Should().BeEmpty();
        _applied.Categories.Should().BeEmpty();
    }

    [Fact]
    public async Task Back_goes_to_the_previous_step_until_the_first()
    {
        await _sut.InitializeAsync(isFirstRun: true);
        await _sut.NextCommand.ExecuteAsync(null);

        _sut.TryGoBack().Should().BeTrue();
        _sut.IsLanguageStep.Should().BeTrue();
        _sut.TryGoBack().Should().BeFalse();
    }
}
