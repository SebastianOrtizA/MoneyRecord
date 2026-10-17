namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Redraws the widgets on the home screen (Android); nothing elsewhere.</summary>
    public interface IHomeScreenWidgets
    {
        /// <summary>Best-effort: a failure is logged, never thrown.</summary>
        Task RefreshAsync();
    }
}
