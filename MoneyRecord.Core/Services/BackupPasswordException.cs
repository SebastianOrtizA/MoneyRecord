namespace MoneyRecord.Services
{
    /// <summary>The backup is encrypted, and no password or a wrong one was given.</summary>
    public sealed class BackupPasswordException(bool wrongPassword)
        : Exception(wrongPassword ? "Wrong backup password" : "The backup is protected with a password")
    {
        /// <summary>A password was given but it's not this backup's; false = none was given.</summary>
        public bool WrongPassword { get; } = wrongPassword;
    }
}
