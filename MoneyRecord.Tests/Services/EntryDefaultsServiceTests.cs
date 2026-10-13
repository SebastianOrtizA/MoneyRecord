using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class EntryDefaultsServiceTests
{
    /// <summary>Preferences kept in memory.</summary>
    private sealed class FakePreferences : IPreferencesService
    {
        private readonly Dictionary<string, object?> _values = [];

        public T Get<T>(string key, T defaultValue) =>
            _values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;

        public void Set<T>(string key, T value) => _values[key] = value;
    }

    private static readonly Account Cash = new() { Id = 1, Name = "Cash", IsDefault = true };
    private static readonly Account Bank = new() { Id = 2, Name = "Bank" };
    private static readonly List<Account> Accounts = [Cash, Bank];

    private static readonly Category Food = new() { Id = 5, Name = "Food", Type = CategoryType.Expense };
    private static readonly Category Transport = new() { Id = 6, Name = "Transport", Type = CategoryType.Expense };
    private static readonly Category Fun = new() { Id = 7, Name = "Fun", Type = CategoryType.Expense };
    private static readonly Category Home = new() { Id = 10, Name = "Home", Type = CategoryType.Expense };
    private static readonly List<Category> Expenses = [Food, Transport, Fun, Home];

    private readonly EntryDefaultsService _sut = new(new FakePreferences());

    [Fact]
    public void Without_history_the_default_account_and_first_category_are_used()
    {
        _sut.DefaultAccount(TransactionType.Expense, Accounts).Should().Be(Cash);
        _sut.DefaultCategory(TransactionType.Expense, Expenses).Should().Be(Food);
        _sut.OrderByRecent(TransactionType.Expense, Expenses).Should().Equal(Expenses);
    }

    [Fact]
    public void The_last_account_and_category_are_remembered_per_type()
    {
        _sut.Remember(TransactionType.Expense, Bank.Id, Fun.Id);

        _sut.DefaultAccount(TransactionType.Expense, Accounts).Should().Be(Bank);
        _sut.DefaultCategory(TransactionType.Expense, Expenses).Should().Be(Fun);
        _sut.DefaultAccount(TransactionType.Income, Accounts).Should().Be(Cash);
    }

    [Fact]
    public void A_deleted_account_or_category_falls_back()
    {
        _sut.Remember(TransactionType.Expense, accountId: 99, categoryId: 99);

        _sut.DefaultAccount(TransactionType.Expense, Accounts).Should().Be(Cash);
        _sut.DefaultCategory(TransactionType.Expense, Expenses).Should().Be(Food);
    }

    [Fact]
    public void Recent_categories_come_first_most_recent_first()
    {
        _sut.Remember(TransactionType.Expense, Cash.Id, Home.Id);
        _sut.Remember(TransactionType.Expense, Cash.Id, Fun.Id);
        _sut.Remember(TransactionType.Expense, Cash.Id, Home.Id);

        _sut.OrderByRecent(TransactionType.Expense, Expenses).Should().Equal(Home, Fun, Food, Transport);
    }

    [Fact]
    public void Only_the_last_few_categories_count_as_recent()
    {
        var many = Enumerable.Range(1, 6).Select(i => new Category { Id = i, Name = $"C{i}" }).ToList();
        foreach (var category in many)
            _sut.Remember(TransactionType.Expense, Cash.Id, category.Id);

        _sut.OrderByRecent(TransactionType.Expense, many).Select(c => c.Id)
            .Should().Equal(6, 5, 4, 3, 1, 2);
    }
}
