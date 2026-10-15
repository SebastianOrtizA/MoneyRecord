namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Keeps the database key where only this app on this device can use it (Android: SecureStorage, whose own key
    /// is in the Android Keystore). It is never backed up, so an encrypted database can't be opened on another phone.
    /// </summary>
    public interface IDatabaseKeyStore
    {
        bool IsSupported { get; }

        Task<string?> GetAsync();

        Task SetAsync(string key);

        void Remove();
    }
}
