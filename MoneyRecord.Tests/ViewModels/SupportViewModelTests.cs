using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MoneyRecord.Tests.ViewModels;

public sealed class SupportViewModelTests
{
    private static readonly TipOption Small = new("tip_small", "$0.99");
    private static readonly TipOption Large = new("tip_large", "$4.99");

    private readonly ITipJar _tipJar = Substitute.For<ITipJar>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IErrorHandler _errors = Substitute.For<IErrorHandler>();
    private readonly SupportViewModel _sut;

    public SupportViewModelTests()
    {
        _tipJar.IsAvailable.Returns(true);
        _tipJar.GetTipsAsync().Returns(Task.FromResult<IReadOnlyList<TipOption>>([Small, Large]));
        _sut = new SupportViewModel(_tipJar, _dialogs, _errors);
    }

    [Fact]
    public async Task The_tips_are_listed_with_the_store_prices()
    {
        await _sut.LoadCommand.ExecuteAsync(null);

        _sut.Tips.Should().Equal(Small, Large);
        _sut.HasTips.Should().BeTrue();
        _sut.ShowTipsUnavailable.Should().BeFalse();
    }

    [Fact]
    public async Task A_store_that_sells_nothing_says_tips_are_unavailable_once_loaded()
    {
        _tipJar.GetTipsAsync().Returns(Task.FromResult<IReadOnlyList<TipOption>>([]));

        _sut.ShowTipsUnavailable.Should().BeFalse("nothing was asked yet");
        await _sut.LoadCommand.ExecuteAsync(null);

        _sut.ShowTipsUnavailable.Should().BeTrue();
        _sut.HasTips.Should().BeFalse();
    }

    [Fact]
    public async Task A_paid_tip_is_thanked_with_a_toast()
    {
        _tipJar.TipAsync(Small.ProductId).Returns(TipOutcome.Thanked);

        await _sut.TipCommand.ExecuteAsync(Small);

        await _dialogs.Received(1).ToastAsync(AppResources.SupportThanks);
        await _errors.DidNotReceiveWithAnyArgs().HandleAsync(default(string)!);
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task A_pending_tip_says_it_waits_for_the_payment()
    {
        _tipJar.TipAsync(Small.ProductId).Returns(TipOutcome.Pending);

        await _sut.TipCommand.ExecuteAsync(Small);

        await _dialogs.Received(1).ToastAsync(AppResources.TipPending);
    }

    [Fact]
    public async Task A_cancelled_tip_says_nothing()
    {
        _tipJar.TipAsync(Small.ProductId).Returns(TipOutcome.Cancelled);

        await _sut.TipCommand.ExecuteAsync(Small);

        await _dialogs.DidNotReceiveWithAnyArgs().ToastAsync(default!);
        await _errors.DidNotReceiveWithAnyArgs().HandleAsync(default(string)!);
    }

    [Fact]
    public async Task A_failed_tip_shows_the_localized_error()
    {
        _tipJar.TipAsync(Small.ProductId).Returns(TipOutcome.Failed);

        await _sut.TipCommand.ExecuteAsync(Small);

        await _errors.Received(1).HandleAsync(AppResources.FailedToTip);
        await _dialogs.DidNotReceiveWithAnyArgs().ToastAsync(default!);
    }

    [Fact]
    public async Task An_exception_goes_to_the_error_handler_and_frees_the_buttons()
    {
        var failure = new InvalidOperationException("billing");
        _tipJar.TipAsync(Small.ProductId).ThrowsAsync(failure);

        await _sut.TipCommand.ExecuteAsync(Small);

        await _errors.Received(1).HandleAsync(failure, AppResources.FailedToTip);
        _sut.IsBusy.Should().BeFalse();
        _sut.TipCommand.CanExecute(Small).Should().BeTrue();
    }

    [Fact]
    public async Task While_a_tip_is_being_paid_no_second_one_can_start()
    {
        var payment = new TaskCompletionSource<TipOutcome>();
        _tipJar.TipAsync(Small.ProductId).Returns(payment.Task);

        var first = _sut.TipCommand.ExecuteAsync(Small);

        _sut.IsBusy.Should().BeTrue();
        _sut.TipCommand.CanExecute(Large).Should().BeFalse();

        payment.SetResult(TipOutcome.Cancelled);
        await first;
        _sut.TipCommand.CanExecute(Large).Should().BeTrue();
        await _tipJar.Received(1).TipAsync(Arg.Any<string>());
    }
}
