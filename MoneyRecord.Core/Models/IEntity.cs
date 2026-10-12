namespace MoneyRecord.Models
{
    /// <summary>A table row with an auto-increment ID (0 = not inserted yet).</summary>
    public interface IEntity
    {
        int Id { get; }
    }
}
