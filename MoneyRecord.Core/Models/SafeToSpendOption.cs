using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>A choice for how far "safe to spend" looks, named in the current language.</summary>
    public sealed record SafeToSpendOption(SafeToSpendHorizon Horizon, string Name)
    {
        public static IReadOnlyList<SafeToSpendOption> All() =>
        [
            new(SafeToSpendHorizon.NextIncome, AppResources.SafeToSpendNextIncome),
            new(SafeToSpendHorizon.EndOfMonth, AppResources.SafeToSpendEndOfMonth)
        ];
    }
}
