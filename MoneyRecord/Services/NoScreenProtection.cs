using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>Platforms that cannot keep the window out of screenshots (iOS, Mac Catalyst, Windows): Settings hides the switch.</summary>
    public sealed class NoScreenProtection : IScreenProtection
    {
        public bool IsSupported => false;

        public void SetSecure(bool secure)
        {
        }
    }
}
