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

    private const int PageSize = 15;

    private bool _loading = true;
    private bool _saving;
    private bool _alreadyRecorded;
    private string? _errorMessage;
    private Rehearsal? _rehearsal;
    private List<Member> _members = new();
    private List<AttendanceRow> _rows = new();
    private List<AttendanceRecord> _recordedAttendance = new();
    private decimal _attendanceFee;
    private int _currentPage = 1;

    private int TotalRows => _alreadyRecorded ? _recordedAttendance.Count : _rows.Count;
    private int TotalPages => (int)Math.Ceiling(TotalRows / (double)PageSize);

    private IEnumerable<AttendanceRow> PagedRows =>
        _rows.Skip((_currentPage - 1) * PageSize).Take(PageSize);

    private IEnumerable<AttendanceRecord> PagedRecords =>
        _recordedAttendance.Skip((_currentPage - 1) * PageSize).Take(PageSize);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var all = await RehearsalService.GetAllAsync();
            _rehearsal = all.FirstOrDefault(r => r.Id == RehearsalId);

            if (_rehearsal is null)
            {
                _errorMessage = "Rehearsal not found.";
                return;
            }

            if (_rehearsal.StoredAttendanceRate.HasValue)
            {
                _alreadyRecorded = true;
                _recordedAttendance = (await AttendanceService.GetByRehearsalAsync(RehearsalId)).ToList();
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
                Paid = false
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

    private void GoToPage(int page)
    {
        if (page >= 1 && page <= TotalPages)
            _currentPage = page;
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
                MarkAsUnpaid = !r.Paid
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
        private bool _attended;
        private bool _paid;

        public bool Attended
        {
            get => _attended;
            set
            {
                _attended = value;
                if (value)
                    _paid = true;
            }
        }

        public bool Paid
        {
            get => _paid;
            set => _paid = value;
        }
    }
}
