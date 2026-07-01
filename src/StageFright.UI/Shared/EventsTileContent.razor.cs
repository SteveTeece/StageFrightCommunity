using System.Globalization;
using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;

namespace StageFright.UI.Shared;

public partial class EventsTileContent : ComponentBase
{
    [Inject] private IEventService EventService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;

    private bool _loading = true;
    private bool _error;
    private DateTime? _recordedDate;
    private DateTime? _pendingDate;
    private DateTime? _upcomingDate;
    private string _participatedLabel = "—";
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

            var recorded = await EventService.GetMostRecentPastWithParticipationAsync(DateTime.Today);
            var pending = await EventService.GetMostRecentPastWithoutParticipationAsync(DateTime.Today);
            var upcoming = await EventService.GetNextUpcomingAsync(DateTime.Today.AddDays(1));

            _recordedDate = recorded?.Date;
            _pendingDate = pending?.Date;
            _upcomingDate = upcoming?.Date;

            if (recorded?.StoredParticipationRate is { } rate)
            {
                double pp = (double)rate;
                double abp = 100.0 - pp;
                _participatedLabel = $"{pp:F1}%";
                _absentLabel = $"{abp:F1}%";
                _hasRate = true;

                _chartData = new ChartData
                {
                    Labels = new List<string> { "Participated", "Absent" },
                    Datasets = new List<IChartDataset>
                    {
                        new DoughnutChartDataset
                        {
                            Data = new List<double?> { pp, abp },
                            BackgroundColor = new List<string> { "#fd7e14", "#dc3545" }
                        }
                    }
                };

                _chartOptions = new DoughnutChartOptions { Responsive = true };
                _chartOptions.Plugins.Legend!.Display = false;
            }

            if (_showYtd)
            {
                var ytdEvents = await EventService.GetYearToDateWithParticipationAsync(DateTime.Today.Year);

                if (ytdEvents.Count > 0)
                {
                    var monthly = ytdEvents
                        .GroupBy(e => e.Date.Month)
                        .OrderBy(g => g.Key)
                        .Select(g => new
                        {
                            Label = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key),
                            AvgRate = g.Average(e => (double)e.StoredParticipationRate!.Value)
                        })
                        .ToList();

                    _ytdChartData = new ChartData
                    {
                        Labels = monthly.Select(m => m.Label).ToList(),
                        Datasets = new List<IChartDataset>
                        {
                            new BarChartDataset
                            {
                                Label = "Participated",
                                Data = monthly.Select(m => (double?)m.AvgRate).ToList(),
                                BackgroundColor = new List<string> { "#fd7e14" },
                                BorderColor = new List<string> { "#fd7e14" }
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
