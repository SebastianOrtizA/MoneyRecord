using MoneyRecord.Services;
using static MoneyRecord.Controls.FabComponents.FabConfiguration;

namespace MoneyRecord.Controls.FabComponents
{
    /// <summary>
    /// Manages FAB position calculations and persistence.
    /// Single Responsibility: Position state management.
    /// </summary>
    public sealed class FabPositionManager
    {
        private readonly IPreferencesService _preferences;
        private double _fabYPercent = -1;
        private double _startFabYPercent;
        private bool _isOnRight = true;

        public FabPositionManager(IPreferencesService preferences)
        {
            _preferences = preferences;
            LoadSavedPosition();
        }

        public double FabYPercent
        {
            get => _fabYPercent;
            set => _fabYPercent = value;
        }

        public double StartFabYPercent
        {
            get => _startFabYPercent;
            set => _startFabYPercent = value;
        }

        public bool IsOnRight
        {
            get => _isOnRight;
            set => _isOnRight = value;
        }

        public bool IsInitialized => _fabYPercent >= 0;

        public double CalculateMinYPercent() => TopMarginPercent;

        public double CalculateMaxYPercent(double gridHeight)
        {
            if (gridHeight <= 0)
                return 0.95;

            return 1.0 - BottomMarginPercent - (FabButtonSize / gridHeight);
        }

        public void InitializePositionIfNeeded(double minYPercent, double maxYPercent)
        {
            if (!IsInitialized || _fabYPercent < minYPercent || _fabYPercent > maxYPercent)
            {
                _fabYPercent = Math.Clamp(DefaultYPercent, minYPercent, maxYPercent);
            }
        }

        public double CalculateNewYPercent(double totalDeltaY, double gridHeight)
        {
            double deltaYPercent = totalDeltaY / gridHeight;
            return _startFabYPercent + deltaYPercent;
        }

        public void ClampPosition(double minYPercent, double maxYPercent)
        {
            _fabYPercent = Math.Clamp(_fabYPercent, minYPercent, maxYPercent);
        }

        public bool ShouldSwitchSide(double totalDeltaX, double gridWidth)
        {
            double currentFabX = _isOnRight
                ? gridWidth - (gridWidth * EdgeMarginPercent) - FabButtonSize / 2
                : (gridWidth * EdgeMarginPercent) + FabButtonSize / 2;

            double newFabCenterX = currentFabX + totalDeltaX;
            return newFabCenterX > gridWidth / 2;
        }

        public (double X, double Y) CalculateContainerPosition(double gridWidth, double gridHeight)
        {
            double fabTopY = _fabYPercent * gridHeight;
            double fabLeftX = _isOnRight
                ? gridWidth - FabButtonSize - (gridWidth * EdgeMarginPercent)
                : gridWidth * EdgeMarginPercent;

            double containerX = _isOnRight
                ? fabLeftX - (ContainerWidth - FabButtonSize)
                : fabLeftX;
            double containerY = fabTopY - (ContainerHeight - FabButtonSize);

            return (containerX, containerY);
        }

        public void SavePosition()
        {
            _preferences.Set(PositionYPercentKey, _fabYPercent);
            _preferences.Set(IsOnRightKey, _isOnRight);
        }

        private void LoadSavedPosition()
        {
            _isOnRight = _preferences.Get(IsOnRightKey, true);
            double savedYPercent = _preferences.Get(PositionYPercentKey, -1d);
            if (savedYPercent >= 0)
            {
                _fabYPercent = savedYPercent;
            }
        }
    }
}
