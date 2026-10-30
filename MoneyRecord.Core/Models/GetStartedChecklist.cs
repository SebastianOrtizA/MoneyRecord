using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    public enum GetStartedStep
    {
        AddEntry,
        Backup,
        AppLock,
        Reminders
    }

    public sealed record GetStartedRow(GetStartedStep Step, bool Done)
    {
        public string Title => Step switch
        {
            GetStartedStep.AddEntry => AppResources.GetStartedAddEntry,
            GetStartedStep.Backup => AppResources.GetStartedBackup,
            GetStartedStep.AppLock => AppResources.GetStartedAppLock,
            _ => AppResources.GetStartedReminders
        };

        /// <summary>Material Design Icons: a ticked circle, or an empty one.</summary>
        public string DoneIcon => IconHelper.GetDisplayIcon(Done ? "F05E0" : "F0130", IconHelper.DefaultCategoryIconCode);
    }

    /// <summary>What the first-run checklist looks at: what the user has already done, and whether the phone can lock the app.</summary>
    public sealed record GetStartedState(bool HasTransactions, bool BackedUp, bool CanLock, bool LockOn, bool RemindersOn);

    /// <summary>Home's "Get started" card: the first things worth doing, each ticked once done.</summary>
    public sealed record GetStartedChecklist(IReadOnlyList<GetStartedRow> Rows)
    {
        public bool IsComplete => Rows.All(r => r.Done);

        public static GetStartedChecklist Build(GetStartedState state)
        {
            var rows = new List<GetStartedRow>
            {
                new(GetStartedStep.AddEntry, state.HasTransactions),
                new(GetStartedStep.Backup, state.BackedUp)
            };
            // Without a phone lock the app lock can't work (IDeviceLock), so it isn't asked for
            if (state.CanLock)
                rows.Add(new(GetStartedStep.AppLock, state.LockOn));
            rows.Add(new(GetStartedStep.Reminders, state.RemindersOn));
            return new GetStartedChecklist(rows);
        }
    }
}
