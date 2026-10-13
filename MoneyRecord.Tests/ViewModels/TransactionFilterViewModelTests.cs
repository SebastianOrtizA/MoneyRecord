using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.ViewModels;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.ViewModels;

public class TransactionFilterViewModelTests
{
    private readonly FakeTimeProvider _clock = new();
    private readonly IDialogService _dialog = Substitute.For<IDialogService>();

    private readonly TransactionFilterViewModel _sut;
    private int _changes;

    public TransactionFilterViewModelTests()
    {
        _sut = new TransactionFilterViewModel(_dialog, timeProvider: _clock);
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

    private void PickInList(string? choice) =>
        _dialog.ActionSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(choice);

    [Fact]
    public async Task A_chip_shows_its_name_until_a_value_is_picked()
    {
        _sut.SetOptions([new LedgerEntry { Kind = LedgerEntryKind.Expense, SourceId = 1, CategoryId = 5, CategoryName = "Food" }]);
        _sut.CategoryChipText.Should().Be(AppResources.FilterByCategory);
        _sut.IsCategoryActive.Should().BeFalse();

        PickInList("Food");
        await _sut.PickCategoryCommand.ExecuteAsync(null);

        _sut.CategoryChipText.Should().Be("Food");
        _sut.IsCategoryActive.Should().BeTrue();
        _sut.ToFilter(ascending: false).CategoryId.Should().Be(5);
        _changes.Should().Be(1);
    }

    [Fact]
    public async Task Picking_all_clears_the_chip_and_cancel_keeps_it()
    {
        _sut.SetOptions([new LedgerEntry { Kind = LedgerEntryKind.Expense, SourceId = 1, Tags = "work" }]);
        _sut.SelectedTag = "work";

        PickInList(null);
        await _sut.PickTagCommand.ExecuteAsync(null);
        _sut.SelectedTag.Should().Be("work");

        PickInList(AppResources.AllTags);
        await _sut.PickTagCommand.ExecuteAsync(null);
        _sut.SelectedTag.Should().BeNull();
        _sut.TagChipText.Should().Be(AppResources.FilterByTag);
    }

    [Fact]
    public async Task Accounts_with_the_same_name_stay_apart()
    {
        _sut.SetOptions(
        [
            new LedgerEntry { Kind = LedgerEntryKind.Expense, SourceId = 1, AccountId = 1, AccountName = "Cash" },
            new LedgerEntry { Kind = LedgerEntryKind.Expense, SourceId = 2, AccountId = 2, AccountName = "Cash" }
        ]);

        PickInList("Cash (2)");
        await _sut.PickAccountCommand.ExecuteAsync(null);

        _sut.SelectedAccount!.Id.Should().Be(2);
    }

    [Theory]
    [InlineData("10", "", "≥ 10")]
    [InlineData("", "1500.5", "≤ 1,500.5")]
    [InlineData("10", "50", "10 – 50")]
    public void The_amount_chip_shows_the_range(string min, string max, string expected)
    {
        TestCulture.WithCulture("en-US", () =>
        {
            _sut.MinAmountText = min;
            _sut.MaxAmountText = max;

            _sut.AmountChipText.Should().Be(expected);
            _sut.IsAmountActive.Should().BeTrue();
        });
    }

    [Fact]
    public void Clear_also_resets_the_tag_and_hides_the_amount_row()
    {
        _sut.SelectedTag = "work";
        _sut.ToggleAmountCommand.Execute(null);

        _sut.ClearCommand.Execute(null);

        _sut.SelectedTag.Should().BeNull();
        _sut.IsAmountVisible.Should().BeFalse();
        _sut.AmountChipText.Should().Be(AppResources.FilterByAmount);
    }
}
