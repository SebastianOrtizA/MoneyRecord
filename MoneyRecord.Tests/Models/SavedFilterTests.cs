using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Models;

public class SavedFilterTests
{
    private static SavedFilter Full(string name = "Weekend food") => new(
        name, "pizza", TransactionType.Expense, [new FilterOption(3, "Food"), new FilterOption(7, "Eating out")],
        new FilterOption(2, "Cash"), 5.5m, 40m, "trip", new FilterOption(9, "Corner Bakery"));

    [Fact]
    public void Serialize_and_parse_round_trip()
    {
        var parsed = SavedFilters.Parse(SavedFilters.Serialize([Full(), new SavedFilter("Everything big", MinAmount: 100m)]));

        parsed.Should().HaveCount(2);
        parsed[0].Should().BeEquivalentTo(Full());
        parsed[1].Name.Should().Be("Everything big");
        parsed[1].Categories.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"a\":1}")]
    public void Parse_of_nothing_or_garbage_gives_no_presets(string? json) =>
        SavedFilters.Parse(json).Should().BeEmpty();

    [Fact]
    public void Parse_drops_nameless_presets_and_keeps_the_cap()
    {
        var many = Enumerable.Range(1, 25).Select(i => new SavedFilter($"F{i}")).Append(new SavedFilter(" ")).ToList();

        var parsed = SavedFilters.Parse(SavedFilters.Serialize(many));

        parsed.Should().HaveCount(SavedFilter.MaxCount);
        parsed.Should().OnlyContain(f => !string.IsNullOrWhiteSpace(f.Name));
    }

    [Fact]
    public void Add_refuses_a_21st_preset_but_replaces_one_with_the_same_name()
    {
        var list = Enumerable.Range(1, SavedFilter.MaxCount).Select(i => new SavedFilter($"F{i}")).ToList();

        SavedFilters.TryAdd(list, new SavedFilter("New")).Should().BeFalse();
        list.Should().HaveCount(SavedFilter.MaxCount);

        SavedFilters.TryAdd(list, new SavedFilter("f3", Tag: "x")).Should().BeTrue();
        list.Should().HaveCount(SavedFilter.MaxCount);
        list[2].Tag.Should().Be("x");
    }

    [Fact]
    public void Pruned_drops_what_no_longer_exists_and_says_so()
    {
        var saved = Full();

        var kept = saved.Pruned(new HashSet<int> { 3, 7 }, new HashSet<int> { 2 }, new HashSet<int> { 9 }, out var droppedNone);
        var pruned = saved.Pruned(new HashSet<int> { 7 }, new HashSet<int>(), new HashSet<int> { 9 }, out var dropped);
        var noCategories = saved.Pruned(new HashSet<int>(), new HashSet<int> { 2 }, new HashSet<int> { 9 }, out _);

        droppedNone.Should().BeFalse();
        kept.Should().BeEquivalentTo(saved);
        dropped.Should().BeTrue();
        pruned.Categories.Should().ContainSingle().Which.Id.Should().Be(7);
        pruned.Account.Should().BeNull();
        pruned.Payee!.Id.Should().Be(9);
        noCategories.Categories.Should().BeNull();
    }

    [Fact]
    public void Rename_refuses_a_name_in_use_by_another_preset()
    {
        var a = new SavedFilter("A");
        var b = new SavedFilter("B");
        var list = new List<SavedFilter> { a, b };

        SavedFilters.TryRename(list, b, "a").Should().BeFalse();
        SavedFilters.TryRename(list, b, "C").Should().BeTrue();
        SavedFilters.TryRename(list, a, "A").Should().BeTrue(); // its own name is fine
        list.Select(f => f.Name).Should().Equal("A", "C");
    }

    [Fact]
    public void Matches_compares_the_narrowing_filters_not_the_sort_or_the_category_order()
    {
        var saved = Full();

        saved.Matches(saved.ToFilter(ascending: false)).Should().BeTrue();
        saved.Matches(saved.ToFilter(ascending: true)).Should().BeTrue();
        saved.Matches(saved.ToFilter(false) with { CategoryIds = [7, 3], Tag = "TRIP", SearchText = " pizza " }).Should().BeTrue();
        saved.Matches(saved.ToFilter(false) with { MaxAmount = 41m }).Should().BeFalse();
        saved.Matches(saved.ToFilter(false) with { CategoryIds = [3] }).Should().BeFalse();
        saved.Matches(new TransactionFilter()).Should().BeFalse();
        new SavedFilter("Nothing").Matches(new TransactionFilter()).Should().BeTrue();
    }

    [Fact]
    public void The_store_keeps_presets_in_preferences()
    {
        var saved = new Dictionary<string, string>();
        var preferences = Substitute.For<IPreferencesService>();
        preferences.Get(Arg.Any<string>(), Arg.Any<string>()).Returns(call => saved.GetValueOrDefault(call.ArgAt<string>(0), call.ArgAt<string>(1)));
        preferences.When(p => p.Set(Arg.Any<string>(), Arg.Any<string>())).Do(call => saved[call.ArgAt<string>(0)] = call.ArgAt<string>(1));
        ISavedFiltersStore store = new SavedFiltersStore(preferences);

        store.Load().Should().BeEmpty();
        store.Save([Full()]);

        store.Load().Should().ContainSingle().Which.Should().BeEquivalentTo(Full());
    }
}
