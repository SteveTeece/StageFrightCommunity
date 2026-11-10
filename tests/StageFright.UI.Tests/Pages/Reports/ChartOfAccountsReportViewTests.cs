using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Reports;

namespace StageFright.UI.Tests.Pages.Reports;

/// <summary>
/// bUnit tests for ChartOfAccountsReportView — the Reports → Chart of Accounts on-screen body
/// (issue #367): loads active account balances into the shared, read-only ChartOfAccountsGrid.
/// </summary>
public class ChartOfAccountsReportViewTests : LocalizedTestContext
{
    private readonly IAccountBalanceService _accountBalanceService = Substitute.For<IAccountBalanceService>();

    public ChartOfAccountsReportViewTests()
    {
        Services.AddSingleton(_accountBalanceService);
    }

    [Fact]
    public void Should_RenderReadOnlyGridWithBalances_When_IncludeBalancesSet()
    {
        _accountBalanceService.GetActiveAccountBalancesAsync(Arg.Any<CancellationToken>()).Returns(new List<AccountBalance>
        {
            new() { AccountId = Guid.NewGuid(), AccountNumber = "1100", Name = "Cash on Hand", Type = AccountType.Asset, Balance = 500m }
        });

        var cut = Render<ChartOfAccountsReportView>(p => p.Add(x => x.IncludeBalances, true));

        Assert.Equal("Assets", cut.Find(".chart-of-accounts-section").TextContent.Trim());
        Assert.Contains("$500.00", cut.Markup);
        Assert.Empty(cut.FindAll("button.btn"));
    }

    [Fact]
    public void Should_ShowErrorAlert_When_BalancesFailToLoad()
    {
        _accountBalanceService.GetActiveAccountBalancesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = Render<ChartOfAccountsReportView>();

        Assert.Contains("InvalidOperationException", cut.Find(".alert-danger").TextContent);
        Assert.Empty(cut.FindAll(".chart-of-accounts-section"));
    }
}
