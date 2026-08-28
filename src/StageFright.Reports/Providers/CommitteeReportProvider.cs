using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Committee Report for the Members module.
/// One section per year (most recent first), columns [Year, Position, Member(s)], with a
/// SummaryRow showing the year's record count. President/Secretary/Treasurer lines are always
/// shown ("Vacant" when unfilled); other positions follow alphabetically; blank positions are
/// grouped under "General Committee Members" last. Matching is case-insensitive and trimmed.
/// Supports filter: Active Only (default) | Archived Only | All.
/// </summary>
public class CommitteeReportProvider : IReportProvider
{
    private readonly ICommitteePositionRecordRepository _committeePositionRecords;
    private readonly IMemberRepository _members;
    private readonly ILocalizer _localizer;

    public CommitteeReportProvider(ICommitteePositionRecordRepository committeePositionRecords, IMemberRepository members, ILocalizer localizer)
    {
        _committeePositionRecords = committeePositionRecords;
        _members = members;
        _localizer = localizer;
    }

    public string ReportId => "committee-report";
    public string ReportName => _localizer.Get<ReportsResource>("Reports_Committee_Name");
    public string ModuleName => "Members";
    public int DisplayOrder => 20;

    public IReadOnlyList<ReportFilterDefinition> Filters =>
    [
        new ReportFilterDefinition
        {
            Key = "memberFilter",
            Type = ReportFilterType.Select,
            Label = _localizer.Get<ReportsResource>("Reports_Committee_MemberStatusFilterLabel"),
            Options = ["Active Only", "Archived Only", "All"],
            OptionLabels =
            [
                _localizer.Get<ReportsResource>("Reports_Filter_OptionActiveOnly"),
                _localizer.Get<ReportsResource>("Reports_Filter_OptionArchivedOnly"),
                _localizer.Get<ReportsResource>("Reports_Filter_OptionAll")
            ],
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

        // Flatten every filtered member's committee position records into (Member, CommitteePositionRecord) pairs
        var records = new List<(Core.Entities.Member Member, Core.Entities.CommitteePositionRecord PositionRecord)>();
        foreach (var member in memberMap.Values.OrderBy(m => m.LastName).ThenBy(m => m.FirstName))
        {
            var positionRecords = await _committeePositionRecords.GetByMemberAsync(member.Id, ct);
            records.AddRange(positionRecords.Select(positionRecord => (member, positionRecord)));
        }

        // One ReportSection per resolved label year, most-recent-first (FR-001/FR-009): rows tied
        // to a CommitteeTerm resolve to that term's LabelYear; legacy pre-feature rows (no
        // CommitteeTermId) still group by their own Year, unchanged.
        var sections = records
            .GroupBy(r => r.PositionRecord.CommitteeTermId is not null
                ? r.PositionRecord.CommitteeTerm!.LabelYear
                : r.PositionRecord.Year)
            .Where(g => g.Key.HasValue)
            .OrderByDescending(g => g.Key)
            .Select(yearGroup =>
            {
                var year = yearGroup.Key!.Value;
                var yearRecords = yearGroup.ToList();

                return new ReportSection
                {
                    Heading = year.ToString(),
                    Rows = BuildPositionLines(year, yearRecords),
                    SummaryRow = new ReportRow { Cells = [year.ToString(), yearRecords.Count.ToString()] }
                };
            })
            .ToList();

        return new ReportData
        {
            Title = _localizer.Get<ReportsResource>("Reports_Committee_Name"),
            SubTitle = _localizer.Get<ReportsResource>("Reports_Committee_SubTitle", memberFilter, DateTime.UtcNow.ToString("d MMMM yyyy")),
            GeneratedAt = DateTime.UtcNow,
            Columns =
            [
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Committee_YearColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Committee_PositionColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Committee_MembersColumn"), Alignment = ReportColumnAlignment.Left }
            ],
            SummaryColumns =
            [
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Committee_YearColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Committee_PositionsRecordedColumn"), Alignment = ReportColumnAlignment.Right }
            ],
            Sections = sections
        };
    }

    private static readonly string[] NamedRoleKeys = ["president", "secretary", "treasurer"];

    /// <summary>
    /// The localised display label for a canonical office-holder role. The lookup key
    /// (<paramref name="roleKey"/>) stays the culture-invariant lowercase token used for matching.
    /// </summary>
    private string NamedRoleLabel(string roleKey) => roleKey switch
    {
        "president" => _localizer.Get<ReportsResource>("Reports_Committee_RolePresident"),
        "secretary" => _localizer.Get<ReportsResource>("Reports_Committee_RoleSecretary"),
        "treasurer" => _localizer.Get<ReportsResource>("Reports_Committee_RoleTreasurer"),
        _ => roleKey
    };

    /// <summary>
    /// Builds one row per position line for a year: President/Secretary/Treasurer first (always emitted,
    /// "Vacant" when unfilled), then every other distinct non-blank position label ordered alphabetically,
    /// then "General Committee Members" last for blank/whitespace-only positions. Matching is
    /// case-insensitive and trimmed (FR-007); members within a line are listed alphabetically (FR-006/FR-006a/FR-010).
    /// </summary>
    private List<ReportRow> BuildPositionLines(
        int year,
        List<(Core.Entities.Member Member, Core.Entities.CommitteePositionRecord PositionRecord)> yearRecords)
    {
        var positionGroups = new Dictionary<string, (string DisplayLabel, List<(Core.Entities.Member Member, Core.Entities.CommitteePositionRecord PositionRecord)> Holders)>();
        var generalMembers = new List<string>();

        foreach (var (member, positionRecord) in yearRecords)
        {
            // New-model rows carry the label on OfficeHolderType (null = general committee);
            // legacy rows carry it on the free-text Position field.
            var trimmed = (positionRecord.OfficeHolderType?.Name ?? positionRecord.Position ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                generalMembers.Add(member.SortableFullName);
                continue;
            }

            var key = trimmed.ToLowerInvariant();
            if (!positionGroups.TryGetValue(key, out var group))
            {
                var displayLabel = NamedRoleKeys.Contains(key) ? NamedRoleLabel(key) : trimmed;
                group = (displayLabel, []);
                positionGroups[key] = group;
            }
            group.Holders.Add((member, positionRecord));
        }

        var rows = new List<ReportRow>();

        foreach (var roleKey in NamedRoleKeys)
        {
            var label = NamedRoleLabel(roleKey);
            var memberText = positionGroups.TryGetValue(roleKey, out var group)
                ? FormatHolders(group.Holders)
                : _localizer.Get<ReportsResource>("Reports_Committee_Vacant");
            rows.Add(new ReportRow { Cells = [year.ToString(), label, memberText] });
        }

        var otherPositionLines = positionGroups
            .Where(kvp => !NamedRoleKeys.Contains(kvp.Key))
            .OrderBy(kvp => kvp.Value.DisplayLabel, StringComparer.OrdinalIgnoreCase);

        foreach (var (_, group) in otherPositionLines)
        {
            rows.Add(new ReportRow { Cells = [year.ToString(), group.DisplayLabel, FormatHolders(group.Holders)] });
        }

        if (generalMembers.Count > 0)
        {
            rows.Add(new ReportRow { Cells = [year.ToString(), _localizer.Get<ReportsResource>("Reports_Committee_GeneralCommitteeMembers"), JoinAlphabetically(generalMembers)] });
        }

        return rows;
    }

    /// <summary>
    /// FR-029: a single holder (legacy or term-based) renders as name only. Multiple holders for
    /// the same term-tracked slot (a special election occurred) render as "Name (Start–End or
    /// 'present')" per holder, ordered by StartDate. Multiple legacy holders (no StartDate to
    /// order by) keep the pre-existing plain alphabetical comma-join.
    /// </summary>
    private string FormatHolders(List<(Core.Entities.Member Member, Core.Entities.CommitteePositionRecord PositionRecord)> holders)
    {
        if (holders.Count == 1)
            return holders[0].Member.SortableFullName;

        if (holders.All(h => h.PositionRecord.StartDate.HasValue))
        {
            var presentLabel = _localizer.Get<ReportsResource>("Reports_Committee_Present");
            return string.Join(", ", holders
                .OrderBy(h => h.PositionRecord.StartDate)
                .Select(h =>
                {
                    var start = h.PositionRecord.StartDate!.Value.ToString("d MMM yyyy");
                    var end = h.PositionRecord.EndDate.HasValue ? h.PositionRecord.EndDate.Value.ToString("d MMM yyyy") : presentLabel;
                    return $"{h.Member.SortableFullName} ({start}–{end})";
                }));
        }

        return JoinAlphabetically(holders.Select(h => h.Member.SortableFullName));
    }

    private static string JoinAlphabetically(IEnumerable<string> names) =>
        string.Join(", ", names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
}
