using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Application service for annual membership fee batch operations.
/// Eligibility: Active members with no existing Annual fee for the current calendar year (paid or unpaid).
/// GL pair on creation: Debit MemberReceivable (0101) / Credit first available Income category.
/// </summary>
public class FeeService : IFeeService
{
    private static readonly Guid MemberReceivableCategoryId = new("00000000-0000-0000-0000-000000000002");
    private const string MemberReceivableGLAccount = "0101";

    private readonly IMemberRepository _memberRepo;
    private readonly IFeeRepository _feeRepo;
    private readonly IGLRepository _glRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly ISettingsRepository _settingsRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public FeeService(
        IMemberRepository memberRepo,
        IFeeRepository feeRepo,
        IGLRepository glRepo,
        ICategoryRepository categoryRepo,
        ISettingsRepository settingsRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _memberRepo = memberRepo;
        _feeRepo = feeRepo;
        _glRepo = glRepo;
        _categoryRepo = categoryRepo;
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

        var categories = await _categoryRepo.GetAllAsync(ct);
        var incomeCategory = categories.FirstOrDefault(c => c.Type == CategoryType.Income && !c.IsSystem)
            ?? throw new ValidationException(
                "No income category configured. Please set up categories in Settings before applying fees.",
                "Category", nameof(ApplyAnnualFeesAsync));

        var currentYear = DateTime.UtcNow.Year;
        var feeDate = new DateTime(currentYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dueDate = new DateTime(currentYear, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        int count = 0;

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
                    CreatedAt = now
                };
                var savedFee = await _feeRepo.AddAsync(fee, innerCt);

                // GL accrual pair: Debit MemberReceivable (member-specific) / Credit Income (org-level)
                await _glRepo.AddPairAsync(
                    new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = feeDate,
                        CategoryId = MemberReceivableCategoryId,
                        DebitAmount = settings.AnnualFee,
                        CreditAmount = 0m,
                        GLAccount = MemberReceivableGLAccount,
                        MemberId = memberId,
                        FeeId = savedFee.Id,
                        Description = $"Annual membership fee {currentYear}",
                        CreatedAt = now
                    },
                    new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = feeDate,
                        CategoryId = incomeCategory.Id,
                        DebitAmount = 0m,
                        CreditAmount = settings.AnnualFee,
                        GLAccount = incomeCategory.GLAccount,
                        MemberId = null,
                        FeeId = savedFee.Id,
                        Description = $"Annual membership fee income {currentYear}",
                        CreatedAt = now
                    },
                    innerCt);

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
