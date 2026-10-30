using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// The Undo offer after deleting a transaction, split or transfer: Undo puts it back (then <c>reload</c>), letting the offer
    /// go away makes the delete final (the receipt photos are removed).
    /// </summary>
    public static class DeleteUndo
    {
        public static Task OfferAsync(IDialogService dialog, ILedgerService ledger, IErrorHandler errors, DeletedEntry deleted,
            string message, Func<Task> reload) =>
            dialog.ToastWithUndoAsync(message, () => UndoAsync(dialog, ledger, errors, deleted, reload), () =>
            {
                ledger.Discard(deleted);
                return Task.CompletedTask;
            });

        private static async Task UndoAsync(IDialogService dialog, ILedgerService ledger, IErrorHandler errors, DeletedEntry deleted,
            Func<Task> reload)
        {
            try
            {
                if (await ledger.RestoreAsync(deleted))
                {
                    await reload();
                    return;
                }

                ledger.Discard(deleted);
                await dialog.ToastAsync(AppResources.UndoNotPossible);
            }
            catch (Exception ex)
            {
                await errors.HandleAsync(ex, AppResources.UndoNotPossible);
            }
        }
    }
}
