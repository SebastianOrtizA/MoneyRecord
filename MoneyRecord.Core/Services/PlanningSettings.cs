using MoneyRecord.Helpers;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class PlanningSettings(IPreferencesService preferences) : IPlanningSettings
    {
        private const string SafeToSpendKey = "Planning.SafeToSpendUntil";

        public SafeToSpendHorizon SafeToSpendUntil
        {
            get => Enum.TryParse<SafeToSpendHorizon>(preferences.Get(SafeToSpendKey, string.Empty), out var value) && Enum.IsDefined(value)
                ? value
                : SafeToSpendHorizon.NextIncome;
            set => preferences.Set(SafeToSpendKey, value.ToString());
        }
    }
}
