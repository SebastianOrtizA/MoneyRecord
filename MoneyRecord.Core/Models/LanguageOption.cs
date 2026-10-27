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
            .. AppLanguage.All.Select(language => new LanguageOption(language.Code, LocalizationService.GetLanguageName(language.Code))),
        ];
    }
}
