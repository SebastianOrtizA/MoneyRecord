using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Reads the text on a receipt photo, on the phone (Android: ML Kit text recognition). Elsewhere it isn't supported
    /// and the add form only attaches the photo. <see cref="Helpers.ReceiptReader"/> finds the total and date in it.
    /// </summary>
    public interface IReceiptScanner
    {
        bool IsSupported { get; }

        /// <summary>The photo's lines of text with their boxes; empty when it has none or can't be read.</summary>
        Task<IReadOnlyList<ReceiptLine>> ReadLinesAsync(string imagePath);
    }
}
