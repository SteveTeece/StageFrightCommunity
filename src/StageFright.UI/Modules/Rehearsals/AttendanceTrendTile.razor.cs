using System.Globalization;
using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;

namespace StageFright.UI.Modules.Rehearsals;

/// <summary>
/// Dashboard tile body for the attendance trend chart (design 3a): average recorded
/// attendance rate per month over the last six months, as a BlazorBootstrap line chart.
/// </summary>
public partial class AttendanceTrendTile : ComponentBase
{
    private const int MonthWindow = 6;

    [Inject] private IRehearsalService RehearsalService { get; set; } = null!;

    private bool _loading = true;
    private bool _error;
    private bool _hasData;

    private LineChart _chart = default!;
    private ChartData _chartData = default!;
    private LineChartOptions _chartOptions = default!;
    private bool _chartInitialized;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var today = DateTime.Today;
            var windowStart = new DateTime(today.Year, today.Month, 1).AddMonths(-(MonthWindow - 1));

            var all = await RehearsalService.GetAllAsync();
            var recorded = all
                .Where(r => r.StoredAttendanceRate.HasValue && r.Date >= windowStart && r.Date.Date <= today)
                .ToList();

            if (recorded.Count > 0)
            {
                var byMonth = recorded
                    .GroupBy(r => (r.Date.Year, r.Date.Month))
                    .ToDictionary(g => g.Key, g => g.Average(r => (double)r.StoredAttendanceRate!.Value));

                var labels = new List<string>();
                var values = new List<double?>();
                for (var i = 0; i < MonthWindow; i++)
                {
                    var month = windowStart.AddMonths(i);
                    labels.Add(CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(month.Month));
                    values.Add(byMonth.TryGetValue((month.Year, month.Month), out var avg) ? avg : null);
                }

                _chartData = new ChartData
                {
                    Labels = labels,
                    Datasets = new List<IChartDataset>
                    {
                        new LineChartDataset
                        {
                            Label = "Attendance %",
                            Data = values,
                            BorderColor = "#38d2ff",
                            BackgroundColor = "rgba(56, 210, 255, 0.15)",
                            PointBackgroundColor = new List<string> { "#38d2ff" },
                            BorderWidth = 2.5
                        }
                    }
                };

                _chartOptions = new LineChartOptions { Responsive = true, MaintainAspectRatio = false };
                _chartOptions.Plugins.Legend!.Display = false;
                _chartOptions.Scales.Y!.Min = 0;
                _chartOptions.Scales.Y.Max = 100;
                _hasData = true;
            }
        }
        catch
        {
            _error = true;
        }
        finally
        {
            _loading = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_loading && !_error && _hasData && !_chartInitialized && _chart is not null)
        {
            _chartInitialized = true;
            await _chart.InitializeAsync(_chartData, _chartOptions);
        }
    }
}
