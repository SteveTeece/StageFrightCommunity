using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Repository contract for AttendanceRecord entities.
/// Does not extend ISoftDeletableRepository because attendance records are permanently immutable —
/// no archive/restore operations are exposed.
/// </summary>
public interface IAttendanceRepository : IRepository<AttendanceRecord>
{
    /// <summary>Returns true if an attendance record already exists for the given rehearsal and member (idempotency check).</summary>
    Task<bool> ExistsAsync(Guid rehearsalId, Guid memberId, CancellationToken ct = default);

    /// <summary>Returns all attendance records for the given rehearsal, with Member navigation loaded, ordered by member name.</summary>
    Task<IReadOnlyList<AttendanceRecord>> GetByRehearsalAsync(Guid rehearsalId, CancellationToken ct = default);

    /// <summary>Inserts a batch of attendance records within the ambient unit-of-work transaction.</summary>
    Task AddBatchAsync(IReadOnlyList<AttendanceRecord> records, CancellationToken ct = default);
}
