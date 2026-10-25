using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// What the user answered when an entry would use money set aside in pockets: <see cref="Proceed"/> false = cancel (save
    /// nothing); otherwise save, then record <see cref="Takes"/> as the entry's take-outs (none = keep the pockets as they are).
    /// </summary>
    public sealed record PocketCoverChoice(bool Proceed, IReadOnlyList<PocketTake> Takes)
    {
        public static PocketCoverChoice Cancelled { get; } = new(false, []);

        public static PocketCoverChoice KeepAsIs { get; } = new(true, []);
    }

    /// <summary>
    /// The question the expense and transfer forms ask before saving an entry that leaves its account with less than its
    /// pockets hold: which pocket(s) to take the difference from, or keep the pockets as they are (the account's Available
    /// then goes negative). Shared by both forms.
    /// </summary>
    public sealed class PocketCoverPrompt(IDialogService dialogs, IPocketService pockets, IErrorHandler errors)
    {
        /// <summary>
        /// What an entry takes out of its account on top of what it took already: a new entry's amount; for an edit on the same
        /// account, the new amount minus the original one (only when the original is counted in the balance, i.e. not dated
        /// in the future).
        /// </summary>
        public static decimal ExtraOutflow(decimal newAmount, decimal originalAmount, DateTime originalDate, DateTime countedBefore,
            bool sameAccount) =>
            sameAccount && originalDate < countedBefore ? newAmount - originalAmount : newAmount;

        /// <param name="extraOutflow">
        /// What the entry takes out of the account on top of what it took already (a new entry's amount; for an edit, the
        /// new amount minus the original one). Zero or less never asks.
        /// </param>
        /// <param name="date">The entry's date: a future-dated entry is in no balance yet, so it never asks.</param>
        public async Task<PocketCoverChoice> AskAsync(int accountId, decimal extraOutflow, DateTime date)
        {
            if (extraOutflow <= 0)
                return PocketCoverChoice.KeepAsIs;

            // Only the part this entry adds: pockets that were already over the balance are not its doing
            var need = Math.Min(extraOutflow, await pockets.GetShortfallAsync(accountId, extraOutflow, date));
            if (need <= 0)
                return PocketCoverChoice.KeepAsIs;

            var left = (await pockets.GetPocketsAsync(accountId)).Where(p => p.Balance > 0).ToList();
            var takes = new List<PocketTake>();
            while (need > 0 && left.Count > 0)
            {
                var labels = PickList.UniqueLabels(left.Select(p => $"{p.Pocket.Name} ({p.BalanceText})"));
                var keep = AppResources.PocketCoverKeep;
                var choice = await dialogs.ActionSheetAsync(
                    string.Format(AppResources.PocketCoverTitle, MoneyFormatter.Format(need, left[0].CurrencyCode)),
                    AppResources.Cancel, [.. labels, keep]);

                if (choice == null)
                    return PocketCoverChoice.Cancelled;
                if (choice == keep)
                    break;

                var index = labels.IndexOf(choice);
                if (index < 0)
                    return PocketCoverChoice.Cancelled;

                var (take, stillShort) = PocketCover.Plan(need, left[index].Balance);
                takes.Add(new PocketTake(left[index].PocketId, take));
                left.RemoveAt(index);
                need = stillShort;
            }

            return new PocketCoverChoice(true, takes);
        }

        /// <summary>
        /// Records the take-outs of a saved entry, linked to it. Best-effort: the entry is saved already, so a failure is
        /// reported (the pockets stay as they were) and nothing is thrown.
        /// </summary>
        public async Task RecordAsync(int accountId, PocketCoverChoice choice, DateTime date, int? transactionId = null,
            int? transferId = null)
        {
            if (choice.Takes.Count == 0)
                return;

            try
            {
                await pockets.CoverAsync(accountId, choice.Takes, date, transactionId, transferId);
            }
            catch (Exception ex)
            {
                await errors.HandleAsync(ex, AppResources.FailedToMovePocket);
            }
        }
    }
}
