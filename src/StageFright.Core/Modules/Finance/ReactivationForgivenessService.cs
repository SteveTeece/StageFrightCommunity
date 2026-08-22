using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Applies GL write-off entries for reactivating members with prior fee balances.
/// Write-off pair per fee: Debit BadDebtExpense (6999) / Credit MemberReceivable (1200).
/// Fee records are never modified (immutable per Constitution §3.4).
/// </summary>
public class ReactivationForgivenessService : IReactivationForgivenessService
{
    private readonly IFeeRepository _feeRepo;
    private readonly IGLRepository _glRepo;
    private readonly IMemberRepository _memberRepo;
    private readonly ISettingsRepository _settingsRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivationForgivenessService(
        IFeeRepository feeRepo,
        IGLRepository glRepo,
        IMemberRepository memberRepo,
        ISettingsRepository settingsRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _feeRepo = feeRepo;
        _glRepo = glRepo;
        _memberRepo = memberRepo;
        _settingsRepo = settingsRepo;
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
        var memberName = member?.FullName ?? "Unknown Member";
        var settings = await _settingsRepo.GetAsync(ct);

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;

            foreach (var feeId in selectedFeeIds)
            {
                if (!feeMap.TryGetValue(feeId, out var fee))
                    continue;

                // Write-off: Debit BadDebtExpense / Credit MemberReceivable gross.
                // Taxable fees (Fee.TaxCode = Taxable) also debit Tax Collected — a bad-debt
                // decreasing adjustment reversing the tax accrued with the fee, at the
                // organisation's current tax rate (single current rate, no rate history — see
                // spec 016 Assumptions).
                var (badDebtAmount, taxAdjustment) = fee.TaxCode == TaxCode.Taxable
                    ? TaxCalculator.SplitInclusive(fee.Amount, settings?.TaxRate ?? 0m)
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
                        TaxCode = fee.TaxCode,
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
                        TaxCode = fee.TaxCode,
                        Description = $"Reactivation forgiveness — receivable cleared for {memberName}",
                        CreatedAt = now
                    }
                };

                if (taxAdjustment != 0m)
                {
                    lines.Add(new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = now,
                        AccountId = SystemAccounts.TaxCollectedId,
                        DebitAmount = taxAdjustment,
                        CreditAmount = 0m,
                        GLAccount = SystemAccounts.TaxCollectedNumber,
                        MemberId = memberId,
                        FeeId = feeId,
                        TaxCode = fee.TaxCode,
                        Description = $"Tax decreasing adjustment — forgiveness for {memberName}",
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
