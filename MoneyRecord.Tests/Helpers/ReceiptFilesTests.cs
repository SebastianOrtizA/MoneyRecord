using FluentAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public sealed class ReceiptFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"moneyrecord-receipts-{Guid.NewGuid():N}");
    private readonly string _receipts;

    public ReceiptFilesTests()
    {
        _receipts = Path.Combine(_root, "receipts");
        Directory.CreateDirectory(_receipts);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Delete_ReceiptInFolder_RemovesIt()
    {
        var file = Path.Combine(_receipts, "receipt_1.jpg");
        File.WriteAllText(file, "x");

        ReceiptFiles.Delete(file, _receipts).Should().BeTrue();
        File.Exists(file).Should().BeFalse();
    }

    [Fact]
    public void Delete_FileOutsideFolder_IsLeftAlone()
    {
        var outside = Path.Combine(_root, "moneyrecord.db3");
        File.WriteAllText(outside, "x");

        ReceiptFiles.Delete(outside, _receipts).Should().BeFalse();
        ReceiptFiles.Delete(Path.Combine(_receipts, "..", "moneyrecord.db3"), _receipts).Should().BeFalse();
        File.Exists(outside).Should().BeTrue();
    }

    [Fact]
    public void Delete_MissingOrEmptyPath_IsNotAnError()
    {
        ReceiptFiles.Delete(null, _receipts).Should().BeFalse();
        ReceiptFiles.Delete("", _receipts).Should().BeFalse();
        ReceiptFiles.Delete(Path.Combine(_receipts, "gone.jpg"), _receipts).Should().BeFalse();
    }
}
