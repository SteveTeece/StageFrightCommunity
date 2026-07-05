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
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivationForgivenessService(
        IFeeRepository feeRepo,
        IGLRepository glRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _feeRepo = feeRepo;
        _glRepo = glRepo;
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

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;

            foreach (var feeId in selectedFeeIds)
            {
                if (!feeMap.TryGetValue(feeId, out var fee))
                    continue;

                // Write-off pair: Debit BadDebtExpense / Credit MemberReceivable
                await _glRepo.AddPairAsync(
                    new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = now,
                        AccountId = SystemAccounts.BadDebtId,
                        DebitAmount = fee.Amount,
                        CreditAmount = 0m,
                        GLAccount = SystemAccounts.BadDebtNumber,
                        MemberId = memberId,
                        FeeId = feeId,
                        Description = $"Reactivation forgiveness write-off for fee {feeId}",
                        CreatedAt = now
                    },
                    new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = now,
                        AccountId = SystemAccounts.MemberReceivableId,
                        DebitAmount = 0m,
                        CreditAmount = fee.Amount,
                        GLAccount = SystemAccounts.MemberReceivableNumber,
                        MemberId = memberId,
                        FeeId = feeId,
                        Description = $"Reactivation forgiveness — receivable cleared for fee {feeId}",
                        CreatedAt = now
                    },
                    innerCt);

                await _audit.LogAsync(
                    nameof(Fee), feeId, AuditAction.Forgiveness,
                    oldValue: null,
                    newValue: $"Forgiveness write-off {fee.Amount:C} for member {memberId}",
                    ct: innerCt);
            }
        }, ct);
    }
}
