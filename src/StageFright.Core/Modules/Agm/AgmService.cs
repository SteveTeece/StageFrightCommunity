using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Agm;

/// <summary>
/// Records and reviews Annual General Meetings: attendance, elections, and the committee-term
/// chain each AGM starts (closing whatever term was previously open).
/// </summary>
public class AgmService : IAgmService
{
    private readonly IAgmRepository _agmRepo;
    private readonly IAgmAttendanceRepository _attendanceRepo;
    private readonly ICommitteeTermRepository _termRepo;
    private readonly ICommitteePositionRecordRepository _positionRepo;
    private readonly ISettingsService _settingsService;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public AgmService(
        IAgmRepository agmRepo,
        IAgmAttendanceRepository attendanceRepo,
        ICommitteeTermRepository termRepo,
        ICommitteePositionRecordRepository positionRepo,
        ISettingsService settingsService,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _agmRepo = agmRepo;
        _attendanceRepo = attendanceRepo;
        _termRepo = termRepo;
        _positionRepo = positionRepo;
        _settingsService = settingsService;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<AnnualGeneralMeeting> RecordAsync(RecordAgmRequest request, CancellationToken ct = default)
    {
        var assignedMemberIds = new List<Guid>();
        assignedMemberIds.AddRange(request.OfficeHolderAssignments.Values);
        assignedMemberIds.AddRange(request.GeneralCommitteeMemberIds);

        var duplicate = assignedMemberIds.GroupBy(id => id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new ValidationException(
                "A member cannot hold more than one committee assignment from the same AGM.",
                nameof(AnnualGeneralMeeting), nameof(RecordAsync));

        AnnualGeneralMeeting savedAgm = null!;

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;
            var settings = await _settingsService.GetAsync(innerCt);

            var agm = new AnnualGeneralMeeting
            {
                Id = Guid.NewGuid(),
                Date = request.Date,
                Notes = request.Notes,
                GeneralCommitteeSeatCountTarget = settings?.GeneralCommitteeSeatCountTarget,
                CreatedAt = now,
                UpdatedAt = now
            };
            savedAgm = await _agmRepo.AddAsync(agm, innerCt);

            var attendedSet = request.AttendedMemberIds.ToHashSet();
            var attendanceRecords = request.AllActiveMemberIds.Select(memberId => new AgmAttendanceRecord
            {
                Id = Guid.NewGuid(),
                AnnualGeneralMeetingId = savedAgm.Id,
                MemberId = memberId,
                Attended = attendedSet.Contains(memberId),
                CreatedAt = now
            });
            await _attendanceRepo.AddRangeAsync(attendanceRecords, innerCt);

            var openTerm = await _termRepo.GetOpenAsync(innerCt);
            if (openTerm is not null)
            {
                openTerm.EndDate = request.Date;
                openTerm.UpdatedAt = now;
                await _termRepo.UpdateAsync(openTerm, innerCt);
            }

            var newTerm = new CommitteeTerm
            {
                Id = Guid.NewGuid(),
                StartedByAgmId = savedAgm.Id,
                StartDate = request.Date,
                EndDate = null,
                LabelYear = request.Date.Month >= 7 ? request.Date.Year + 1 : request.Date.Year,
                CreatedAt = now,
                UpdatedAt = now
            };
            var savedTerm = await _termRepo.AddAsync(newTerm, innerCt);

            foreach (var (officeHolderTypeId, memberId) in request.OfficeHolderAssignments)
            {
                var record = new CommitteePositionRecord
                {
                    Id = Guid.NewGuid(),
                    MemberId = memberId,
                    CommitteeTermId = savedTerm.Id,
                    OfficeHolderTypeId = officeHolderTypeId,
                    StartDate = request.Date,
                    EndDate = null,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _positionRepo.AddAsync(record, innerCt);
            }

            foreach (var memberId in request.GeneralCommitteeMemberIds)
            {
                var record = new CommitteePositionRecord
                {
                    Id = Guid.NewGuid(),
                    MemberId = memberId,
                    CommitteeTermId = savedTerm.Id,
                    OfficeHolderTypeId = null,
                    StartDate = request.Date,
                    EndDate = null,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _positionRepo.AddAsync(record, innerCt);
            }

            await _audit.LogAsync(nameof(AnnualGeneralMeeting), savedAgm.Id, AuditAction.Create, ct: innerCt);
        }, ct);

        return savedAgm;
    }

    public Task<AnnualGeneralMeeting?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _agmRepo.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<AnnualGeneralMeeting>> GetPastAsync(CancellationToken ct = default) =>
        _agmRepo.GetPastOrderedAsync(ct);

    public async Task ArchiveAsync(Guid id, string deletedBy, CancellationToken ct = default)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            await _agmRepo.ArchiveAsync(id, deletedBy, innerCt);

            var now = DateTime.UtcNow;
            var attendanceRecords = await _attendanceRepo.GetByAgmAsync(id, innerCt);
            foreach (var record in attendanceRecords)
            {
                record.IsDeleted = true;
                record.DeletedAt = now;
                record.DeletedBy = deletedBy;
                await _attendanceRepo.UpdateAsync(record, innerCt);
            }

            await _audit.LogAsync(nameof(AnnualGeneralMeeting), id, AuditAction.Delete, ct: innerCt);
        }, ct);
    }

    public async Task<CommitteePositionRecord> RecordSpecialElectionAsync(RecordSpecialElectionRequest request, CancellationToken ct = default)
    {
        var outgoing = await _positionRepo.GetByIdAsync(request.OutgoingPositionRecordId, ct)
            ?? throw new EntityNotFoundException(nameof(CommitteePositionRecord), request.OutgoingPositionRecordId, nameof(RecordSpecialElectionAsync));

        var termId = outgoing.CommitteeTermId
            ?? throw new DataIntegrityException(
                "The outgoing position record does not belong to a committee term.",
                nameof(CommitteePositionRecord), nameof(RecordSpecialElectionAsync), outgoing.Id);

        var term = await _termRepo.GetByIdAsync(termId, ct)
            ?? throw new EntityNotFoundException(nameof(CommitteeTerm), termId, nameof(RecordSpecialElectionAsync));

        if (term.EndDate is not null)
            throw new DataIntegrityException(
                "This committee term has already been closed by a later AGM; a special election can no longer be recorded against it.",
                nameof(CommitteeTerm), nameof(RecordSpecialElectionAsync), term.Id);

        var existingOpenForIncoming = await _positionRepo.GetOpenByMemberInTermAsync(term.Id, request.IncomingMemberId, ct);
        if (existingOpenForIncoming is not null)
            throw new ValidationException(
                "The incoming member already holds an open committee slot in this term.",
                nameof(CommitteePositionRecord), nameof(RecordSpecialElectionAsync), request.IncomingMemberId);

        CommitteePositionRecord savedIncoming = null!;

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;

            outgoing.EndDate = request.ReplacementDate;
            outgoing.UpdatedAt = now;
            await _positionRepo.UpdateAsync(outgoing, innerCt);

            var incoming = new CommitteePositionRecord
            {
                Id = Guid.NewGuid(),
                MemberId = request.IncomingMemberId,
                CommitteeTermId = term.Id,
                OfficeHolderTypeId = outgoing.OfficeHolderTypeId,
                StartDate = request.ReplacementDate,
                EndDate = null,
                CreatedAt = now,
                UpdatedAt = now
            };
            savedIncoming = await _positionRepo.AddAsync(incoming, innerCt);

            await _audit.LogAsync(nameof(CommitteePositionRecord), outgoing.Id, AuditAction.Update, ct: innerCt);
            await _audit.LogAsync(nameof(CommitteePositionRecord), savedIncoming.Id, AuditAction.Create, ct: innerCt);
        }, ct);

        return savedIncoming;
    }
}
