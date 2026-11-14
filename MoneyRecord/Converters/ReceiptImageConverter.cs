using System.Globalization;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// A receipt photo's path as an image source. It reads through <see cref="IReceiptStore"/>, so an encrypted photo is
    /// decrypted in memory and never written back as a plain file.
    /// </summary>
    public class ReceiptImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string { Length: > 0 } path)
                return null;

            // A converter can't take constructor injection
            var store = IPlatformApplication.Current!.Services.GetRequiredService<IReceiptStore>();
            return new StreamImageSource { Stream = _ => store.OpenReadAsync(path) };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
