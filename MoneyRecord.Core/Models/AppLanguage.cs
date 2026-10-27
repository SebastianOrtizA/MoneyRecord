using System.Globalization;

namespace MoneyRecord.Models
{
    /// <summary>
    /// A language the app is translated to. The one place a language is declared: the language picker, the device-language
    /// detection, the culture a bare code resolves to, the store-changelog locale and the plural rule all read <see cref="All"/>.
    /// A new language is one row here plus its <c>AppResources.xx.resx</c>, widget strings and <c>fastlane</c> folder
    /// (tests fail until all three exist).
    /// </summary>
    /// <param name="Code">Two-letter code ("pt"): the saved language setting and the resource suffix.</param>
    /// <param name="DefaultCulture">The culture used when the device has no region of this language, and the store locale ("pt-BR").</param>
    /// <param name="ZeroIsSingular">"0 transaction" (French) rather than "0 transactions".</param>
    public sealed record AppLanguage(string Code, string DefaultCulture, bool ZeroIsSingular = false)
    {
        /// <summary>English first: it is also the fallback for every other device language.</summary>
        public static IReadOnlyList<AppLanguage> All { get; } =
        [
            new("en", "en-US"),
            new("es", "es-ES"),
            new("pt", "pt-BR"),
            new("fr", "fr-FR", ZeroIsSingular: true),
            new("it", "it-IT"),
        ];

        public static AppLanguage English => All[0];

        /// <summary>The language of a code or culture name ("es", "es-CO"); null when the app has none.</summary>
        public static AppLanguage? Find(string? code) =>
            string.IsNullOrEmpty(code) ? null : All.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

        /// <summary>The app language a culture speaks; English when it has no translation.</summary>
        public static AppLanguage For(CultureInfo culture) => Find(culture.TwoLetterISOLanguageName) ?? English;
    }
}
