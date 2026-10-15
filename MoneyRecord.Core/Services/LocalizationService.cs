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

        public const string LanguagePreferenceKey = "Language";

        /// <summary>The languages the app is translated to; the language setting is one of these or empty (the device's).</summary>
        public static IReadOnlyList<string> SupportedLanguages { get; } = ["en", "es"];

        /// <summary>A language's name in that language ("English", "Español"), for a language picker.</summary>
        public static string GetLanguageName(string code)
        {
            var culture = CultureInfo.GetCultureInfo(code);
            return culture.TextInfo.ToUpper(culture.NativeName[0]) + culture.NativeName[1..];
        }

        /// <summary>The chosen language ("en", "es"), or empty to follow the device.</summary>
        public string LanguageSetting { get; private set; } = string.Empty;

        private LocalizationService()
        {
            SetCultureFromSystem();
        }

        /// <summary>Applies the saved language. Called once at startup, before any page is created.</summary>
        public void LoadLanguage(IPreferencesService preferences) =>
            ApplyLanguage(preferences.Get(LanguagePreferenceKey, string.Empty));

        /// <summary>Saves and applies a language ("en", "es", or empty for the device's).</summary>
        public void SetLanguage(string language, IPreferencesService preferences)
        {
            preferences.Set(LanguagePreferenceKey, language);
            ApplyLanguage(language);
        }

        private void ApplyLanguage(string language)
        {
            LanguageSetting = SupportedLanguages.Contains(language) ? language : string.Empty;
            if (LanguageSetting.Length == 0)
                SetCultureFromSystem();
            else
                SetCulture(LanguageSetting);
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
            // Threads started later too (the daily background job): amounts and dates in the app's language there as well
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
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
