using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Repository contract for AgmAttendanceRecord entities.
/// Does not extend ISoftDeletableRepository because attendance records are permanently immutable —
/// no archive/restore operations are exposed.
/// </summary>
public interface IAgmAttendanceRepository : IRepository<AgmAttendanceRecord>
{
    /// <summary>Inserts a batch of attendance records within the ambient unit-of-work transaction.</summary>
    Task AddRangeAsync(IEnumerable<AgmAttendanceRecord> records, CancellationToken ct = default);

    /// <summary>Returns all attendance records for the given AGM.</summary>
    Task<IReadOnlyList<AgmAttendanceRecord>> GetByAgmAsync(Guid agmId, CancellationToken ct = default);
}
