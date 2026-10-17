using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;

namespace MoneyRecord.Services
{
    public sealed class SplitService : ISplitService
    {
        private readonly DatabaseInitializer _dbInitializer;

        public SplitService(DatabaseInitializer dbInitializer)
        {
            _dbInitializer = dbInitializer;
        }

        public async Task<List<Transaction>> SaveAsync(Transaction template, IReadOnlyList<SplitPart> parts,
            IReadOnlyCollection<int> replacedIds)
        {
            if (parts.Count == 0)
                throw new ArgumentException("A payment needs at least one part.", nameof(parts));
            if (parts.Any(p => p.AmountCents <= 0))
                throw new ArgumentException("Every part needs a positive amount.", nameof(parts));

            var splitId = parts.Count > 1 ? template.SplitId ?? Guid.NewGuid().ToString("N") : null;
            var converted = template.ConvertedAmountCents is { } total
                ? SplitAllocation.Share(total, parts.Select(p => p.AmountCents).ToList())
                : null;

            var saved = parts.Select((part, i) => new Transaction
            {
                Date = template.Date,
                Type = template.Type,
                AccountId = template.AccountId,
                PayeeId = template.PayeeId,
                Tags = template.Tags,
                ReceiptPhotoPath = template.ReceiptPhotoPath,
                ConvertedAmountCents = converted?[i],
                ConvertedCurrencyCode = converted == null ? null : template.ConvertedCurrencyCode,
                SplitId = splitId,
                TripId = template.TripId,
                TripCategoryId = template.TripCategoryId,
                CategoryId = part.CategoryId,
                AmountCents = part.AmountCents,
                Description = part.Description
            }).ToList();

            await _dbInitializer.RunInTransactionAsync(db =>
            {
                foreach (var id in replacedIds)
                    db.Delete<Transaction>(id);
                foreach (var transaction in saved)
                    db.Insert(transaction);
            });
            return saved;
        }
    }
}
