using MoneyRecord.Resources.Strings;
using System.Globalization;

namespace MoneyRecord.Services
{
    public class LocalizationService
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public event EventHandler? LanguageChanged;

        private LocalizationService()
        {
            SetCultureFromSystem();
        }

        public void SetCultureFromSystem()
        {
            var systemCulture = CultureInfo.CurrentCulture;
            
            // Check if system language is Spanish
            if (systemCulture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase))
            {
                SetCulture("es");
            }
            else
            {
                // Default to English for all other languages
                SetCulture("en");
            }
        }

        public void SetCulture(string cultureCode)
        {
            var culture = new CultureInfo(cultureCode);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            AppResources.Culture = culture;
            
            LanguageChanged?.Invoke(this, EventArgs.Empty);
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
