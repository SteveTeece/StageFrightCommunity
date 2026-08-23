using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Agm;

/// <summary>Assembles the printable AGM attendance report from the AGM's fixed, persisted roster. Read-only.</summary>
public class AgmAttendanceSheetService : IAgmAttendanceSheetService
{
    private readonly IAgmRepository _agmRepo;
    private readonly IAgmAttendanceRepository _attendanceRepo;

    public AgmAttendanceSheetService(IAgmRepository agmRepo, IAgmAttendanceRepository attendanceRepo)
    {
        _agmRepo = agmRepo;
        _attendanceRepo = attendanceRepo;
    }

    public async Task<AgmAttendanceSheetData> GenerateAsync(Guid agmId, CancellationToken ct = default)
    {
        var agm = await _agmRepo.GetByIdAsync(agmId, ct)
            ?? throw new EntityNotFoundException("AnnualGeneralMeeting", agmId, nameof(GenerateAsync));

        var records = await _attendanceRepo.GetByAgmAsync(agmId, ct);

        var members = records.Select(r => new AgmAttendanceSheetMember
        {
            FirstName = r.Member.FirstName,
            LastName = r.Member.LastName,
            Attended = r.Attended
        }).ToList();

        return new AgmAttendanceSheetData
        {
            AgmDate = agm.Date,
            Members = members
        };
    }
}
