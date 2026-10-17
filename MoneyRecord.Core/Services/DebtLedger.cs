using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using SQLite;

namespace MoneyRecord.Services
{
    /// <summary>
    /// How a debt's money moves, inside a database transaction: what is left, and recording a payment (a transfer between
    /// a real account and the debt's hidden account, plus its interest). Shared by <see cref="DebtService"/> and the
    /// recurring processing (scheduled payments), so both follow one rule.
    /// </summary>
    public static class DebtLedger
    {
        public const string InterestIconCode = "F03F0"; // percent

        /// <summary>Lent / borrowed so far (the start plus additions) and paid back, in cents, per debt.</summary>
        public sealed class Totals
        {
            public int DebtId { get; set; }
            public DebtDirection Direction { get; set; }
            public long InitialCents { get; set; }
            public long InCents { get; set; }
            public long OutCents { get; set; }
            public long InterestCents { get; set; }

            // Lent: the hidden account's balance is what the person owes (money in = lent, out = paid back);
            // borrowed: its negative is what the user owes (money out = borrowed, in = paid back)
            public long TotalCents => Direction == DebtDirection.Lent ? InitialCents + InCents : -InitialCents + OutCents;

            public long PaidCents => Direction == DebtDirection.Lent ? OutCents : InCents;

            public long OutstandingCents => Math.Max(0, TotalCents - PaidCents);
        }

        private const string TotalsSql =
            "SELECT d.Id AS DebtId, d.Direction AS Direction, a.InitialBalanceCents AS InitialCents, " +
            "IFNULL((SELECT SUM(ABS(t.DestinationAmountCents)) FROM \"Transfer\" t WHERE t.DestinationAccountId = d.AccountId), 0) AS InCents, " +
            "IFNULL((SELECT SUM(ABS(t.AmountCents)) FROM \"Transfer\" t WHERE t.SourceAccountId = d.AccountId), 0) AS OutCents, " +
            "IFNULL((SELECT SUM(ABS(x.AmountCents)) FROM \"Transaction\" x WHERE x.DebtId = d.Id), 0) AS InterestCents " +
            "FROM \"Debt\" d JOIN \"Account\" a ON a.Id = d.AccountId";

        public static Task<List<Totals>> GetTotalsAsync(SQLiteAsyncConnection db) => db.QueryAsync<Totals>(TotalsSql);

        public static Totals GetTotals(SQLiteConnection db, int debtId) =>
            db.Query<Totals>(TotalsSql + " WHERE d.Id = ?", debtId).FirstOrDefault()
            ?? throw new InvalidOperationException($"Debt {debtId} does not exist.");

        /// <summary>
        /// Records a payment of <paramref name="cents"/> (at most what is left) through <paramref name="accountId"/>, and its
        /// interest as an expense (borrowed) or income (lent) in the "Interest" category. A payment that settles the debt
        /// stops its schedule. Returns true when the debt is now paid off.
        /// </summary>
        public static bool RecordPayment(SQLiteConnection db, Debt debt, int accountId, DateTime date, long cents, long interestCents)
        {
            if (cents <= 0 || interestCents < 0)
                throw new ArgumentOutOfRangeException(nameof(cents), "A payment needs a positive amount.");
            var left = GetTotals(db, debt.Id).OutstandingCents;
            if (cents > left)
                throw new InvalidOperationException($"Debt {debt.Id} has {left} cents left, less than the payment of {cents}.");

            var account = RealAccount(db, debt, accountId);
            // Lent: the person pays into the user's account; borrowed: the user pays out of it
            Insert(db, debt, account, date, cents, paysIntoDebt: debt.Direction == DebtDirection.Borrowed);

            if (interestCents > 0)
            {
                var type = debt.Direction == DebtDirection.Borrowed ? TransactionType.Expense : TransactionType.Income;
                db.Insert(new Transaction
                {
                    Date = date,
                    AmountCents = interestCents,
                    Type = type,
                    CategoryId = InterestCategoryId(db, type),
                    AccountId = account.Id,
                    PayeeId = debt.PayeeId,
                    DebtId = debt.Id,
                    Description = AppResources.DebtInterest
                });
            }

            if (cents < left)
                return false;
            db.Execute("UPDATE \"RecurringTransaction\" SET IsActive = 0 WHERE DebtId = ?", debt.Id);
            return true;
        }

        /// <summary>More lent or borrowed through <paramref name="accountId"/> (the start, or more later).</summary>
        public static void RecordAddition(SQLiteConnection db, Debt debt, int accountId, DateTime date, long cents)
        {
            if (cents <= 0)
                throw new ArgumentOutOfRangeException(nameof(cents), "An amount must be positive.");
            Insert(db, debt, RealAccount(db, debt, accountId), date, cents, paysIntoDebt: debt.Direction == DebtDirection.Lent);
        }

        private static void Insert(SQLiteConnection db, Debt debt, Account account, DateTime date, long cents, bool paysIntoDebt) =>
            db.Insert(new Transfer
            {
                Date = date,
                AmountCents = cents,
                DestinationAmountCents = cents,
                SourceAccountId = paysIntoDebt ? account.Id : debt.AccountId,
                DestinationAccountId = paysIntoDebt ? debt.AccountId : account.Id
            });

        /// <summary>One of the user's accounts, in the debt's currency (its money can't change currency on the way).</summary>
        internal static Account RealAccount(SQLiteConnection db, Debt debt, int accountId)
        {
            var account = db.Find<Account>(accountId);
            if (account == null || account.IsDebt)
                throw new InvalidOperationException($"Account {accountId} is not one of the user's accounts.");
            var ledger = db.Find<Account>(debt.AccountId)
                ?? throw new InvalidOperationException($"Debt {debt.Id} has lost its account.");
            if (!MoneyFormatter.SameCurrency(account.CurrencyCode, ledger.CurrencyCode))
                throw new InvalidOperationException($"Account {accountId} is not in the debt's currency {ledger.CurrencyCode}.");
            return account;
        }

        /// <summary>The "Interest" category of that type (by its name in the current language), created when there is none.</summary>
        public static int InterestCategoryId(SQLiteConnection db, TransactionType type)
        {
            var categoryType = type == TransactionType.Income ? CategoryType.Income : CategoryType.Expense;
            var name = AppResources.InterestCategory;
            var existing = db.Table<Category>().Where(c => c.Type == categoryType).ToList()
                .FirstOrDefault(c => string.Equals(c.Name.Trim(), name, StringComparison.CurrentCultureIgnoreCase));
            if (existing != null)
                return existing.Id;

            var category = new Category { Name = name, Type = categoryType, IconCode = InterestIconCode };
            db.Insert(category);
            return category.Id;
        }
    }
}
