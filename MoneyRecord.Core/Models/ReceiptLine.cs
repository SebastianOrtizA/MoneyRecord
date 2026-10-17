namespace MoneyRecord.Models
{
    /// <summary>One line of text the OCR found on a receipt photo, with its box in the photo's pixels.</summary>
    public sealed record ReceiptLine(string Text, int Left, int Top, int Right, int Bottom)
    {
        public int CenterY => (Top + Bottom) / 2;
    }

    /// <summary>What a receipt says: its total and its date, each null when it couldn't be read.</summary>
    public sealed record ReceiptReading(decimal? Amount, DateTime? Date)
    {
        public static ReceiptReading None { get; } = new(null, null);

        public bool IsEmpty => Amount == null && Date == null;
    }
}
