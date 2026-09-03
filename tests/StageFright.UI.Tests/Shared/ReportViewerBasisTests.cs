using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Rendering;
using StageFright.UI.Shared;

namespace StageFright.UI.Tests.Shared;

/// <summary>
/// T041 / FR-012 (spec 028): <see cref="ReportViewer"/> shows <see cref="ReportData.BasisOfAccounting"/>
/// beneath the subtitle when the provider sets it, and renders no basis line when it is null.
/// </summary>
public class ReportViewerBasisTests : LocalizedTestContext
{
    private const string Basis =
        "Basis of accounting: member fees are recognised when levied (accrual basis); " +
        "all other income and expenditure is recognised when received or paid (cash basis).";

    private readonly IReportProvider _provider = Substitute.For<IReportProvider>();
    private readonly IPdfReportRenderer _pdfRenderer = Substitute.For<IPdfReportRenderer>();
    private readonly ICsvReportExporter _csvExporter = Substitute.For<ICsvReportExporter>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    public ReportViewerBasisTests()
    {
        Services.AddSingleton(_pdfRenderer);
        Services.AddSingleton(_csvExporter);
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new Settings { OrganizationName = "Test Organisation" });
        Services.AddSingleton(_settingsService);

        _provider.ReportId.Returns("balance-sheet");
        _provider.ReportName.Returns("Statement of Financial Position");
        _provider.ModuleName.Returns("Finance");
        _provider.DisplayOrder.Returns(0);
        _provider.Filters.Returns(Array.Empty<ReportFilterDefinition>());
    }

    private static ReportData Report(string? basis) => new()
    {
        Title = "Statement of Financial Position",
        SubTitle = "As at 30 June 2026",
        GeneratedAt = DateTime.UtcNow,
        Columns = [new ReportColumn { Header = "Account" }],
        Sections = [new ReportSection { Rows = [new ReportRow { Cells = ["Cash"] }] }],
        BasisOfAccounting = basis
    };

    [Fact]
    public async Task WhenReportHasBasisOfAccounting_RendersItBeneathTheSubtitle()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(Report(Basis));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        var markup = cut.Markup;
        Assert.Contains("accrual basis", markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cash basis", markup, StringComparison.OrdinalIgnoreCase);

        // Document order: the basis line follows the subtitle.
        Assert.True(
            markup.IndexOf("As at 30 June 2026", StringComparison.Ordinal)
            < markup.IndexOf("accrual basis", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenReportHasNoBasisOfAccounting_RendersNoBasisLine()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(Report(null));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        Assert.DoesNotContain("basis of accounting", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }
}
