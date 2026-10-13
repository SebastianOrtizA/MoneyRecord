namespace MoneyRecord.Controls
{
    /// <summary>A filter chip: the <c>FilterChip</c> style gives it the active colours while <see cref="IsActive"/>.</summary>
    public class FilterChip : Button
    {
        public static readonly BindableProperty IsActiveProperty =
            BindableProperty.Create(nameof(IsActive), typeof(bool), typeof(FilterChip), false);

        /// <summary>True while the chip's filter narrows the list.</summary>
        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }
    }
}
