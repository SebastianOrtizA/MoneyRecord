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
        _sut.SelectedCategories = [new FilterOption(3, "Food")];
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
        _sut.SetOptions([new LedgerEntry { Kind = LedgerEntryKind.Expense, SourceId = 1, AccountId = 5, AccountName = "Bank" }]);
        _sut.AccountChipText.Should().Be(AppResources.FilterByAccount);
        _sut.IsAccountActive.Should().BeFalse();

        PickInList("Bank");
        await _sut.PickAccountCommand.ExecuteAsync(null);

        _sut.AccountChipText.Should().Be("Bank");
        _sut.IsAccountActive.Should().BeTrue();
        _sut.ToFilter(ascending: false).AccountId.Should().Be(5);
        _changes.Should().Be(1);
    }

    [Fact]
    public async Task A_pick_raises_Picked_but_cancelling_or_typing_an_amount_does_not()
    {
        _sut.SetOptions([new LedgerEntry { Kind = LedgerEntryKind.Expense, SourceId = 1, AccountId = 5, AccountName = "Bank" }]);
        var picked = 0;
        _sut.Picked += () => picked++;

        PickInList(null);
        await _sut.PickAccountCommand.ExecuteAsync(null);
        _sut.MinAmountText = "10";
        picked.Should().Be(0);

        PickInList("Bank");
        await _sut.PickAccountCommand.ExecuteAsync(null);
        picked.Should().Be(1);
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
    public void Clear_also_resets_the_tag_and_the_amount()
    {
        _sut.SelectedTag = "work";
        _sut.MinAmountText = "10";

        _sut.ClearCommand.Execute(null);

        _sut.SelectedTag.Should().BeNull();
        _sut.ActiveCount.Should().Be(0);
        _sut.AmountChipText.Should().Be(AppResources.FilterByAmount);
    }

    [Fact]
    public void The_active_count_counts_each_filter_once_and_leaves_the_search_out()
    {
        _sut.SearchText = "bread";
        _sut.ActiveCount.Should().Be(0);

        _sut.SelectedTag = "work";
        _sut.MinAmountText = "10";
        _sut.MaxAmountText = "50";

        _sut.ActiveCount.Should().Be(2, "the tag, and the amount range as one");
    }

    [Fact]
    public void Every_filter_that_is_on_is_a_chip_search_included()
    {
        _sut.SetOptions([new LedgerEntry { Kind = LedgerEntryKind.Expense, SourceId = 1, CategoryId = 5, CategoryName = "Food", Tags = "work" }]);
        _sut.ActiveChips.Should().BeEmpty();

        _sut.SearchText = "bread";
        _sut.SelectedTag = "work";   // raises Changed at once, and the chips follow
        _sut.SelectedCategories = [_sut.Categories[0]];

        _sut.ActiveChips.Select(c => c.Kind).Should().Equal(ActiveFilterKind.Search, ActiveFilterKind.Category, ActiveFilterKind.Tag);
        _sut.ActiveChips[0].Text.Should().Contain("bread");
        _sut.ActiveChips[1].Text.Should().Be("Food");
    }

    [Fact]
    public void A_chips_cross_turns_that_filter_off_at_once_and_leaves_the_others()
    {
        _sut.SetOptions([new LedgerEntry { Kind = LedgerEntryKind.Expense, SourceId = 1, CategoryId = 5, CategoryName = "Food", Tags = "work" }]);
        _sut.SelectedTag = "work";
        _sut.SelectedCategories = [_sut.Categories[0]];
        _sut.MinAmountText = "10";
        TypingPause();
        _changes = 0;

        _sut.RemoveChipCommand.Execute(_sut.ActiveChips.Single(c => c.Kind == ActiveFilterKind.Amount));

        _sut.MinAmountText.Should().BeEmpty();
        _sut.ActiveChips.Select(c => c.Kind).Should().Equal(ActiveFilterKind.Category, ActiveFilterKind.Tag);
        _changes.Should().BeGreaterThan(0, "no waiting for the typing pause");

        _sut.RemoveChipCommand.Execute(_sut.ActiveChips.Single(c => c.Kind == ActiveFilterKind.Tag));
        _sut.SelectedTag.Should().BeNull();
        _sut.SelectedCategories.Should().NotBeEmpty();
    }

    [Fact]
    public void ShowOnly_turns_every_other_filter_off_and_keeps_the_type_and_category_with_one_change()
    {
        _sut.SetOptions([new LedgerEntry { Kind = LedgerEntryKind.Expense, SourceId = 1, CategoryId = 5, CategoryName = "Food", Tags = "work" }]);
        _sut.SelectedTag = "work";
        _sut.MinAmountText = "10";
        _changes = 0;

        _sut.ShowOnly(TransactionType.Expense, [5]);

        _changes.Should().Be(1);
        _sut.SelectedType!.Type.Should().Be(TransactionType.Expense);
        _sut.SelectedCategories.Should().Equal(new FilterOption(5, "Food"));
        _sut.SelectedTag.Should().BeNull();
        _sut.IsAmountActive.Should().BeFalse();
    }

    [Fact]
    public void Ticking_categories_adds_and_removes_them_one_at_a_time_and_the_filter_matches_any()
    {
        _sut.SetOptions(
        [
            new LedgerEntry { Kind = LedgerEntryKind.Expense, SourceId = 1, CategoryId = 5, CategoryName = "Food" },
            new LedgerEntry { Kind = LedgerEntryKind.Expense, SourceId = 2, CategoryId = 6, CategoryName = "Fun" }
        ]);
        _changes = 0;

        _sut.ToggleCategoryCommand.Execute(_sut.CategoryChoices[0]);
        _sut.ToggleCategoryCommand.Execute(_sut.CategoryChoices[1]);

        _changes.Should().Be(2);
        _sut.CategoryChoices.Select(c => c.IsSelected).Should().Equal(true, true);
        _sut.ActiveChips.Select(c => c.Text).Should().Equal("Food", "Fun");
        _sut.ActiveCount.Should().Be(1, "the categories count as one filter");
        _sut.ToFilter(false).CategoryIds.Should().BeEquivalentTo([5, 6]);

        _sut.RemoveChipCommand.Execute(_sut.ActiveChips[0]);
        _sut.ToFilter(false).CategoryIds.Should().Equal(6);

        _sut.ToggleCategoryCommand.Execute(_sut.CategoryChoices[1]);
        _sut.ToFilter(false).CategoryIds.Should().BeNull();
    }

    [Fact]
    public void ShowOnly_can_filter_to_one_payee_and_its_chip_removes_it()
    {
        _sut.SetOptions([new LedgerEntry { Kind = LedgerEntryKind.Expense, SourceId = 1, PayeeId = 3, PayeeName = "Bakery" }]);

        _sut.ShowOnly(TransactionType.Expense, null, new FilterOption(3, "Bakery"));

        _sut.ToFilter(false).PayeeId.Should().Be(3);
        _sut.ActiveChips.Select(c => c.Kind).Should().Equal(ActiveFilterKind.Type, ActiveFilterKind.Payee);
        _sut.ActiveChips[1].Text.Should().Be("Bakery");
        _sut.ActiveCount.Should().Be(2);

        _sut.RemoveChipCommand.Execute(_sut.ActiveChips.Single(c => c.Kind == ActiveFilterKind.Payee));
        _sut.ToFilter(false).PayeeId.Should().BeNull();
    }

    [Fact]
    public void ShowOnly_keeps_the_payee_even_when_no_loaded_entry_has_it()
    {
        _sut.SetOptions([]);

        _sut.ShowOnly(null, null, new FilterOption(7, "Bakery"));

        _sut.ToFilter(false).PayeeId.Should().Be(7);
        _sut.IsPayeeActive.Should().BeTrue();
    }

    [Fact]
    public void ShowOnly_ignores_a_category_without_entries()
    {
        _sut.ShowOnly(TransactionType.Income, [42]);

        _sut.SelectedType!.Type.Should().Be(TransactionType.Income);
        _sut.IsCategoryActive.Should().BeFalse();
    }
}
