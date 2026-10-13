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
        /// True until the first-run setup is finished. The first call decides it and saves the answer, so call it before
        /// anything opens the database: a database that already exists then (an update from an older version) counts
        /// as set up.
        /// </summary>
        bool ResolveFirstRunPending();

        /// <summary>Creates the chosen accounts and categories in one transaction and marks the setup as done.</summary>
        Task ApplyAsync(SetupChoices choices);
    }
}
