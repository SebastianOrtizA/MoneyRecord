using FluentAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public sealed class ReceiptDraftTests : IDisposable
{
    private readonly string _receipts = Path.Combine(Path.GetTempPath(), $"moneyrecord-draft-{Guid.NewGuid():N}");
    private readonly ReceiptDraft _draft;

    public ReceiptDraftTests()
    {
        Directory.CreateDirectory(_receipts);
        _draft = new ReceiptDraft(_receipts);
    }

    public void Dispose() => Directory.Delete(_receipts, recursive: true);

    [Fact]
    public void Cancel_DeletesTheUnsavedPhoto_KeepsTheSavedOne()
    {
        var saved = Photo("saved.jpg");
        var taken = Photo("taken.jpg");
        _draft.Load(saved);
        _draft.Set(taken);

        _draft.Discard();

        _draft.Path.Should().Be(saved);
        File.Exists(saved).Should().BeTrue();
        File.Exists(taken).Should().BeFalse();
    }

    [Fact]
    public void Replace_BeforeSaving_DeletesTheEarlierUnsavedPhoto()
    {
        var first = Photo("first.jpg");
        var second = Photo("second.jpg");
        _draft.Load(null);
        _draft.Set(first);

        _draft.Set(second);

        File.Exists(first).Should().BeFalse();
        File.Exists(second).Should().BeTrue();
    }

    [Fact]
    public void Remove_DeletesTheSavedReceiptOnlyOnSave()
    {
        var saved = Photo("saved.jpg");
        _draft.Load(saved);

        _draft.Remove();
        File.Exists(saved).Should().BeTrue("the change isn't saved yet");

        _draft.Commit();
        _draft.Path.Should().BeNull();
        File.Exists(saved).Should().BeFalse();
    }

    [Fact]
    public void ReplaceAndSave_DeletesTheOldReceipt_KeepsTheNewOne()
    {
        var saved = Photo("saved.jpg");
        var taken = Photo("taken.jpg");
        _draft.Load(saved);
        _draft.Set(taken);

        _draft.Commit();
        _draft.Discard(); // leaving the page after saving

        File.Exists(saved).Should().BeFalse();
        File.Exists(taken).Should().BeTrue();
        _draft.Path.Should().Be(taken);
    }

    [Fact]
    public void Load_DiscardsAnUnsavedPhotoFromBefore()
    {
        var taken = Photo("taken.jpg");
        _draft.Load(null);
        _draft.Set(taken);

        _draft.Load(null);

        File.Exists(taken).Should().BeFalse();
    }

    private string Photo(string name)
    {
        var path = Path.Combine(_receipts, name);
        File.WriteAllText(path, name);
        return path;
    }
}
