using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Committee Report for the Members module.
/// Columns: Member Name, Year, Position — ordered year DESC.
/// Supports filter: Active Only (default) | Archived Only | All.
/// </summary>
public class CommitteeReportProvider : IReportProvider
{
    private readonly ICommitteeMembershipRepository _committeeMemberships;
    private readonly IMemberRepository _members;

    public CommitteeReportProvider(ICommitteeMembershipRepository committeeMemberships, IMemberRepository members)
    {
        _committeeMemberships = committeeMemberships;
        _members = members;
    }

    public string ReportId => "committee-report";
    public string ReportName => "Committee Report";
    public string ModuleName => "Members";
    public int DisplayOrder => 20;

    public IReadOnlyList<ReportFilterDefinition> Filters =>
    [
        new ReportFilterDefinition
        {
            Key = "memberFilter",
            Type = ReportFilterType.Select,
            Label = "Member Status",
            Options = ["Active Only", "Archived Only", "All"],
            DefaultValue = "Active Only"
        }
    ];

    public async Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var memberFilter = filters.Get("memberFilter") ?? "Active Only";

        // Load members matching the filter
        IEnumerable<Core.Entities.Member> filteredMembers = memberFilter switch
        {
            "Archived Only" => await _members.GetArchivedAsync(ct),
            "All" => (await _members.GetAllAsync(ct)).Concat(await _members.GetArchivedAsync(ct)),
            _ => await _members.GetByStatusAsync(MemberStatus.Active, ct)
        };

        var memberMap = filteredMembers.ToDictionary(m => m.Id);

        // Get all committee memberships for the filtered set
        var rows = new List<ReportRow>();
        foreach (var member in memberMap.Values.OrderBy(m => m.Name))
        {
            var memberships = await _committeeMemberships.GetByMemberAsync(member.Id, ct);
            foreach (var membership in memberships.OrderByDescending(m => m.Year))
            {
                rows.Add(new ReportRow
                {
                    Cells = [member.Name, membership.Year.ToString(), membership.Position]
                });
            }
        }

        return new ReportData
        {
            Title = "Committee Report",
            SubTitle = $"Filter: {memberFilter} — {DateTime.UtcNow:d MMMM yyyy}",
            GeneratedAt = DateTime.UtcNow,
            Columns =
            [
                new ReportColumn { Header = "Member", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Year", Alignment = ReportColumnAlignment.Right },
                new ReportColumn { Header = "Position", Alignment = ReportColumnAlignment.Left }
            ],
            Sections = [new ReportSection { Rows = rows }]
        };
    }
}
