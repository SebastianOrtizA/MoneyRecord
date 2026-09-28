using FluentAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class TransactionEnrichmentServiceTests
{
    private readonly ITransactionRepository _transactionRepo = Substitute.For<ITransactionRepository>();
    private readonly ITransferRepository _transferRepo = Substitute.For<ITransferRepository>();
    private readonly ICategoryRepository _categoryRepo = Substitute.For<ICategoryRepository>();
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly TransactionEnrichmentService _sut;

    public TransactionEnrichmentServiceTests()
    {
        _sut = new TransactionEnrichmentService(_transactionRepo, _transferRepo, _categoryRepo, _accountRepo);
    }

    [Fact]
    public async Task GetEnrichedTransactionsAsync_EnrichesWithCategoryAndAccountNames()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);

        _transactionRepo.GetByDateRangeAsync(start, end).Returns(new List<Transaction>
        {
            new() { Id = 1, CategoryId = 10, AccountId = 20, Amount = 100m, Type = TransactionType.Income }
        });
        _categoryRepo.GetAllAsync().Returns(new List<Category>
        {
            new() { Id = 10, Name = "Salary", IconCode = "F0001" }
        });
        _accountRepo.GetAllAsync().Returns(new List<Account>
        {
            new() { Id = 20, Name = "Checking", IconCode = "F0070" }
        });

        var result = await _sut.GetEnrichedTransactionsAsync(start, end);

        result.Should().HaveCount(1);
        result[0].CategoryName.Should().Be("Salary");
        result[0].CategoryIconCode.Should().Be("F0001");
        result[0].AccountName.Should().Be("Checking");
        result[0].AccountIconCode.Should().Be("F0070");
    }

    [Fact]
    public async Task GetEnrichedTransactionsAsync_MissingCategory_SetsUnknown()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);

        _transactionRepo.GetByDateRangeAsync(start, end).Returns(new List<Transaction>
        {
            new() { Id = 1, CategoryId = 999, AccountId = null, Amount = 50m }
        });
        _categoryRepo.GetAllAsync().Returns(new List<Category>());
        _accountRepo.GetAllAsync().Returns(new List<Account>());

        var result = await _sut.GetEnrichedTransactionsAsync(start, end);

        result[0].CategoryName.Should().Be("Unknown");
        result[0].CategoryIconCode.Should().Be("F0770");
    }

    [Fact]
    public async Task GetEnrichedTransactionsAsync_NullAccountId_SetsCashDefaults()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);

        _transactionRepo.GetByDateRangeAsync(start, end).Returns(new List<Transaction>
        {
            new() { Id = 1, CategoryId = 1, AccountId = null, Amount = 50m }
        });
        _categoryRepo.GetAllAsync().Returns(new List<Category>
        {
            new() { Id = 1, Name = "Food" }
        });
        _accountRepo.GetAllAsync().Returns(new List<Account>());

        var result = await _sut.GetEnrichedTransactionsAsync(start, end);

        result[0].AccountName.Should().Be("Cash");
        result[0].AccountIconCode.Should().Be("F0115");
    }

    [Fact]
    public async Task GetEnrichedTransfersAsync_EnrichesWithAccountNames()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);

        _transferRepo.GetByDateRangeAsync(start, end).Returns(new List<Transfer>
        {
            new() { Id = 1, SourceAccountId = 1, DestinationAccountId = 2, Amount = 500m }
        });
        _accountRepo.GetAllAsync().Returns(new List<Account>
        {
            new() { Id = 1, Name = "Checking" },
            new() { Id = 2, Name = "Savings" }
        });

        var result = await _sut.GetEnrichedTransfersAsync(start, end);

        result.Should().HaveCount(1);
        result[0].SourceAccountName.Should().Be("Checking");
        result[0].DestinationAccountName.Should().Be("Savings");
    }

    [Fact]
    public async Task GetEnrichedTransfersAsync_MissingAccounts_SetsUnknown()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);

        _transferRepo.GetByDateRangeAsync(start, end).Returns(new List<Transfer>
        {
            new() { Id = 1, SourceAccountId = 999, DestinationAccountId = 888, Amount = 100m }
        });
        _accountRepo.GetAllAsync().Returns(new List<Account>());

        var result = await _sut.GetEnrichedTransfersAsync(start, end);

        result[0].SourceAccountName.Should().Be("Unknown");
        result[0].DestinationAccountName.Should().Be("Unknown");
    }

    [Fact]
    public async Task GetAllEnrichedTransfersAsync_EnrichesAllTransfers()
    {
        _transferRepo.GetAllAsync().Returns(new List<Transfer>
        {
            new() { Id = 1, SourceAccountId = 1, DestinationAccountId = 2, Amount = 100m },
            new() { Id = 2, SourceAccountId = 2, DestinationAccountId = 1, Amount = 50m }
        });
        _accountRepo.GetAllAsync().Returns(new List<Account>
        {
            new() { Id = 1, Name = "A" },
            new() { Id = 2, Name = "B" }
        });

        var result = await _sut.GetAllEnrichedTransfersAsync();

        result.Should().HaveCount(2);
        result[0].SourceAccountName.Should().Be("A");
        result[0].DestinationAccountName.Should().Be("B");
        result[1].SourceAccountName.Should().Be("B");
        result[1].DestinationAccountName.Should().Be("A");
    }

    [Fact]
    public async Task GetEnrichedTransferAsync_ExistingTransfer_EnrichesIt()
    {
        _transferRepo.GetByIdAsync(1).Returns(new Transfer
        {
            Id = 1, SourceAccountId = 1, DestinationAccountId = 2, Amount = 200m
        });
        _accountRepo.GetAllAsync().Returns(new List<Account>
        {
            new() { Id = 1, Name = "Main" },
            new() { Id = 2, Name = "Backup" }
        });

        var result = await _sut.GetEnrichedTransferAsync(1);

        result.Should().NotBeNull();
        result!.SourceAccountName.Should().Be("Main");
        result.DestinationAccountName.Should().Be("Backup");
    }

    [Fact]
    public async Task GetEnrichedTransferAsync_NonExistent_ReturnsNull()
    {
        _transferRepo.GetByIdAsync(999).Returns((Transfer?)null);

        var result = await _sut.GetEnrichedTransferAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEnrichedTransactionsAsync_NullCategoryName_FallsBackToUnknown()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);

        _transactionRepo.GetByDateRangeAsync(start, end).Returns(new List<Transaction>
        {
            new() { Id = 1, CategoryId = 1, AccountId = 1 }
        });
        _categoryRepo.GetAllAsync().Returns(new List<Category>
        {
            new() { Id = 1, Name = null!, IconCode = null! }
        });
        _accountRepo.GetAllAsync().Returns(new List<Account>
        {
            new() { Id = 1, Name = null!, IconCode = null! }
        });

        var result = await _sut.GetEnrichedTransactionsAsync(start, end);

        result[0].CategoryName.Should().Be("Unknown");
        result[0].CategoryIconCode.Should().Be("F0770");
        result[0].AccountName.Should().Be("Cash");
        result[0].AccountIconCode.Should().Be("F0070");
    }
}
