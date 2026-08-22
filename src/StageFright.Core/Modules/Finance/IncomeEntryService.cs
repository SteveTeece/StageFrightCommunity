using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Records non-member income (raffles, donations, fundraising) directly to the GL.
/// GL pair under an Income journal entry: Debit the chosen deposit bank account
/// (default Cash on Hand 1100) / Credit selected Income account.
/// </summary>
public class IncomeEntryService : IIncomeEntryService
{

    private readonly IAccountRepository _accountRepo;
    private readonly IGLRepository _glRepo;
    private readonly IJournalEntryRepository _journalRepo;
    private readonly ISettingsRepository _settingsRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public IncomeEntryService(
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

        var depositAccountId = request.DepositAccountId ?? SystemAccounts.CashId;
        var depositAccount = all.FirstOrDefault(a => a.Id == depositAccountId)
            ?? throw new EntityNotFoundException(
                nameof(Account), depositAccountId, nameof(RecordIncomeAsync));

        if (!depositAccount.IsBankAccount)
            throw new ValidationException(
                "The deposit account must be a bank/cash account.", nameof(Account), nameof(RecordIncomeAsync));

        var settings = await _settingsRepo.GetAsync(ct);
        var isTaxApplicable = settings?.IsTaxApplicable ?? false;
        var taxCode = isTaxApplicable ? (request.TaxCode ?? TaxCode.TaxExempt) : (TaxCode?)null;

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;
            var description = string.IsNullOrWhiteSpace(request.Description)
                ? $"Income — {account.Name}"
                : request.Description.Trim();

            var entry = await _journalRepo.AddAsync(new JournalEntry
            {
                Id = Guid.NewGuid(),
                Type = JournalEntryType.Income,
                Date = request.Date,
                Description = description,
                CreatedAt = now
            }, innerCt);

            // Taxable while tax applies: DR Bank gross / CR Income net / CR Tax Collected.
            // Otherwise a 2-line pair; postings while tax doesn't apply carry no tax code at all.
            var (incomeAmount, taxAmount) = taxCode == TaxCode.Taxable
                ? TaxCalculator.SplitInclusive(request.Amount, settings?.TaxRate ?? 0m)
                : (request.Amount, 0m);

            var lines = new List<Transaction>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    AccountId = depositAccount.Id,
                    DebitAmount = request.Amount,
                    CreditAmount = 0m,
                    GLAccount = depositAccount.AccountNumber,
                    JournalEntryId = entry.Id,
                    TaxCode = taxCode,
                    Description = description,
                    CreatedAt = now
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    AccountId = account.Id,
                    DebitAmount = 0m,
                    CreditAmount = incomeAmount,
                    GLAccount = account.AccountNumber,
                    JournalEntryId = entry.Id,
                    TaxCode = taxCode,
                    Description = description,
                    CreatedAt = now
                }
            };

            if (taxAmount != 0m)
            {
                lines.Add(new Transaction
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    AccountId = SystemAccounts.TaxCollectedId,
                    DebitAmount = 0m,
                    CreditAmount = taxAmount,
                    GLAccount = SystemAccounts.TaxCollectedNumber,
                    JournalEntryId = entry.Id,
                    TaxCode = taxCode,
                    Description = $"Tax collected — {description}",
                    CreatedAt = now
                });
            }

            await _glRepo.AddBalancedSetAsync(lines, innerCt);

            await _audit.LogAsync(
                nameof(Transaction), Guid.Empty, AuditAction.Create,
                oldValue: null,
                newValue: $"Other income {request.Amount:C} to account '{account.Name}' deposited to '{depositAccount.Name}' on {request.Date:yyyy-MM-dd}",
                ct: innerCt);

        }, ct);
    }
}
