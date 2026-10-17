using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using SQLite;

namespace MoneyRecord.Services
{
    public sealed class DebtService : IDebtService
    {
        private readonly DatabaseInitializer _dbInitializer;
        private readonly IAccountRepository _accountRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IPayeeRepository _payeeRepository;
        private readonly IPayeeService _payeeService;
        private readonly TimeProvider _time;

        public DebtService(DatabaseInitializer dbInitializer, IAccountRepository accountRepository,
            ICategoryRepository categoryRepository, IPayeeRepository payeeRepository, IPayeeService payeeService, TimeProvider time)
        {
            _dbInitializer = dbInitializer;
            _accountRepository = accountRepository;
            _categoryRepository = categoryRepository;
            _payeeRepository = payeeRepository;
            _payeeService = payeeService;
            _time = time;
        }

        public async Task<List<DebtSummary>> GetAllAsync()
        {
            var db = await _dbInitializer.GetConnectionAsync();
            var debts = await db.Table<Debt>().ToListAsync();
            if (debts.Count == 0)
                return [];

            var totals = (await DebtLedger.GetTotalsAsync(db)).ToDictionary(t => t.DebtId);
            var schedules = (await db.Table<RecurringTransaction>().Where(r => r.DebtId != null && r.IsActive).ToListAsync())
                .GroupBy(r => r.DebtId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.NextOccurrence).First());
            var accounts = (await _accountRepository.GetAllAsync()).ToDictionary(a => a.Id);
            var payees = (await _payeeRepository.GetAllAsync()).ToDictionary(p => p.Id, p => p.Name);
            var today = _time.GetLocalNow().Date;

            return debts
                .Select(d =>
                {
                    var total = totals.GetValueOrDefault(d.Id);
                    return new DebtSummary(d, payees.GetValueOrDefault(d.PayeeId, string.Empty),
                        MoneyFormatter.ResolveCurrencyCode(accounts.GetValueOrDefault(d.AccountId)?.CurrencyCode),
                        total?.TotalCents ?? 0, total?.PaidCents ?? 0, total?.InterestCents ?? 0,
                        schedules.GetValueOrDefault(d.Id), today);
                })
                .OrderBy(s => s.IsPaidOff)
                .ThenBy(s => s.Debt.DueDate ?? DateTime.MaxValue)
                .ThenBy(s => s.PersonName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public async Task<List<DebtMovement>> GetMovementsAsync(int debtId)
        {
            var db = await _dbInitializer.GetConnectionAsync();
            var debt = await db.FindAsync<Debt>(debtId)
                ?? throw new InvalidOperationException($"Debt {debtId} does not exist.");
            var accounts = (await _accountRepository.GetAllAsync()).ToDictionary(a => a.Id);
            var ledger = accounts.GetValueOrDefault(debt.AccountId);
            var currency = MoneyFormatter.ResolveCurrencyCode(ledger?.CurrencyCode);
            var name = (int id) => accounts.GetValueOrDefault(id)?.Name ?? string.Empty;

            var movements = new List<DebtMovement>();
            if (ledger is { InitialBalanceCents: not 0 })
            {
                movements.Add(new DebtMovement
                {
                    Kind = DebtMovementKind.Added, Direction = debt.Direction, Date = ledger.CreatedDate,
                    AmountCents = Math.Abs(ledger.InitialBalanceCents), CurrencyCode = currency
                });
            }

            var transfers = await db.Table<Transfer>()
                .Where(t => t.SourceAccountId == debt.AccountId || t.DestinationAccountId == debt.AccountId)
                .ToListAsync();
            foreach (var transfer in transfers)
            {
                var intoDebt = transfer.DestinationAccountId == debt.AccountId;
                // Lent: money into the debt = lent, out = paid back; borrowed: the other way round
                var added = intoDebt == (debt.Direction == DebtDirection.Lent);
                movements.Add(new DebtMovement
                {
                    Kind = added ? DebtMovementKind.Added : DebtMovementKind.Payment, Direction = debt.Direction,
                    Date = transfer.Date, CurrencyCode = currency, TransferId = transfer.Id,
                    AmountCents = Math.Abs(intoDebt ? transfer.DestinationAmountCents : transfer.AmountCents),
                    AccountName = name(intoDebt ? transfer.SourceAccountId : transfer.DestinationAccountId)
                });
            }

            foreach (var interest in await db.Table<Transaction>().Where(t => t.DebtId == debtId).ToListAsync())
            {
                movements.Add(new DebtMovement
                {
                    Kind = DebtMovementKind.Interest, Direction = debt.Direction, Date = interest.Date,
                    AmountCents = Math.Abs(interest.AmountCents), CurrencyCode = currency, TransactionId = interest.Id,
                    AccountName = interest.AccountId is { } id ? name(id) : string.Empty
                });
            }

            // A payment's interest is listed just above its payment (same date, inserted after it)
            return movements.OrderByDescending(m => m.Date).ThenByDescending(m => m.Kind == DebtMovementKind.Interest).ToList();
        }

        public async Task<int> AddAsync(DebtDraft draft, DebtScheduleDraft? schedule)
        {
            var cents = Money.ToCents(draft.Amount);
            if (cents <= 0)
                throw new ArgumentOutOfRangeException(nameof(draft), "A debt needs a positive amount.");
            var person = PayeeNames.Normalize(draft.PersonName);
            var payeeId = await _payeeService.ResolveAsync(person)
                ?? throw new ArgumentException("A debt needs a person.", nameof(draft));

            var debt = new Debt
            {
                PayeeId = payeeId,
                Direction = draft.Direction,
                Note = draft.Note.Trim(),
                DueDate = draft.DueDate?.Date,
                CreatedDate = _time.GetLocalNow().DateTime
            };
            try
            {
                await _dbInitializer.RunInTransactionAsync(db =>
                {
                    var ledger = new Account
                    {
                        Name = person,
                        IsDebt = true,
                        CurrencyCode = MoneyFormatter.ResolveCurrencyCode(draft.CurrencyCode),
                        IconCode = DebtSummary.IconCode,
                        AllowNegativeBalance = true,
                        CreatedDate = draft.Date,
                        // Owed already: the start is the balance it opens with (lent = owed to the user, positive)
                        InitialBalanceCents = draft.AccountId == null
                            ? (draft.Direction == DebtDirection.Lent ? cents : -cents)
                            : 0
                    };
                    db.Insert(ledger);
                    debt.AccountId = ledger.Id;
                    db.Insert(debt);

                    if (draft.AccountId is { } accountId)
                        DebtLedger.RecordAddition(db, debt, accountId, draft.Date, cents);
                    if (schedule != null)
                        SaveSchedule(db, debt, person, schedule);
                });
            }
            finally
            {
                _accountRepository.InvalidateCache();
                _categoryRepository.InvalidateCache();
            }
            return debt.Id;
        }

        public async Task UpdateAsync(int debtId, string personName, string note, DateTime? dueDate, DebtScheduleDraft? schedule)
        {
            var person = PayeeNames.Normalize(personName);
            var payeeId = await _payeeService.ResolveAsync(person)
                ?? throw new ArgumentException("A debt needs a person.", nameof(personName));
            try
            {
                await _dbInitializer.RunInTransactionAsync(db =>
                {
                    var debt = db.Find<Debt>(debtId)
                        ?? throw new InvalidOperationException($"Debt {debtId} does not exist.");
                    debt.PayeeId = payeeId;
                    debt.Note = note.Trim();
                    debt.DueDate = dueDate?.Date;
                    db.Update(debt);
                    db.Execute("UPDATE \"Account\" SET Name = ? WHERE Id = ?", person, debt.AccountId);

                    if (schedule != null)
                        SaveSchedule(db, debt, person, schedule);
                    else
                        db.Execute("DELETE FROM \"RecurringTransaction\" WHERE DebtId = ?", debtId);
                });
            }
            finally
            {
                _accountRepository.InvalidateCache();
                _categoryRepository.InvalidateCache();
            }
        }

        // The debt's one schedule: updated in place (a new first date restarts it), or created
        private static void SaveSchedule(SQLiteConnection db, Debt debt, string person, DebtScheduleDraft draft)
        {
            var cents = Money.ToCents(draft.Amount);
            var interestCents = Money.ToCents(draft.Interest);
            if (cents <= 0 || interestCents < 0)
                throw new ArgumentOutOfRangeException(nameof(draft), "A scheduled payment needs a positive amount.");
            DebtLedger.RealAccount(db, debt, draft.AccountId);

            var type = debt.Direction == DebtDirection.Borrowed ? TransactionType.Expense : TransactionType.Income;
            var rule = db.Table<RecurringTransaction>().FirstOrDefault(r => r.DebtId == debt.Id) ?? new RecurringTransaction
            {
                DebtId = debt.Id,
                CreatedDate = DateTime.Now
            };
            var first = draft.FirstDate.Date;
            if (rule.Id == 0 || rule.NextOccurrence.Date != first || !rule.IsActive)
            {
                rule.StartDate = first;
                rule.NextOccurrence = first;
            }
            rule.AccountId = draft.AccountId;
            rule.AmountCents = cents;
            rule.InterestCents = interestCents;
            rule.TransactionType = type;
            rule.CategoryId = interestCents > 0 ? DebtLedger.InterestCategoryId(db, type) : 0;
            rule.PayeeId = debt.PayeeId;
            rule.Description = ScheduleDescription(debt.Direction, person);
            rule.Frequency = draft.Frequency;
            rule.DayOfMonth = null;
            rule.EndDate = null;
            rule.IsActive = true;

            if (rule.Id == 0)
                db.Insert(rule);
            else
                db.Update(rule);
        }

        /// <summary>"Payment to Ana" (borrowed) or "Payment from Ana" (lent): the schedule's name in Upcoming and reminders.</summary>
        public static string ScheduleDescription(DebtDirection direction, string person) =>
            string.Format(direction == DebtDirection.Borrowed ? AppResources.DebtPaymentTo : AppResources.DebtPaymentFrom, person);

        public async Task<bool> RecordPaymentAsync(int debtId, int accountId, DateTime date, decimal amount, decimal interest)
        {
            var paidOff = false;
            try
            {
                await _dbInitializer.RunInTransactionAsync(db =>
                {
                    var debt = db.Find<Debt>(debtId)
                        ?? throw new InvalidOperationException($"Debt {debtId} does not exist.");
                    paidOff = DebtLedger.RecordPayment(db, debt, accountId, date, Money.ToCents(amount), Money.ToCents(interest));
                });
            }
            finally
            {
                _categoryRepository.InvalidateCache();
            }
            return paidOff;
        }

        public Task AddToDebtAsync(int debtId, int accountId, DateTime date, decimal amount) =>
            _dbInitializer.RunInTransactionAsync(db =>
            {
                var debt = db.Find<Debt>(debtId)
                    ?? throw new InvalidOperationException($"Debt {debtId} does not exist.");
                DebtLedger.RecordAddition(db, debt, accountId, date, Money.ToCents(amount));
            });

        public Task DeleteMovementAsync(DebtMovement movement) =>
            _dbInitializer.RunInTransactionAsync(db =>
            {
                if (movement.TransferId is { } transferId)
                    db.Delete<Transfer>(transferId);
                if (movement.TransactionId is { } transactionId)
                    db.Delete<Transaction>(transactionId);
            });

        public async Task DeleteAsync(int debtId)
        {
            try
            {
                await _dbInitializer.RunInTransactionAsync(db =>
                {
                    var debt = db.Find<Debt>(debtId)
                        ?? throw new InvalidOperationException($"Debt {debtId} does not exist.");
                    db.Execute("DELETE FROM \"Transfer\" WHERE SourceAccountId = ? OR DestinationAccountId = ?", debt.AccountId, debt.AccountId);
                    db.Execute("DELETE FROM \"Transaction\" WHERE DebtId = ?", debtId);
                    db.Execute("DELETE FROM \"RecurringTransaction\" WHERE DebtId = ?", debtId);
                    db.Delete<Debt>(debtId);
                    db.Delete<Account>(debt.AccountId);
                });
            }
            finally
            {
                _accountRepository.InvalidateCache();
            }
        }
    }
}
