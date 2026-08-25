using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Agm;

/// <summary>
/// Assembles the printable AGM attendance report — the AGM's fixed, persisted roster once
/// recorded, or every currently-active member (unchecked) while still scheduled. Read-only.
/// </summary>
public class AgmAttendanceSheetService : IAgmAttendanceSheetService
{
    private readonly IAgmRepository _agmRepo;
    private readonly IAgmAttendanceRepository _attendanceRepo;
    private readonly IMemberRepository _memberRepo;

    public AgmAttendanceSheetService(IAgmRepository agmRepo, IAgmAttendanceRepository attendanceRepo, IMemberRepository memberRepo)
    {
        _agmRepo = agmRepo;
        _attendanceRepo = attendanceRepo;
        _memberRepo = memberRepo;
    }

    public async Task<AgmAttendanceSheetData> GenerateAsync(Guid agmId, CancellationToken ct = default)
    {
        var agm = await _agmRepo.GetByIdAsync(agmId, ct)
            ?? throw new EntityNotFoundException("AnnualGeneralMeeting", agmId, nameof(GenerateAsync));

        List<AgmAttendanceSheetMember> members;

        if (agm.IsRecorded)
        {
            var records = await _attendanceRepo.GetByAgmAsync(agmId, ct);
            members = records.Select(r => new AgmAttendanceSheetMember
            {
                FirstName = r.Member.FirstName,
                LastName = r.Member.LastName,
                Attended = r.Attended
            }).ToList();
        }
        else
        {
            var activeMembers = (await _memberRepo.GetByStatusAsync(MemberStatus.Active, ct))
                .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
                .ToList();
            members = activeMembers.Select(m => new AgmAttendanceSheetMember
            {
                FirstName = m.FirstName,
                LastName = m.LastName,
                Attended = false
            }).ToList();
        }

        return new AgmAttendanceSheetData
        {
            AgmDate = agm.Date,
            Members = members
        };
    }
}
