using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Finance;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for FinancePage — the Finance Overview tabbed screen. Verifies the
/// "Record Expense" tab's position, content, and query-string navigation state
/// (FR-008/FR-009/FR-011), and that recording a member payment happens inline on the
/// Outstanding tab (no separate "Record Member Payment" tab).
/// </summary>
public class FinancePageTests : BunitContext
{
    private readonly IMemberBalanceService _memberBalanceService = Substitute.For<IMemberBalanceService>();
    private readonly IPaymentService _paymentService = Substitute.For<IPaymentService>();
    private readonly IMemberService _memberService = Substitute.For<IMemberService>();
    private readonly IIncomeEntryService _incomeEntryService = Substitute.For<IIncomeEntryService>();
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IExpensePaymentService _expensePaymentService = Substitute.For<IExpensePaymentService>();

    public FinancePageTests()
    {
        Services.AddSingleton(_memberBalanceService);
        Services.AddSingleton(_paymentService);
        Services.AddSingleton(_memberService);
        Services.AddSingleton(_incomeEntryService);
        Services.AddSingleton(_accountService);
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(_expensePaymentService);

        _memberBalanceService.GetAllMemberBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MemberBalance>());
        _incomeEntryService.GetIncomeAccountsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>());
        _accountService.GetBankAccountsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                new()
                {
                    Id = Guid.NewGuid(), Name = "Operating Account", Type = AccountType.Asset,
                    AccountNumber = "1110", IsBankAccount = true,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            });
        _expensePaymentService.GetExpenseAccountsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                new()
                {
                    Id = Guid.NewGuid(), Name = "Venue Hire", Type = AccountType.Expense,
                    AccountNumber = "6000", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            });
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns((StageFright.Core.Entities.Settings?)null);
    }

    [Fact]
    public void Should_RenderTabsInOrder_When_PageLoads()
    {
        JSInterop.SetupVoid("window.blazorBootstrap.tabs.initialize", _ => true);

        var cut = Render<FinancePage>();

        var outstandingIndex = cut.Markup.IndexOf("Outstanding", StringComparison.Ordinal);
        var recordIncomeIndex = cut.Markup.IndexOf("Record Income", StringComparison.Ordinal);
        var recordExpenseIndex = cut.Markup.IndexOf("Record Expense", StringComparison.Ordinal);
        var annualFeesIndex = cut.Markup.IndexOf("Apply Annual Fees", StringComparison.Ordinal);

        Assert.True(outstandingIndex >= 0);
        Assert.True(recordIncomeIndex > outstandingIndex);
        Assert.True(recordExpenseIndex > recordIncomeIndex);
        Assert.True(annualFeesIndex > recordExpenseIndex);
        Assert.DoesNotContain("Record Member Payment", cut.Markup);
    }

    [Fact]
    public void Should_RenderExpenseForm_When_PageLoads()
    {
        JSInterop.SetupVoid("window.blazorBootstrap.tabs.initialize", _ => true);

        var cut = Render<FinancePage>();

        // ExpensePaymentPage's own form fields confirm the Record Expense tab renders the
        // real expense-recording component (embedded as-is), not a placeholder link.
        Assert.Contains("Paid from", cut.Markup);
        Assert.Contains("Expense account", cut.Markup);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("outstanding", 0)]
    [InlineData("record-income", 1)]
    [InlineData("record-expense", 2)]
    [InlineData("annual-fees", 3)]
    public void OnInitialized_SetsDefaultTabIndex_FromTabQuery(string? tabQuery, int expectedIndex)
    {
        var page = CreateUninitializedPage(tabQuery);

        InvokeOnInitialized(page);

        Assert.Equal(expectedIndex, GetPrivateProperty<int>(page, "DefaultTabIndex"));
    }

    [Fact]
    public void OnInitialized_SetsSelectedMemberId_FromMemberIdQuery()
    {
        var memberId = Guid.NewGuid();
        var page = CreateUninitializedPage(tabQuery: "outstanding", memberIdQuery: memberId);

        InvokeOnInitialized(page);

        Assert.Equal(memberId, GetPrivateProperty<Guid>(page, "SelectedMemberId"));
    }

    [Fact]
    public void OnInitialized_DefaultsSelectedMemberIdToEmpty_WhenNoMemberIdQuery()
    {
        var page = CreateUninitializedPage(tabQuery: null);

        InvokeOnInitialized(page);

        Assert.Equal(Guid.Empty, GetPrivateProperty<Guid>(page, "SelectedMemberId"));
    }

    [Fact]
    public void NavToTab_NavigatesToFinanceWithRecordExpenseTabKey()
    {
        JSInterop.SetupVoid("window.blazorBootstrap.tabs.initialize", _ => true);
        var cut = Render<FinancePage>();

        typeof(FinancePage).GetMethod("NavToTab", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(cut.Instance, new object[] { "record-expense" });

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/finance?tab=record-expense", nav.Uri);
    }

    // --- Reflection helpers: [SupplyParameterFromQuery]/[Inject] properties are private,
    // and there's no Router pipeline in a bare bUnit render to populate TabQuery from the URL. ---

    private static FinancePage CreateUninitializedPage(string? tabQuery, Guid? memberIdQuery = null)
    {
        var page = new FinancePage();
        var type = typeof(FinancePage);

        type.GetProperty("TabQuery", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(page, tabQuery);
        type.GetProperty("MemberIdQuery", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(page, memberIdQuery);

        return page;
    }

    private static void InvokeOnInitialized(FinancePage page) =>
        typeof(FinancePage).GetMethod("OnInitialized", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(page, null);

    private static T GetPrivateProperty<T>(FinancePage page, string name) =>
        (T)typeof(FinancePage).GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(page)!;
}
