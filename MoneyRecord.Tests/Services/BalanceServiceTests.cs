using FluentAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class BalanceServiceTests
{
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly ITransactionRepository _transactionRepo = Substitute.For<ITransactionRepository>();
    private readonly ITransferRepository _transferRepo = Substitute.For<ITransferRepository>();
    private readonly BalanceService _sut;

    public BalanceServiceTests()
    {
        _sut = new BalanceService(_accountRepo, _transactionRepo, _transferRepo);
    }

    [Fact]
    public async Task GetTotalBalanceAsync_NoTransactions_ReturnsInitialBalances()
    {
        _accountRepo.GetAllAsync().Returns(new List<Account>
        {
            new() { Id = 1, InitialBalance = 100m },
            new() { Id = 2, InitialBalance = 200m }
        });
        _transactionRepo.GetAllAsync().Returns(new List<Transaction>());

        var result = await _sut.GetTotalBalanceAsync();

        result.Should().Be(300m);
    }

    [Fact]
    public async Task GetTotalBalanceAsync_WithIncomesAndExpenses_CalculatesCorrectly()
    {
        _accountRepo.GetAllAsync().Returns(new List<Account>
        {
            new() { Id = 1, InitialBalance = 500m }
        });
        _transactionRepo.GetAllAsync().Returns(new List<Transaction>
        {
            new() { Id = 1, Amount = 200m, Type = TransactionType.Income },
            new() { Id = 2, Amount = -150m, Type = TransactionType.Expense },
            new() { Id = 3, Amount = 50m, Type = TransactionType.Income }
        });

        var result = await _sut.GetTotalBalanceAsync();

        // 500 + |200| + |50| - |-150| = 500 + 250 - 150 = 600
        result.Should().Be(600m);
    }

    [Fact]
    public async Task GetAccountBalanceAsync_NonExistentAccount_ReturnsZero()
    {
        _accountRepo.GetByIdAsync(999).Returns((Account?)null);

        var result = await _sut.GetAccountBalanceAsync(999);

        result.Should().Be(0m);
    }

    [Fact]
    public async Task GetAccountBalanceAsync_WithTransactionsAndTransfers_CalculatesCorrectly()
    {
        _accountRepo.GetByIdAsync(1).Returns(new Account { Id = 1, InitialBalance = 1000m });
        _transactionRepo.GetByAccountIdAsync(1).Returns(new List<Transaction>
        {
            new() { Id = 1, Amount = 300m, Type = TransactionType.Income, AccountId = 1 },
            new() { Id = 2, Amount = -200m, Type = TransactionType.Expense, AccountId = 1 }
        });
        _transferRepo.GetBySourceAccountIdAsync(1).Returns(new List<Transfer>
        {
            new() { Id = 1, Amount = 100m, SourceAccountId = 1, DestinationAccountId = 2 }
        });
        _transferRepo.GetByDestinationAccountIdAsync(1).Returns(new List<Transfer>
        {
            new() { Id = 2, Amount = 50m, SourceAccountId = 2, DestinationAccountId = 1 }
        });

        var result = await _sut.GetAccountBalanceAsync(1);

        // 1000 + |300| - |-200| - |100| + |50| = 1000 + 300 - 200 - 100 + 50 = 1050
        result.Should().Be(1050m);
    }

    [Fact]
    public async Task GetTotalIncomesAsync_FiltersCorrectly()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);
        _transactionRepo.GetByDateRangeAsync(start, end).Returns(new List<Transaction>
        {
            new() { Id = 1, Amount = 500m, Type = TransactionType.Income },
            new() { Id = 2, Amount = -300m, Type = TransactionType.Expense },
            new() { Id = 3, Amount = 200m, Type = TransactionType.Income }
        });

        var result = await _sut.GetTotalIncomesAsync(start, end);

        result.Should().Be(700m);
    }

    [Fact]
    public async Task GetTotalExpensesAsync_FiltersCorrectly()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);
        _transactionRepo.GetByDateRangeAsync(start, end).Returns(new List<Transaction>
        {
            new() { Id = 1, Amount = 500m, Type = TransactionType.Income },
            new() { Id = 2, Amount = -300m, Type = TransactionType.Expense },
            new() { Id = 3, Amount = -100m, Type = TransactionType.Expense }
        });

        var result = await _sut.GetTotalExpensesAsync(start, end);

        result.Should().Be(400m);
    }

    [Fact]
    public async Task GetBalanceAsync_WithDateRange_ReturnsNetIncome()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);
        _transactionRepo.GetByDateRangeAsync(start, end).Returns(new List<Transaction>
        {
            new() { Id = 1, Amount = 500m, Type = TransactionType.Income },
            new() { Id = 2, Amount = -300m, Type = TransactionType.Expense }
        });

        var result = await _sut.GetBalanceAsync(start, end);

        // incomes (500) - expenses (-300 sum = -300) => 500 - (-300) = 800
        result.Should().Be(800m);
    }

    [Fact]
    public async Task GetBalanceAsync_WithoutDateRange_UsesAllTransactions()
    {
        _transactionRepo.GetAllAsync().Returns(new List<Transaction>
        {
            new() { Id = 1, Amount = 1000m, Type = TransactionType.Income },
            new() { Id = 2, Amount = -400m, Type = TransactionType.Expense }
        });

        var result = await _sut.GetBalanceAsync();

        // incomes (1000) - expenses (-400) => 1000 - (-400) = 1400
        result.Should().Be(1400m);
    }

    [Fact]
    public async Task GetAllAccountBalancesAsync_CalculatesEachAccountCorrectly()
    {
        _accountRepo.GetAllAsync().Returns(new List<Account>
        {
            new() { Id = 1, Name = "Checking", InitialBalance = 1000m, CreatedDate = new DateTime(2026, 1, 1) },
            new() { Id = 2, Name = "Savings", InitialBalance = 5000m, CreatedDate = new DateTime(2026, 1, 1) }
        });
        _transactionRepo.GetAllAsync().Returns(new List<Transaction>
        {
            new() { Id = 1, Amount = 200m, Type = TransactionType.Income, AccountId = 1, Date = new DateTime(2026, 3, 1) },
            new() { Id = 2, Amount = -100m, Type = TransactionType.Expense, AccountId = 1, Date = new DateTime(2026, 3, 15) },
            new() { Id = 3, Amount = 500m, Type = TransactionType.Income, AccountId = 2, Date = new DateTime(2026, 2, 1) }
        });
        _transferRepo.GetAllAsync().Returns(new List<Transfer>
        {
            new() { Id = 1, Amount = 300m, SourceAccountId = 1, DestinationAccountId = 2, Date = new DateTime(2026, 4, 1) }
        });

        var result = await _sut.GetAllAccountBalancesAsync();

        result.Should().HaveCount(2);

        var checking = result.First(b => b.AccountId == 1);
        // 1000 + 200 - 100 - 300 + 0 = 800
        checking.CurrentBalance.Should().Be(800m);

        var savings = result.First(b => b.AccountId == 2);
        // 5000 + 500 - 0 - 0 + 300 = 5800
        savings.CurrentBalance.Should().Be(5800m);
    }

    [Fact]
    public async Task GetAllAccountBalancesAsync_LastActivityDate_UsesLatestTransaction()
    {
        var latestDate = new DateTime(2026, 6, 15);
        _accountRepo.GetAllAsync().Returns(new List<Account>
        {
            new() { Id = 1, Name = "Test", InitialBalance = 0m, CreatedDate = new DateTime(2026, 1, 1) }
        });
        _transactionRepo.GetAllAsync().Returns(new List<Transaction>
        {
            new() { Id = 1, Amount = 100m, Type = TransactionType.Income, AccountId = 1, Date = new DateTime(2026, 3, 1) },
            new() { Id = 2, Amount = -50m, Type = TransactionType.Expense, AccountId = 1, Date = latestDate }
        });
        _transferRepo.GetAllAsync().Returns(new List<Transfer>());

        var result = await _sut.GetAllAccountBalancesAsync();

        result.First().LastActivityDate.Should().Be(latestDate);
    }

    [Fact]
    public async Task GetAllAccountBalancesAsync_NoActivity_FallsBackToCreatedDate()
    {
        var createdDate = new DateTime(2026, 1, 15);
        _accountRepo.GetAllAsync().Returns(new List<Account>
        {
            new() { Id = 1, Name = "Empty", InitialBalance = 100m, CreatedDate = createdDate }
        });
        _transactionRepo.GetAllAsync().Returns(new List<Transaction>());
        _transferRepo.GetAllAsync().Returns(new List<Transfer>());

        var result = await _sut.GetAllAccountBalancesAsync();

        result.First().LastActivityDate.Should().Be(createdDate);
    }

    [Fact]
    public async Task GetLastTransactionDateForAccountAsync_ReturnsLatestDate()
    {
        _transactionRepo.GetByAccountIdAsync(1).Returns(new List<Transaction>
        {
            new() { Id = 1, Date = new DateTime(2026, 1, 1), AccountId = 1 },
            new() { Id = 2, Date = new DateTime(2026, 6, 15), AccountId = 1 },
            new() { Id = 3, Date = new DateTime(2026, 3, 10), AccountId = 1 }
        });

        var result = await _sut.GetLastTransactionDateForAccountAsync(1);

        result.Should().Be(new DateTime(2026, 6, 15));
    }

    [Fact]
    public async Task GetLastTransactionDateForAccountAsync_NoTransactions_ReturnsNull()
    {
        _transactionRepo.GetByAccountIdAsync(1).Returns(new List<Transaction>());

        var result = await _sut.GetLastTransactionDateForAccountAsync(1);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCategoryExpensesAsync_FiltersByCategoryAndType()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);
        _transactionRepo.GetByDateRangeAsync(start, end).Returns(new List<Transaction>
        {
            new() { Id = 1, Amount = -200m, Type = TransactionType.Expense, CategoryId = 5 },
            new() { Id = 2, Amount = -100m, Type = TransactionType.Expense, CategoryId = 5 },
            new() { Id = 3, Amount = 500m, Type = TransactionType.Income, CategoryId = 5 },
            new() { Id = 4, Amount = -300m, Type = TransactionType.Expense, CategoryId = 10 }
        });

        var result = await _sut.GetCategoryExpensesAsync(5, start, end);

        // Only expenses for category 5: |-200| + |-100| = 300
        result.Should().Be(300m);
    }
}
