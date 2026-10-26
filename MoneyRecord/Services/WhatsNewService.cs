using Microsoft.Extensions.Logging;
using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using System.Globalization;

namespace MoneyRecord.Services
{
    /// <summary>
    /// Shows the running version's release notes (the store changelog bundled as an asset, in the app's language) in an
    /// alert, once after each update.
    /// </summary>
    public sealed class WhatsNewService(WhatsNewTracker tracker, IDialogService dialogs, IErrorHandler errors,
        ILogger<WhatsNewService> logger) : IWhatsNewService
    {
        // Home appears again while the alert is up: one at a time
        private bool _showing;

        private static string Version => AppInfo.Current.VersionString;

        public void SkipForNewInstall() => tracker.MarkSeen(Version);

        public async Task ShowIfNewAsync()
        {
            if (_showing || !tracker.IsUnseen(Version))
                return;

            _showing = true;
            try
            {
                var notes = await ReadNotesAsync(tracker.LastSeen);
                // Recorded either way: a missing text must not come back at every start
                tracker.MarkSeen(Version);
                if (notes != null)
                    await ShowNotesAsync(notes);
            }
            finally
            {
                _showing = false;
            }
        }

        public async Task ShowAsync()
        {
            var notes = await ReadNotesAsync(string.Empty);
            if (notes == null)
                await errors.HandleAsync(AppResources.FailedToLoadWhatsNew);
            else
                await ShowNotesAsync(notes);
        }

        private Task ShowNotesAsync(string notes) =>
            dialogs.AlertAsync(string.Format(AppResources.WhatsNewTitle, Version), notes, AppResources.WhatsNewGotIt);

        /// <summary>
        /// The notes of the versions after <paramref name="lastSeen"/> (empty: just the running version's) in the app's
        /// language (the English ones when that has none); null when there are none.
        /// </summary>
        private async Task<string?> ReadNotesAsync(string lastSeen)
        {
            var locale = WhatsNewChangelog.Locale(AppResources.Culture ?? CultureInfo.CurrentUICulture);
            foreach (var candidate in locale == WhatsNewChangelog.Fallback ? [locale] : (string[])[locale, WhatsNewChangelog.Fallback])
            {
                try
                {
                    await using var stream = await FileSystem.Current.OpenAppPackageFileAsync(WhatsNewChangelog.AssetPath(candidate));
                    using var reader = new StreamReader(stream);
                    var notes = WhatsNewChangelog.Since(await reader.ReadToEndAsync(), lastSeen, Version);
                    if (notes != null)
                        return notes;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Release notes for {Locale} could not be read", candidate);
                }
            }
            return null;
        }
    }
}
