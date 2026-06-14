using Microsoft.AspNetCore.Components;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;

namespace StageFright.UI.Pages.Reports;

public partial class ReportsPage : ComponentBase
{
    [Parameter] public string? ReportId { get; set; }

    [Inject] private IReportProviderRegistry Registry { get; set; } = null!;

    private IReadOnlyList<ReportMenuSection> _sections = Array.Empty<ReportMenuSection>();
    private IReportProvider? _currentProvider;

    protected override void OnInitialized()
    {
        _sections = Registry.GetMenuSections();
        UpdateCurrentProvider();
    }

    protected override void OnParametersSet()
    {
        UpdateCurrentProvider();
    }

    private void UpdateCurrentProvider()
    {
        _currentProvider = string.IsNullOrEmpty(ReportId) ? null : Registry.GetProvider(ReportId);
    }
}
