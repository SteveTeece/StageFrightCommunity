using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Members;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Resources;

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
    private readonly ILocalizer _localizer;

    public MemberListReportProvider(IMemberRepository members, AgeCalculationService ageCalc, ILocalizer localizer)
    {
        _members = members;
        _ageCalc = ageCalc;
        _localizer = localizer;
    }

    public string ReportId => "member-list";
    public string ReportName => _localizer.Get<ReportsResource>("Reports_MemberList_Name");
    public string ModuleName => "Members";
    public int DisplayOrder => 10;

    public IReadOnlyList<ReportFilterDefinition> Filters =>
    [
        new ReportFilterDefinition
        {
            Key = "memberStatus",
            Type = ReportFilterType.Select,
            Label = _localizer.Get<ReportsResource>("Reports_MemberList_StatusFilterLabel"),
            Options = ["Active", "Inactive", "Archived", "All"],
            OptionLabels =
            [
                _localizer.Enum(MemberStatus.Active),
                _localizer.Enum(MemberStatus.Inactive),
                _localizer.Get<ReportsResource>("Reports_Filter_OptionArchived"),
                _localizer.Get<ReportsResource>("Reports_Filter_OptionAll")
            ],
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
                        m.IsDeleted
                            ? _localizer.Get<ReportsResource>("Reports_Filter_OptionArchived")
                            : _localizer.Enum(m.Status)
                    ]
                };
            }).ToList();

        return new ReportData
        {
            Title = _localizer.Get<ReportsResource>("Reports_MemberList_Name"),
            SubTitle = _localizer.Get<ReportsResource>("Reports_MemberList_SubTitle", statusFilter, today.ToString("d MMMM yyyy")),
            GeneratedAt = today,
            Columns =
            [
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_MemberList_NameColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_MemberList_AddressColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_MemberList_PhoneColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_MemberList_EmailColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_MemberList_JoinDateColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_MemberList_AgeColumn"), Alignment = ReportColumnAlignment.Right },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_MemberList_StatusColumn"), Alignment = ReportColumnAlignment.Left }
            ],
            Sections = [new ReportSection { Rows = rows }]
        };
    }
}
