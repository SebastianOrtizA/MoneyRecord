using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.ViewModels;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.ViewModels;

public sealed class PocketCoverPromptTests
{
    private static readonly DateTime Today = new(2026, 10, 16);

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IPocketService _pockets = Substitute.For<IPocketService>();
    private readonly Account _bank = new() { Id = 1, Name = "Bank", CurrencyCode = "USD" };
    private readonly PocketCoverPrompt _sut;

    public PocketCoverPromptTests()
    {
        _sut = new PocketCoverPrompt(_dialogs, _pockets);
    }

    private PocketSummary Pocket(int id, string name, long cents) =>
        new(new Pocket { Id = id, AccountId = _bank.Id, Name = name }, _bank, cents);

    // Shortfall as the service would report it: the pockets' total minus what stays in the account
    private void Setup(decimal shortfall, params PocketSummary[] pockets)
    {
        _pockets.GetShortfallAsync(_bank.Id, Arg.Any<decimal>(), Arg.Any<DateTime>()).Returns(shortfall);
        _pockets.GetPocketsAsync(_bank.Id).Returns(pockets.ToList());
    }

    private static string Label(PocketSummary p) => $"{p.Pocket.Name} ({p.BalanceText})";

    [Fact]
    public async Task No_shortfall_never_asks()
    {
        Setup(0m, Pocket(10, "Travel", 50000));

        var choice = await _sut.AskAsync(_bank.Id, 100m, Today);

        choice.Proceed.Should().BeTrue();
        choice.Takes.Should().BeEmpty();
        await _dialogs.DidNotReceiveWithAnyArgs().ActionSheetAsync(default!, default!, default!);
    }

    [Fact]
    public async Task An_outflow_of_zero_or_less_never_asks()
    {
        Setup(300m, Pocket(10, "Travel", 50000));

        (await _sut.AskAsync(_bank.Id, 0m, Today)).Proceed.Should().BeTrue();
        (await _sut.AskAsync(_bank.Id, -20m, Today)).Takes.Should().BeEmpty();

        await _dialogs.DidNotReceiveWithAnyArgs().ActionSheetAsync(default!, default!, default!);
    }

    [Fact]
    public async Task Picking_a_pocket_takes_the_shortfall_from_it()
    {
        var travel = Pocket(10, "Travel", 50000);
        var rainy = Pocket(11, "Rainy", 20000);
        Setup(300m, travel, rainy);
        _dialogs.ActionSheetAsync(Arg.Any<string>(), AppResources.Cancel, Arg.Any<IReadOnlyList<string>>()).Returns(Label(travel));

        var choice = await _sut.AskAsync(_bank.Id, 1800m, Today);

        choice.Proceed.Should().BeTrue();
        choice.Takes.Should().Equal(new PocketTake(10, 300m));
        await _dialogs.Received(1).ActionSheetAsync(Arg.Any<string>(), AppResources.Cancel,
            Arg.Is<IReadOnlyList<string>>(o => o.SequenceEqual(new[] { Label(travel), Label(rainy), AppResources.PocketCoverKeep })));
    }

    [Fact]
    public async Task A_pocket_that_cannot_cover_it_all_asks_again_with_the_rest()
    {
        var small = Pocket(10, "Small", 10000);
        var big = Pocket(11, "Big", 50000);
        Setup(300m, small, big);
        _dialogs.ActionSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(Label(small), Label(big));

        var choice = await _sut.AskAsync(_bank.Id, 1800m, Today);

        choice.Takes.Should().Equal(new PocketTake(10, 100m), new PocketTake(11, 200m));
        await _dialogs.Received(2).ActionSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>());
        await _dialogs.Received(1).ActionSheetAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(o => o.SequenceEqual(new[] { Label(big), AppResources.PocketCoverKeep })));
    }

    [Fact]
    public async Task Keeping_the_pockets_proceeds_without_take_outs()
    {
        Setup(300m, Pocket(10, "Travel", 50000));
        _dialogs.ActionSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(AppResources.PocketCoverKeep);

        var choice = await _sut.AskAsync(_bank.Id, 1800m, Today);

        choice.Proceed.Should().BeTrue();
        choice.Takes.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelling_saves_nothing()
    {
        Setup(300m, Pocket(10, "Travel", 50000));
        _dialogs.ActionSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns((string?)null);

        var choice = await _sut.AskAsync(_bank.Id, 1800m, Today);

        choice.Proceed.Should().BeFalse();
    }

    [Fact]
    public async Task Only_what_this_entry_adds_is_asked_when_the_pockets_were_already_over_the_balance()
    {
        var travel = Pocket(10, "Travel", 50000);
        // The account is already 400 short; this 100 expense adds 100 of it
        Setup(500m, travel);
        _dialogs.ActionSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns(Label(travel));

        var choice = await _sut.AskAsync(_bank.Id, 100m, Today);

        choice.Takes.Should().Equal(new PocketTake(10, 100m));
    }

    [Fact]
    public async Task Pockets_with_the_same_name_get_distinct_labels()
    {
        var first = Pocket(10, "Trip", 30000);
        var second = Pocket(11, "Trip", 30000);
        Setup(100m, first, second);
        _dialogs.ActionSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns($"{Label(second)} (2)");

        var choice = await _sut.AskAsync(_bank.Id, 100m, Today);

        choice.Takes.Should().Equal(new PocketTake(11, 100m));
    }
}
