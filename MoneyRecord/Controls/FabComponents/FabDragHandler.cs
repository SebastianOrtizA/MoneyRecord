using static MoneyRecord.Controls.FabComponents.FabConfiguration;

namespace MoneyRecord.Controls.FabComponents
{
    /// <summary>
    /// Handles drag gesture state for the FAB.
    /// Single Responsibility: Drag state tracking and velocity calculation.
    /// </summary>
    public sealed class FabDragHandler
    {
        private bool _isDragging;
        private bool _hasMoved;
        private DateTime _lastUpdateTime;
        private double _lastTotalX;
        private double _lastTotalY;
        private double _velocityX;
        private double _velocityY;

        public bool IsDragging => _isDragging;
        public bool HasMoved => _hasMoved;
        public double VelocityX => _velocityX;
        public double VelocityY => _velocityY;

        public void StartDrag()
        {
            _isDragging = true;
            _hasMoved = false;
            _lastUpdateTime = DateTime.UtcNow;
            _lastTotalX = 0;
            _lastTotalY = 0;
            _velocityX = 0;
            _velocityY = 0;
        }

        public void UpdateMovement(double totalX, double totalY)
        {
            if (Math.Abs(totalX) > DragThreshold || Math.Abs(totalY) > DragThreshold)
            {
                _hasMoved = true;
            }

            // Calculate velocity for smoother movement prediction
            var now = DateTime.UtcNow;
            var deltaTime = (now - _lastUpdateTime).TotalSeconds;

            if (deltaTime > 0.001) // Avoid division by zero
            {
                double deltaX = totalX - _lastTotalX;
                double deltaY = totalY - _lastTotalY;

                // Smooth velocity using exponential moving average
                const double smoothingFactor = 0.3;
                _velocityX = _velocityX * (1 - smoothingFactor) + (deltaX / deltaTime) * smoothingFactor;
                _velocityY = _velocityY * (1 - smoothingFactor) + (deltaY / deltaTime) * smoothingFactor;
            }

            _lastTotalX = totalX;
            _lastTotalY = totalY;
            _lastUpdateTime = now;
        }

        public void EndDrag()
        {
            _isDragging = false;
            _velocityX = 0;
            _velocityY = 0;
        }

        public async Task ResetMovedStateAsync()
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Task.Delay(PostDragDelayMs);
                _hasMoved = false;
            });
        }
    }
}
