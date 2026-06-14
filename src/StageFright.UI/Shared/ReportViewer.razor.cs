using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using StageFright.Core.Enums;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Rendering;

namespace StageFright.UI.Shared;

public partial class ReportViewer : ComponentBase, IDisposable
{
    [Parameter] public IReportProvider? Provider { get; set; }

    [Inject] private IPdfReportRenderer PdfRenderer { get; set; } = null!;
    [Inject] private ICsvReportExporter CsvExporter { get; set; } = null!;
    [Inject] private ILogger<ReportViewer> Logger { get; set; } = null!;

    private ReportData? _report;
    private string? _error;
    private bool _generating;
    private bool _showCancel;
    private CancellationTokenSource? _cts;
    private ReportFilterValues _filterValues = new();

    protected override async Task OnParametersSetAsync()
    {
        if (Provider == null)
        {
            _report = null;
            _error = null;
            return;
        }

        // Initialize filter defaults
        _filterValues = new ReportFilterValues();
        foreach (var filter in Provider.Filters)
        {
            if (!string.IsNullOrEmpty(filter.DefaultValue))
                _filterValues.Set(filter.Key, filter.DefaultValue);
        }

        await GenerateReportAsync();
    }

    private async Task Regenerate() => await GenerateReportAsync();

    private async Task GenerateReportAsync()
    {
        if (Provider == null) return;

        _report = null;
        _error = null;
        _generating = true;
        _showCancel = false;
        StateHasChanged();

        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        // Show cancel button after 5 seconds
        var cancelTimer = Task.Delay(5000, _cts.Token)
            .ContinueWith(_ =>
            {
                if (!_cts.Token.IsCancellationRequested)
                {
                    _showCancel = true;
                    InvokeAsync(StateHasChanged);
                }
            }, TaskScheduler.Default);

        try
        {
            _report = await Provider.GenerateAsync(_filterValues, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            _error = "Report generation was cancelled.";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate report '{ReportId}'", Provider.ReportId);
            _error = $"An error occurred while generating the report. Please try again. ({ex.GetType().Name})";
        }
        finally
        {
            _generating = false;
            _showCancel = false;
            StateHasChanged();
        }
    }

    private void CancelGeneration()
    {
        _cts?.Cancel();
    }

    private void PrintReport()
    {
        if (_report == null) return;

        try
        {
            var bytes = PdfRenderer.Render(_report);
            var tempPath = Path.Combine(Path.GetTempPath(), $"report_{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(tempPath, bytes);
#pragma warning disable CA1416
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true });
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to render PDF for report '{ReportId}'", Provider?.ReportId);
            _error = "Unable to generate PDF. Please try again.";
            StateHasChanged();
        }
    }

    private void ExportCsv()
    {
        if (_report == null) return;

        try
        {
            var csv = CsvExporter.Export(_report);
            var tempPath = Path.Combine(Path.GetTempPath(), $"report_{Guid.NewGuid():N}.csv");
            File.WriteAllText(tempPath, csv, System.Text.Encoding.UTF8);
#pragma warning disable CA1416
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true });
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export CSV for report '{ReportId}'", Provider?.ReportId);
            _error = "Unable to export CSV. Please try again.";
            StateHasChanged();
        }
    }

    private void OnFilterChanged(string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            _filterValues.Set(key, value);
    }

    private static string AlignClass(ReportColumnAlignment alignment)
        => alignment switch
        {
            ReportColumnAlignment.Right => "text-end",
            ReportColumnAlignment.Center => "text-center",
            _ => ""
        };

    public void Dispose()
    {
        _cts?.Dispose();
    }
}
