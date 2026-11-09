using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>An account of the report: its texts ready for the row (all in the account's own currency).</summary>
    public sealed record AccountReportRowItem(string Name, string NetText, string DetailText, string BalanceText);

    /// <summary>The accounts of one currency with their sums, so currencies are never added together.</summary>
    public sealed record AccountReportGroupItem(string CurrencyCode, string NetText, string DetailText,
        IReadOnlyList<AccountReportRowItem> Rows);

    /// <summary>
    /// What happened to each account in the shared period: income, expenses and transfers, the net change and today's balance,
    /// grouped by account currency.
    /// </summary>
    public partial class AccountReportViewModel : ObservableObject
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransferRepository _transferRepository;
        private readonly IBalanceService _balanceService;
        private readonly IErrorHandler _errorHandler;

        // While the page is shown: a period change reloads it
        private IDisposable? _periodWatch;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private IReadOnlyList<AccountReportGroupItem> groups = [];

        public PeriodViewModel Period { get; }

        public AccountReportViewModel(PeriodViewModel period, IAccountRepository accountRepository,
            ITransactionRepository transactionRepository, ITransferRepository transferRepository,
            IBalanceService balanceService, IErrorHandler errorHandler)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _transferRepository = transferRepository;
            _balanceService = balanceService;
            _errorHandler = errorHandler;
            Period = period;
        }

        /// <summary>The page appeared: load, and follow the shared period until <see cref="Deactivate"/>.</summary>
        public Task InitializeAsync()
        {
            _periodWatch ??= Period.WatchChanges(() => LoadCommand.Execute(null));
            return LoadCommand.ExecuteAsync(null);
        }

        public void Deactivate()
        {
            _periodWatch?.Dispose();
            _periodWatch = null;
        }

        private static string Detail(decimal income, decimal expense, decimal transfers, string code) =>
            string.Format(AppResources.AccountReportRowDetail, MoneyFormatter.Format(income, code),
                MoneyFormatter.Format(expense, code), MoneyFormatter.Format(transfers, code));

        private static AccountReportGroupItem ToItem(AccountReportGroup group)
        {
            var code = group.CurrencyCode;
            return new(code, MoneyFormatter.Format(group.Net, code), Detail(group.Income, group.Expense, group.Transfers, code),
                [.. group.Rows.Select(r => new AccountReportRowItem(r.Name, MoneyFormatter.Format(r.Net, code),
                    Detail(r.Income, r.Expense, r.Transfers, code),
                    string.Format(AppResources.AccountReportBalance, MoneyFormatter.Format(r.Balance, code))))]);
        }

        /// <summary>Each run cancels the one in progress, so an older period's results never overwrite a newer one's.</summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;
                var (start, end) = Period.Range;
                // Net counts every entry dated in the period, future-dated ones included, while the balance counts only what is dated
                // up to today (IBalanceService.CountedBefore): they differ while the period reaches past today
                // The range's end is inclusive; the sums take an exclusive bound
                var before = end.AddTicks(1);
                var accountsTask = _accountRepository.GetOwnAsync();
                var incomesTask = _transactionRepository.SumCentsByAccountAsync(TransactionType.Income, start, before);
                var expensesTask = _transactionRepository.SumCentsByAccountAsync(TransactionType.Expense, start, before);
                var outgoingTask = _transferRepository.SumCentsBySourceAsync(start, before);
                var incomingTask = _transferRepository.SumCentsByDestinationAsync(start, before);
                var balancesTask = _balanceService.GetAllAccountBalancesAsync();
                await Task.WhenAll(accountsTask, incomesTask, expensesTask, outgoingTask, incomingTask, balancesTask);
                cancellationToken.ThrowIfCancellationRequested();

                var balances = (await balancesTask).ToDictionary(b => b.AccountId, b => b.CurrentBalance);
                var report = AccountReportBuilder.Build(await accountsTask, MoneyFormatter.BaseCurrency,
                    await incomesTask, await expensesTask, await outgoingTask, await incomingTask, balances);

                Groups = [.. report.Select(ToItem)];
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadReportData);
            }
            finally
            {
                // The newer load owns the spinner
                if (!cancellationToken.IsCancellationRequested)
                    IsLoading = false;
            }
        }
    }
}
