using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for recording member payments with FIFO GL allocation.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Records a payment, creates GL allocation pairs via FIFO (oldest fee first),
    /// and handles overpayment with a credit GL pair. Runs inside a unit-of-work transaction.
    /// </summary>
    Task<Payment> RecordAsync(RecordPaymentRequest request, CancellationToken ct = default);

    /// <summary>Updates only the Notes field on an existing payment. Audits old and new values.</summary>
    Task UpdateNotesAsync(Guid paymentId, string? notes, CancellationToken ct = default);
}
