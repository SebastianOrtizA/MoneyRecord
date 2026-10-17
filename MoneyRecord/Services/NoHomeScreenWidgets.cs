using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>Platforms without the home-screen widget (iOS, Mac Catalyst, Windows).</summary>
    public sealed class NoHomeScreenWidgets : IHomeScreenWidgets
    {
        public void Refresh()
        {
        }
    }
}
