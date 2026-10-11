using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class ErrorHandlerTests
{
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    [Fact]
    public async Task Shows_the_user_message_in_an_error_alert()
    {
        await new ErrorHandler(_dialogs).HandleAsync(new InvalidOperationException("db locked"), "Could not save");

        await _dialogs.Received(1).AlertAsync(AppResources.Error, "Could not save");
    }

    [Fact]
    public async Task Shows_a_message_without_an_exception()
    {
        await new ErrorHandler(_dialogs).HandleAsync("Please enter a valid amount");

        await _dialogs.Received(1).AlertAsync(AppResources.Error, "Please enter a valid amount");
    }
}
