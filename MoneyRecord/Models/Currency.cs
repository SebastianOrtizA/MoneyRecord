namespace MoneyRecord.Models
{
    public class Currency
    {
        public string Code { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public override string ToString() => $"{Code} ({Symbol}) - {Name}";
    }
}
