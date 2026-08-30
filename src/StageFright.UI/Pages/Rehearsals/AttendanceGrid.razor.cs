using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Rehearsals;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Rehearsals;

public partial class AttendanceGrid
{
    [Parameter] public Guid RehearsalId { get; set; }

    [Inject] private IRehearsalService RehearsalService { get; set; } = null!;
    [Inject] private IAttendanceService AttendanceService { get; set; } = null!;
    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<RehearsalsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private bool _loading = true;
    private bool _saving;
    private bool _alreadyRecorded;
    private bool _isFutureDate;
    private string? _errorMessage;
    private Rehearsal? _rehearsal;
    private List<Member> _members = new();
    private List<AttendanceRow> _rows = new();
    private List<AttendanceRecord> _recordedAttendance = new();
    private IReadOnlyDictionary<Guid, bool> _paidByMemberId = new Dictionary<Guid, bool>();
    private decimal _attendanceFee;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var all = await RehearsalService.GetAllAsync();
            _rehearsal = all.FirstOrDefault(r => r.Id == RehearsalId);

            if (_rehearsal is null)
            {
                _errorMessage = L["Rehearsals_Attendance_NotFound"];
                return;
            }

            if (_rehearsal.StoredAttendanceRate.HasValue)
            {
                _alreadyRecorded = true;
                _recordedAttendance = (await AttendanceService.GetByRehearsalAsync(RehearsalId)).ToList();
                _paidByMemberId = await AttendanceService.GetPaidStatusByRehearsalAsync(RehearsalId);
                return;
            }

            if (_rehearsal.Date.Date > DateTime.Today)
            {
                _isFutureDate = true;
                return;
            }

            var settings = await SettingsService.GetAsync();
            _attendanceFee = settings?.AttendanceFee ?? 0m;

            _members = (await MemberService.GetByStatusAsync(MemberStatus.Active))
                .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
                .ToList();

            _rows = _members.Select(m => new AttendanceRow
            {
                MemberId = m.Id,
                MemberName = m.SortableFullName,
                MemberIsActive = m.Status == MemberStatus.Active,
                Attended = false,
                Paid = false
            }).ToList();
        }
        catch (Exception)
        {
            _errorMessage = Shared["Shared_Error_Unexpected"];
        }
        finally
        {
            _loading = false;
        }
    }

    private bool AllAttended => _rows.Count > 0 && _rows.All(r => r.Attended);

    private void ToggleSelectAll(bool value)
    {
        foreach (var row in _rows)
            row.Attended = value;
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
        catch (Exception)
        {
            _errorMessage = Shared["Shared_Error_Unexpected"];
        }
        finally
        {
            _saving = false;
        }
    }

    /// <summary>Browser tab title — depends on whether attendance is already recorded.</summary>
    private string PageTitleText() =>
        _alreadyRecorded ? L["Rehearsals_Attendance_PageTitleView"] : L["Rehearsals_Attendance_PageTitleRecord"];

    /// <summary>Page heading — depends on whether attendance is already recorded.</summary>
    private string HeadingText() =>
        _alreadyRecorded ? L["Rehearsals_Attendance_HeadingView"] : L["Rehearsals_Attendance_HeadingRecord"];

    /// <summary>Info banner shown when attendance has already been recorded, with the stored rate.</summary>
    private string RecordedBannerText() =>
        Loc.Get<RehearsalsResource>("Rehearsals_Attendance_RecordedBanner", _rehearsal?.StoredAttendanceRate?.ToString("F1") ?? string.Empty);

    /// <summary>Notice shown when the rehearsal date is still in the future.</summary>
    private string FutureNoticeText() =>
        Loc.Get<RehearsalsResource>("Rehearsals_Attendance_FutureNotice", _rehearsal?.Date.ToString("dddd, d MMMM yyyy") ?? string.Empty);

    /// <summary>aria-label for a row's Attended checkbox.</summary>
    private string AttendedAriaLabel(string memberName) =>
        Loc.Get<RehearsalsResource>("Rehearsals_Attendance_AttendedAriaLabel", memberName);

    /// <summary>aria-label for a row's Paid checkbox in the read-only recorded view.</summary>
    private string PaidAriaLabel(string memberName) =>
        Loc.Get<RehearsalsResource>("Rehearsals_Attendance_PaidAriaLabel", memberName);

    /// <summary>aria-label for a row's Paid checkbox while recording.</summary>
    private string MarkPaidAriaLabel(string memberName) =>
        Loc.Get<RehearsalsResource>("Rehearsals_Attendance_MarkPaidAriaLabel", memberName);

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
                else
                    _paid = false;
            }
        }

        public bool Paid
        {
            get => _paid;
            set => _paid = value;
        }
    }
}
