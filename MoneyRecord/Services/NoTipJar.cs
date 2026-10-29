using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>Every build but the Google Play one (GitHub / F-Droid APK, iOS, Mac Catalyst, Windows): no store billing, so the Support screen is hidden.</summary>
    public sealed class NoTipJar : ITipJar
    {
        public bool IsAvailable => false;

        public Task<IReadOnlyList<TipOption>> GetTipsAsync() => Task.FromResult<IReadOnlyList<TipOption>>([]);

        public Task<TipOutcome> TipAsync(string productId) => Task.FromResult(TipOutcome.Failed);

        public Task ConsumePendingAsync() => Task.CompletedTask;
    }
}
