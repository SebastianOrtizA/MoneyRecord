using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
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
    public void Cancel_DeletesTheUnsavedFiles_KeepsTheSavedOnes()
    {
        var saved = File("saved.jpg");
        var taken = File("taken.jpg");
        _draft.Load([saved]);
        _draft.Add(taken);

        _draft.Discard();

        _draft.Items.Should().Equal(saved);
        System.IO.File.Exists(saved.Path).Should().BeTrue();
        System.IO.File.Exists(taken.Path).Should().BeFalse();
    }

    [Fact]
    public void Several_files_can_be_added_and_all_are_kept_on_save()
    {
        var photo = File("a.jpg");
        var pdf = File("invoice.pdf");
        _draft.Load(null);
        _draft.Add(photo);
        _draft.Add(pdf);

        _draft.Commit();
        _draft.Discard(); // leaving the page after saving

        _draft.Items.Should().Equal(photo, pdf);
        System.IO.File.Exists(photo.Path).Should().BeTrue();
        System.IO.File.Exists(pdf.Path).Should().BeTrue();
    }

    [Fact]
    public void Removing_an_unsaved_file_deletes_it_at_once()
    {
        var taken = File("taken.jpg");
        _draft.Load(null);
        _draft.Add(taken);

        _draft.Remove(taken);

        _draft.Items.Should().BeEmpty();
        System.IO.File.Exists(taken.Path).Should().BeFalse();
    }

    [Fact]
    public void Removing_a_saved_file_deletes_it_only_on_save()
    {
        var saved = File("saved.jpg");
        var other = File("other.pdf");
        _draft.Load([saved, other]);

        _draft.Remove(saved);
        System.IO.File.Exists(saved.Path).Should().BeTrue("the change isn't saved yet");

        _draft.Commit();
        _draft.Items.Should().Equal(other);
        System.IO.File.Exists(saved.Path).Should().BeFalse();
        System.IO.File.Exists(other.Path).Should().BeTrue();
    }

    [Fact]
    public void Removing_a_saved_file_and_cancelling_brings_it_back()
    {
        var saved = File("saved.jpg");
        _draft.Load([saved]);
        _draft.Remove(saved);

        _draft.Discard();

        _draft.Items.Should().Equal(saved);
    }

    [Fact]
    public void Load_DiscardsAnUnsavedFileFromBefore()
    {
        var taken = File("taken.jpg");
        _draft.Load(null);
        _draft.Add(taken);

        _draft.Load(null);

        System.IO.File.Exists(taken.Path).Should().BeFalse();
    }

    private TransactionAttachment File(string name)
    {
        var path = Path.Combine(_receipts, name);
        System.IO.File.WriteAllText(path, name);
        return new TransactionAttachment { Path = path, Name = name, Kind = TransactionAttachments.KindOf(name) };
    }
}
