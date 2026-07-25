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

        // Flatten every filtered member's committee memberships into (Member, CommitteeMembership) pairs
        var records = new List<(Core.Entities.Member Member, Core.Entities.CommitteeMembership Membership)>();
        foreach (var member in memberMap.Values.OrderBy(m => m.Name))
        {
            var memberships = await _committeeMemberships.GetByMemberAsync(member.Id, ct);
            records.AddRange(memberships.Select(membership => (member, membership)));
        }

        // One ReportSection per year with at least one matching record, most-recent-year-first (FR-001/FR-009)
        var sections = records
            .GroupBy(r => r.Membership.Year)
            .OrderByDescending(g => g.Key)
            .Select(yearGroup =>
            {
                var yearRecords = yearGroup.ToList();

                return new ReportSection
                {
                    Heading = yearGroup.Key.ToString(),
                    Rows = BuildPositionLines(yearGroup.Key, yearRecords),
                    SummaryRow = new ReportRow { Cells = [yearGroup.Key.ToString(), yearRecords.Count.ToString()] }
                };
            })
            .ToList();

        return new ReportData
        {
            Title = "Committee Report",
            SubTitle = $"Filter: {memberFilter} — {DateTime.UtcNow:d MMMM yyyy}",
            GeneratedAt = DateTime.UtcNow,
            Columns =
            [
                new ReportColumn { Header = "Year", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Position", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Member(s)", Alignment = ReportColumnAlignment.Left }
            ],
            SummaryColumns =
            [
                new ReportColumn { Header = "Year", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Positions Recorded", Alignment = ReportColumnAlignment.Right }
            ],
            Sections = sections
        };
    }

    private static readonly string[] NamedRoleKeys = ["president", "secretary", "treasurer"];

    private static readonly Dictionary<string, string> NamedRoleLabels = new()
    {
        ["president"] = "President",
        ["secretary"] = "Secretary",
        ["treasurer"] = "Treasurer"
    };

    /// <summary>
    /// Builds one row per position line for a year: President/Secretary/Treasurer first (always emitted,
    /// "Vacant" when unfilled), then every other distinct non-blank position label ordered alphabetically,
    /// then "General Committee Members" last for blank/whitespace-only positions. Matching is
    /// case-insensitive and trimmed (FR-007); members within a line are listed alphabetically (FR-006/FR-006a/FR-010).
    /// </summary>
    private static List<ReportRow> BuildPositionLines(
        int year,
        List<(Core.Entities.Member Member, Core.Entities.CommitteeMembership Membership)> yearRecords)
    {
        var positionGroups = new Dictionary<string, (string DisplayLabel, List<string> MemberNames)>();
        var generalMembers = new List<string>();

        foreach (var (member, membership) in yearRecords)
        {
            var trimmed = membership.Position.Trim();
            if (trimmed.Length == 0)
            {
                generalMembers.Add(member.Name);
                continue;
            }

            var key = trimmed.ToLowerInvariant();
            if (!positionGroups.TryGetValue(key, out var group))
            {
                var displayLabel = NamedRoleLabels.TryGetValue(key, out var canonicalLabel) ? canonicalLabel : trimmed;
                group = (displayLabel, []);
                positionGroups[key] = group;
            }
            group.MemberNames.Add(member.Name);
        }

        var rows = new List<ReportRow>();

        foreach (var roleKey in NamedRoleKeys)
        {
            var label = NamedRoleLabels[roleKey];
            var memberText = positionGroups.TryGetValue(roleKey, out var group)
                ? JoinAlphabetically(group.MemberNames)
                : "Vacant";
            rows.Add(new ReportRow { Cells = [year.ToString(), label, memberText] });
        }

        var otherPositionLines = positionGroups
            .Where(kvp => !NamedRoleKeys.Contains(kvp.Key))
            .OrderBy(kvp => kvp.Value.DisplayLabel, StringComparer.OrdinalIgnoreCase);

        foreach (var (_, group) in otherPositionLines)
        {
            rows.Add(new ReportRow { Cells = [year.ToString(), group.DisplayLabel, JoinAlphabetically(group.MemberNames)] });
        }

        if (generalMembers.Count > 0)
        {
            rows.Add(new ReportRow { Cells = [year.ToString(), "General Committee Members", JoinAlphabetically(generalMembers)] });
        }

        return rows;
    }

    private static string JoinAlphabetically(IEnumerable<string> names) =>
        string.Join(", ", names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
}
