namespace MoneyRecord.Controls
{
    /// <summary>A page that can show a sheet or overlay on top of itself; Android back closes it before leaving the page.</summary>
    public interface IOverlayHost
    {
        /// <summary>True when an overlay was open and is now closed.</summary>
        bool TryCloseOverlay();
    }
}
