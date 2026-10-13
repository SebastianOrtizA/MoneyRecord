using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;

namespace MoneyRecord.Models
{
    /// <summary>A language the app can run in; an empty code follows the device.</summary>
    public sealed record LanguageOption(string Code, string Name)
    {
        /// <summary>The device default (named in the current language), then each supported language in its own.</summary>
        public static IReadOnlyList<LanguageOption> All() =>
        [
            new(string.Empty, AppResources.SystemDefault),
            .. LocalizationService.SupportedLanguages.Select(code => new LanguageOption(code, LocalizationService.GetLanguageName(code))),
        ];
    }
}
