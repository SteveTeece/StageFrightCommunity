using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Providers;
using StageFright.Reports.Rendering;
using StageFright.UI.Resources.Strings;
using StageFright.UI.Shared;

namespace StageFright.UI.Pages.Reports;

/// <summary>
/// Read-only report of one bank deposit (issue #399), opened from Reports → Bank Deposits:
/// the shared <see cref="Shared.DepositDetailView"/> plus a Print button that renders the
/// deposit shown through <see cref="IBankDepositReportBuilder"/> and the standard PDF renderer.
/// It has no reverse action; that stays on Finance → Banking's deposit page.
/// </summary>
public partial class BankDepositReportPage : ComponentBase
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private IBankingService BankingService { get; set; } = null!;
    [Inject] private IBankDepositReportBuilder ReportBuilder { get; set; } = null!;
    [Inject] private IPdfReportRenderer PdfRenderer { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;
    [Inject] private ILogger<BankDepositReportPage> Logger { get; set; } = null!;

    private DepositDetail? _detail;
    private bool _loading = true;
    private bool _loadFailed;
    private string? _errorMessage;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _loadFailed = false;
        _errorMessage = null;
        try
        {
            _detail = await BankingService.GetDepositAsync(Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load deposit {DepositId} for its report", Id);
            _detail = null;
            _loadFailed = true;
            _errorMessage = Loc.Get<FinanceResource>("Finance_Banking_LoadError", ErrorDetail.For(ex, L["Finance_Banking_UnexpectedErrorDetail"]));
        }
        _loading = false;
    }

    private async Task PrintAsync()
    {
        if (_detail is null)
            return;

        _errorMessage = null;
        try
        {
            var report = ReportBuilder.Build(_detail);
            var settings = await SettingsService.GetAsync();
            var bytes = PdfRenderer.Render(report, settings?.OrganizationName ?? string.Empty);
            var tempPath = Path.Combine(Path.GetTempPath(), $"bank-deposit_{Guid.NewGuid():N}.pdf");
            await File.WriteAllBytesAsync(tempPath, bytes);
#pragma warning disable CA1416
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true });
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to print deposit {DepositId}", _detail.Id);
            _errorMessage = Shared["Shared_Report_PdfError"];
        }
    }
}
