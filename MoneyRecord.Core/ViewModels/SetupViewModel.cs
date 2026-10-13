using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>A starting account typed in the setup.</summary>
    public partial class SetupAccountRow : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        /// <summary>The starting balance as typed (in the base currency).</summary>
        [ObservableProperty]
        private string balance = string.Empty;

        public string IconCode { get; init; } = IconHelper.DefaultAccountIconCode;
    }

    /// <summary>A default category the setup offers.</summary>
    public partial class SetupCategoryOption : ObservableObject
    {
        public SetupCategoryOption(DefaultCategory category, bool exists)
        {
            Category = category;
            Exists = exists;
            isSelected = true;
        }

        public DefaultCategory Category { get; }

        /// <summary>Already among the user's categories (setup reopened from Settings): shown checked, can't change.</summary>
        public bool Exists { get; }

        public bool CanToggle => !Exists;

        public string Name => Category.Name;

        public string DisplayIcon => IconHelper.GetCategoryDisplayIcon(Category.IconCode);

        [ObservableProperty]
        private bool isSelected;

        /// <summary>The app language changed: the name follows it.</summary>
        public void RefreshName() => OnPropertyChanged(nameof(Name));
    }

    /// <summary>
    /// The setup assistant: language and base currency → starting accounts → default categories. On the first run it
    /// replaces the seeded Cash account and categories; reopened from Settings it only adds.
    /// </summary>
    public partial class SetupViewModel : ObservableObject
    {
        public const int StepCount = 3;

        private readonly ISetupService _setupService;
        private readonly ICurrencyService _currencyService;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IPreferencesService _preferences;
        private readonly LocalizationService _localization;
        private readonly IDialogService _dialogService;
        private readonly IErrorHandler _errorHandler;

        public SetupViewModel(ISetupService setupService, ICurrencyService currencyService,
            ICategoryRepository categoryRepository, IPreferencesService preferences, LocalizationService localization,
            IDialogService dialogService, IErrorHandler errorHandler)
        {
            _setupService = setupService;
            _currencyService = currencyService;
            _categoryRepository = categoryRepository;
            _preferences = preferences;
            _localization = localization;
            _dialogService = dialogService;
            _errorHandler = errorHandler;

            Currencies = currencyService.GetAvailableCurrencies();
            baseCurrency = currencyService.GetCurrency(currencyService.GetBaseCurrencyCode());
            Languages = LanguageOption.All();
            selectedLanguage = Languages.First(l => l.Code == localization.LanguageSetting);
        }

        /// <summary>Raised when the setup is finished (or skipped).</summary>
        public event EventHandler? Completed;

        /// <summary>First run: replaces the seed data, can be skipped. From Settings: only adds.</summary>
        public bool IsFirstRun { get; private set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLanguageStep), nameof(IsAccountsStep), nameof(IsCategoriesStep),
            nameof(IsLastStep), nameof(CanGoBack), nameof(StepText), nameof(NextText), nameof(Progress))]
        private int step = 1;

        public double Progress => (double)Step / StepCount;

        public bool IsLanguageStep => Step == 1;
        public bool IsAccountsStep => Step == 2;
        public bool IsCategoriesStep => Step == 3;
        public bool IsLastStep => Step == StepCount;
        public bool CanGoBack => Step > 1;
        public string StepText => string.Format(AppResources.SetupStepOf, Step, StepCount);
        public string NextText => !IsLastStep ? AppResources.Next : IsFirstRun ? AppResources.SetupGetStarted : AppResources.Save;

        [ObservableProperty]
        private IReadOnlyList<LanguageOption> languages;

        [ObservableProperty]
        private LanguageOption selectedLanguage;

        public List<Currency> Currencies { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AccountsDescription))]
        private Currency? baseCurrency;

        public ObservableCollection<SetupAccountRow> Accounts { get; } = [];

        public string AccountsDescription => string.Format(
            IsFirstRun ? AppResources.SetupAccountsDescription : AppResources.SetupAccountsAddDescription,
            BaseCurrency?.Code ?? _currencyService.GetBaseCurrencyCode());

        public string CategoriesDescription =>
            IsFirstRun ? AppResources.SetupCategoriesDescription : AppResources.SetupCategoriesAddDescription;

        public IReadOnlyList<SetupCategoryOption> ExpenseCategories { get; private set; } = [];
        public IReadOnlyList<SetupCategoryOption> IncomeCategories { get; private set; } = [];

        [ObservableProperty]
        private bool isSaving;

        public async Task InitializeAsync(bool isFirstRun)
        {
            IsFirstRun = isFirstRun;
            Step = 1;
            Accounts.Clear();

            // First run: the seeded Cash account, and every default category; from Settings, add nothing unless asked
            List<Category> existing = isFirstRun ? [] : await _categoryRepository.GetAllAsync();
            if (isFirstRun)
                Accounts.Add(new SetupAccountRow { Name = AppResources.DefaultAccountCash, IconCode = Account.CashIconCode });

            var options = DefaultCategory.All
                .Select(d => new SetupCategoryOption(d, existing.Any(d.Matches)))
                .ToList();
            foreach (var option in options)
                option.IsSelected = isFirstRun || option.Exists;

            ExpenseCategories = options.Where(o => o.Category.Type == CategoryType.Expense).ToList();
            IncomeCategories = options.Where(o => o.Category.Type == CategoryType.Income).ToList();

            OnPropertyChanged(string.Empty);
        }

        partial void OnSelectedLanguageChanged(LanguageOption value)
        {
            if (value == null || value.Code == _localization.LanguageSetting)
                return;

            var cashName = AppResources.DefaultAccountCash;
            _localization.SetLanguage(value.Code, _preferences);

            // An untouched Cash row follows the language, like the categories
            foreach (var row in Accounts.Where(r => r.Name == cashName))
                row.Name = AppResources.DefaultAccountCash;
            foreach (var option in ExpenseCategories.Concat(IncomeCategories))
                option.RefreshName();

            // The "System default" label is translated too; keep the choice selected
            Languages = LanguageOption.All();
            SelectedLanguage = Languages.First(l => l.Code == value.Code);

            // Every localized text (step, buttons, descriptions)
            OnPropertyChanged(string.Empty);
        }

        partial void OnBaseCurrencyChanged(Currency? value)
        {
            if (value != null)
                _currencyService.SetBaseCurrencyCode(value.Code);
        }

        [RelayCommand]
        private Task NextAsync()
        {
            if (!IsLastStep)
            {
                Step++;
                return Task.CompletedTask;
            }
            return FinishAsync();
        }

        [RelayCommand]
        private void Back() => TryGoBack();

        /// <summary>Goes to the previous step; false on the first one (Android back then leaves the page).</summary>
        public bool TryGoBack()
        {
            if (!CanGoBack)
                return false;
            Step--;
            return true;
        }

        [RelayCommand]
        private void AddAccount() => Accounts.Add(new SetupAccountRow());

        [RelayCommand]
        private void RemoveAccount(SetupAccountRow row) => Accounts.Remove(row);

        [RelayCommand]
        private void ToggleCategory(SetupCategoryOption option)
        {
            if (option.CanToggle)
                option.IsSelected = !option.IsSelected;
        }

        /// <summary>Skips the remaining steps: what they show (Cash, every default category) is used.</summary>
        [RelayCommand]
        private Task SkipAsync() => FinishAsync();

        private async Task FinishAsync()
        {
            if (IsSaving)
                return;

            var currencyCode = BaseCurrency?.Code ?? _currencyService.GetBaseCurrencyCode();
            var accounts = Accounts
                .Where(r => !string.IsNullOrWhiteSpace(r.Name))
                .Select(r => new SetupAccount(r.Name.Trim(), AmountParser.Parse(r.Balance, currencyCode, allowNegative: true), r.IconCode))
                .ToList();

            if (IsFirstRun && accounts.Count == 0)
            {
                Step = 2;
                await _dialogService.AlertAsync(AppResources.SetupAccountsTitle, AppResources.SetupNeedsAccount);
                return;
            }

            var categories = ExpenseCategories.Concat(IncomeCategories)
                .Where(o => o.IsSelected && !o.Exists)
                .Select(o => o.Category)
                .ToList();

            try
            {
                IsSaving = true;
                await _setupService.ApplyAsync(new SetupChoices(currencyCode, accounts, categories, ReplaceStartingData: IsFirstRun));
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.SetupFailed);
                return;
            }
            finally
            {
                IsSaving = false;
            }

            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}
