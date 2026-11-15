using System.Globalization;
using QRCoder;

namespace MoneyRecord.Converters
{
    /// <summary>Draws a text (the sync pairing code) as a QR code image; no text, no image.</summary>
    public sealed class QrCodeImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string { Length: > 0 } text)
                return null;
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.L);
            var png = new PngByteQRCode(data).GetGraphic(8);
            return ImageSource.FromStream(() => new MemoryStream(png));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
