using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Rehearsals;

/// <summary>
/// Application service for rehearsal scheduling and attendance rate management.
/// FreezeAttendanceRateAsync is idempotent: if StoredAttendanceRate is already set, the call is a no-op.
/// </summary>
public class RehearsalService : IRehearsalService
{
    private readonly IRehearsalRepository _rehearsalRepo;
    private readonly IMemberRepository _memberRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public RehearsalService(
        IRehearsalRepository rehearsalRepo,
        IMemberRepository memberRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _rehearsalRepo = rehearsalRepo;
        _memberRepo = memberRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<Rehearsal> ScheduleAsync(ScheduleRehearsalRequest request, CancellationToken ct = default)
    {
        var rehearsal = new Rehearsal
        {
            Id = Guid.NewGuid(),
            Date = request.Date,
            Time = request.Time,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Rehearsal saved = null!;
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            saved = await _rehearsalRepo.AddAsync(rehearsal, innerCt);
            await _audit.LogAsync("Rehearsal", saved.Id, AuditAction.Create, ct: innerCt);
        }, ct);

        return saved;
    }

    public Task<IReadOnlyList<Rehearsal>> GetAllAsync(CancellationToken ct = default) =>
        _rehearsalRepo.GetAllAsync(ct);

    public Task<Rehearsal?> GetMostRecentPastAsync(DateTime asOf, CancellationToken ct = default) =>
        _rehearsalRepo.GetMostRecentPastAsync(asOf, ct);

    public Task<Rehearsal?> GetMostRecentPastWithoutAttendanceAsync(DateTime asOf, CancellationToken ct = default) =>
        _rehearsalRepo.GetMostRecentPastWithoutAttendanceAsync(asOf, ct);

    public Task<Rehearsal?> GetMostRecentPastWithAttendanceAsync(DateTime asOf, CancellationToken ct = default) =>
        _rehearsalRepo.GetMostRecentPastWithAttendanceAsync(asOf, ct);

    public Task<Rehearsal?> GetNextUpcomingAsync(DateTime asOf, CancellationToken ct = default) =>
        _rehearsalRepo.GetNextUpcomingAsync(asOf, ct);

    public async Task FreezeAttendanceRateAsync(Guid rehearsalId, DateTime rehearsalDate, int presentCount, CancellationToken ct = default)
    {
        var rehearsal = await _rehearsalRepo.GetByIdAsync(rehearsalId, ct)
            ?? throw new EntityNotFoundException("Rehearsal", rehearsalId, nameof(FreezeAttendanceRateAsync));

        // Idempotent: if already frozen, do nothing
        if (rehearsal.StoredAttendanceRate.HasValue)
            return;

        var activeMembers = await _memberRepo.GetActiveAsOfAsync(rehearsalDate, ct);
        int denominator = activeMembers.Count;

        decimal rate = denominator > 0
            ? Math.Round((decimal)presentCount / denominator * 100m, 2)
            : 0m;

        rehearsal.StoredAttendanceRate = rate;
        rehearsal.UpdatedAt = DateTime.UtcNow;

        await _rehearsalRepo.UpdateAsync(rehearsal, ct);
    }
}
