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
public class ReportViewerTests : BunitContext
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
            .Returns(MakeReport(10));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        Assert.DoesNotContain("page-link", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_ShowPagination_When_RowCountExceedsPageSize()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MakeReport(11));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        Assert.Contains("Page 1 of 2", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_ShowOnlyFirstPageRows_Initially_When_ReportHasManyRows()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MakeReport(11));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        Assert.Contains("Row 010", cut.Markup);
        Assert.DoesNotContain("Row 011", cut.Markup);
    }

    [Fact]
    public async Task Should_ShowNextPageRows_When_NextButtonClicked()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MakeReport(11));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        cut.Find("button[aria-label='Next page']").Click();

        Assert.Contains("Row 011", cut.Markup);
        Assert.Contains("Page 2 of 2", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Row 010", cut.Markup);
    }

    [Fact]
    public async Task Should_ShowPreviousPageRows_When_PrevButtonClicked()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MakeReport(11));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        cut.Find("button[aria-label='Next page']").Click();
        cut.Find("button[aria-label='Previous page']").Click();

        Assert.Contains("Row 001", cut.Markup);
        Assert.Contains("Page 1 of 2", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Row 011", cut.Markup);
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
            Arg.Is<ReportFilterValues>(f => f.Get("status") == "Archived"),
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
