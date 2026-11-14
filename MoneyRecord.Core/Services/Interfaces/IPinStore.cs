namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Where the PIN hash and the lockout state live: secure storage (Android only for now), kept out of backups like the
    /// database key. A restore on a new phone needs the PIN set again.
    /// </summary>
    public interface IPinStore
    {
        bool IsSupported { get; }

        Task<string?> GetHashAsync();

        /// <summary>Saves the hash; null removes it.</summary>
        Task SetHashAsync(string? hash);

        Task<string?> GetLockoutAsync();

        /// <summary>Saves the lockout state; null clears it.</summary>
        Task SetLockoutAsync(string? state);
    }
}
