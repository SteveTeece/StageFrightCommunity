using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
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
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private ILogger<ReportViewer> Logger { get; set; } = null!;

    private ReportData? _report;
    private string? _error;
    private bool _generating;
    private bool _showCancel;
    private CancellationTokenSource? _cts;
    private ReportFilterValues _filterValues = new();

    private int _currentPage = 1;
    private const int PageSize = 15;

    private bool UseMasterDetail => _report?.SummaryColumns?.Count > 0;
    private int _masterDetailGeneration;

    private int TotalDataRows => _report?.Sections.Sum(s => s.Rows.Count) ?? 0;
    private int TotalPages => TotalDataRows <= PageSize ? 1 : (int)Math.Ceiling(TotalDataRows / (double)PageSize);
    private bool UsePaging => TotalDataRows > PageSize;
    private int PageStartIndex => (_currentPage - 1) * PageSize;
    private int PageEndIndex => PageStartIndex + PageSize;

    private void PrevPage() { if (_currentPage > 1) _currentPage--; }
    private void NextPage() { if (_currentPage < TotalPages) _currentPage++; }

    private IReadOnlyList<PagedSection> GetPagedSections()
    {
        if (_report == null) return Array.Empty<PagedSection>();
        if (!UsePaging) return _report.Sections.Select(s => new PagedSection(s.Heading, s.Rows, s.Subtotal)).ToList();

        var result = new List<PagedSection>();
        var globalIndex = 0;

        foreach (var section in _report.Sections)
        {
            var sectionEnd = globalIndex + section.Rows.Count;

            if (sectionEnd > PageStartIndex && globalIndex < PageEndIndex)
            {
                var rowStart = Math.Max(0, PageStartIndex - globalIndex);
                var rowEnd = Math.Min(section.Rows.Count, PageEndIndex - globalIndex);
                var visibleRows = (IReadOnlyList<ReportRow>)section.Rows.Skip(rowStart).Take(rowEnd - rowStart).ToList();
                var showSubtotal = section.Subtotal != null && sectionEnd <= PageEndIndex;
                result.Add(new PagedSection(section.Heading, visibleRows, showSubtotal ? section.Subtotal : null));
            }

            globalIndex = sectionEnd;
        }

        return result;
    }

    private sealed record PagedSection(string? Heading, IReadOnlyList<ReportRow> Rows, ReportRow? Subtotal);

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
        _currentPage = 1;
        _masterDetailGeneration++;
        StateHasChanged();

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        // Show cancel button after 5 seconds
        var cancelTimer = Task.Delay(5000, token)
            .ContinueWith(_ =>
            {
                if (!token.IsCancellationRequested)
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

    private async Task PrintReport()
    {
        if (_report == null) return;

        try
        {
            var settings = await SettingsService.GetAsync();
            var orgName = settings?.OrganizationName ?? string.Empty;
            var bytes = PdfRenderer.Render(_report, orgName);
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
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
