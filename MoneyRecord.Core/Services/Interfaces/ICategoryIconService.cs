using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Interface for icon service providing Material Design Icons for categories and accounts.
    /// </summary>
    public interface ICategoryIconService
    {
        List<CategoryIcon> GetExpenseIcons();
        List<CategoryIcon> GetIncomeIcons();
        List<CategoryIcon> GetIconsForType(CategoryType type);
        string GetDefaultIconCode(CategoryType type);
        List<AccountIcon> GetAccountIcons();
        string GetDefaultAccountIconCode();
    }
}
