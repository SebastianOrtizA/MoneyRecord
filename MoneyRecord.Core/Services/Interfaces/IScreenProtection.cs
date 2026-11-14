namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Keeps the app's window out of screenshots, screen recordings and the recent apps screen. Android only for now.
    /// </summary>
    public interface IScreenProtection
    {
        bool IsSupported { get; }

        /// <summary>Secure on: the window can't be captured. The lock screen is always secure; the rest follows the setting.</summary>
        void SetSecure(bool secure);
    }
}
