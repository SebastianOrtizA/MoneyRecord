namespace MoneyRecord.Controls.FabComponents
{
    /// <summary>
    /// Manages layout alignment for FAB-related UI elements.
    /// Single Responsibility: UI alignment updates.
    /// </summary>
    public sealed class FabLayoutManager
    {
        private readonly VerticalStackLayout? _fabStackLayout;
        private readonly VerticalStackLayout? _actionButtonsContainer;
        private readonly HorizontalStackLayout? _incomeButtonRow;
        private readonly HorizontalStackLayout? _transferButtonRow;
        private readonly HorizontalStackLayout? _expenseButtonRow;
        private readonly Grid? _dragHandle;

        public FabLayoutManager(
            VerticalStackLayout? fabStackLayout,
            VerticalStackLayout? actionButtonsContainer,
            HorizontalStackLayout? incomeButtonRow,
            HorizontalStackLayout? transferButtonRow,
            HorizontalStackLayout? expenseButtonRow,
            Grid? dragHandle)
        {
            _fabStackLayout = fabStackLayout;
            _actionButtonsContainer = actionButtonsContainer;
            _incomeButtonRow = incomeButtonRow;
            _transferButtonRow = transferButtonRow;
            _expenseButtonRow = expenseButtonRow;
            _dragHandle = dragHandle;
        }

        public void UpdateAlignment(bool isOnRight)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var alignment = isOnRight ? LayoutOptions.End : LayoutOptions.Start;

                UpdateHorizontalOptions(alignment);
                UpdateFlowDirections(isOnRight);
            });
        }

        private void UpdateHorizontalOptions(LayoutOptions alignment)
        {
            if (_fabStackLayout is not null)
                _fabStackLayout.HorizontalOptions = alignment;

            if (_actionButtonsContainer is not null)
                _actionButtonsContainer.HorizontalOptions = alignment;

            if (_incomeButtonRow is not null)
                _incomeButtonRow.HorizontalOptions = alignment;

            if (_transferButtonRow is not null)
                _transferButtonRow.HorizontalOptions = alignment;

            if (_expenseButtonRow is not null)
                _expenseButtonRow.HorizontalOptions = alignment;

            if (_dragHandle is not null)
                _dragHandle.HorizontalOptions = alignment;
        }

        private void UpdateFlowDirections(bool isOnRight)
        {
            var flowDirection = isOnRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;

            if (_incomeButtonRow is not null)
                _incomeButtonRow.FlowDirection = flowDirection;

            if (_transferButtonRow is not null)
                _transferButtonRow.FlowDirection = flowDirection;

            if (_expenseButtonRow is not null)
                _expenseButtonRow.FlowDirection = flowDirection;
        }
    }
}
