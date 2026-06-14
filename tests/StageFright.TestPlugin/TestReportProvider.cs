using StageFright.Core.Enums;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;

namespace StageFright.TestPlugin;

/// <summary>
/// Test-fixture report provider. Appears in the Reports menu under "TestPlugin" (alphabetically
/// after core sections), confirming that plugin reports render and that a provider failure
/// is isolated gracefully.
/// </summary>
public class TestReportProvider : IReportProvider
{
    public string ReportId => "test-plugin-report";
    public string ReportName => "Test Plugin Report";
    public string ModuleName => "TestPlugin";
    public int DisplayOrder => 1;
    public IReadOnlyList<ReportFilterDefinition> Filters => [];

    public Task<ReportData> GenerateAsync(ReportFilterValues filters, CancellationToken ct = default)
    {
        var data = new ReportData
        {
            Title = "Test Plugin Report",
            GeneratedAt = DateTime.UtcNow,
            Columns = [new ReportColumn { Header = "Metric", Alignment = ReportColumnAlignment.Left }, new ReportColumn { Header = "Value", Alignment = ReportColumnAlignment.Right }],
            Sections =
            [
                new ReportSection
                {
                    Rows = [new ReportRow { Cells = ["Plugin Status", "Active"] }]
                }
            ]
        };

        return Task.FromResult(data);
    }
}
