namespace MoneyRecord.Controls
{
    /// <summary>The filter sheet; a page names its own filters in <c>FilterSheet.Sections</c>.</summary>
    public partial class FilterSheet : ContentView
    {
        public FilterSheet()
        {
            InitializeComponent();
            // Named here: the scroll is declared inside the sheet, after its attributes
            Sheet.Scroll = SectionsScroll;
        }

        /// <summary>The page's filters, shown between the title and the Clear / OK buttons (scrolls when long).</summary>
        public View? Sections
        {
            get => SectionsHost.Content;
            set => SectionsHost.Content = value;
        }
    }
}
