using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Records Cash on Hand deposited into a bank account.
/// GL pair under a BankDeposit journal entry: Debit destination bank account / Credit Cash on Hand.
/// </summary>
public class BankDepositService : IBankDepositService
{
    private readonly IAccountRepository _accountRepo;
    private readonly IGLRepository _glRepo;
    private readonly IJournalEntryRepository _journalRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizer _localizer;

    public BankDepositService(
        IAccountRepository accountRepo,
        IGLRepository glRepo,
        IJournalEntryRepository journalRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork,
        ILocalizer localizer)
    {
        _accountRepo = accountRepo;
        _glRepo = glRepo;
        _journalRepo = journalRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    public async Task RecordDepositAsync(RecordBankDepositRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0m)
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_BankDeposit_AmountPositive"),
                nameof(Transaction), nameof(RecordDepositAsync));

        var all = await _accountRepo.GetAllAsync(ct);

        var toAccount = all.FirstOrDefault(a => a.Id == request.ToAccountId)
            ?? throw new EntityNotFoundException(nameof(Account), request.ToAccountId, nameof(RecordDepositAsync));

        if (!toAccount.IsBankAccount)
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_BankDeposit_TargetMustBeBank"),
                nameof(Account), nameof(RecordDepositAsync));

        if (toAccount.Id == SystemAccounts.CashId)
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_BankDeposit_TargetNotCashOnHand"),
                nameof(Account), nameof(RecordDepositAsync));

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;
            var description = string.IsNullOrWhiteSpace(request.Description)
                ? $"Bank deposit — {toAccount.Name}"
                : request.Description.Trim();

            var entry = await _journalRepo.AddAsync(new JournalEntry
            {
                Id = Guid.NewGuid(),
                Type = JournalEntryType.BankDeposit,
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
                    AccountId = SystemAccounts.CashId,
                    DebitAmount = 0m,
                    CreditAmount = request.Amount,
                    GLAccount = SystemAccounts.CashNumber,
                    JournalEntryId = entry.Id,
                    Description = description,
                    CreatedAt = now
                }
            }, innerCt);

            await _audit.LogAsync(
                nameof(JournalEntry), entry.Id, AuditAction.Create,
                oldValue: null,
                newValue: $"Bank deposit {request.Amount:C} to '{toAccount.Name}' on {request.Date:yyyy-MM-dd}",
                ct: innerCt);

        }, ct);
    }
}
