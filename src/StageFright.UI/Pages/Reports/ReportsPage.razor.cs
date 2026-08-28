using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Localization;
using StageFright.Reports.Registry;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Reports;

public partial class ReportsPage : ComponentBase
{
    [Parameter] public string? ReportId { get; set; }

    [Inject] private IReportProviderRegistry Registry { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private IReportProvider? _currentProvider;

    private string NotFoundText() =>
        Loc.Get<SharedResource>("Shared_ReportsPage_NotFound", ReportId ?? string.Empty);

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
