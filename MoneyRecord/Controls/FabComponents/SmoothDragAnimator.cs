namespace MoneyRecord.Controls.FabComponents
{
    /// <summary>
    /// Provides smooth interpolation for drag movements to eliminate flickering.
    /// Single Responsibility: Smooth position interpolation during drag operations.
    /// </summary>
    public sealed class SmoothDragAnimator
    {
        private double _targetX;
        private double _targetY;
        private double _currentX;
        private double _currentY;
        private bool _isAnimating;
        private readonly double _smoothingFactor;
        private readonly double _threshold;

        /// <summary>
        /// Creates a new smooth drag animator.
        /// </summary>
        /// <param name="smoothingFactor">Interpolation factor (0.0-1.0). Higher = faster response, lower = smoother.</param>
        /// <param name="threshold">Minimum distance threshold to trigger animation update.</param>
        public SmoothDragAnimator(double smoothingFactor = 0.3, double threshold = 0.5)
        {
            _smoothingFactor = Math.Clamp(smoothingFactor, 0.1, 1.0);
            _threshold = threshold;
        }

        public double CurrentX => _currentX;
        public double CurrentY => _currentY;
        public bool IsAnimating => _isAnimating;

        /// <summary>
        /// Initializes the animator with the starting position.
        /// </summary>
        public void Initialize(double x, double y)
        {
            _currentX = x;
            _currentY = y;
            _targetX = x;
            _targetY = y;
        }

        /// <summary>
        /// Sets the target position for smooth interpolation.
        /// </summary>
        public void SetTarget(double x, double y)
        {
            _targetX = x;
            _targetY = y;
        }

        /// <summary>
        /// Performs one step of smooth interpolation toward the target.
        /// Returns true if position changed significantly.
        /// </summary>
        public bool Step()
        {
            double deltaX = _targetX - _currentX;
            double deltaY = _targetY - _currentY;

            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            if (distance < _threshold)
            {
                _currentX = _targetX;
                _currentY = _targetY;
                return false;
            }

            // Lerp towards target
            _currentX += deltaX * _smoothingFactor;
            _currentY += deltaY * _smoothingFactor;

            return true;
        }

        /// <summary>
        /// Starts the smooth animation loop on the given view.
        /// </summary>
        public void StartAnimation(View targetView, Action<double, double> onPositionUpdate)
        {
            if (_isAnimating) return;
            _isAnimating = true;

            RunAnimationLoop(targetView, onPositionUpdate);
        }

        /// <summary>
        /// Stops the animation loop.
        /// </summary>
        public void StopAnimation()
        {
            _isAnimating = false;
        }

        /// <summary>
        /// Immediately snaps to target position (use when drag ends).
        /// </summary>
        public void SnapToTarget()
        {
            _currentX = _targetX;
            _currentY = _targetY;
        }

        private void RunAnimationLoop(View targetView, Action<double, double> onPositionUpdate)
        {
            if (!_isAnimating) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!_isAnimating) return;

                bool hasMovement = Step();
                onPositionUpdate(_currentX, _currentY);

                if (_isAnimating)
                {
                    // Use dispatcher timer for smooth 60fps-like updates
                    targetView.Dispatcher.DispatchDelayed(
                        TimeSpan.FromMilliseconds(16), // ~60fps
                        () => RunAnimationLoop(targetView, onPositionUpdate));
                }
            });
        }
    }
}
