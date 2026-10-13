using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>A starting account from the setup: created in the base currency.</summary>
    public sealed record SetupAccount(string Name, decimal InitialBalance, string IconCode);

    /// <summary>
    /// What the setup creates. With <see cref="ReplaceStartingData"/> (first run) the seeded accounts and categories are
    /// replaced by these; otherwise they are added, skipping categories that already exist.
    /// </summary>
    public sealed record SetupChoices(
        string CurrencyCode,
        IReadOnlyList<SetupAccount> Accounts,
        IReadOnlyList<DefaultCategory> Categories,
        bool ReplaceStartingData);

    /// <summary>The first-run setup: whether it is still to do, and creating its accounts and categories.</summary>
    public interface ISetupService
    {
        /// <summary>
        /// True until the first-run setup is finished. Call before anything opens the database: a database that
        /// already exists (an update from an older version) counts as set up.
        /// </summary>
        bool IsFirstRunPending();

        /// <summary>Creates the chosen accounts and categories in one transaction and marks the setup as done.</summary>
        Task ApplyAsync(SetupChoices choices);
    }
}
