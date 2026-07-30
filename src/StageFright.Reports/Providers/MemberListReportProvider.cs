using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Members;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Member List report for the Members module.
/// Columns: Name, Address, Phone, Email, Join Date, Age (if DOB), Status.
/// Supports status filter: Active (default) | Inactive | Archived | All.
/// </summary>
public class MemberListReportProvider : IReportProvider
{
    private readonly IMemberRepository _members;
    private readonly AgeCalculationService _ageCalc;

    public MemberListReportProvider(IMemberRepository members, AgeCalculationService ageCalc)
    {
        _members = members;
        _ageCalc = ageCalc;
    }

    public string ReportId => "member-list";
    public string ReportName => "Member List";
    public string ModuleName => "Members";
    public int DisplayOrder => 10;

    public IReadOnlyList<ReportFilterDefinition> Filters =>
    [
        new ReportFilterDefinition
        {
            Key = "memberStatus",
            Type = ReportFilterType.Select,
            Label = "Status",
            Options = ["Active", "Inactive", "Archived", "All"],
            DefaultValue = "Active"
        }
    ];

    public async Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var statusFilter = filters.Get("memberStatus") ?? "Active";
        var today = DateTime.UtcNow;

        var members = statusFilter switch
        {
            "Inactive" => await _members.GetByStatusAsync(MemberStatus.Inactive, ct),
            "Archived" => await _members.GetArchivedAsync(ct),
            "All" => (await _members.GetAllAsync(ct)).Concat(await _members.GetArchivedAsync(ct)).ToList(),
            _ => await _members.GetByStatusAsync(MemberStatus.Active, ct)
        };

        var rows = members
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .Select(m =>
            {
                var ageStr = _ageCalc.Calculate(m.DateOfBirth, today)?.ToString();

                return new ReportRow
                {
                    Cells =
                    [
                        m.SortableFullName,
                        m.StreetAddress,
                        m.Phone ?? string.Empty,
                        m.Email ?? string.Empty,
                        m.JoinDate.ToString("yyyy-MM-dd"),
                        ageStr ?? string.Empty,
                        m.IsDeleted ? "Archived" : m.Status.ToString()
                    ]
                };
            }).ToList();

        return new ReportData
        {
            Title = "Member List",
            SubTitle = $"Status: {statusFilter} — {today:d MMMM yyyy}",
            GeneratedAt = today,
            Columns =
            [
                new ReportColumn { Header = "Name", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Address", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Phone", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Email", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Join Date", Alignment = ReportColumnAlignment.Left, Format = ReportColumnFormat.Date },
                new ReportColumn { Header = "Age", Alignment = ReportColumnAlignment.Right },
                new ReportColumn { Header = "Status", Alignment = ReportColumnAlignment.Left }
            ],
            Sections = [new ReportSection { Rows = rows }]
        };
    }
}
