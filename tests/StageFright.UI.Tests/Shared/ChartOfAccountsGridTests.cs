using Bunit;
using Microsoft.AspNetCore.Components;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Shared;

namespace StageFright.UI.Tests.Shared;

/// <summary>
/// bUnit tests for ChartOfAccountsGrid (issue #367) — the one Chart of Accounts grid shared by
/// Finance → Chart of Accounts and Reports → Chart of Accounts: a header row per account type in
/// report order, accounts in number order beneath, Balance column driven by IncludeBalances, and
/// the optional row-actions / rename-edit slots.
/// </summary>
public class ChartOfAccountsGridTests : LocalizedTestContext
{
    private static readonly AccountBalance Cash = Make("1100", "Cash on Hand", AccountType.Asset, balance: 120m, isSystem: true);
    private static readonly AccountBalance Bank = Make("1200", "Bank", AccountType.Asset, balance: 3400m, isBank: true);
    private static readonly AccountBalance Payable = Make("2100", "Accounts Payable", AccountType.Liability, balance: 0m);
    private static readonly AccountBalance Donations = Make("4100", "Donations", AccountType.Income, balance: 75m);
    private static readonly AccountBalance Hire = Make("6100", "Venue Hire", AccountType.Expense, balance: 40m);

    private IRenderedComponent<ChartOfAccountsGrid> RenderGrid(
        IEnumerable<AccountBalance>? accounts = null,
        bool includeBalances = false,
        RenderFragment<AccountBalance>? rowActions = null) =>
        Render<ChartOfAccountsGrid>(p =>
        {
            p.Add(x => x.Accounts, accounts ?? [Hire, Donations, Bank, Payable, Cash]);
            p.Add(x => x.IncludeBalances, includeBalances);
            if (rowActions is not null)
                p.Add(x => x.RowActions, rowActions);
        });

    [Fact]
    public void Should_RenderHeaderRowPerAccountType_InReportOrder_When_AccountsSpanTypes()
    {
        var cut = RenderGrid();

        var headings = cut.FindAll(".chart-of-accounts-section").Select(h => h.TextContent.Trim()).ToList();

        Assert.Equal(["Assets", "Liabilities", "Income", "Expenses"], headings);
    }

    [Fact]
    public void Should_ListAccountsByNumberUnderTheirHeader_When_Rendered()
    {
        var cut = RenderGrid();

        var numbers = cut.FindAll("code").Select(c => c.TextContent).ToList();

        Assert.Equal(["1100", "1200", "2100", "4100", "6100"], numbers);
    }

    [Fact]
    public void Should_HideBalanceColumn_When_IncludeBalancesOff()
    {
        var cut = RenderGrid(includeBalances: false);

        Assert.DoesNotContain(cut.FindAll("th"), th => th.TextContent.Contains("Balance"));
        Assert.DoesNotContain("3,400.00", cut.Markup);
    }

    [Fact]
    public void Should_ShowFormattedBalances_When_IncludeBalancesOn()
    {
        var cut = RenderGrid(includeBalances: true);

        Assert.Contains(cut.FindAll("th"), th => th.TextContent.Contains("Balance"));
        Assert.Contains("3,400.00", cut.Markup);
    }

    [Fact]
    public void Should_ShowErrorIndicator_When_BalanceHasError()
    {
        var broken = Make("1300", "Broken", AccountType.Asset, balance: null);
        broken.HasError = true;

        var cut = RenderGrid([broken], includeBalances: true);

        Assert.Single(cut.FindAll("span.text-danger"));
    }

    [Fact]
    public void Should_ShowSystemAndBankBadges_When_AccountsAreFlagged()
    {
        var cut = RenderGrid();

        Assert.Contains(cut.FindAll(".badge"), b => b.TextContent.Contains("System"));
        Assert.Contains(cut.FindAll(".badge"), b => b.TextContent.Contains("Bank"));
    }

    [Fact]
    public void Should_RenderNoActionsColumn_When_RowActionsUnset()
    {
        var cut = RenderGrid();

        Assert.Empty(cut.FindAll(".row-action"));
    }

    [Fact]
    public void Should_RenderRowActionsPerAccount_When_RowActionsSet()
    {
        RenderFragment<AccountBalance> actions = account => builder =>
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "class", "row-action");
            builder.AddContent(2, account.Name);
            builder.CloseElement();
        };

        var cut = RenderGrid(rowActions: actions);

        Assert.Equal(5, cut.FindAll(".row-action").Count);
    }

    [Fact]
    public void Should_RenderEditTemplate_ForEditingAccountOnly_When_EditingAccountIdSet()
    {
        RenderFragment<AccountBalance> editor = account => builder =>
        {
            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "class", "rename-input");
            builder.AddAttribute(2, "value", account.Name);
            builder.CloseElement();
        };

        var cut = Render<ChartOfAccountsGrid>(p => p
            .Add(x => x.Accounts, new[] { Cash, Bank })
            .Add(x => x.EditingAccountId, Bank.AccountId)
            .Add(x => x.EditTemplate, editor));

        Assert.Equal("Bank", Assert.Single(cut.FindAll(".rename-input")).GetAttribute("value"));
        Assert.Contains("Cash on Hand", cut.Markup);
    }

    [Fact]
    public void Should_RenderNoHeaderRows_When_NoAccounts()
    {
        var cut = RenderGrid([]);

        Assert.Empty(cut.FindAll(".chart-of-accounts-section"));
    }

    private static AccountBalance Make(string number, string name, AccountType type, decimal? balance,
        bool isSystem = false, bool isBank = false) => new()
    {
        AccountId = Guid.NewGuid(),
        AccountNumber = number,
        Name = name,
        Type = type,
        Balance = balance,
        IsSystem = isSystem,
        IsBankAccount = isBank
    };
}
