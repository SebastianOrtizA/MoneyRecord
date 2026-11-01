namespace MoneyRecord.Models
{
    /// <summary>A chip of the "Group by" row in the filter sheet, the one in use marked.</summary>
    public record GroupingChoice(GroupingMode Mode, string Name, bool IsSelected);
}
