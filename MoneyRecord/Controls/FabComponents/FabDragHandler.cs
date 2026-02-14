using static MoneyRecord.Controls.FabComponents.FabConfiguration;

namespace MoneyRecord.Controls.FabComponents
{
    /// <summary>
    /// Handles drag gesture state for the FAB.
    /// Single Responsibility: Drag state tracking.
    /// </summary>
    public sealed class FabDragHandler
    {
        private bool _isDragging;
        private bool _hasMoved;

        public bool IsDragging => _isDragging;
        public bool HasMoved => _hasMoved;

        public void StartDrag()
        {
            _isDragging = true;
            _hasMoved = false;
        }

        public void UpdateMovement(double totalX, double totalY)
        {
            if (Math.Abs(totalX) > DragThreshold || Math.Abs(totalY) > DragThreshold)
            {
                _hasMoved = true;
            }
        }

        public void EndDrag()
        {
            _isDragging = false;
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
