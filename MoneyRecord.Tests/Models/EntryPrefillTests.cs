using AwesomeAssertions;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Models;

public class EntryPrefillTests
{
    [Fact]
    public void From_copies_only_the_repeatable_fields()
    {
        var original = new Transaction
        {
            Date = new DateTime(2026, 10, 1),
            AmountCents = 350,
            CategoryId = 4,
            AccountId = 2,
            PayeeId = 8,
            Tags = "coffee,work",
            Description = "Latte",
            ReceiptPhotoPath = "/receipts/a.jpg",
            SplitId = "split-1",
            TripId = 3,
        };

        var prefill = EntryPrefill.From(original);

        prefill.Should().Be(new EntryPrefill(350, 4, 2, 8, "coffee,work", "Latte"));
    }
}
