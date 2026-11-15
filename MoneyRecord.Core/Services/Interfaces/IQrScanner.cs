namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Scans a QR code with the camera (Android: Google's code scanner, which needs Play services and no camera permission
    /// in this app). Elsewhere it isn't supported and the code is typed instead.
    /// </summary>
    public interface IQrScanner
    {
        bool IsSupported { get; }

        /// <summary>The text of the scanned code; null when cancelled; throws when the scanner can't run.</summary>
        Task<string?> ScanAsync();
    }
}
