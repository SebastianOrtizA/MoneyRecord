using MoneyRecord.Models;

namespace MoneyRecord.ViewModels
{
    /// <summary>One saved filter in the chip row and the filter sheet; <see cref="IsActive"/> while the list is filtered exactly so.</summary>
    public sealed record SavedFilterChoice(SavedFilter Filter, bool IsActive)
    {
        public string Name => Filter.Name;
    }
}
