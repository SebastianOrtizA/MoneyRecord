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
    private readonly IErrorHandler _errors = Substitute.For<IErrorHandler>();
    private readonly Account _bank = new() { Id = 1, Name = "Bank", CurrencyCode = "USD" };
    private readonly PocketCoverPrompt _sut;

    public PocketCoverPromptTests()
    {
        _sut = new PocketCoverPrompt(_dialogs, _pockets, _errors);
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

    // ---- ExtraOutflow ----

    private static readonly DateTime CountedBefore = new(2026, 10, 17);

    [Fact]
    public void An_edit_on_the_same_account_is_charged_only_the_difference()
    {
        PocketCoverPrompt.ExtraOutflow(1800m, 1500m, Today, CountedBefore, sameAccount: true).Should().Be(300m);
        PocketCoverPrompt.ExtraOutflow(1000m, 1500m, Today, CountedBefore, sameAccount: true).Should().Be(-500m, "a smaller amount never asks");
    }

    [Fact]
    public void An_edit_moved_to_another_account_is_charged_in_full()
    {
        PocketCoverPrompt.ExtraOutflow(1800m, 1500m, Today, CountedBefore, sameAccount: false).Should().Be(1800m);
    }

    [Fact]
    public void An_original_dated_in_the_future_is_in_no_balance_so_the_new_amount_is_charged_in_full()
    {
        PocketCoverPrompt.ExtraOutflow(1800m, 1500m, CountedBefore, CountedBefore, sameAccount: true).Should().Be(1800m);
    }

    // ---- RecordAsync ----

    [Fact]
    public async Task Recording_covers_the_pockets_linked_to_the_saved_entry()
    {
        var takes = new[] { new PocketTake(10, 100m) };

        await _sut.RecordAsync(_bank.Id, new PocketCoverChoice(true, takes), Today, transactionId: 7);

        await _pockets.Received(1).CoverAsync(_bank.Id, takes, Today, 7, null);
    }

    [Fact]
    public async Task Recording_nothing_does_not_touch_the_pockets()
    {
        await _sut.RecordAsync(_bank.Id, PocketCoverChoice.KeepAsIs, Today, transferId: 3);

        await _pockets.DidNotReceiveWithAnyArgs().CoverAsync(default, default!, default, default, default);
    }

    [Fact]
    public async Task A_failed_recording_is_reported_not_thrown()
    {
        var error = new InvalidOperationException("data changed");
        _pockets.CoverAsync(Arg.Any<int>(), Arg.Any<IReadOnlyList<PocketTake>>(), Arg.Any<DateTime>(), Arg.Any<int?>(), Arg.Any<int?>())
            .Returns<Task>(_ => throw error);

        var act = () => _sut.RecordAsync(_bank.Id, new PocketCoverChoice(true, [new PocketTake(10, 100m)]), Today, transactionId: 7);

        await act.Should().NotThrowAsync();
        await _errors.Received(1).HandleAsync(error, AppResources.FailedToMovePocket);
    }
}
