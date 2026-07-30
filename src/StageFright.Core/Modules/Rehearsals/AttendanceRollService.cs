using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Rehearsals;

/// <summary>Assembles the printable attendance roll for a scheduled rehearsal. Read-only.</summary>
public class AttendanceRollService : IAttendanceRollService
{
    private readonly IRehearsalRepository _rehearsalRepo;
    private readonly IMemberRepository _memberRepo;
    private readonly IAttendanceRepository _attendanceRepo;
    private readonly IMemberBalanceService _memberBalanceService;
    private readonly IFeeRepository _feeRepo;

    public AttendanceRollService(
        IRehearsalRepository rehearsalRepo,
        IMemberRepository memberRepo,
        IAttendanceRepository attendanceRepo,
        IMemberBalanceService memberBalanceService,
        IFeeRepository feeRepo)
    {
        _rehearsalRepo = rehearsalRepo;
        _memberRepo = memberRepo;
        _attendanceRepo = attendanceRepo;
        _memberBalanceService = memberBalanceService;
        _feeRepo = feeRepo;
    }

    public async Task<AttendanceRollData> GenerateAsync(Guid rehearsalId, CancellationToken ct = default)
    {
        var rehearsal = await _rehearsalRepo.GetByIdAsync(rehearsalId, ct)
            ?? throw new EntityNotFoundException("Rehearsal", rehearsalId, nameof(GenerateAsync));

        var activeMembers = (await _memberRepo.GetActiveAsOfAsync(rehearsal.Date, ct))
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .ToList();

        var attendanceByMember = (await _attendanceRepo.GetByRehearsalAsync(rehearsalId, ct))
            .ToDictionary(a => a.MemberId, a => a.Attended);

        var members = new List<AttendanceRollMember>();
        foreach (var member in activeMembers)
        {
            var attended = attendanceByMember.TryGetValue(member.Id, out var wasAttended) && wasAttended;
            members.Add(new AttendanceRollMember
            {
                FirstName = member.FirstName,
                LastName = member.LastName,
                Attended = attended,
                RehearsalFeePaid = await IsRehearsalFeePaidAsync(member.Id, rehearsalId, ct)
            });
        }

        return new AttendanceRollData
        {
            RehearsalDate = rehearsal.Date,
            RehearsalTime = rehearsal.Time,
            Members = members
        };
    }

    private async Task<bool> IsRehearsalFeePaidAsync(Guid memberId, Guid rehearsalId, CancellationToken ct)
    {
        var memberFees = await _feeRepo.GetByMemberAsync(memberId, ct);
        var fee = memberFees.FirstOrDefault(f => f.FeeType == FeeType.Attendance && f.RehearsalId == rehearsalId);
        if (fee is null)
            return false;

        var outstanding = await _memberBalanceService.GetOutstandingFeesAsync(memberId, ct);
        return !outstanding.Any(f => f.FeeId == fee.Id);
    }
}
