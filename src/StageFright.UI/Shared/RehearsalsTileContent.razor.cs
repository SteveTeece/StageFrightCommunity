using System.Globalization;
using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;

namespace StageFright.UI.Shared;

public partial class RehearsalsTileContent : ComponentBase
{
    [Inject] private IRehearsalService RehearsalService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;

    private bool _loading = true;
    private bool _error;
    private DateTime? _recordedDate;
    private DateTime? _pendingDate;
    private DateTime? _upcomingDate;
    private string _attendedLabel = "—";
    private string _absentLabel = "—";
    private bool _hasRate;
    private bool _showYtd;

    private DoughnutChart _chart = default!;
    private ChartData _chartData = default!;
    private DoughnutChartOptions _chartOptions = default!;
    private bool _chartInitialized;

    private BarChart _ytdChart = default!;
    private ChartData _ytdChartData = default!;
    private BarChartOptions _ytdChartOptions = default!;
    private bool _ytdChartInitialized;
    private bool _hasYtdData;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var settings = await SettingsService.GetAsync();
            _showYtd = settings?.ShowParticipationGraphs ?? false;

            var recorded = await RehearsalService.GetMostRecentPastWithAttendanceAsync(DateTime.Today);
            var pending = await RehearsalService.GetMostRecentPastWithoutAttendanceAsync(DateTime.Today);
            var upcoming = await RehearsalService.GetNextUpcomingAsync(DateTime.Today.AddDays(1));

            _recordedDate = recorded?.Date;
            _pendingDate = pending?.Date;
            _upcomingDate = upcoming?.Date;

            if (recorded?.StoredAttendanceRate is { } rate)
            {
                double ap = (double)rate;
                double abp = 100.0 - ap;
                _attendedLabel = $"{ap:F1}%";
                _absentLabel = $"{abp:F1}%";
                _hasRate = true;

                _chartData = new ChartData
                {
                    Labels = new List<string> { "Attended", "Absent" },
                    Datasets = new List<IChartDataset>
                    {
                        new DoughnutChartDataset
                        {
                            Data = new List<double?> { ap, abp },
                            BackgroundColor = new List<string> { "#198754", "#dc3545" }
                        }
                    }
                };

                _chartOptions = new DoughnutChartOptions { Responsive = true };
                _chartOptions.Plugins.Legend!.Display = false;
            }

            if (_showYtd)
            {
                var ytdRehearsals = await RehearsalService.GetYearToDateWithAttendanceAsync(DateTime.Today.Year);

                if (ytdRehearsals.Count > 0)
                {
                    var monthly = ytdRehearsals
                        .GroupBy(r => r.Date.Month)
                        .OrderBy(g => g.Key)
                        .Select(g => new
                        {
                            Label = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key),
                            AvgRate = g.Average(r => (double)r.StoredAttendanceRate!.Value)
                        })
                        .ToList();

                    _ytdChartData = new ChartData
                    {
                        Labels = monthly.Select(m => m.Label).ToList(),
                        Datasets = new List<IChartDataset>
                        {
                            new BarChartDataset
                            {
                                Label = "Attended",
                                Data = monthly.Select(m => (double?)m.AvgRate).ToList(),
                                BackgroundColor = new List<string> { "#198754" },
                                BorderColor = new List<string> { "#198754" }
                            },
                            new BarChartDataset
                            {
                                Label = "Absent",
                                Data = monthly.Select(m => (double?)(100.0 - m.AvgRate)).ToList(),
                                BackgroundColor = new List<string> { "#dc3545" },
                                BorderColor = new List<string> { "#dc3545" }
                            }
                        }
                    };

                    _ytdChartOptions = new BarChartOptions { Responsive = true };
                    _ytdChartOptions.Plugins.Legend!.Display = true;
                    _ytdChartOptions.Scales.X!.Stacked = true;
                    _ytdChartOptions.Scales.Y!.Stacked = true;
                    _ytdChartOptions.Scales.Y.Min = 0;
                    _ytdChartOptions.Scales.Y.Max = 100;
                    _hasYtdData = true;
                }
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
        if (!_loading && !_error && _hasRate && !_chartInitialized && _chart is not null)
        {
            _chartInitialized = true;
            await _chart.InitializeAsync(_chartData, _chartOptions);
        }

        if (!_loading && !_error && _hasYtdData && !_ytdChartInitialized && _ytdChart is not null)
        {
            _ytdChartInitialized = true;
            await _ytdChart.InitializeAsync(_ytdChartData, _ytdChartOptions);
        }
    }
}
