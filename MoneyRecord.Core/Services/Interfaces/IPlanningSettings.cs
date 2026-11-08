using MoneyRecord.Helpers;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Planning choices saved in the preferences.</summary>
    public interface IPlanningSettings
    {
        /// <summary>How far "safe to spend" looks; next income by default.</summary>
        SafeToSpendHorizon SafeToSpendUntil { get; set; }
    }
}
