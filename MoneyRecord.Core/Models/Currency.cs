namespace MoneyRecord.Models
{
    public class Currency
    {
        public string Code { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// The bare sign people write ("$" for COP, whose <see cref="Symbol"/> is "COL$"), for text that also shows the
        /// code. Empty = the same as <see cref="Symbol"/>.
        /// </summary>
        public string PlainSymbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public override string ToString() => $"{Code} ({Symbol}) - {Name}";
    }
}
