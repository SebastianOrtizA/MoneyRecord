namespace MoneyRecord.Models
{
    /// <summary>
    /// One movement read from a statement file (CSV, OFX or QIF), before it is checked against an account.
    /// <see cref="Amount"/> is signed: negative = money out. A null <see cref="Date"/> or <see cref="Amount"/> means the file's
    /// value could not be read (the preview shows the row as a problem).
    /// </summary>
    public sealed record StatementEntry(int Line, DateTime? Date, string Description, decimal? Amount, string? ExternalId = null,
        string? CategoryName = null, string? PayeeName = null);

    /// <summary>The movements of a statement file, and the currency the file declares (null = it doesn't say).</summary>
    public sealed record ParsedStatement(IReadOnlyList<StatementEntry> Entries, string? CurrencyCode = null);
}
