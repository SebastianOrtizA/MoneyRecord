using System.Security.Cryptography;
using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public sealed class AttachmentHashesTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"moneyrecord-hash-{Guid.NewGuid():N}");
    private DatabaseInitializer _database = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_folder);
        _database = new DatabaseInitializer(new AppPaths(_folder));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await (await _database.GetConnectionAsync()).CloseAsync();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task Files_on_this_device_get_their_sha256_and_size_once()
    {
        var db = await _database.GetConnectionAsync();
        var file = Path.Combine(_folder, "receipt.jpg");
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(file, bytes);
        await db.InsertAsync(new TransactionAttachment { TransactionId = 1, Path = file });
        await db.InsertAsync(new TransactionAttachment { TransactionId = 1, Path = Path.Combine(_folder, "missing.jpg") });
        await db.InsertAsync(new TransactionAttachment { TransactionId = 1, Path = "" }); // a receipt that arrived by sync

        (await AttachmentHashes.FillAsync(_database)).Should().Be(1);

        var rows = await db.Table<TransactionAttachment>().OrderBy(a => a.Id).ToListAsync();
        rows[0].ContentHash.Should().Be(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        rows[0].SizeBytes.Should().Be(5);
        rows[1].ContentHash.Should().BeNull();
        rows[2].ContentHash.Should().BeNull();
        (await AttachmentHashes.FillAsync(_database)).Should().Be(0);
    }
}
