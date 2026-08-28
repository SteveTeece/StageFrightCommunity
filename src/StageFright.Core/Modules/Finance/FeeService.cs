using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Application service for annual membership fee batch operations.
/// Eligibility: Active members with no existing Annual fee for the current calendar year (paid or unpaid).
/// GL pair on creation: Debit MemberReceivable (1200) / Credit first available Income account.
/// </summary>
public class FeeService : IFeeService
{

    private readonly IMemberRepository _memberRepo;
    private readonly IFeeRepository _feeRepo;
    private readonly IGLRepository _glRepo;
    private readonly IAccountRepository _accountRepo;
    private readonly ISettingsRepository _settingsRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizer _localizer;

    public FeeService(
        IMemberRepository memberRepo,
        IFeeRepository feeRepo,
        IGLRepository glRepo,
        IAccountRepository accountRepo,
        ISettingsRepository settingsRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork,
        ILocalizer localizer)
    {
        _memberRepo = memberRepo;
        _feeRepo = feeRepo;
        _glRepo = glRepo;
        _accountRepo = accountRepo;
        _settingsRepo = settingsRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    public async Task<IReadOnlyList<Member>> GetEligibleMembersAsync(CancellationToken ct = default)
    {
        var activeMembers = await _memberRepo.GetByStatusAsync(MemberStatus.Active, ct);
        var currentYear = DateTime.UtcNow.Year;

        var eligible = new List<Member>();
        foreach (var member in activeMembers)
        {
            if (!await _feeRepo.AnnualFeeExistsAsync(member.Id, currentYear, ct))
                eligible.Add(member);
        }

        return eligible;
    }

    public async Task<int> ApplyAnnualFeesAsync(IReadOnlyList<Guid> memberIds, CancellationToken ct = default)
    {
        if (memberIds.Count == 0)
            return 0;

        var settings = await _settingsRepo.GetAsync(ct)
            ?? throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_Settings_NotConfigured"),
                "Settings", nameof(ApplyAnnualFeesAsync));

        var accounts = await _accountRepo.GetAllAsync(ct);
        var incomeAccount = accounts.FirstOrDefault(c => c.Type == AccountType.Income && !c.IsSystem)
            ?? throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_Fee_NoIncomeAccount"),
                "Account", nameof(ApplyAnnualFeesAsync));

        var currentYear = DateTime.UtcNow.Year;
        var feeDate = new DateTime(currentYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dueDate = new DateTime(currentYear, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        int count = 0;

        // Per-fee-type tax treatment, stamped on the Fee at accrual (drives forgiveness/tax reporting).
        var taxCode = settings.IsTaxApplicable
            ? settings.AnnualFeeTaxCode ?? TaxCode.TaxExempt
            : (TaxCode?)null;
        var (incomeAmount, taxAmount) = taxCode == TaxCode.Taxable
            ? TaxCalculator.SplitInclusive(settings.AnnualFee, settings.TaxRate ?? 0m)
            : (settings.AnnualFee, 0m);

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var memberId in memberIds)
            {
                var now = DateTime.UtcNow;

                var fee = new Fee
                {
                    Id = Guid.NewGuid(),
                    MemberId = memberId,
                    FeeType = FeeType.Annual,
                    Amount = settings.AnnualFee,
                    FeeDate = feeDate,
                    DueDate = dueDate,
                    PaidAtCreation = false,
                    TaxCode = taxCode,
                    CreatedAt = now
                };
                var savedFee = await _feeRepo.AddAsync(fee, innerCt);

                // GL accrual: Debit MemberReceivable gross / Credit Income net
                // (+ Credit Tax Collected when the fee is taxable while tax applies).
                var lines = new List<Transaction>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Date = feeDate,
                        AccountId = SystemAccounts.MemberReceivableId,
                        DebitAmount = settings.AnnualFee,
                        CreditAmount = 0m,
                        GLAccount = SystemAccounts.MemberReceivableNumber,
                        MemberId = memberId,
                        FeeId = savedFee.Id,
                        TaxCode = taxCode,
                        Description = $"Annual membership fee {currentYear}",
                        CreatedAt = now
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Date = feeDate,
                        AccountId = incomeAccount.Id,
                        DebitAmount = 0m,
                        CreditAmount = incomeAmount,
                        GLAccount = incomeAccount.AccountNumber,
                        MemberId = null,
                        FeeId = savedFee.Id,
                        TaxCode = taxCode,
                        Description = $"Annual membership fee income {currentYear}",
                        CreatedAt = now
                    }
                };

                if (taxAmount != 0m)
                {
                    lines.Add(new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = feeDate,
                        AccountId = SystemAccounts.TaxCollectedId,
                        DebitAmount = 0m,
                        CreditAmount = taxAmount,
                        GLAccount = SystemAccounts.TaxCollectedNumber,
                        MemberId = null,
                        FeeId = savedFee.Id,
                        TaxCode = taxCode,
                        Description = $"Tax collected — annual membership fee {currentYear}",
                        CreatedAt = now
                    });
                }

                await _glRepo.AddBalancedSetAsync(lines, innerCt);

                await _audit.LogAsync(
                    nameof(Fee), savedFee.Id, AuditAction.Create,
                    oldValue: null,
                    newValue: $"Annual fee {settings.AnnualFee:C} for member {memberId} ({currentYear})",
                    ct: innerCt);

                count++;
            }
        }, ct);

        return count;
    }
}
