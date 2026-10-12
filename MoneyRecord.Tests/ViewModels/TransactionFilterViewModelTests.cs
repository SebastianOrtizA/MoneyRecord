using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Models;
using MoneyRecord.ViewModels;
using Xunit;

namespace MoneyRecord.Tests.ViewModels;

public class TransactionFilterViewModelTests
{
    private readonly FakeTimeProvider _clock = new();

    private readonly TransactionFilterViewModel _sut;
    private int _changes;

    public TransactionFilterViewModelTests()
    {
        _sut = new TransactionFilterViewModel(timeProvider: _clock);
        _sut.Changed += () => _changes++;
    }

    private void TypingPause() => _clock.Advance(TransactionFilterViewModel.DefaultTypingDelay);

    [Fact]
    public void Typing_changes_the_filter_once_after_the_pause()
    {
        _sut.SearchText = "c";
        _sut.SearchText = "co";
        _sut.SearchText = "coffee";
        _changes.Should().Be(0);

        TypingPause();

        _changes.Should().Be(1);
        _sut.ToFilter(ascending: false).SearchText.Should().Be("coffee");
    }

    [Fact]
    public void A_picker_changes_the_filter_at_once()
    {
        _sut.SelectedType = _sut.Types.Single(t => t.Type == TransactionType.Expense);

        _changes.Should().Be(1);
        _sut.ToFilter(ascending: false).Type.Should().Be(TransactionType.Expense);
    }

    [Fact]
    public void The_search_key_does_not_wait_and_drops_the_pending_change()
    {
        _sut.SearchText = "rent";
        _sut.SearchCommand.Execute(null);
        _changes.Should().Be(1);

        TypingPause();

        _changes.Should().Be(1);
    }

    [Fact]
    public void Clear_resets_everything_with_one_change()
    {
        _sut.SelectedCategory = new FilterOption(3, "Food");
        _sut.MinAmountText = "10";
        TypingPause();
        _changes = 0;

        _sut.ClearCommand.Execute(null);
        TypingPause();

        _changes.Should().Be(1);
        var filter = _sut.ToFilter(ascending: false);
        filter.IsActive.Should().BeFalse();
        _sut.SelectedType.Should().Be(_sut.Types[0]);
    }

    [Fact]
    public void An_empty_amount_is_no_limit()
    {
        _sut.MinAmountText = "250";
        _sut.MaxAmountText = "";

        var filter = _sut.ToFilter(ascending: true);

        filter.MinAmount.Should().Be(250m);
        filter.MaxAmount.Should().BeNull();
        filter.Ascending.Should().BeTrue();
    }

    [Fact]
    public void Options_come_from_the_entries()
    {
        _sut.SetOptions(
        [
            new LedgerEntry
            {
                Kind = LedgerEntryKind.Transfer, SourceId = 1, AccountId = 1, AccountName = "Checking",
                CounterpartAccountId = 2, CounterpartAccountName = "Savings"
            },
            new LedgerEntry { Kind = LedgerEntryKind.Expense, SourceId = 2, CategoryId = 5, CategoryName = "Food", AccountId = 1, AccountName = "Checking" }
        ]);

        _sut.Categories.Should().Equal(new FilterOption(5, "Food"));
        _sut.Accounts.Select(a => a.Name).Should().BeEquivalentTo("Checking", "Savings");
    }
}
