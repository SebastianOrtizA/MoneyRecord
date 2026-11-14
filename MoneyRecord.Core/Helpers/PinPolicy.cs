namespace MoneyRecord.Helpers
{
    public enum PinProblem
    {
        None,
        Length,
        NotDigits,
        Repeated
    }

    /// <summary>The rules for an app PIN: 4 to 8 digits, not one digit repeated ("0000", "7777").</summary>
    public static class PinPolicy
    {
        public const int MinLength = 4;
        public const int MaxLength = 8;

        public static PinProblem Check(string? pin)
        {
            if (string.IsNullOrEmpty(pin) || pin.Length < MinLength || pin.Length > MaxLength)
                return PinProblem.Length;
            if (!pin.All(char.IsAsciiDigit))
                return PinProblem.NotDigits;
            return pin.All(c => c == pin[0]) ? PinProblem.Repeated : PinProblem.None;
        }
    }
}
