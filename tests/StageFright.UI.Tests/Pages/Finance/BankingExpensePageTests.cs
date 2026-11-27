using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Finance;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for BankingExpensePage (issue #400): the Banking screen's Record expense page shows
/// the standard expense form, can pay from Petty Cash or a bank account, records through the
/// expense service, and always offers a Back to Banking link.
/// </summary>
public class BankingExpensePageTests : LocalizedTestContext
{
    private readonly IExpensePaymentService _expenseService = Substitute.For<IExpensePaymentService>();
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IExternalOrganisationService _organisationService = Substitute.For<IExternalOrganisationService>();

    private static readonly Guid BankFeesId = Guid.NewGuid();
    private static readonly Guid OperatingId = Guid.NewGuid();

    public BankingExpensePageTests()
    {
        Services.AddSingleton(_expenseService);
        Services.AddSingleton(_accountService);
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(_organisationService);

        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(new AppSettings
        {
            Id = Guid.NewGuid(), OrganizationName = "Test Choir",
            AnnualFee = 50m, AttendanceFee = 10m,
            MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
            MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            IsTaxApplicable = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        _expenseService.GetExpenseAccountsAsync(Arg.Any<CancellationToken>()).Returns(new List<Account>
        {
            MakeAccount(BankFeesId, "Bank Fees", AccountType.Expense, "6100")
        });
        _accountService.GetBankAccountsAsync(Arg.Any<CancellationToken>()).Returns(new List<Account>
        {
            MakeAccount(SystemAccounts.CashId, "Petty Cash", AccountType.Asset, "1100", isBank: true),
            MakeAccount(OperatingId, "Operating Account", AccountType.Asset, "1110", isBank: true)
        });
        _organisationService.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(new List<ExternalOrganisation>());
    }

    [Fact]
    public void Should_RouteFromBanking_When_PageDeclared()
    {
        var routes = typeof(BankingExpensePage).GetCustomAttributes(typeof(RouteAttribute), false)
            .Cast<RouteAttribute>().Select(r => r.Template).ToList();

        Assert.Equal(["/finance/banking/expense"], routes);
    }

    [Fact]
    public void Should_ShowExpenseFormAndBackToBankingLink_When_Rendered()
    {
        var cut = Render<BankingExpensePage>();

        Assert.Equal("Record Expense", cut.Find("h4").TextContent);
        Assert.NotNull(cut.Find("#expenseAmount"));
        var link = cut.Find("#backToBanking");
        Assert.Equal("/finance/banking", link.GetAttribute("href"));
        Assert.Equal("Back to Banking", link.TextContent);
    }

    [Fact]
    public void Should_OfferPettyCashAndBankAccounts_When_ChoosingWhereMoneyCameFrom()
    {
        var cut = Render<BankingExpensePage>();

        var values = cut.FindAll("#bankAccount option").Select(o => o.GetAttribute("value")).ToList();

        Assert.Contains(SystemAccounts.CashId.ToString(), values);
        Assert.Contains(OperatingId.ToString(), values);
    }

    [Fact]
    public async Task Should_RecordExpenseFromPettyCash_When_Submitted()
    {
        RecordExpenseRequest? sent = null;
        await _expenseService.RecordExpenseAsync(Arg.Do<RecordExpenseRequest>(r => sent = r), Arg.Any<CancellationToken>());
        var cut = Render<BankingExpensePage>();

        await cut.Find("#bankAccount").ChangeAsync(new ChangeEventArgs { Value = SystemAccounts.CashId.ToString() });
        await cut.Find("#expenseAmount").ChangeAsync(new ChangeEventArgs { Value = "12.50" });
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        Assert.NotNull(sent);
        Assert.Equal(SystemAccounts.CashId, sent!.BankAccountId);
        Assert.Equal(BankFeesId, sent.ExpenseAccountId);
        Assert.Equal(12.50m, sent.Amount);
        Assert.NotEmpty(cut.FindAll(".alert-success"));
        Assert.Equal("/finance/banking", cut.Find("#backToBanking").GetAttribute("href"));
    }

    [Fact]
    public void Should_ShowBackToBankingLink_When_FormFailsToLoad()
    {
        _expenseService.GetExpenseAccountsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<Account>>(new DataAccessException("disk unavailable", "Account", "GetAll")));

        var cut = Render<BankingExpensePage>();

        Assert.NotEmpty(cut.FindAll(".alert-danger"));
        Assert.Equal("/finance/banking", cut.Find("#backToBanking").GetAttribute("href"));
    }

    private static Account MakeAccount(Guid id, string name, AccountType type, string number, bool isBank = false) => new()
    {
        Id = id, Name = name, Type = type, AccountNumber = number, IsBankAccount = isBank,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
