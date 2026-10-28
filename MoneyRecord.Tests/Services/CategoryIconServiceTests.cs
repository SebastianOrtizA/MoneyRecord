using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>An icon the app picks itself must be in the picker, or editing that category / account highlights nothing.</summary>
public class CategoryIconServiceTests
{
    private readonly CategoryIconService _service = new();

    [Theory]
    [InlineData(CategoryType.Income)]
    [InlineData(CategoryType.Expense)]
    public void DefaultAndSeedCategoryIcons_AreInThePicker(CategoryType type)
    {
        var codes = _service.GetIconsForType(type).Select(i => i.Code).ToList();

        codes.Should().Contain(_service.GetDefaultIconCode(type));
        codes.Should().Contain(DefaultCategory.All.Where(c => c.Type == type).Select(c => c.IconCode));
    }

    [Theory]
    [InlineData(CategoryType.Income)]
    [InlineData(CategoryType.Expense)]
    public void EveryCategoryIcon_IsUniqueAndAMaterialDesignCodePoint(CategoryType type)
    {
        var codes = _service.GetIconsForType(type).Select(i => i.Code).ToList();

        codes.Should().OnlyHaveUniqueItems();
        codes.Should().OnlyContain(code => Convert.ToInt32(code, 16) >= 0xF0000 && Convert.ToInt32(code, 16) <= 0xFFFFF);
        _service.GetIconsForType(type).Select(i => i.Name).Should().OnlyContain(name => name.Length > 0);
    }

    [Fact]
    public void DefaultAndCashAccountIcons_AreInThePicker()
    {
        var codes = _service.GetAccountIcons().Select(i => i.Code).ToList();

        codes.Should().Contain(_service.GetDefaultAccountIconCode());
        codes.Should().Contain(Account.CashIconCode);
    }
}
