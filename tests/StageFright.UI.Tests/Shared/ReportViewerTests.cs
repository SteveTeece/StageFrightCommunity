using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Rendering;
using StageFright.UI.Shared;

namespace StageFright.UI.Tests.Shared;

/// <summary>
/// bUnit tests for ReportViewer component:
/// - Shows "Generating report..." loading modal on generation
/// - Renders report data once generated
/// - Shows user-friendly error on GenerateAsync failure
/// - Print button invokes IPdfReportRenderer
/// - Export button invokes ICsvReportExporter
/// </summary>
public class ReportViewerTests : LocalizedTestContext
{
    private readonly IReportProvider _provider = Substitute.For<IReportProvider>();
    private readonly IPdfReportRenderer _pdfRenderer = Substitute.For<IPdfReportRenderer>();
    private readonly ICsvReportExporter _csvExporter = Substitute.For<ICsvReportExporter>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    private static readonly ReportData SampleReport = new()
    {
        Title = "Test Report",
        GeneratedAt = DateTime.UtcNow,
        Columns = [new ReportColumn { Header = "Name" }],
        Sections =
        [
            new ReportSection { Rows = [new ReportRow { Cells = ["Alice"] }] }
        ]
    };

    private static readonly ReportData MasterDetailReport = new()
    {
        Title = "Member Account Summary",
        GeneratedAt = DateTime.UtcNow,
        Columns =
        [
            new ReportColumn { Header = "Date / Item" },
            new ReportColumn { Header = "Description" },
            new ReportColumn { Header = "Debit" },
            new ReportColumn { Header = "Credit" },
            new ReportColumn { Header = "Aging" },
            new ReportColumn { Header = "Balance" }
        ],
        SummaryColumns =
        [
            new ReportColumn { Header = "Member" },
            new ReportColumn { Header = "Current" },
            new ReportColumn { Header = "30 Days" },
            new ReportColumn { Header = "60 Days" },
            new ReportColumn { Header = "90+ Days" },
            new ReportColumn { Header = "Balance" }
        ],
        Sections =
        [
            new ReportSection
            {
                Heading = "Amanda Scott",
                SummaryRow = new ReportRow { Cells = ["Amanda Scott", "Current: 0.00", "30 days: 0.00", "60 days: 0.00", "90+ days: 0.00", "5.00"] },
                Rows =
                [
                    new ReportRow { Cells = ["Opening Balance", "", "", "", "", "0.00"] },
                    new ReportRow { Cells = ["Closing Balance", "", "", "", "", "5.00"], IsEmphasized = true }
                ]
            }
        ]
    };

    private static readonly ReportData TwoMemberMasterDetailReport = new()
    {
        Title = "Member Account Summary",
        GeneratedAt = DateTime.UtcNow,
        Columns = MasterDetailReport.Columns,
        SummaryColumns = MasterDetailReport.SummaryColumns,
        Sections =
        [
            new ReportSection
            {
                Heading = "Amanda Scott",
                SummaryRow = new ReportRow { Cells = ["Amanda Scott", "Current: 0.00", "30 days: 0.00", "60 days: 0.00", "90+ days: 0.00", "5.00"] },
                Rows = [new ReportRow { Cells = ["Amanda Detail Marker", "", "", "", "", "0.00"] }]
            },
            new ReportSection
            {
                Heading = "Bob Jones",
                SummaryRow = new ReportRow { Cells = ["Bob Jones", "Current: 0.00", "30 days: 0.00", "60 days: 0.00", "90+ days: 0.00", "10.00"] },
                Rows = [new ReportRow { Cells = ["Bob Detail Marker", "", "", "", "", "0.00"] }]
            }
        ]
    };

    public ReportViewerTests()
    {
        Services.AddSingleton(_pdfRenderer);
        Services.AddSingleton(_csvExporter);
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new Settings { OrganizationName = "Test Organisation" });
        Services.AddSingleton(_settingsService);

        _provider.ReportId.Returns("test-report");
        _provider.ReportName.Returns("Test Report");
        _provider.ModuleName.Returns("Finance");
        _provider.DisplayOrder.Returns(0);
        _provider.Filters.Returns(Array.Empty<ReportFilterDefinition>());
    }

    [Fact]
    public async Task WhenProviderSet_ShowsGeneratingModal()
    {
        // Provider never completes — should show loading state
        var tcs = new TaskCompletionSource<ReportData>();
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));

        Assert.Contains("Generating", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenGenerationSucceeds_RendersReportTitle()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(SampleReport);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));

        await cut.InvokeAsync(() => { }); // allow async initialization

        Assert.Contains("Test Report", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenGenerationSucceeds_RendersDataRows()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(SampleReport);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));

        await cut.InvokeAsync(() => { });

        Assert.Contains("Alice", cut.Markup);
    }

    [Fact]
    public async Task WhenGenerationThrows_ShowsUserFriendlyError()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));

        await cut.InvokeAsync(() => { });

        // Should show error message, not throw
        Assert.Contains("error", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WhenNoProvider_RendersEmpty()
    {
        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, (IReportProvider?)null));

        Assert.DoesNotContain("Generating", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenMemberRowExpanded_ShowsOpeningClosingAndTransactionDetail()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MasterDetailReport);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        cut.Find("button[aria-label='Expand child item']").Click();

        Assert.Contains("Opening Balance", cut.Markup);
        Assert.Contains("Closing Balance", cut.Markup);
    }

    [Fact]
    public async Task WhenMemberRowExpanded_ShowsDebitAndCreditColumnHeaders()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MasterDetailReport);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        cut.Find("button[aria-label='Expand child item']").Click();

        var headerText = cut.FindAll("table thead th").Select(th => th.TextContent.Trim()).ToList();
        Assert.Contains("Debit", headerText);
        Assert.Contains("Credit", headerText);
    }

    [Fact]
    public async Task WhenOneMemberRowExpanded_OtherMemberRowRemainsCollapsed()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(TwoMemberMasterDetailReport);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        cut.FindAll("button[aria-label='Expand child item']")[0].Click();

        Assert.Contains("Amanda Detail Marker", cut.Markup);
        Assert.DoesNotContain("Bob Detail Marker", cut.Markup);
    }

    [Fact]
    public async Task ExpandToggle_IsButtonWithAriaExpandedReflectingState()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MasterDetailReport);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        var button = cut.Find("button[aria-label='Expand child item']");
        Assert.Equal("button", button.TagName.ToLowerInvariant());
        Assert.Equal("false", button.GetAttribute("aria-expanded"));

        button.Click();

        button = cut.Find("button[aria-label='Expand child item']");
        Assert.Equal("true", button.GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task WhenReportRegenerates_PreviouslyExpandedRowResetsToCollapsed()
    {
        // A real provider builds fresh ReportSection instances on every call; simulate that here
        // rather than returning the same static object, since Radzen's expand tracking keys off
        // item identity — reusing the same instance wouldn't exercise FR-011's reset behavior.
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(_ => BuildFreshMasterDetailReport());

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        cut.Find("button[aria-label='Expand child item']").Click();
        Assert.Contains("Opening Balance", cut.Markup);

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Refresh").Click();
        await cut.InvokeAsync(() => { });

        Assert.DoesNotContain("Opening Balance", cut.Markup);
    }

    private static ReportData BuildFreshMasterDetailReport() => new()
    {
        Title = "Member Account Summary",
        GeneratedAt = DateTime.UtcNow,
        Columns = MasterDetailReport.Columns,
        SummaryColumns = MasterDetailReport.SummaryColumns,
        Sections =
        [
            new ReportSection
            {
                Heading = "Amanda Scott",
                SummaryRow = new ReportRow { Cells = ["Amanda Scott", "Current: 0.00", "30 days: 0.00", "60 days: 0.00", "90+ days: 0.00", "5.00"] },
                Rows =
                [
                    new ReportRow { Cells = ["Opening Balance", "", "", "", "", "0.00"] },
                    new ReportRow { Cells = ["Closing Balance", "", "", "", "", "5.00"], IsEmphasized = true }
                ]
            }
        ]
    };

    [Fact]
    public async Task WhenSummaryColumnsPopulated_RendersOneRowPerSectionWithNoDetailVisible()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MasterDetailReport);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        Assert.Contains("Amanda Scott", cut.Markup);
        Assert.DoesNotContain("Opening Balance", cut.Markup);
        Assert.DoesNotContain("Closing Balance", cut.Markup);
    }

    private static ReportData MakeReport(int rowCount) => new()
    {
        Title = "Paging Test",
        GeneratedAt = DateTime.UtcNow,
        Columns = [new ReportColumn { Header = "Name" }],
        Sections =
        [
            new ReportSection
            {
                Rows = Enumerable.Range(1, rowCount)
                    .Select(i => new ReportRow { Cells = [$"Row {i:D3}"] })
                    .ToList()
            }
        ]
    };

    [Fact]
    public async Task Should_NotShowPagination_When_RowCountAtPageSize()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MakeReport(15));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        Assert.DoesNotContain("page-link", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_ShowPagination_When_RowCountExceedsPageSize()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MakeReport(16));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        Assert.Contains("Page 1 of 2", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_ShowOnlyFirstPageRows_Initially_When_ReportHasManyRows()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MakeReport(16));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        Assert.Contains("Row 015", cut.Markup);
        Assert.DoesNotContain("Row 016", cut.Markup);
    }

    [Fact]
    public async Task Should_ShowNextPageRows_When_NextButtonClicked()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MakeReport(16));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        cut.Find("button[aria-label='Next page']").Click();

        Assert.Contains("Row 016", cut.Markup);
        Assert.Contains("Page 2 of 2", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Row 015", cut.Markup);
    }

    [Fact]
    public async Task Should_ShowPreviousPageRows_When_PrevButtonClicked()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MakeReport(16));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        cut.Find("button[aria-label='Next page']").Click();
        cut.Find("button[aria-label='Previous page']").Click();

        Assert.Contains("Row 001", cut.Markup);
        Assert.Contains("Page 1 of 2", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Row 016", cut.Markup);
    }

    // --- Filter type rendering ---

    [Fact]
    public async Task Should_RenderDropdown_When_FilterTypeIsSelect()
    {
        _provider.Filters.Returns(
        [
            new ReportFilterDefinition { Key = "status", Type = ReportFilterType.Select, Label = "Status", Options = ["Active", "Archived"], DefaultValue = "Active" }
        ]);
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>()).Returns(SampleReport);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        var select = cut.Find("select");
        Assert.Equal(2, select.Children.Length);
    }

    [Fact]
    public async Task Should_RegenerateWithChosenOption_When_SelectChangedAndApplyClicked()
    {
        _provider.Filters.Returns(
        [
            new ReportFilterDefinition { Key = "status", Type = ReportFilterType.Select, Label = "Status", Options = ["Active", "Archived"], DefaultValue = "Active" }
        ]);
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>()).Returns(SampleReport);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        cut.Find("select").Change("Archived");
        cut.Find("button.btn-primary").Click();

        await _provider.Received().GenerateAsync(
            Arg.Is<ReportFilterValues>(f => f!.Get("status") == "Archived"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RenderCheckbox_When_FilterTypeIsBoolean()
    {
        _provider.Filters.Returns(
        [
            new ReportFilterDefinition { Key = "compare", Type = ReportFilterType.Boolean, Label = "Compare", DefaultValue = "false" }
        ]);
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>()).Returns(SampleReport);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        var checkbox = cut.Find("input[type=checkbox]");
        Assert.False(checkbox.HasAttribute("checked"));
    }

    [Fact]
    public async Task Should_RenderTextInput_When_FilterTypeIsText()
    {
        _provider.Filters.Returns(
        [
            new ReportFilterDefinition { Key = "account", Type = ReportFilterType.Text, Label = "Account", DefaultValue = "" }
        ]);
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>()).Returns(SampleReport);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        Assert.NotNull(cut.Find("input[type=text]"));
    }

    [Fact]
    public async Task Should_RenderDateInput_When_FilterTypeIsDate()
    {
        _provider.Filters.Returns(
        [
            new ReportFilterDefinition { Key = "asAt", Type = ReportFilterType.Date, Label = "As at", DefaultValue = "2026-06-30" }
        ]);
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>()).Returns(SampleReport);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        Assert.NotNull(cut.Find("input[type=date]"));
    }
}
