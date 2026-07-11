using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Transfers funds between two bank/cash accounts.
/// GL pair under a Transfer journal entry: Debit destination / Credit source.
/// </summary>
public class AccountTransferService : IAccountTransferService
{
    private readonly IAccountRepository _accountRepo;
    private readonly IGLRepository _glRepo;
    private readonly IJournalEntryRepository _journalRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public AccountTransferService(
        IAccountRepository accountRepo,
        IGLRepository glRepo,
        IJournalEntryRepository journalRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _accountRepo = accountRepo;
        _glRepo = glRepo;
        _journalRepo = journalRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task RecordTransferAsync(RecordTransferRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0m)
            throw new ValidationException(
                "Transfer amount must be greater than zero.", nameof(Transaction), nameof(RecordTransferAsync));

        if (request.FromAccountId == request.ToAccountId)
            throw new ValidationException(
                "The source and destination accounts must differ.", nameof(Account), nameof(RecordTransferAsync));

        var all = await _accountRepo.GetAllAsync(ct);

        var fromAccount = all.FirstOrDefault(a => a.Id == request.FromAccountId)
            ?? throw new EntityNotFoundException(nameof(Account), request.FromAccountId, nameof(RecordTransferAsync));

        var toAccount = all.FirstOrDefault(a => a.Id == request.ToAccountId)
            ?? throw new EntityNotFoundException(nameof(Account), request.ToAccountId, nameof(RecordTransferAsync));

        if (!fromAccount.IsBankAccount || !toAccount.IsBankAccount)
            throw new ValidationException(
                "Transfers are only permitted between bank/cash accounts.", nameof(Account), nameof(RecordTransferAsync));

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;
            var description = string.IsNullOrWhiteSpace(request.Description)
                ? $"Transfer — {fromAccount.Name} to {toAccount.Name}"
                : request.Description.Trim();

            var entry = await _journalRepo.AddAsync(new JournalEntry
            {
                Id = Guid.NewGuid(),
                Type = JournalEntryType.Transfer,
                Date = request.Date,
                Description = description,
                CreatedAt = now
            }, innerCt);

            await _glRepo.AddBalancedSetAsync(new[]
            {
                new Transaction
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    AccountId = toAccount.Id,
                    DebitAmount = request.Amount,
                    CreditAmount = 0m,
                    GLAccount = toAccount.AccountNumber,
                    JournalEntryId = entry.Id,
                    Description = description,
                    CreatedAt = now
                },
                new Transaction
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    AccountId = fromAccount.Id,
                    DebitAmount = 0m,
                    CreditAmount = request.Amount,
                    GLAccount = fromAccount.AccountNumber,
                    JournalEntryId = entry.Id,
                    Description = description,
                    CreatedAt = now
                }
            }, innerCt);

            await _audit.LogAsync(
                nameof(JournalEntry), entry.Id, AuditAction.Create,
                oldValue: null,
                newValue: $"Transfer {request.Amount:C} from '{fromAccount.Name}' to '{toAccount.Name}' on {request.Date:yyyy-MM-dd}",
                ct: innerCt);

        }, ct);
    }
}
