using System.Globalization;
using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;

namespace StageFright.UI.Modules.Finance;

/// <summary>
/// Dashboard tile body for outstanding member fee balances (design 4): member count and
/// per-fee-type outstanding totals, plus a calendar-year trend chart plotting all three
/// metrics (members owing, Attendance $, Annual $) as separate lines.
/// </summary>
public partial class OutstandingBalancesTile : ComponentBase
{
    [Inject] private IMemberBalanceService MemberBalanceService { get; set; } = null!;
    [Inject] private IFinanceSummaryService FinanceSummaryService { get; set; } = null!;

    private bool _loading = true;
    private bool _error;
    private bool _hasChartData;
    private int _memberCount;
    private decimal _attendanceOutstanding;
    private decimal _annualOutstanding;

    private LineChart _chart = default!;
    private ChartData _chartData = default!;
    private LineChartOptions _chartOptions = default!;
    private bool _chartInitialized;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var balancesTask = MemberBalanceService.GetAllMemberBalancesAsync();
            var summaryTask = FinanceSummaryService.GetOutstandingFeeSummaryAsync();
            var trendTask = FinanceSummaryService.GetOutstandingBalanceTrendAsync(DateTime.Today);

            await Task.WhenAll(balancesTask, summaryTask, trendTask);

            var balances = await balancesTask;
            var summary = await summaryTask;
            var trend = await trendTask;

            _memberCount = balances.Count;
            _attendanceOutstanding = summary.OutstandingAttendanceFees;
            _annualOutstanding = summary.OutstandingAnnualFees;

            if (trend.Any(m => m.MemberCount != 0 || m.OutstandingAttendanceFees != 0m || m.OutstandingAnnualFees != 0m))
            {
                _chartData = new ChartData
                {
                    Labels = trend
                        .Select(m => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m.Month))
                        .ToList(),
                    Datasets = new List<IChartDataset>
                    {
                        new LineChartDataset
                        {
                            Label = "Members Owing",
                            Data = trend.Select(m => (double?)m.MemberCount).ToList(),
                            BorderColor = "#8a8fff",
                            BackgroundColor = "rgba(138, 143, 255, 0.15)",
                            PointBackgroundColor = new List<string> { "#8a8fff" },
                            BorderWidth = 2
                        },
                        new LineChartDataset
                        {
                            Label = "Attendance",
                            Data = trend.Select(m => (double?)(double)m.OutstandingAttendanceFees).ToList(),
                            BorderColor = "#ff7d92",
                            BackgroundColor = "rgba(255, 125, 146, 0.15)",
                            PointBackgroundColor = new List<string> { "#ff7d92" },
                            BorderWidth = 2
                        },
                        new LineChartDataset
                        {
                            Label = "Annual",
                            Data = trend.Select(m => (double?)(double)m.OutstandingAnnualFees).ToList(),
                            BorderColor = "#ffb020",
                            BackgroundColor = "rgba(255, 176, 32, 0.15)",
                            PointBackgroundColor = new List<string> { "#ffb020" },
                            BorderWidth = 2
                        }
                    }
                };

                _chartOptions = new LineChartOptions { Responsive = true };
                _chartOptions.Plugins.Legend!.Display = true;
                _hasChartData = true;
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
        if (!_loading && !_error && _hasChartData && !_chartInitialized && _chart is not null)
        {
            _chartInitialized = true;
            await _chart.InitializeAsync(_chartData, _chartOptions);
        }
    }
}
