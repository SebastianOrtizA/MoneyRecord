using MoneyRecord.Controls.FabComponents;
using MoneyRecord.Services;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Controls
{
    public partial class FloatingActionMenu : ContentView
    {
        private readonly FabPositionManager _positionManager;
        private readonly FabDragHandler _dragHandler;
        private readonly SmoothDragAnimator _smoothAnimator;
        private FabLayoutManager? _layoutManager;

        private Grid? _rootGrid;
        private Grid? _fabContainer;
        private Grid? _dragHandle;

        private double MinYPercent => _positionManager.CalculateMinYPercent();
        private double MaxYPercent => _positionManager.CalculateMaxYPercent(_rootGrid?.Height ?? 0);

        public FloatingActionMenu()
        {
            InitializeComponent();

            // Resolve services from DI container
            var navigationService = ResolveService<INavigationService>();
            var preferencesService = ResolveService<IPreferencesService>();

            BindingContext = new FloatingMenuViewModel(navigationService);

            // Initialize managers
            _positionManager = new FabPositionManager(preferencesService);
            _dragHandler = new FabDragHandler();
            _smoothAnimator = new SmoothDragAnimator(smoothingFactor: 0.35, threshold: 0.3);

            InitializeElementReferences();
            InitializeLayoutManager();
            SetupGestures();
        }

        private static T ResolveService<T>() where T : class
        {
            return Application.Current?.Handler?.MauiContext?.Services.GetService<T>()
                ?? throw new InvalidOperationException($"Service {typeof(T).Name} not found in DI container.");
        }

        private void InitializeElementReferences()
        {
            _rootGrid = this.FindByName<Grid>("RootGrid");
            _fabContainer = this.FindByName<Grid>("FabContainer");
            _dragHandle = this.FindByName<Grid>("DragHandle");

            if (_rootGrid is not null)
            {
                _rootGrid.SizeChanged += OnRootGridSizeChanged;
            }
        }

        private void InitializeLayoutManager()
        {
            var fabStackLayout = this.FindByName<VerticalStackLayout>("FabStackLayout");
            var actionButtonsContainer = this.FindByName<VerticalStackLayout>("ActionButtonsContainer");
            var incomeButtonRow = this.FindByName<HorizontalStackLayout>("IncomeButtonRow");
            var transferButtonRow = this.FindByName<HorizontalStackLayout>("TransferButtonRow");
            var expenseButtonRow = this.FindByName<HorizontalStackLayout>("ExpenseButtonRow");

            _layoutManager = new FabLayoutManager(
                fabStackLayout,
                actionButtonsContainer,
                incomeButtonRow,
                transferButtonRow,
                expenseButtonRow,
                _dragHandle);
        }

        private void SetupGestures()
        {
            if (_dragHandle is null) return;

            var panGesture = new PanGestureRecognizer();
            panGesture.PanUpdated += OnPanUpdated;
            _dragHandle.GestureRecognizers.Add(panGesture);

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += OnFabTapped;
            _dragHandle.GestureRecognizers.Add(tapGesture);
        }

        private void OnFabTapped(object? sender, TappedEventArgs e)
        {
            if (!_dragHandler.HasMoved && BindingContext is FloatingMenuViewModel vm)
            {
                vm.ToggleMenuCommand.Execute(null);
            }
        }

        private void OnRootGridSizeChanged(object? sender, EventArgs e)
        {
            if (_rootGrid is null || _rootGrid.Width <= 0 || _rootGrid.Height <= 0) return;

            _positionManager.InitializePositionIfNeeded(MinYPercent, MaxYPercent);
            UpdateContainerPosition();
            _layoutManager?.UpdateAlignment(_positionManager.IsOnRight);
        }

        private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
        {
            if (_rootGrid is null || _rootGrid.Height <= 0) return;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    HandleDragStarted();
                    break;

                case GestureStatus.Running:
                    HandleDragRunning(e.TotalX, e.TotalY);
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    HandleDragEnded();
                    break;
            }
        }

        private void HandleDragStarted()
        {
            _dragHandler.StartDrag();
            _positionManager.StartFabYPercent = _positionManager.FabYPercent;

            if (BindingContext is FloatingMenuViewModel { IsExpanded: true } vm)
            {
                vm.IsExpanded = false;
            }

            // Initialize smooth animator with current position
            if (_fabContainer is not null)
            {
                _smoothAnimator.Initialize(_fabContainer.TranslationX, _fabContainer.TranslationY);
                _smoothAnimator.StartAnimation(_fabContainer, ApplySmoothPosition);
            }
        }

        private void HandleDragRunning(double totalX, double totalY)
        {
            if (_rootGrid is null) return;

            _dragHandler.UpdateMovement(totalX, totalY);

            // Update Y position
            double newYPercent = _positionManager.CalculateNewYPercent(totalY, _rootGrid.Height);
            _positionManager.FabYPercent = newYPercent;
            _positionManager.ClampPosition(MinYPercent, MaxYPercent);

            // Check for side switch
            bool shouldBeOnRight = _positionManager.ShouldSwitchSide(totalX, _rootGrid.Width);
            if (shouldBeOnRight != _positionManager.IsOnRight)
            {
                _positionManager.IsOnRight = shouldBeOnRight;
                _layoutManager?.UpdateAlignment(_positionManager.IsOnRight);
            }

            // Set target for smooth animation instead of direct update
            var (targetX, targetY) = _positionManager.CalculateContainerPosition(_rootGrid.Width, _rootGrid.Height);
            _smoothAnimator.SetTarget(targetX, targetY);
        }

        private void HandleDragEnded()
        {
            _smoothAnimator.StopAnimation();
            _smoothAnimator.SnapToTarget();
            _dragHandler.EndDrag();

            // Apply final position
            UpdateContainerPosition();

            if (_dragHandler.HasMoved)
            {
                _positionManager.SavePosition();
            }

            _ = _dragHandler.ResetMovedStateAsync();
        }

        private void ApplySmoothPosition(double x, double y)
        {
            if (_fabContainer is null) return;

            _fabContainer.TranslationX = x;
            _fabContainer.TranslationY = y;
        }

        private void UpdateContainerPosition()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_fabContainer is null || _rootGrid is null) return;
                if (_rootGrid.Width <= 0 || _rootGrid.Height <= 0) return;

                var (x, y) = _positionManager.CalculateContainerPosition(_rootGrid.Width, _rootGrid.Height);
                _fabContainer.TranslationX = x;
                _fabContainer.TranslationY = y;
            });
        }
    }
}
