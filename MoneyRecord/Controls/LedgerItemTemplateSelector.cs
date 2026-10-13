using MoneyRecord.Models;

namespace MoneyRecord.Controls
{
    /// <summary>The Transactions list holds entries, or groups of entries when grouped.</summary>
    public class LedgerItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? EntryTemplate { get; set; }

        public DataTemplate? GroupTemplate { get; set; }

        protected override DataTemplate? OnSelectTemplate(object item, BindableObject container) =>
            item is TransactionGroup ? GroupTemplate : EntryTemplate;
    }
}
