using System.Globalization;
using AwesomeAssertions;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.ViewModels;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.ViewModels;

/// <summary>Switching the language in step 1 (live, while the setup is open).</summary>
[Collection(LanguageCollection.Name)]
public sealed class SetupViewModelLanguageTests : LanguageTestBase
{
    private static readonly CultureInfo Spanish = new("es");

    private readonly FakePreferences _preferences = new();
    private readonly SetupViewModel _sut;

    public SetupViewModelLanguageTests()
    {
        _sut = new SetupViewModel(Substitute.For<ISetupService>(), new CurrencyService(new FakePreferences()),
            Substitute.For<ICategoryRepository>(), _preferences, LocalizationService.Instance,
            Substitute.For<IDialogService>(), Substitute.For<IErrorHandler>());
    }

    private static string InSpanish(string key) => AppResources.ResourceManager.GetString(key, Spanish)!;

    private void SelectSpanish() => _sut.SelectedLanguage = _sut.Languages.Single(l => l.Code == "es");

    [Fact]
    public void Languages_are_the_device_default_plus_each_supported_one()
    {
        _sut.Languages.Select(l => l.Code).Should().Equal(["", .. LocalizationService.SupportedLanguages]);
        _sut.SelectedLanguage.Code.Should().Be("en");
    }

    [Fact]
    public async Task Choosing_a_language_saves_it_and_translates_the_setup()
    {
        await _sut.InitializeAsync(isFirstRun: true);
        var changed = new List<string?>();
        _sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        SelectSpanish();

        _preferences.Get(LocalizationService.LanguagePreferenceKey, string.Empty).Should().Be("es");
        _sut.StepText.Should().Be(string.Format(InSpanish(nameof(AppResources.SetupStepOf)), 1, SetupViewModel.StepCount));
        _sut.ExpenseCategories[0].Name.Should().Be(InSpanish(_sut.ExpenseCategories[0].Category.NameKey));
        changed.Should().Contain(string.Empty, "every localized text is refreshed");
    }

    [Fact]
    public async Task The_language_list_is_rebuilt_and_keeps_the_choice()
    {
        await _sut.InitializeAsync(isFirstRun: true);

        SelectSpanish();

        _sut.Languages[0].Name.Should().Be(InSpanish(nameof(AppResources.LanguageSystem)));
        _sut.SelectedLanguage.Should().Be(_sut.Languages.Single(l => l.Code == "es"), "the picker finds it by value");
    }

    [Fact]
    public async Task An_untouched_Cash_row_follows_the_language_and_a_renamed_one_does_not()
    {
        await _sut.InitializeAsync(isFirstRun: true);
        _sut.AddAccountCommand.Execute(null);
        _sut.Accounts[1].Name = "Wallet";

        SelectSpanish();

        _sut.Accounts.Select(a => a.Name).Should().Equal(InSpanish(nameof(AppResources.DefaultAccountCash)), "Wallet");
    }

    [Fact]
    public async Task Choosing_the_current_language_again_changes_nothing()
    {
        await _sut.InitializeAsync(isFirstRun: true);
        var changed = 0;
        _sut.PropertyChanged += (_, _) => changed++;

        _sut.SelectedLanguage = _sut.Languages.Single(l => l.Code == "en");

        _preferences.Get(LocalizationService.LanguagePreferenceKey, "unset").Should().Be("unset");
        changed.Should().BeLessThanOrEqualTo(1, "only SelectedLanguage itself, if anything");
    }
}
