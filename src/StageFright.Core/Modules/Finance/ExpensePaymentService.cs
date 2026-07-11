using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Records expense payments out of a bank/cash account.
/// GL pair under an ExpensePayment journal entry: Debit Expense / Credit Bank.
/// </summary>
public class ExpensePaymentService : IExpensePaymentService
{
    private readonly IAccountRepository _accountRepo;
    private readonly IGLRepository _glRepo;
    private readonly IJournalEntryRepository _journalRepo;
    private readonly ISettingsRepository _settingsRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public ExpensePaymentService(
        IAccountRepository accountRepo,
        IGLRepository glRepo,
        IJournalEntryRepository journalRepo,
        ISettingsRepository settingsRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _accountRepo = accountRepo;
        _glRepo = glRepo;
        _journalRepo = journalRepo;
        _settingsRepo = settingsRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<Account>> GetExpenseAccountsAsync(CancellationToken ct = default)
    {
        var all = await _accountRepo.GetAllAsync(ct);
        return all
            .Where(a => a.Type == AccountType.Expense && !a.IsSystem)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .ToList();
    }

    public async Task RecordExpenseAsync(RecordExpenseRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0m)
            throw new ValidationException(
                "Expense amount must be greater than zero.", nameof(Transaction), nameof(RecordExpenseAsync));

        var all = await _accountRepo.GetAllAsync(ct);

        var bankAccount = all.FirstOrDefault(a => a.Id == request.BankAccountId)
            ?? throw new EntityNotFoundException(nameof(Account), request.BankAccountId, nameof(RecordExpenseAsync));

        if (!bankAccount.IsBankAccount)
            throw new ValidationException(
                "The payment account must be a bank/cash account.", nameof(Account), nameof(RecordExpenseAsync));

        var expenseAccount = all.FirstOrDefault(a => a.Id == request.ExpenseAccountId)
            ?? throw new EntityNotFoundException(nameof(Account), request.ExpenseAccountId, nameof(RecordExpenseAsync));

        if (expenseAccount.Type != AccountType.Expense)
            throw new ValidationException(
                "Selected account is not an Expense account.", nameof(Account), nameof(RecordExpenseAsync));

        if (expenseAccount.IsSystem)
            throw new ValidationException(
                "System accounts cannot be used for manual expense payments.", nameof(Account), nameof(RecordExpenseAsync));

        var settings = await _settingsRepo.GetAsync(ct);
        var isRegistered = settings?.IsGstRegistered ?? false;
        var gstCode = isRegistered ? (request.GstCode ?? GstCode.GstFree) : (GstCode?)null;

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;
            var payee = string.IsNullOrWhiteSpace(request.Payee) ? null : request.Payee.Trim();
            var detail = string.IsNullOrWhiteSpace(request.Description)
                ? $"Expense — {expenseAccount.Name}"
                : request.Description.Trim();
            var description = payee is null ? detail : $"{payee} — {detail}";

            var entry = await _journalRepo.AddAsync(new JournalEntry
            {
                Id = Guid.NewGuid(),
                Type = JournalEntryType.ExpensePayment,
                Date = request.Date,
                Description = description,
                CreatedAt = now
            }, innerCt);

            // Taxable while registered: DR Expense net / DR GST Paid / CR Bank gross.
            // Otherwise a 2-line pair; unregistered postings carry no GST code at all.
            var (expenseAmount, gstAmount) = gstCode == GstCode.Gst
                ? GstCalculator.SplitInclusive(request.Amount)
                : (request.Amount, 0m);

            var lines = new List<Transaction>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    AccountId = expenseAccount.Id,
                    DebitAmount = expenseAmount,
                    CreditAmount = 0m,
                    GLAccount = expenseAccount.AccountNumber,
                    JournalEntryId = entry.Id,
                    GstCode = gstCode,
                    Description = description,
                    CreatedAt = now
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    AccountId = bankAccount.Id,
                    DebitAmount = 0m,
                    CreditAmount = request.Amount,
                    GLAccount = bankAccount.AccountNumber,
                    JournalEntryId = entry.Id,
                    GstCode = gstCode,
                    Description = description,
                    CreatedAt = now
                }
            };

            if (gstAmount != 0m)
            {
                lines.Add(new Transaction
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    AccountId = SystemAccounts.GstPaidId,
                    DebitAmount = gstAmount,
                    CreditAmount = 0m,
                    GLAccount = SystemAccounts.GstPaidNumber,
                    JournalEntryId = entry.Id,
                    GstCode = gstCode,
                    Description = $"GST paid — {description}",
                    CreatedAt = now
                });
            }

            await _glRepo.AddBalancedSetAsync(lines, innerCt);

            await _audit.LogAsync(
                nameof(JournalEntry), entry.Id, AuditAction.Create,
                oldValue: null,
                newValue: $"Expense {request.Amount:C} from '{bankAccount.Name}' to '{expenseAccount.Name}' on {request.Date:yyyy-MM-dd}",
                ct: innerCt);

        }, ct);
    }
}
