namespace MoneyRecord.Helpers
{
    /// <summary>
    /// The receipt photo on the add/edit transaction page. Tracks which photo is saved with the transaction, so that
    /// photos taken but never saved are deleted, and a removed or replaced receipt is deleted only once the change is saved.
    /// </summary>
    public sealed class ReceiptDraft
    {
        private readonly string _receiptsFolder;
        private string? _savedPath;

        public ReceiptDraft(string receiptsFolder)
        {
            _receiptsFolder = receiptsFolder;
        }

        /// <summary>The photo shown on the page (null = none).</summary>
        public string? Path { get; private set; }

        /// <summary>Starts over with the receipt the stored transaction has (null for a new transaction).</summary>
        public void Load(string? savedPath)
        {
            Discard();
            _savedPath = Path = savedPath;
        }

        /// <summary>A new photo was taken or picked; an earlier unsaved one is deleted.</summary>
        public void Set(string newPath)
        {
            Discard();
            Path = newPath;
        }

        public void Remove()
        {
            Discard();
            Path = null;
        }

        /// <summary>Leaving without saving: deletes an unsaved photo and goes back to the saved receipt.</summary>
        public void Discard()
        {
            if (Path != _savedPath)
                ReceiptFiles.Delete(Path, _receiptsFolder);
            Path = _savedPath;
        }

        /// <summary>The transaction was saved with <see cref="Path"/>: a receipt it no longer uses is deleted.</summary>
        public void Commit()
        {
            if (_savedPath != Path)
                ReceiptFiles.Delete(_savedPath, _receiptsFolder);
            _savedPath = Path;
        }
    }
}
