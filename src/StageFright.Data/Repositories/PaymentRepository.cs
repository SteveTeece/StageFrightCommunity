using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly StageFrightDbContext _db;
    private readonly IAuditTrailService _audit;

    public PaymentRepository(StageFrightDbContext db, IAuditTrailService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Payments.FindAsync(new object[] { id }, ct);
    }

    public async Task<Payment> AddAsync(Payment payment, CancellationToken ct = default)
    {
        try
        {
            var entry = await _db.Payments.AddAsync(payment, ct);
            await _db.SaveChangesAsync(ct);
            return entry.Entity;
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(Payment), nameof(AddAsync), null, ex);
        }
    }

    public async Task UpdateNotesAsync(Guid id, string? notes, CancellationToken ct = default)
    {
        string? oldNotes;
        try
        {
            var payment = await _db.Payments.FindAsync(new object[] { id }, ct)
                ?? throw new EntityNotFoundException(nameof(Payment), id, nameof(UpdateNotesAsync));

            oldNotes = payment.Notes;
            payment.Notes = notes;
            payment.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not EntityNotFoundException and not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(Payment), nameof(UpdateNotesAsync), id, ex);
        }

        await _audit.LogAsync(nameof(Payment), id, Core.Enums.AuditAction.Update,
            oldNotes, notes, ct: ct);
    }

    public async Task<IReadOnlyList<Payment>> GetByMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        return await _db.Payments
            .Where(p => p.MemberId == memberId)
            .OrderByDescending(p => p.Date)
            .ToListAsync(ct);
    }
}
