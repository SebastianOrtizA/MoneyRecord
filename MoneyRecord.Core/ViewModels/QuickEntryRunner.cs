using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// The one-tap save of a quick entry. It keeps the checks the add form makes (an account that can't go negative, money set
    /// aside in pockets, budget alerts), saves the transaction dated now, and offers Undo, which deletes it again.
    /// </summary>
    public sealed class QuickEntryRunner(IQuickEntryService quickEntries, IAccountRepository accounts, IBalanceService balances,
        PocketCoverPrompt pocketCover, IReminderService reminders, ILedgerService ledger, IDialogService dialogs,
        IErrorHandler errors, TimeProvider clock)
    {
        /// <param name="amountCents">The amount typed for an entry that asks for one; null = the stored amount.</param>
        /// <param name="changed">Runs after the transaction is saved and after an Undo (Home reloads).</param>
        /// <returns>True when the transaction was saved.</returns>
        public async Task<bool> RunAsync(QuickEntryItem item, long? amountCents, Func<Task> changed)
        {
            var cents = amountCents ?? item.Entry.AmountCents;
            if (item.IsBroken || cents <= 0)
                return false;

            try
            {
                var account = await accounts.GetByIdAsync(item.Entry.AccountId);
                if (account == null)
                    return false;

                var amount = Helpers.Money.FromCents(cents);
                var cover = PocketCoverChoice.KeepAsIs;
                if (item.Entry.Type == TransactionType.Expense)
                {
                    if (!account.AllowNegativeBalance && await balances.GetAccountBalanceAsync(account.Id) - amount < 0)
                    {
                        await errors.HandleAsync(string.Format(AppResources.InsufficientAccountBalance, account.Name));
                        return false;
                    }

                    cover = await pocketCover.AskAsync(account.Id, amount, clock.GetLocalNow().DateTime);
                    if (!cover.Proceed)
                        return false;
                }

                var saved = await quickEntries.UseAsync(item.Id, cents);
                await pocketCover.RecordAsync(account.Id, cover, saved.Date, transactionId: saved.Id);
                if (saved.Type == TransactionType.Expense)
                    await reminders.CheckBudgetsAsync();

                await changed();
                var kind = saved.Type == TransactionType.Income ? LedgerEntryKind.Income : LedgerEntryKind.Expense;
                await dialogs.ToastWithUndoAsync(
                    string.Format(AppResources.QuickEntrySaved, item.Name, Helpers.MoneyFormatter.Format(amount, item.CurrencyCode)),
                    () => UndoAsync(saved.Id, kind, changed),
                    () => Task.CompletedTask);
                return true;
            }
            catch (Exception ex)
            {
                await errors.HandleAsync(ex, AppResources.FailedToSaveQuickEntry);
                return false;
            }
        }

        private async Task UndoAsync(int transactionId, LedgerEntryKind kind, Func<Task> changed)
        {
            try
            {
                if (await ledger.DeleteAsync(new LedgerEntry { Kind = kind, SourceId = transactionId }) is { } deleted)
                    ledger.Discard(deleted);
                await changed();
            }
            catch (Exception ex)
            {
                await errors.HandleAsync(ex, AppResources.FailedToSaveQuickEntry);
            }
        }
    }
}
