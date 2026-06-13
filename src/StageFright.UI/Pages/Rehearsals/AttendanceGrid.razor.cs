using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Rehearsals;

namespace StageFright.UI.Pages.Rehearsals;

public partial class AttendanceGrid
{
    [Parameter] public Guid RehearsalId { get; set; }

    [Inject] private IRehearsalService RehearsalService { get; set; } = null!;
    [Inject] private IAttendanceService AttendanceService { get; set; } = null!;
    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private bool _loading = true;
    private bool _saving;
    private bool _alreadyRecorded;
    private string? _errorMessage;
    private Rehearsal? _rehearsal;
    private List<Member> _members = new();
    private List<AttendanceRow> _rows = new();
    private decimal _attendanceFee;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _rehearsal = await RehearsalService.GetMostRecentPastAsync(DateTime.MaxValue);
            // Load the specific rehearsal
            var all = await RehearsalService.GetAllAsync();
            _rehearsal = all.FirstOrDefault(r => r.Id == RehearsalId);

            if (_rehearsal is null)
            {
                _errorMessage = "Rehearsal not found.";
                return;
            }

            // If rate already set, attendance is recorded and locked
            if (_rehearsal.StoredAttendanceRate.HasValue)
            {
                _alreadyRecorded = true;
                return;
            }

            var settings = await SettingsService.GetAsync();
            _attendanceFee = settings?.AttendanceFee ?? 0m;

            var active = await MemberService.GetByStatusAsync(MemberStatus.Active);
            var inactive = await MemberService.GetByStatusAsync(MemberStatus.Inactive);
            _members = active.Concat(inactive).OrderBy(m => m.Name).ToList();

            _rows = _members.Select(m => new AttendanceRow
            {
                MemberId = m.Id,
                MemberName = m.Name,
                MemberIsActive = m.Status == MemberStatus.Active,
                Attended = false,
                MarkAsUnpaid = false
            }).ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load attendance grid: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SaveAttendance()
    {
        _saving = true;
        _errorMessage = null;

        try
        {
            var items = _rows.Select(r => new AttendanceBatchItem
            {
                MemberId = r.MemberId,
                Attended = r.Attended,
                MarkAsUnpaid = r.MarkAsUnpaid
            }).ToList();

            await AttendanceService.RecordBatchAsync(RehearsalId, items);
            Nav.NavigateTo("/rehearsals");
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to save attendance: {ex.Message}";
        }
        finally
        {
            _saving = false;
        }
    }

    private sealed class AttendanceRow
    {
        public Guid MemberId { get; init; }
        public string MemberName { get; init; } = string.Empty;
        public bool MemberIsActive { get; init; }
        public bool Attended { get; set; }
        public bool MarkAsUnpaid { get; set; }
    }
}
