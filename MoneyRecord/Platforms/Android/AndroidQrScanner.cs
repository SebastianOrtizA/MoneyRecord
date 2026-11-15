using Android.Gms.Extensions;
using MoneyRecord.Services.Interfaces;
using Xamarin.Google.MLKit.Vision.Barcode.Common;
using Xamarin.Google.MLKit.Vision.CodeScanner;
using Application = Android.App.Application;

namespace MoneyRecord.Platforms.Android
{
    /// <summary>
    /// QR scanning on Android with Google's code scanner: Play services shows the camera screen, so the app needs no camera
    /// permission and never sees the camera. Without Play services (or while it installs the module) scanning fails and the
    /// code is typed instead.
    /// </summary>
    public sealed class AndroidQrScanner : IQrScanner
    {
        public bool IsSupported => true;

        public async Task<string?> ScanAsync()
        {
            try
            {
                var options = new GmsBarcodeScannerOptions.Builder()
                    .SetBarcodeFormats(Barcode.FormatQrCode)!
                    .Build();
                var scanner = GmsBarcodeScanning.GetClient(Application.Context, options);
                var barcode = (Barcode?)await scanner.StartScan().AsAsync<Barcode>();
                return barcode?.RawValue;
            }
            catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
            {
                return null; // the user closed the scanner
            }
            catch (Java.Lang.Exception ex)
            {
                // Play services can't scan (not installed, module still downloading): the caller says so
                throw new InvalidOperationException("The code scanner isn't available.", ex);
            }
        }
    }
}
