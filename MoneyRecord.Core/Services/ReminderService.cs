using Microsoft.Extensions.Logging;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class ReminderService(
        IRecurringTransactionService recurring,
        IBudgetRepository budgets,
        ICategoryRepository categories,
        IBalanceService balances,
        IReminderSettings settings,
        INotificationService notifications,
        IDialogService dialogs,
        IPreferencesService preferences,
        TimeProvider clock,
        ILogger<ReminderService> logger) : IReminderService
    {
        // Notification IDs: one per recurrence and per budget, so a newer reminder replaces the older one (bill IDs would
        // only reach the budget range after 100,000 recurrences)
        public const int RecurringAddedId = 1;
        public const int BillIdBase = 100_000;
        public const int BudgetIdBase = 200_000;

        // The app's routes (see AppShell); Core can't name the page types
        public const string UpcomingRoute = "RecurringTransactionsPage?Show=Upcoming";
        public const string TransactionsRoute = "//TransactionsPage";
        public const string BudgetsRoute = "//BudgetPage";

        // What was already notified: the last bill occurrence per recurrence, and per budget its period + alert level
        private const string BillNotifiedKey = "Reminders.Bill.";
        private const string BudgetNotifiedKey = "Reminders.Budget.";

        private bool CanNotify => settings.Enabled && notifications.IsSupported && notifications.AreAllowed;

        public async Task RunDailyAsync()
        {
            // Before processing: today's occurrences are still upcoming until they are created
            if (CanNotify && settings.BillDaysBefore != ReminderSettings.BillsOff)
                await NotifyBillsAsync();

            var created = await recurring.ProcessDueTransactionsAsync();
            if (created > 0 && CanNotify && settings.RecurringAdded)
            {
                notifications.Show(new AppNotification(RecurringAddedId, ReminderChannel.Bills,
                    AppResources.RecurringTransactions, Plurals.RecurringAdded(created), TransactionsRoute));
            }

            await CheckBudgetsCoreAsync();
        }

        private async Task NotifyBillsAsync()
        {
            var today = clock.GetLocalNow().DateTime.Date;
            var due = (await recurring.GetUpcomingAsync(settings.BillDaysBefore))
                .Where(e => e.Type == TransactionType.Expense && e.Date >= today);

            // The soonest occurrence of each recurrence not notified yet (a daily one gets one reminder a day)
            var bills = due.GroupBy(e => e.RecurringId)
                .Select(g => g.FirstOrDefault(e => e.Date > preferences.Get(BillNotifiedKey + g.Key, DateTime.MinValue)))
                .OfType<UpcomingEntry>();

            foreach (var bill in bills)
            {
                var parts = new[] { bill.WhenText, MoneyFormatter.Format(bill.Amount, bill.CurrencyCode), bill.AccountName };
                notifications.Show(new AppNotification(BillIdBase + bill.RecurringId, ReminderChannel.Bills,
                    string.Format(AppResources.BillDueTitle, bill.Description),
                    string.Join(" · ", parts.Where(p => p.Length > 0)), UpcomingRoute));
                preferences.Set(BillNotifiedKey + bill.RecurringId, bill.Date);
            }
        }

        public async Task CheckBudgetsAsync()
        {
            // Best-effort after a save: never keep the user on a form that was already saved
            try
            {
                await CheckBudgetsCoreAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Budget alerts check failed");
            }
        }

        private async Task CheckBudgetsCoreAsync()
        {
            if (!CanNotify || !settings.BudgetAlerts)
                return;

            var active = await budgets.GetActiveBudgetsAsync();
            if (active.Count == 0)
                return;

            var names = (await categories.GetAllAsync()).ToDictionary(c => c.Id, c => c.Name);
            var now = clock.GetLocalNow().DateTime;

            // Each budget in its own current day, month or year: one query per period
            foreach (var period in active.GroupBy(b => b.Period))
            {
                var (start, end) = DateRangeHelper.GetBudgetWindow(period.Key, now);
                var spentByCategory = await balances.GetExpensesByCategoryAsync(start, end);

                foreach (var budget in period)
                {
                    var progress = new BudgetProgress
                    {
                        CategoryName = names.GetValueOrDefault(budget.CategoryId, string.Empty),
                        LimitAmount = budget.LimitAmount,
                        SpentAmount = spentByCategory.GetValueOrDefault(budget.CategoryId)
                    };
                    progress.CalculateProgress();
                    NotifyBudget(budget, start, progress);
                }
            }
        }

        private void NotifyBudget(Budget budget, DateTime periodStart, BudgetProgress progress)
        {
            // 80% (High) and over the limit (Exceeded) each notify once per budget period
            if (progress.Level is not (BudgetLevel.High or BudgetLevel.Exceeded))
                return;

            // The budget (its creation, so a reused ID starts over) and its current period (a Month → Year change too),
            // then the highest level notified in it
            var window = $"{budget.CreatedDate.Ticks}:{budget.Period}:{periodStart:yyyyMMdd}";
            var notified = preferences.Get(BudgetNotifiedKey + budget.Id, string.Empty).Split('|');
            if (notified.Length == 2 && notified[0] == window && int.Parse(notified[1]) >= (int)progress.Level)
                return;

            var title = progress.Level == BudgetLevel.Exceeded
                ? AppResources.BudgetExceededTitle
                : AppResources.BudgetNearLimitTitle;
            notifications.Show(new AppNotification(BudgetIdBase + budget.Id, ReminderChannel.Budgets,
                string.Format(title, progress.CategoryName), progress.SpentOfLimitText, BudgetsRoute));
            preferences.Set(BudgetNotifiedKey + budget.Id, $"{window}|{(int)progress.Level}");
        }

        public async Task OfferAsync()
        {
            // Best-effort after a save, like the budget check
            try
            {
                await OfferOnceAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Reminders offer failed");
            }
        }

        private async Task OfferOnceAsync()
        {
            if (settings.Enabled || settings.Offered || !notifications.IsSupported)
                return;

            settings.Offered = true;
            if (await dialogs.ConfirmAsync(AppResources.RemindersOfferTitle, AppResources.RemindersOfferMessage,
                    AppResources.RemindersOfferAccept, AppResources.NotNow))
            {
                await EnableAsync();
            }
        }

        public async Task<bool> EnableAsync()
        {
            settings.Offered = true;
            var allowed = await notifications.RequestPermissionAsync();
            settings.Enabled = allowed;

            if (!allowed && await dialogs.ConfirmAsync(AppResources.NotificationsBlockedTitle,
                    AppResources.NotificationsBlockedMessage, AppResources.OpenSystemSettings, AppResources.Cancel))
            {
                notifications.OpenSystemSettings();
            }
            return allowed;
        }
    }
}
