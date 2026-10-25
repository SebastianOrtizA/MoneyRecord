using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class HomeLayoutPage : ContentPage
    {
        private readonly HomeLayoutViewModel _viewModel;

        public HomeLayoutPage(HomeLayoutViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        private const string DraggedKey = "card";

        private void OnDragStarting(object? sender, DragStartingEventArgs e)
        {
            if (sender is BindableObject { BindingContext: HomeCardItem item } row)
            {
                e.Data.Properties[DraggedKey] = item;
                (row as VisualElement)!.Opacity = 0.5;
            }
        }

        private void OnDragEnded(object? sender, DropCompletedEventArgs e)
        {
            if (sender is VisualElement row)
                row.Opacity = 1;
        }

        // The row under the finger shows where the card would land
        private void OnDragOver(object? sender, DragEventArgs e)
        {
            if (sender is VisualElement row)
                row.Scale = 1.03;
        }

        private void OnDragLeave(object? sender, DragEventArgs e)
        {
            if (sender is VisualElement row)
                row.Scale = 1;
        }

        private void OnDrop(object? sender, DropEventArgs e)
        {
            if (sender is VisualElement row)
                row.Scale = 1;
            if (sender is BindableObject { BindingContext: HomeCardItem target }
                && e.Data.Properties.TryGetValue(DraggedKey, out var dragged) && dragged is HomeCardItem item)
                _viewModel.MoveTo(item, target);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.Initialize();
        }
    }
}
