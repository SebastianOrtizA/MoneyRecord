namespace MoneyRecord.Models
{
    public class Currency
    {
        public string Code { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>"USD ($) - US Dollar"; the symbol is left out when it is the code itself ("AED - UAE Dirham").</summary>
        public override string ToString() => Symbol == Code ? $"{Code} - {Name}" : $"{Code} ({Symbol}) - {Name}";
    }
}
