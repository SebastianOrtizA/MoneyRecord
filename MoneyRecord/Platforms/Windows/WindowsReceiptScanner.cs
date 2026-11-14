using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace MoneyRecord.Platforms.Windows
{
    /// <summary>
    /// Receipt text on Windows: the system's OCR (Windows.Media.Ocr), in the languages of the user's profile, on the PC.
    /// Not supported (the add form only attaches the photo) when no OCR language is installed.
    /// </summary>
    public sealed class WindowsReceiptScanner(IReceiptStore receipts) : IReceiptScanner
    {
        private readonly Lazy<OcrEngine?> _engine = new(OcrEngine.TryCreateFromUserProfileLanguages);

        public bool IsSupported => _engine.Value != null;

        public async Task<IReadOnlyList<ReceiptLine>> ReadLinesAsync(string imagePath)
        {
            if (_engine.Value is not { } engine)
                return [];

            // An encrypted photo is read from a decrypted copy that goes right after
            var readable = await receipts.GetReadablePathAsync(imagePath);
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(readable);
                using var stream = await file.OpenReadAsync();
                var decoder = await BitmapDecoder.CreateAsync(stream);

                // The OCR refuses images beyond its limit: shrink a big photo (the lines come back in the shrunk pixels,
                // which is all ReceiptReader needs: it compares positions)
                var transform = new BitmapTransform();
                var longest = Math.Max(decoder.PixelWidth, decoder.PixelHeight);
                if (longest > OcrEngine.MaxImageDimension)
                {
                    var scale = (double)OcrEngine.MaxImageDimension / longest;
                    transform.ScaledWidth = (uint)(decoder.PixelWidth * scale);
                    transform.ScaledHeight = (uint)(decoder.PixelHeight * scale);
                }

                using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                    transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
                var result = await engine.RecognizeAsync(bitmap);

                return result.Lines
                    .Where(line => line.Words.Count > 0)
                    .Select(line => new ReceiptLine(line.Text,
                        (int)line.Words.Min(w => w.BoundingRect.Left),
                        (int)line.Words.Min(w => w.BoundingRect.Top),
                        (int)line.Words.Max(w => w.BoundingRect.Right),
                        (int)line.Words.Max(w => w.BoundingRect.Bottom)))
                    .ToList();
            }
            finally
            {
                if (readable != imagePath)
                    File.Delete(readable);
            }
        }
    }
}
