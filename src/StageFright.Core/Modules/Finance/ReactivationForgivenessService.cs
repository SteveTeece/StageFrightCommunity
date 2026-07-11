using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Applies GL write-off entries for reactivating members with prior fee balances.
/// Write-off pair per fee: Debit BadDebtExpense (9900) / Credit MemberReceivable (0101).
/// Fee records are never modified (immutable per Constitution §3.4).
/// </summary>
public class ReactivationForgivenessService : IReactivationForgivenessService
{
    private readonly IFeeRepository _feeRepo;
    private readonly IGLRepository _glRepo;
    private readonly IMemberRepository _memberRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivationForgivenessService(
        IFeeRepository feeRepo,
        IGLRepository glRepo,
        IMemberRepository memberRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _feeRepo = feeRepo;
        _glRepo = glRepo;
        _memberRepo = memberRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ForgivenessItem>> GetForgivenessItemsAsync(
        Guid memberId, CancellationToken ct = default)
    {
        var fees = await _feeRepo.GetByMemberAsync(memberId, ct);
        var currentYear = DateTime.UtcNow.Year;

        return fees
            .Select(f => new ForgivenessItem
            {
                FeeId = f.Id,
                Year = f.FeeDate.Year,
                FeeDate = f.FeeDate,
                Amount = f.Amount,
                IsDefaultForgiven = f.FeeDate.Year < currentYear
            })
            .ToList();
    }

    public async Task ApplyForgivenessAsync(
        Guid memberId,
        IReadOnlyList<Guid> selectedFeeIds,
        CancellationToken ct = default)
    {
        if (selectedFeeIds.Count == 0)
            return;

        var fees = await _feeRepo.GetByMemberAsync(memberId, ct);
        var feeMap = fees.ToDictionary(f => f.Id);
        var member = await _memberRepo.GetByIdAsync(memberId, ct);
        var memberName = member?.Name ?? "Unknown Member";

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;

            foreach (var feeId in selectedFeeIds)
            {
                if (!feeMap.TryGetValue(feeId, out var fee))
                    continue;

                // Write-off: Debit BadDebtExpense / Credit MemberReceivable gross.
                // Taxable fees (Fee.GstCode = Gst) also debit GST Collected — a bad-debt
                // decreasing adjustment reversing the GST accrued with the fee.
                var (badDebtAmount, gstAdjustment) = fee.GstCode == GstCode.Gst
                    ? GstCalculator.SplitInclusive(fee.Amount)
                    : (fee.Amount, 0m);

                var lines = new List<Transaction>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Date = now,
                        AccountId = SystemAccounts.BadDebtId,
                        DebitAmount = badDebtAmount,
                        CreditAmount = 0m,
                        GLAccount = SystemAccounts.BadDebtNumber,
                        MemberId = memberId,
                        FeeId = feeId,
                        GstCode = fee.GstCode,
                        Description = $"Reactivation forgiveness write-off for {memberName}",
                        CreatedAt = now
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Date = now,
                        AccountId = SystemAccounts.MemberReceivableId,
                        DebitAmount = 0m,
                        CreditAmount = fee.Amount,
                        GLAccount = SystemAccounts.MemberReceivableNumber,
                        MemberId = memberId,
                        FeeId = feeId,
                        GstCode = fee.GstCode,
                        Description = $"Reactivation forgiveness — receivable cleared for {memberName}",
                        CreatedAt = now
                    }
                };

                if (gstAdjustment != 0m)
                {
                    lines.Add(new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = now,
                        AccountId = SystemAccounts.GstCollectedId,
                        DebitAmount = gstAdjustment,
                        CreditAmount = 0m,
                        GLAccount = SystemAccounts.GstCollectedNumber,
                        MemberId = memberId,
                        FeeId = feeId,
                        GstCode = fee.GstCode,
                        Description = $"GST decreasing adjustment — forgiveness for {memberName}",
                        CreatedAt = now
                    });
                }

                await _glRepo.AddBalancedSetAsync(lines, innerCt);

                await _audit.LogAsync(
                    nameof(Fee), feeId, AuditAction.Forgiveness,
                    oldValue: null,
                    newValue: $"Forgiveness write-off {fee.Amount:C} for member {memberId}",
                    ct: innerCt);
            }
        }, ct);
    }
}
