using MoneyRecord.Resources.Strings;
using System.Globalization;

namespace MoneyRecord.Services
{
    public class LocalizationService
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        // Captured before SetCulture overwrites CurrentCulture, so the device region is never lost
        // (e.g. a fr-FR device runs the app in en-US but its currency is still EUR).
        public static readonly CultureInfo DeviceCulture = CultureInfo.CurrentCulture;

        public event EventHandler? LanguageChanged;

        private LocalizationService()
        {
            SetCultureFromSystem();
        }

        public void SetCultureFromSystem()
        {
            // Spanish devices get Spanish; every other language defaults to English
            var language = DeviceCulture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase)
                ? "es"
                : "en";
            SetCulture(language);
        }

        public void SetCulture(string cultureCode)
        {
            var culture = ResolveCulture(cultureCode, DeviceCulture);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            AppResources.Culture = culture;
            
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Turns a language code into a specific culture so currency and number formats keep working:
        /// "es" on an es-CO device → es-CO (never neutral "es", whose currency symbol is "¤").
        /// Falls back to en-US / es-ES when the device language differs or has no region.
        /// </summary>
        public static CultureInfo ResolveCulture(string cultureCode, CultureInfo deviceCulture)
        {
            var requested = new CultureInfo(cultureCode);
            if (!requested.IsNeutralCulture)
                return requested;

            if (!deviceCulture.IsNeutralCulture
                && !string.IsNullOrEmpty(deviceCulture.Name)
                && deviceCulture.TwoLetterISOLanguageName.Equals(requested.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
            {
                return deviceCulture;
            }

            return requested.TwoLetterISOLanguageName switch
            {
                "es" => new CultureInfo("es-ES"),
                _ => new CultureInfo("en-US")
            };
        }

        public string GetString(string key)
        {
            return AppResources.ResourceManager.GetString(key, AppResources.Culture) ?? key;
        }

        public string GetString(string key, params object[] args)
        {
            var format = GetString(key);
            return string.Format(format, args);
        }

        public string CurrentLanguage => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        // Commonly used localized strings
        public string Cancel => AppResources.Cancel;
        public string Save => AppResources.Save;
        public string Delete => AppResources.Delete;
        public string Edit => AppResources.Edit;
        public string OK => AppResources.OK;
        public string Error => AppResources.Error;
        public string Yes => AppResources.Yes;
        public string No => AppResources.No;
    }
}
