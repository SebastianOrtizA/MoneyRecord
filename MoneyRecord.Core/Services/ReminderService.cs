using Microsoft.Extensions.Logging;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class ReminderService(
        IRecurringTransactionService recurring,
        IDebtService debts,
        IBudgetRepository budgets,
        ICategoryRepository categories,
        IConversionService conversion,
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
        public const int DebtDueIdBase = 300_000;
        public const int DebtPaidIdBase = 400_000;
        public const int PendingIdBase = 500_000;

        // The app's routes (see AppShell); Core can't name the page types
        public const string UpcomingRoute = "RecurringTransactionsPage?Show=Upcoming";
        public const string TransactionsRoute = "//TransactionsPage";
        public const string BudgetsRoute = "//BudgetPage";
        public const string DebtsRoute = "DebtsPage";

        /// <summary>The add form on a variable schedule's occurrence (see AddTransactionViewModel).</summary>
        public static string PendingRoute(int recurringId, DateTime occurrence) =>
            $"{BalanceWidget.AddTransactionRoute}?RecurringId={recurringId}&Occurrence={occurrence:yyyy-MM-dd}";

        // What was already notified: the last bill occurrence per recurrence, per budget its period + alert level, and per
        // debt its due date
        private const string BillNotifiedKey = "Reminders.Bill.";
        private const string BudgetNotifiedKey = "Reminders.Budget.";
        private const string DebtDueNotifiedKey = "Reminders.DebtDue.";
        private const string PendingNotifiedKey = "Reminders.Pending.";

        private bool CanNotify => settings.Enabled && notifications.IsSupported && notifications.AreAllowed;

        public async Task RunDailyAsync()
        {
            // Before processing: today's occurrences are still upcoming until they are created
            if (CanNotify && settings.BillDaysBefore != ReminderSettings.BillsOff)
                await NotifyBillsAsync();

            var run = await ProcessRecurringAsync();
            if (CanNotify)
                await NotifyPendingAsync();
            if (run.Created > 0 && CanNotify && settings.RecurringAdded)
            {
                notifications.Show(new AppNotification(RecurringAddedId, ReminderChannel.Bills,
                    AppResources.RecurringTransactions, Plurals.RecurringAdded(run.Created), TransactionsRoute));
            }

            if (CanNotify && settings.BillDaysBefore != ReminderSettings.BillsOff)
                await NotifyDebtsDueAsync();
            await CheckBudgetsCoreAsync();
        }

        public async Task<RecurringRun> ProcessRecurringAsync()
        {
            var run = await recurring.ProcessDueTransactionsAsync();
            if (run.PaidOffDebtIds.Count > 0 && CanNotify)
            {
                // Best-effort: the payments are saved already
                try
                {
                    var paidOff = (await debts.GetAllAsync()).Where(d => run.PaidOffDebtIds.Contains(d.DebtId));
                    foreach (var debt in paidOff)
                    {
                        notifications.Show(new AppNotification(DebtPaidIdBase + debt.DebtId, ReminderChannel.Bills,
                            string.Format(debt.Direction == DebtDirection.Lent ? AppResources.DebtPaidOffLentTitle : AppResources.DebtPaidOffBorrowedTitle,
                                debt.PersonName),
                            debt.PaidOfTotalText, DebtsRoute));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Debt paid-off notification failed");
                }
            }
            return run;
        }

        /// <summary>
        /// A variable-amount schedule with occurrences waiting to be entered: one notification per schedule, when it has a
        /// pending date not notified yet. It opens the add form on the oldest one.
        /// </summary>
        private async Task NotifyPendingAsync()
        {
            foreach (var pending in (await recurring.GetPendingAsync()).GroupBy(e => e.RecurringId))
            {
                var oldest = pending.First();
                if (pending.Max(e => e.Date) <= preferences.Get(PendingNotifiedKey + pending.Key, DateTime.MinValue))
                    continue;

                var parts = new[] { oldest.EstimateText, oldest.AccountName };
                notifications.Show(new AppNotification(PendingIdBase + pending.Key, ReminderChannel.Bills,
                    string.Format(AppResources.PendingEntryTitle, oldest.Description),
                    string.Join(" · ", parts.Where(p => p.Length > 0)), PendingRoute(pending.Key, oldest.Date)));
                preferences.Set(PendingNotifiedKey + pending.Key, pending.Max(e => e.Date));
            }
        }

        private async Task NotifyDebtsDueAsync()
        {
            var today = clock.GetLocalNow().DateTime.Date;
            foreach (var debt in await debts.GetAllAsync())
            {
                // An open debt whose due date is within the chosen days, once per due date
                if (debt.IsPaidOff || debt.Debt.DueDate is not { } due || due.Date < today
                    || (due.Date - today).Days > settings.BillDaysBefore
                    || preferences.Get(DebtDueNotifiedKey + debt.DebtId, DateTime.MinValue) == due.Date)
                    continue;

                var days = (due.Date - today).Days;
                var when = days switch { 0 => AppResources.Today, 1 => AppResources.Tomorrow, _ => Plurals.InDays(days) };
                notifications.Show(new AppNotification(DebtDueIdBase + debt.DebtId, ReminderChannel.Bills,
                    string.Format(debt.Direction == DebtDirection.Lent ? AppResources.DebtDueLentTitle : AppResources.DebtDueBorrowedTitle,
                        debt.PersonName),
                    $"{when} · {debt.OutstandingText}", DebtsRoute));
                preferences.Set(DebtDueNotifiedKey + debt.DebtId, due.Date);
            }
        }

        private async Task NotifyBillsAsync()
        {
            var today = clock.GetLocalNow().DateTime.Date;
            var due = (await recurring.GetUpcomingAsync(settings.BillDaysBefore))
                .Where(e => e.Type == TransactionType.Expense && e.Date >= today && !e.IsPending);

            // The soonest occurrence of each recurrence not notified yet (a daily one gets one reminder a day)
            var bills = due.GroupBy(e => e.RecurringId)
                .Select(g => g.FirstOrDefault(e => e.Date > preferences.Get(BillNotifiedKey + g.Key, DateTime.MinValue)))
                .OfType<UpcomingEntry>();

            foreach (var bill in bills)
            {
                var parts = new[] { bill.WhenText, bill.IsVariable ? AppResources.AmountVaries : MoneyFormatter.Format(bill.Amount, bill.CurrencyCode), bill.AccountName };
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
                var spentByCategory = await conversion.GetExpensesByCategoryAsync(start, end);

                foreach (var budget in period)
                {
                    var progress = new BudgetProgress
                    {
                        CategoryName = names.GetValueOrDefault(budget.CategoryId, string.Empty),
                        LimitAmount = budget.LimitAmount
                    };
                    progress.SetSpent(spentByCategory.GetValueOrDefault(budget.CategoryId) ?? BudgetSpent.None);
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
