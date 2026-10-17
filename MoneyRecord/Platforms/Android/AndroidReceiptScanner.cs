using Android.Gms.Extensions;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;
using Application = Android.App.Application;
using MLText = Xamarin.Google.MLKit.Vision.Text.Text;

namespace MoneyRecord.Platforms.Android
{
    /// <summary>
    /// Receipt text on Android: ML Kit's Latin-script text recognition, with its model inside the APK, so it works offline
    /// and without Play Services. The photo never leaves the phone.
    /// </summary>
    public sealed class AndroidReceiptScanner : IReceiptScanner
    {
        public bool IsSupported => true;

        public async Task<IReadOnlyList<ReceiptLine>> ReadLinesAsync(string imagePath)
        {
            // From the file: ML Kit turns the photo by its EXIF orientation
            using var file = new Java.IO.File(imagePath);
            var image = InputImage.FromFilePath(Application.Context, global::Android.Net.Uri.FromFile(file)!);
            using var recognizer = TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions);
            var text = (MLText)await recognizer.Process(image).AsAsync<MLText>();

            return text.TextBlocks
                .SelectMany(block => block.Lines)
                .Where(line => line.BoundingBox != null)
                .Select(line => new ReceiptLine(line.Text, line.BoundingBox!.Left, line.BoundingBox.Top, line.BoundingBox.Right,
                    line.BoundingBox.Bottom))
                .ToList();
        }
    }
}
