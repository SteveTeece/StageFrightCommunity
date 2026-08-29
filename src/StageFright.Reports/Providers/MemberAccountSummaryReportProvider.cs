using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Providers;

/// <summary>
/// Generates the Member Account Summary report.
/// Per member: opening balance, period transactions, closing balance,
/// fee aging by DueDate (current / 30 / 60 / 90+ days as of today) computed from
/// GL-derived remaining amounts so buckets always sum to the member's balance (issue #244).
/// Only members with an outstanding (positive) balance appear.
/// Includes archived members per FR-036.
/// </summary>
public class MemberAccountSummaryReportProvider : IReportProvider
{
    private readonly IGLRepository _gl;
    private readonly IMemberRepository _members;
    private readonly IMemberBalanceService _balances;
    private readonly ILocalizer _localizer;

    public MemberAccountSummaryReportProvider(IGLRepository gl, IMemberRepository members, IMemberBalanceService balances, ILocalizer localizer)
    {
        _gl = gl;
        _members = members;
        _balances = balances;
        _localizer = localizer;
    }

    public string ReportId => "member-account-summary";
    public string ReportName => _localizer.Get<ReportsResource>("Reports_MemberAccountSummary_Name");
    public string ModuleName => "Finance";
    public int DisplayOrder => 40;

    public IReadOnlyList<ReportFilterDefinition> Filters =>
    [
        new ReportFilterDefinition { Key = "dateFrom", Type = ReportFilterType.Date, Label = _localizer.Get<ReportsResource>("Reports_Filter_From"), DefaultValue = $"{DateTime.UtcNow.Year}-01-01" },
        new ReportFilterDefinition { Key = "dateTo", Type = ReportFilterType.Date, Label = _localizer.Get<ReportsResource>("Reports_Filter_To"), DefaultValue = $"{DateTime.UtcNow.Year}-12-31" },
        new ReportFilterDefinition { Key = "includeArchived", Type = ReportFilterType.Boolean, Label = _localizer.Get<ReportsResource>("Reports_MemberAccountSummary_IncludeArchivedFilterLabel"), DefaultValue = "false" }
    ];

    public async Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var (from, to) = ParseDateRange(filters);

        var activeMembers = await _members.GetAllAsync(ct);
        var allMembers = activeMembers.ToList();
        if (filters.Get("includeArchived") == "true")
        {
            var archivedMembers = await _members.GetArchivedAsync(ct);
            allMembers.AddRange(archivedMembers);
        }
        allMembers = allMembers.OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToList();

        var today = DateTime.UtcNow.Date;
        var sections = new List<ReportSection>();

        foreach (var member in allMembers)
        {
            var closingBalance = await _gl.GetMemberBalanceAsync(member.Id, ct);
            if (closingBalance <= 0m)
                continue;

            var periodTxns = await _gl.GetByMemberAsync(member.Id, from, to, ct);
            var periodChange = periodTxns.Sum(t => t.DebitAmount - t.CreditAmount);
            var openingBalance = closingBalance - periodChange;

            var outstandingFees = await _balances.GetOutstandingFeesAsync(member.Id, ct);
            var outstandingFeeIds = outstandingFees.Select(f => f.FeeId).ToHashSet();

            // Any receivable credit not allocated to a specific fee (e.g. overpayment)
            // is walked off the oldest fees first so the buckets sum to the GL balance.
            var unallocatedCredit = outstandingFees.Sum(f => f.RemainingAmount) - closingBalance;

            decimal aging0 = 0, aging30 = 0, aging60 = 0, aging90Plus = 0;
            foreach (var fee in outstandingFees)
            {
                var remaining = fee.RemainingAmount;
                if (unallocatedCredit > 0m)
                {
                    var applied = Math.Min(unallocatedCredit, remaining);
                    remaining -= applied;
                    unallocatedCredit -= applied;
                }
                if (remaining <= 0m)
                    continue;

                var daysOverdue = (today - fee.DueDate.Date).Days;
                if (daysOverdue <= 0) aging0 += remaining;
                else if (daysOverdue <= 30) aging30 += remaining;
                else if (daysOverdue <= 60) aging60 += remaining;
                else aging90Plus += remaining;
            }

            var rows = new List<ReportRow>
            {
                new() { Cells = [_localizer.Get<ReportsResource>("Reports_Common_OpeningBalance"), string.Empty, string.Empty, string.Empty, FormatCurrency(openingBalance)] }
            };

            // Only show line items still relevant to what's owed: transactions tied to a fee
            // that's fully settled are hidden, since the opening/closing balance rows already
            // account for them; adjustments with no FeeId (e.g. overpayments) always show.
            var displayedTxns = periodTxns.Where(t => t.FeeId is null || outstandingFeeIds.Contains(t.FeeId.Value));

            foreach (var txn in displayedTxns.OrderBy(t => t.Date))
            {
                rows.Add(new ReportRow
                {
                    Cells =
                    [
                        txn.Date.ToString("yyyy-MM-dd"),
                        txn.Description ?? string.Empty,
                        txn.DebitAmount > 0 ? FormatCurrency(txn.DebitAmount) : string.Empty,
                        txn.CreditAmount > 0 ? FormatCurrency(txn.CreditAmount) : string.Empty,
                        string.Empty
                    ]
                });
            }

            rows.Add(new ReportRow
            {
                Cells = [_localizer.Get<ReportsResource>("Reports_Common_ClosingBalance"), string.Empty, string.Empty, string.Empty, FormatCurrency(closingBalance)],
                IsEmphasized = true
            });

            // Aging is not repeated here — it's already shown per-bucket on the collapsed
            // member summary row (SummaryRow below).
            var label = member.IsDeleted
                ? _localizer.Get<ReportsResource>("Reports_MemberAccountSummary_ArchivedSuffix", member.SortableFullName)
                : member.SortableFullName;
            var summaryRow = new ReportRow
            {
                Cells =
                [
                    label,
                    _localizer.Get<ReportsResource>("Reports_MemberAccountSummary_AgingCurrent", FormatCurrency(aging0)),
                    _localizer.Get<ReportsResource>("Reports_MemberAccountSummary_Aging30", FormatCurrency(aging30)),
                    _localizer.Get<ReportsResource>("Reports_MemberAccountSummary_Aging60", FormatCurrency(aging60)),
                    _localizer.Get<ReportsResource>("Reports_MemberAccountSummary_Aging90Plus", FormatCurrency(aging90Plus)),
                    FormatCurrency(closingBalance)
                ]
            };
            sections.Add(new ReportSection { Heading = label, Rows = rows, SummaryRow = summaryRow });
        }

        return new ReportData
        {
            Title = _localizer.Get<ReportsResource>("Reports_MemberAccountSummary_Name"),
            SubTitle = _localizer.Get<ReportsResource>("Reports_Common_DateRangeSubtitle", from.ToString("d MMMM yyyy"), to.ToString("d MMMM yyyy")),
            GeneratedAt = DateTime.UtcNow,
            Columns =
            [
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_MemberAccountSummary_DateItemColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Description"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Debit"), Alignment = ReportColumnAlignment.Right },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Credit"), Alignment = ReportColumnAlignment.Right },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Balance"), Alignment = ReportColumnAlignment.Right }
            ],
            SummaryColumns =
            [
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_MemberAccountSummary_MemberColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_MemberAccountSummary_CurrentColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_MemberAccountSummary_ThirtyDaysColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_MemberAccountSummary_SixtyDaysColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_MemberAccountSummary_NinetyPlusDaysColumn"), Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = _localizer.Get<ReportsResource>("Reports_Column_Balance"), Alignment = ReportColumnAlignment.Right }
            ],
            Sections = sections
        };
    }

    private static (DateTime From, DateTime To) ParseDateRange(ReportFilterValues filters)
    {
        var year = DateTime.UtcNow.Year;
        var from = DateTime.TryParse(filters.Get("dateFrom"), out var df)
            ? DateTime.SpecifyKind(df.Date, DateTimeKind.Utc)
            : new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = DateTime.TryParse(filters.Get("dateTo"), out var dt)
            ? new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59, DateTimeKind.Utc)
            : new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        return (from, to);
    }

    private static string FormatCurrency(decimal amount) => MoneyFormatter.Format(amount);
}
