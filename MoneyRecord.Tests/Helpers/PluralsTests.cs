using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

[Collection(LanguageCollection.Name)]
public class PluralsTests : LanguageTestBase
{
    [Fact]
    public void One_UsesSingular()
    {
        Plurals.Transactions(1).Should().Be(string.Format(LocalizationService.Instance.GetString("TransactionCountOne"), 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(11)]
    public void OtherCounts_UsePlural(int count)
    {
        Plurals.Transactions(count).Should().Be(string.Format(LocalizationService.Instance.GetString("TransactionCountOther"), count));
    }

    [Fact]
    public void Recurring_UseSingularAndPlural()
    {
        Plurals.Recurring(1).Should().Be(string.Format(LocalizationService.Instance.GetString("RecurringCountOne"), 1));
        Plurals.Recurring(2).Should().Be(string.Format(LocalizationService.Instance.GetString("RecurringCountOther"), 2));
    }

    [Fact]
    public void Accounts_UseSingularAndPlural()
    {
        Plurals.Accounts(1).Should().Be(string.Format(LocalizationService.Instance.GetString("AccountCountOne"), 1));
        Plurals.Accounts(3).Should().Be(string.Format(LocalizationService.Instance.GetString("AccountCountOther"), 3));
    }
}
