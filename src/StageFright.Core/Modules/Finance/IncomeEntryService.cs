using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Records non-member income (raffles, donations, fundraising) directly to the GL.
/// GL pair: Debit Cash (0100) / Credit selected Income account.
/// </summary>
public class IncomeEntryService : IIncomeEntryService
{

    private readonly IAccountRepository _accountRepo;
    private readonly IGLRepository _glRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public IncomeEntryService(
        IAccountRepository accountRepo,
        IGLRepository glRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _accountRepo = accountRepo;
        _glRepo = glRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<Account>> GetIncomeAccountsAsync(CancellationToken ct = default)
    {
        var all = await _accountRepo.GetAllAsync(ct);
        return all
            .Where(c => c.Type == AccountType.Income && !c.IsSystem)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToList();
    }

    public async Task RecordIncomeAsync(RecordIncomeRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0m)
            throw new ValidationException(
                "Income amount must be greater than zero.", nameof(Transaction), nameof(RecordIncomeAsync));

        var all = await _accountRepo.GetAllAsync(ct);
        var account = all.FirstOrDefault(c => c.Id == request.AccountId)
            ?? throw new EntityNotFoundException(
                nameof(Account), request.AccountId, nameof(RecordIncomeAsync));

        if (account.Type != AccountType.Income)
            throw new ValidationException(
                "Selected account is not an Income account.", nameof(Account), nameof(RecordIncomeAsync));

        if (account.IsSystem)
            throw new ValidationException(
                "System accounts cannot be used for manual income entries.", nameof(Account), nameof(RecordIncomeAsync));

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;
            var description = string.IsNullOrWhiteSpace(request.Description)
                ? $"Income — {account.Name}"
                : request.Description.Trim();

            await _glRepo.AddPairAsync(
                new Transaction
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    AccountId = SystemAccounts.CashId,
                    DebitAmount = request.Amount,
                    CreditAmount = 0m,
                    GLAccount = SystemAccounts.CashNumber,
                    MemberId = null,
                    PaymentId = null,
                    FeeId = null,
                    Description = description,
                    CreatedAt = now
                },
                new Transaction
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    AccountId = account.Id,
                    DebitAmount = 0m,
                    CreditAmount = request.Amount,
                    GLAccount = account.AccountNumber,
                    MemberId = null,
                    PaymentId = null,
                    FeeId = null,
                    Description = description,
                    CreatedAt = now
                },
                innerCt);

            await _audit.LogAsync(
                nameof(Transaction), Guid.Empty, AuditAction.Create,
                oldValue: null,
                newValue: $"Other income {request.Amount:C} to account '{account.Name}' on {request.Date:yyyy-MM-dd}",
                ct: innerCt);

        }, ct);
    }
}
