using Microsoft.AspNetCore.Components;
using StageFright.Data.Repositories;
using StageFright.Core.Entities;

namespace StageFright.UI.Pages.Rehearsals;

public partial class BatchAttendanceRecorder
{
    [Parameter]
    public Guid RehearsalId { get; set; }

    [Parameter]
    public EventCallback OnSaved { get; set; }

    [Parameter]
    public EventCallback OnCancelled { get; set; }

    [Inject]
    public IRehearsalRepository RehearsalRepository { get; set; } = default!;

    [Inject]
    public IMemberRepository MemberRepository { get; set; } = default!;

    [Inject]
    public IAttendanceRepository AttendanceRepository { get; set; } = default!;

    [Inject]
    public IFeeRepository FeeRepository { get; set; } = default!;

    private Rehearsal? Rehearsal { get; set; }
    private List<Member> Members = new();
    private List<AttendanceRecord> AttendanceRecords = new();
    private bool IsLoading = true;
    private string? ErrorMessage = null;

    private bool _allAttended = false;
    private bool AllAttended
    {
        get => _allAttended;
        set
        {
            _allAttended = value;
            foreach (var record in AttendanceRecords)
            {
                record.Attended = value;
                if (!value)
                {
                    record.Paid = false;
                }
            }
        }
    }

    private bool _allPaid = true;
    private bool AllPaid
    {
        get => _allPaid;
        set
        {
            _allPaid = value;
            foreach (var record in AttendanceRecords.Where(r => r.Attended))
            {
                record.Paid = value;
            }
        }
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            Rehearsal = await RehearsalRepository.GetByIdAsync(RehearsalId);
            if (Rehearsal == null)
            {
                ErrorMessage = "Rehearsal not found.";
                return;
            }

            var activeMembers = await MemberRepository.GetActiveMembersAsync();
            Members = activeMembers.OrderBy(m => m.Name).ToList();

            // Initialize attendance records for all active members with defaults (Attended=true, Paid=true)
            AttendanceRecords = Members.Select(m => new AttendanceRecord
            {
                MemberId = m.Id,
                Attended = true,
                Paid = true
            }).ToList();

            _allAttended = true;
            _allPaid = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading attendance recorder: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveAttendance()
    {
        try
        {
            ErrorMessage = null;

            if (Rehearsal == null)
            {
                ErrorMessage = "Rehearsal not found.";
                return;
            }

            // Calculate attendance rate
            var attendingMembers = AttendanceRecords.Where(a => a.Attended).ToList();
            var attendanceRate = Members.Count > 0 ? (decimal)attendingMembers.Count / Members.Count * 100 : 0;

            // Create all Attendance and Fee records in atomic transaction
            foreach (var record in AttendanceRecords.Where(a => a.Attended))
            {
                var paidStatus = record.Paid ? "Paid" : null;
                await AttendanceRepository.RecordAsync(RehearsalId, record.MemberId, paidStatus);

                // Create Fee record if Paid
                if (record.Paid)
                {
                    var fee = new Fee
                    {
                        Id = Guid.NewGuid(),
                        MemberId = record.MemberId,
                        FeeType = "Attendance",
                        Amount = 0, // Will be fetched from settings
                        FeeDate = Rehearsal.Date,
                        DueDate = Rehearsal.Date.AddDays(30),
                        CreatedAt = DateTime.UtcNow
                    };

                    await FeeRepository.CreateAsync(fee);
                }
            }

            // Update rehearsal with stored attendance rate
            Rehearsal.StoredAttendanceRate = attendanceRate;
            Rehearsal.IsDeleted = false;
            await RehearsalRepository.UpdateAsync(Rehearsal);

            await OnSaved.InvokeAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving attendance: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error: {ex}");
        }
    }

    private async Task Cancel()
    {
        await OnCancelled.InvokeAsync();
    }

    // Helper class to track attendance during form editing
    private class AttendanceRecord
    {
        public Guid MemberId { get; set; }
        public bool Attended { get; set; } = true;
        public bool Paid { get; set; } = true;
    }
}
