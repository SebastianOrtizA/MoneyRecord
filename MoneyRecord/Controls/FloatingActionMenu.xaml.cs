using MoneyRecord.ViewModels;

namespace MoneyRecord.Controls
{
    public partial class FloatingActionMenu : ContentView
    {
        // Percentage-based margins
        private const double EdgeMarginPercent = 0.05;   // 5% of screen width for left/right
        private const double TopMarginPercent = 0.20;    // 20% of screen height for top
        private const double BottomMarginPercent = 0.05; // 5% of screen height for bottom
        private const double FabButtonSize = 64;
        private const double ContainerWidth = 280;
        private const double ContainerHeight = 300;
        private const string PositionYPercentKey = "FABPositionYPercent_v5";
        private const string IsOnRightKey = "FABIsOnRight_v5";

        // FAB position stored as percentage of available height (0.0 to 1.0)
        private double _fabYPercent = -1; // -1 means not initialized
        private double _startFabYPercent;
        private bool _isOnRight = true;
        private bool _isDragging;
        private bool _hasMoved;

        // Use RootGrid dimensions directly
        private Grid? _rootGrid;

        // Calculated limits (in percentage)
        private double MinYPercent => TopMarginPercent;
        private double MaxYPercent
        {
            get
            {
                double height = _rootGrid?.Height ?? 0;
                if (height <= 0) return 0.95;
                return 1.0 - BottomMarginPercent - (FabButtonSize / height);
            }
        }

        // Element references
        private Grid? _fabContainer;
        private Grid? _dragHandle;
        private VerticalStackLayout? _fabStackLayout;
        private VerticalStackLayout? _actionButtonsContainer;
        private HorizontalStackLayout? _incomeButtonRow;
        private HorizontalStackLayout? _transferButtonRow;
        private HorizontalStackLayout? _expenseButtonRow;

        public FloatingActionMenu()
        {
            InitializeComponent();
            BindingContext = new FloatingMenuViewModel();

            // Get references to named elements
            _rootGrid = this.FindByName<Grid>("RootGrid");
            _fabContainer = this.FindByName<Grid>("FabContainer");
            _dragHandle = this.FindByName<Grid>("DragHandle");
            _fabStackLayout = this.FindByName<VerticalStackLayout>("FabStackLayout");
            _actionButtonsContainer = this.FindByName<VerticalStackLayout>("ActionButtonsContainer");
            _incomeButtonRow = this.FindByName<HorizontalStackLayout>("IncomeButtonRow");
            _transferButtonRow = this.FindByName<HorizontalStackLayout>("TransferButtonRow");
            _expenseButtonRow = this.FindByName<HorizontalStackLayout>("ExpenseButtonRow");

            // Load saved preferences
            _isOnRight = Preferences.Get(IsOnRightKey, true);
            double savedYPercent = Preferences.Get(PositionYPercentKey, -1d);
            if (savedYPercent >= 0)
            {
                _fabYPercent = savedYPercent;
            }

            // Subscribe to RootGrid size changes
            if (_rootGrid != null)
            {
                _rootGrid.SizeChanged += OnRootGridSizeChanged;
            }

            // Add pan gesture for dragging
            if (_dragHandle != null)
            {
                var panGesture = new PanGestureRecognizer();
                panGesture.PanUpdated += OnPanUpdated;
                _dragHandle.GestureRecognizers.Add(panGesture);

                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += OnFabTapped;
                _dragHandle.GestureRecognizers.Add(tapGesture);
            }
        }

        private void SavePosition()
        {
            Preferences.Set(PositionYPercentKey, _fabYPercent);
            Preferences.Set(IsOnRightKey, _isOnRight);
        }

        private void OnFabTapped(object? sender, TappedEventArgs e)
        {
            if (!_hasMoved && BindingContext is FloatingMenuViewModel vm)
            {
                vm.ToggleMenuCommand.Execute(null);
            }
        }

        private void OnRootGridSizeChanged(object? sender, EventArgs e)
        {
            if (_rootGrid == null || _rootGrid.Width <= 0 || _rootGrid.Height <= 0) return;

            // Initialize position if not set or out of bounds
            if (_fabYPercent < 0 || _fabYPercent < MinYPercent || _fabYPercent > MaxYPercent)
            {
                _fabYPercent = Math.Clamp(0.80, MinYPercent, MaxYPercent);
            }

            UpdateContainerPosition();
            UpdateLayoutAlignment();
        }

        private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
        {
            if (_rootGrid == null || _rootGrid.Height <= 0) return;

            double gridHeight = _rootGrid.Height;
            double gridWidth = _rootGrid.Width;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _isDragging = true;
                    _hasMoved = false;
                    _startFabYPercent = _fabYPercent;

                    if (BindingContext is FloatingMenuViewModel vm && vm.IsExpanded)
                    {
                        vm.IsExpanded = false;
                    }
                    break;

                case GestureStatus.Running:
                    if (Math.Abs(e.TotalX) > 5 || Math.Abs(e.TotalY) > 5)
                    {
                        _hasMoved = true;
                    }

                    // Convert pixel movement to percentage
                    double deltaYPercent = e.TotalY / gridHeight;
                    double newYPercent = _startFabYPercent + deltaYPercent;

                    // Apply limits
                    _fabYPercent = Math.Clamp(newYPercent, MinYPercent, MaxYPercent);

                    // Check side switch
                    double currentFabX = _isOnRight 
                        ? gridWidth - (gridWidth * EdgeMarginPercent) - FabButtonSize / 2
                        : (gridWidth * EdgeMarginPercent) + FabButtonSize / 2;

                    double newFabCenterX = currentFabX + e.TotalX;
                    bool shouldBeOnRight = newFabCenterX > gridWidth / 2;

                    if (shouldBeOnRight != _isOnRight)
                    {
                        _isOnRight = shouldBeOnRight;
                        UpdateLayoutAlignment();
                    }

                    UpdateContainerPosition();
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    _isDragging = false;
                    if (_hasMoved)
                    {
                        SavePosition();
                    }
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Task.Delay(100);
                        _hasMoved = false;
                    });
                    break;
            }
        }

        private void UpdateLayoutAlignment()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var alignment = _isOnRight ? LayoutOptions.End : LayoutOptions.Start;

                if (_fabStackLayout != null) _fabStackLayout.HorizontalOptions = alignment;
                if (_actionButtonsContainer != null) _actionButtonsContainer.HorizontalOptions = alignment;
                if (_incomeButtonRow != null) _incomeButtonRow.HorizontalOptions = alignment;
                if (_transferButtonRow != null) _transferButtonRow.HorizontalOptions = alignment;
                if (_expenseButtonRow != null) _expenseButtonRow.HorizontalOptions = alignment;
                if (_dragHandle != null) _dragHandle.HorizontalOptions = alignment;

                var flowDirection = _isOnRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
                if (_incomeButtonRow != null) _incomeButtonRow.FlowDirection = flowDirection;
                if (_transferButtonRow != null) _transferButtonRow.FlowDirection = flowDirection;
                if (_expenseButtonRow != null) _expenseButtonRow.FlowDirection = flowDirection;
            });
        }

        private void UpdateContainerPosition()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_fabContainer == null || _rootGrid == null) return;

                double gridWidth = _rootGrid.Width;
                double gridHeight = _rootGrid.Height;

                if (gridWidth <= 0 || gridHeight <= 0) return;

                // Calculate FAB position in pixels
                // _fabYPercent represents where the TOP of the FAB should be as a % of grid height
                double fabTopY = _fabYPercent * gridHeight;
                double fabLeftX = _isOnRight 
                    ? gridWidth - FabButtonSize - (gridWidth * EdgeMarginPercent)
                    : gridWidth * EdgeMarginPercent;

                // The container holds the FAB at its bottom
                // Container position = FAB position - offset to place FAB at container bottom
                double containerX = _isOnRight 
                    ? fabLeftX - (ContainerWidth - FabButtonSize)
                    : fabLeftX;
                double containerY = fabTopY - (ContainerHeight - FabButtonSize);

                _fabContainer.TranslationX = containerX;
                _fabContainer.TranslationY = containerY;
            });
        }
    }
}
