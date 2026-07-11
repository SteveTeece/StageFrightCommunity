using Microsoft.AspNetCore.Components;
using StageFright.Reports.Registry;

namespace StageFright.UI.Pages.Reports;

public partial class ReportsPage : ComponentBase
{
    [Parameter] public string? ReportId { get; set; }

    [Inject] private IReportProviderRegistry Registry { get; set; } = null!;

    private IReportProvider? _currentProvider;

    protected override void OnInitialized()
    {
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
