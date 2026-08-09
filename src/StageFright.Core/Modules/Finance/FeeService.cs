using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

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

    public FeeService(
        IMemberRepository memberRepo,
        IFeeRepository feeRepo,
        IGLRepository glRepo,
        IAccountRepository accountRepo,
        ISettingsRepository settingsRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _memberRepo = memberRepo;
        _feeRepo = feeRepo;
        _glRepo = glRepo;
        _accountRepo = accountRepo;
        _settingsRepo = settingsRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
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
                "Application settings are not configured.", "Settings", nameof(ApplyAnnualFeesAsync));

        var accounts = await _accountRepo.GetAllAsync(ct);
        var incomeAccount = accounts.FirstOrDefault(c => c.Type == AccountType.Income && !c.IsSystem)
            ?? throw new ValidationException(
                "No income account configured. Please set up accounts in Settings before applying fees.",
                "Account", nameof(ApplyAnnualFeesAsync));

        var currentYear = DateTime.UtcNow.Year;
        var feeDate = new DateTime(currentYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dueDate = new DateTime(currentYear, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        int count = 0;

        // Per-fee-type GST treatment, stamped on the Fee at accrual (drives forgiveness/BAS).
        var gstCode = settings.IsGstRegistered
            ? settings.AnnualFeeGstCode ?? GstCode.GstFree
            : (GstCode?)null;
        var (incomeAmount, gstAmount) = gstCode == GstCode.Gst
            ? GstCalculator.SplitInclusive(settings.AnnualFee)
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
                    GstCode = gstCode,
                    CreatedAt = now
                };
                var savedFee = await _feeRepo.AddAsync(fee, innerCt);

                // GL accrual: Debit MemberReceivable gross / Credit Income net
                // (+ Credit GST Collected when the fee is taxable while registered).
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
                        GstCode = gstCode,
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
                        GstCode = gstCode,
                        Description = $"Annual membership fee income {currentYear}",
                        CreatedAt = now
                    }
                };

                if (gstAmount != 0m)
                {
                    lines.Add(new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = feeDate,
                        AccountId = SystemAccounts.GstCollectedId,
                        DebitAmount = 0m,
                        CreditAmount = gstAmount,
                        GLAccount = SystemAccounts.GstCollectedNumber,
                        MemberId = null,
                        FeeId = savedFee.Id,
                        GstCode = gstCode,
                        Description = $"GST collected — annual membership fee {currentYear}",
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
