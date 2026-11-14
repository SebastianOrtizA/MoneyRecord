using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// The files attached on the add/edit transaction page. Tracks which ones are saved with the transaction, so that files added
    /// but never saved are deleted, and a removed one is deleted only once the change is saved.
    /// </summary>
    public sealed class ReceiptDraft
    {
        private readonly string _receiptsFolder;
        private List<TransactionAttachment> _saved = [];
        private readonly List<TransactionAttachment> _items = [];

        public ReceiptDraft(string receiptsFolder)
        {
            _receiptsFolder = receiptsFolder;
        }

        /// <summary>The attachments shown on the page, in order.</summary>
        public IReadOnlyList<TransactionAttachment> Items => _items;

        /// <summary>Starts over with the attachments the stored transaction has (none for a new one).</summary>
        public void Load(IEnumerable<TransactionAttachment>? saved)
        {
            Discard();
            _saved = saved?.ToList() ?? [];
            _items.Clear();
            _items.AddRange(_saved);
        }

        /// <summary>A photo or file was taken or picked.</summary>
        public void Add(TransactionAttachment attachment) => _items.Add(attachment);

        /// <summary>Takes it off the page; the file goes when the change is saved (at once if it was never saved).</summary>
        public void Remove(TransactionAttachment attachment)
        {
            if (!_items.Remove(attachment))
                return;
            if (!IsSaved(attachment.Path))
                ReceiptFiles.Delete(attachment.Path, _receiptsFolder);
        }

        /// <summary>Leaving without saving: deletes the files added meanwhile and goes back to the saved attachments.</summary>
        public void Discard()
        {
            foreach (var item in _items.Where(i => !IsSaved(i.Path)))
                ReceiptFiles.Delete(item.Path, _receiptsFolder);
            _items.Clear();
            _items.AddRange(_saved);
        }

        /// <summary>The transaction was saved with <see cref="Items"/>: files it no longer uses are deleted.</summary>
        public void Commit()
        {
            foreach (var old in _saved.Where(s => _items.All(i => i.Path != s.Path)))
                ReceiptFiles.Delete(old.Path, _receiptsFolder);
            _saved = _items.ToList();
        }

        private bool IsSaved(string path) => _saved.Any(s => s.Path == path);
    }
}
