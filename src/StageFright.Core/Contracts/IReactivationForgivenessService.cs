using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Contracts;

/// <summary>
/// Service for the reactivation forgiveness workflow: writing off prior outstanding fees
/// to BadDebtExpense (GL#9900) when a member is reactivated.
/// </summary>
public interface IReactivationForgivenessService
{
    /// <summary>
    /// Returns all fees for the member grouped for the forgiveness dialog.
    /// Fees from prior calendar years have <see cref="ForgivenessItem.IsDefaultForgiven"/> = true
    /// (pre-checked in the UI). Current-year fees default to false.
    /// </summary>
    Task<IReadOnlyList<ForgivenessItem>> GetForgivenessItemsAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>
    /// Writes off the selected fees to BadDebtExpense via GL reversal pairs
    /// (Debit BadDebtExpense GL#9900 / Credit MemberReceivable GL#0101 per fee).
    /// Runs inside a unit-of-work transaction. Audits each forgiveness with Action=Forgiveness.
    /// Fee records are never modified.
    /// </summary>
    Task ApplyForgivenessAsync(Guid memberId, IReadOnlyList<Guid> selectedFeeIds, CancellationToken ct = default);
}
