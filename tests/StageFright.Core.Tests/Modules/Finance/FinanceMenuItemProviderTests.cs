using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for FinanceMenuItemProvider: confirms "Record Expense" is no longer a
/// standalone Finance sub-item (FR-007) now that it's reachable via the Finance Overview
/// tab instead, while the other expanded accounting sub-items remain untouched.
/// </summary>
public class FinanceMenuItemProviderTests
{
    private readonly FinanceMenuItemProvider _sut = new();

    [Fact]
    public void GetMenuItems_DoesNotIncludeRecordExpense_AsSubItem()
    {
        var financeItem = Assert.Single(_sut.GetMenuItems());

        Assert.DoesNotContain(financeItem.SubItems!, item => item.Title == "Record Expense");
    }

    [Theory]
    [InlineData("Overview")]
    [InlineData("Chart of Accounts")]
    [InlineData("Record Bank Deposit")]
    [InlineData("Journal Entries")]
    [InlineData("Reconciliation")]
    [InlineData("Opening Balances")]
    public void GetMenuItems_StillIncludes_OtherSubItems(string expectedTitle)
    {
        var financeItem = Assert.Single(_sut.GetMenuItems());

        Assert.Contains(financeItem.SubItems!, item => item.Title == expectedTitle);
    }

    [Fact]
    public void GetMenuItems_ChartOfAccounts_StillRoutesToFinanceAccounts()
    {
        var financeItem = Assert.Single(_sut.GetMenuItems());

        var chartOfAccounts = Assert.Single(financeItem.SubItems!, item => item.Title == "Chart of Accounts");
        Assert.Equal("/finance/accounts", chartOfAccounts.Route);
    }

    [Fact]
    public void GetMenuItems_RecordBankDeposit_RoutesToFinanceBankDeposit()
    {
        var financeItem = Assert.Single(_sut.GetMenuItems());

        var bankDeposit = Assert.Single(financeItem.SubItems!, item => item.Title == "Record Bank Deposit");
        Assert.Equal("/finance/bank-deposit", bankDeposit.Route);
    }
}
