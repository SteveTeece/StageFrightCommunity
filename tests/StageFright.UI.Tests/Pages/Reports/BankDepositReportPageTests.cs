using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;
using StageFright.Reports.Rendering;
using StageFright.UI.Pages.Reports;

namespace StageFright.UI.Tests.Pages.Reports;

/// <summary>
/// bUnit tests for BankDepositReportPage (issue #399): the read-only deposit record with no reverse
/// action, Print building the report from the deposit shown, and the not-found, load-error and
/// print-error states. The print success path launches the OS PDF viewer, so it is not run here;
/// the renderer is made to fail after the build step to prove the hand-off.
/// </summary>
public class BankDepositReportPageTests : LocalizedTestContext
{
    private static readonly Guid DepositId = Guid.NewGuid();
    private readonly IBankingService _banking = Substitute.For<IBankingService>();
    private readonly IBankDepositReportBuilder _builder = Substitute.For<IBankDepositReportBuilder>();
    private readonly IPdfReportRenderer _pdf = Substitute.For<IPdfReportRenderer>();
    private readonly ISettingsService _settings = Substitute.For<ISettingsService>();
    private readonly ReportData _report = new() { Title = "Bank Deposit" };

    public BankDepositReportPageTests()
    {
        Services.AddSingleton(_banking);
        Services.AddSingleton(_builder);
        Services.AddSingleton(_pdf);
        Services.AddSingleton(_settings);
        _banking.GetDepositAsync(DepositId, Arg.Any<CancellationToken>()).Returns(Recorded());
        _builder.Build(Arg.Any<DepositDetail>()).Returns(_report);
        _settings.GetAsync(Arg.Any<CancellationToken>()).Returns(new StageFright.Core.Entities.Settings { OrganizationName = "StageFright Players" });
        _pdf.Render(Arg.Any<ReportData>(), Arg.Any<string>()).Throws(new InvalidOperationException("no pdf in tests"));
    }

    private static DepositDetail Recorded() => new()
    {
        Id = DepositId, Date = new DateTime(2026, 3, 4), Description = "March takings", BankAccountName = "Community Bank",
        CashAmount = 40m, Total = 90m, CanReverse = true,
        Items = [new AwaitingDepositItem { Id = Guid.NewGuid(), ReceivedDate = new DateTime(2026, 3, 2), PayerName = "Alice Smith", Reference = "CHQ 1", Amount = 50m }]
    };

    private IRenderedComponent<BankDepositReportPage> RenderPage() =>
        Render<BankDepositReportPage>(ps => ps.Add(p => p.Id, DepositId));

    [Fact]
    public void Should_ShowReadOnlyDepositRecord_When_Opened()
    {
        var cut = RenderPage();

        Assert.Equal("March takings", cut.Find("#depositDescription").TextContent);
        Assert.Equal("Community Bank", cut.Find("#depositBank").TextContent);
        Assert.Equal(MoneyFormatter.Format(90m), cut.Find("#depositTotal").TextContent);
        Assert.Contains("Alice Smith", cut.Markup);
        Assert.Empty(cut.FindAll("#reverseDeposit"));
        Assert.Equal("/reports/bank-deposits", cut.Find("#backToDeposits").GetAttribute("href"));
        Assert.NotNull(cut.Find("#printDeposit"));
    }

    [Fact]
    public async Task Should_BuildReportFromShownDepositAndRender_When_PrintClicked()
    {
        var cut = RenderPage();

        await cut.Find("#printDeposit").ClickAsync(new MouseEventArgs());

        _builder.Received(1).Build(Arg.Is<DepositDetail>(d => d!.Id == DepositId));
        _pdf.Received(1).Render(_report, "StageFright Players");
    }

    [Fact]
    public async Task Should_ShowPdfError_When_RenderFails()
    {
        var cut = RenderPage();

        await cut.Find("#printDeposit").ClickAsync(new MouseEventArgs());

        Assert.Equal("Unable to generate PDF. Please try again.", cut.Find("[role=alert]").TextContent);
    }

    [Fact]
    public void Should_ShowNotFoundWithoutPrint_When_DepositMissing()
    {
        _banking.GetDepositAsync(DepositId, Arg.Any<CancellationToken>()).Returns((DepositDetail?)null);

        var cut = RenderPage();

        Assert.NotNull(cut.Find("#depositNotFound"));
        Assert.Empty(cut.FindAll("#printDeposit"));
    }

    [Fact]
    public void Should_ShowLoadErrorWithoutNotFound_When_LoadFails()
    {
        _banking.GetDepositAsync(DepositId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DepositDetail?>(new InvalidOperationException("db down")));

        var cut = RenderPage();

        Assert.NotNull(cut.Find("[role=alert]"));
        Assert.Empty(cut.FindAll("#depositNotFound"));
        Assert.Empty(cut.FindAll("#printDeposit"));
    }
}
