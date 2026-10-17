using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public sealed class TripEndPlanTests
{
    private static readonly Category Food = new() { Id = 1, Name = "Food", Type = CategoryType.Expense };
    private static readonly Category Fun = new() { Id = 2, Name = "Fun", Type = CategoryType.Expense };
    private static readonly Category Travel = new() { Id = 3, Name = "Travel", Type = CategoryType.Expense, IsTravel = true };
    private static readonly Category Salary = new() { Id = 4, Name = "Salary", Type = CategoryType.Income };

    [Fact]
    public void Targets_AreExpenseCategories_WithoutTravel()
    {
        TripEndPlan.Targets([Food, Travel, Salary, Fun]).Should().Equal(Food, Fun);
    }

    [Fact]
    public void Rows_AreOnlyTheTripCategoriesInUse_InTheirOrder()
    {
        var lodging = new TripCategory { Id = 10, Name = "Lodging" };
        var flights = new TripCategory { Id = 11, Name = "Flights" };
        var other = new TripCategory { Id = 12, Name = "Other" };
        var expenses = new[]
        {
            new Transaction { TripCategoryId = 12 }, new Transaction { TripCategoryId = 10 },
            new Transaction { TripCategoryId = 10 }, new Transaction { TripCategoryId = null }
        };

        var rows = TripEndPlan.Rows(expenses, [lodging, flights, other], [Food, Fun]);

        rows.Select(r => r.Source.Id).Should().Equal(10, 12);
    }

    [Fact]
    public void Rows_AreFilledFromTheLastChoice_WhenItIsStillOffered()
    {
        var lodging = new TripCategory { Id = 10, LastCategoryId = 2 };
        var flights = new TripCategory { Id = 11, LastCategoryId = 99 };
        var food = new TripCategory { Id = 12, LastCategoryId = 3 };
        var expenses = new[] { new Transaction { TripCategoryId = 10 }, new Transaction { TripCategoryId = 11 }, new Transaction { TripCategoryId = 12 } };

        var rows = TripEndPlan.Rows(expenses, [lodging, flights, food], TripEndPlan.Targets([Food, Fun, Travel]));

        // A category that is gone, and Travel (never a target), leave the row empty
        rows.Select(r => r.Target).Should().Equal(Fun, null, null);
    }

    [Fact]
    public void Rows_AreEmpty_WhenNoExpenseHasATripCategory()
    {
        TripEndPlan.Rows([new Transaction()], [new TripCategory { Id = 10 }], [Food]).Should().BeEmpty();
    }
}
