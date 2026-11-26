using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Finance;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for RecordIncome page:
/// - Renders account dropdown with available income accounts
/// - Shows warning when no accounts configured
/// - Amount > 0 validation enforced client-side
/// - Account must be selected
/// - Submit calls IIncomeEntryService.RecordIncomeAsync
/// - Success message displayed after save with Record Another option
/// - Account pre-selected when only one account exists
/// </summary>
public class RecordIncomeTests : LocalizedTestContext
{
    private readonly IIncomeEntryService _incomeService = Substitute.For<IIncomeEntryService>();
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IExternalOrganisationService _organisationService = Substitute.For<IExternalOrganisationService>();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid TownHallId = Guid.NewGuid();

    public RecordIncomeTests()
    {
        Services.AddSingleton(_incomeService);
        Services.AddSingleton(_accountService);
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(_organisationService);

        _organisationService.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(new List<ExternalOrganisation>
        {
            new() { Id = TownHallId, Name = "Town Hall", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        });

        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings
            {
                Id = Guid.NewGuid(), OrganizationName = "Test Choir",
                AnnualFee = 50m, AttendanceFee = 10m,
                MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
                MinimumMemberAge = 0, SchemaVersion = "1.1.0",
                IsTaxApplicable = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        _incomeService.GetIncomeAccountsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                MakeAccount(AccountId, "Raffle Income")
            });

        _incomeService.RecordIncomeAsync(Arg.Any<RecordIncomeRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _accountService.GetBankAccountsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                new()
                {
                    Id = SystemAccounts.CashId, Name = "Cash on Hand", Type = AccountType.Asset,
                    AccountNumber = "1100", IsSystem = true, IsBankAccount = true,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            });
    }

    // --- Rendering ---

    [Fact]
    public void Renders_PageTitle_RecordIncome()
    {
        var cut = Render<RecordIncome>();

        Assert.Contains("Record Income", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Renders_AccountDropdown_WithAvailableAccounts()
    {
        var cut = Render<RecordIncome>();

        var select = cut.Find("#account");
        Assert.Contains("Raffle Income", select.InnerHtml);
    }

    [Fact]
    public void Renders_AmountInput()
    {
        var cut = Render<RecordIncome>();

        cut.Find("#amount");
    }

    [Fact]
    public void Renders_DateInput()
    {
        var cut = Render<RecordIncome>();

        cut.Find("#incomeDate");
    }

    [Fact]
    public void Renders_DescriptionTextArea()
    {
        var cut = Render<RecordIncome>();

        cut.Find("#description");
    }

    [Fact]
    public void Renders_ClearButton()
    {
        var cut = Render<RecordIncome>();

        // RecordIncome is embedded as a tab; the old Cancel nav link was replaced
        // with a Clear button that resets the form in-place.
        var clearBtn = cut.Find("button.btn-outline-secondary");
        Assert.Contains("Clear", clearBtn.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    // --- No-accounts state ---

    [Fact]
    public void WhenNoAccounts_ShowsWarning_AndNoForm()
    {
        _incomeService.GetIncomeAccountsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>());

        var cut = Render<RecordIncome>();

        Assert.Empty(cut.FindAll("#account"));
        Assert.Contains("No income accounts", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // --- Pre-selection when single account ---

    [Fact]
    public void WhenSingleAccount_PreSelectsItInDropdown()
    {
        var cut = Render<RecordIncome>();

        // Only one account; code-behind sets _form.AccountId to its Id
        var select = cut.Find("#account");
        Assert.Equal(AccountId.ToString(), select.GetAttribute("value"));
    }

    // --- Validation ---

    [Fact]
    public async Task Submit_WithZeroAmount_ShowsValidationError_DoesNotCallService()
    {
        var cut = Render<RecordIncome>();

        // Leave amount at default (0) and submit
        await cut.Find("button.btn-primary").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("greater than zero", cut.Markup, StringComparison.OrdinalIgnoreCase);
        await _incomeService.DidNotReceive().RecordIncomeAsync(
            Arg.Any<RecordIncomeRequest>(), Arg.Any<CancellationToken>());
    }

    // --- Successful submission ---

    [Fact]
    public async Task Submit_WithValidData_CallsRecordIncomeAsync()
    {
        var cut = Render<RecordIncome>();

        await cut.Find("#amount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs
        {
            Value = "150.00"
        });

        await cut.Find("button.btn-primary").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _incomeService.Received(1).RecordIncomeAsync(
            Arg.Any<RecordIncomeRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_Success_ShowsSuccessMessage()
    {
        var cut = Render<RecordIncome>();

        await cut.Find("#amount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs
        {
            Value = "75.50"
        });

        await cut.Find("button.btn-primary").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("recorded successfully", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Submit_Success_ShowsRecordAnotherButton()
    {
        var cut = Render<RecordIncome>();

        await cut.Find("#amount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs
        {
            Value = "50"
        });

        await cut.Find("button.btn-primary").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("Record Another", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // --- External organisation picker (spec 032 US3, FR-010) ---

    [Fact]
    public void Should_ListOnlyActiveOrganisationsAfterNoneOption_When_PageLoads()
    {
        var cut = Render<RecordIncome>();

        var options = cut.FindAll("#incomeOrganisation option");
        Assert.Equal(2, options.Count);
        Assert.Equal(string.Empty, options[0].GetAttribute("value"));
        Assert.Equal("Town Hall", options[1].TextContent.Trim());
        _organisationService.DidNotReceiveWithAnyArgs().GetArchivedAsync(Xunit.TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_SendChosenOrganisationId_When_Submitted()
    {
        var cut = Render<RecordIncome>();

        cut.Find("#incomeOrganisation").Change(TownHallId.ToString());
        await cut.Find("#amount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "200" });
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _incomeService.Received(1).RecordIncomeAsync(
            Arg.Is<RecordIncomeRequest>(r => r!.ExternalOrganisationId == TownHallId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_SendNullOrganisation_When_NoneChosen()
    {
        var cut = Render<RecordIncome>();

        await cut.Find("#amount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "200" });
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _incomeService.Received(1).RecordIncomeAsync(
            Arg.Is<RecordIncomeRequest>(r => r!.ExternalOrganisationId == null),
            Arg.Any<CancellationToken>());
    }

    // --- Spec 033: payment method, bank account and reference (FR-004) ---

    private static readonly Guid BankId = Guid.NewGuid();

    private void SetBankAccounts(params Account[] banks)
    {
        var petty = new Account
        {
            Id = SystemAccounts.CashId, Name = "Petty Cash", AccountNumber = "1100", Type = AccountType.Asset,
            IsBankAccount = true, IsSystem = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _accountService.GetBankAccountsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>([petty, .. banks]));
    }

    private static Account MakeBank(Guid id, string name) => new()
    {
        Id = id, Name = name, AccountNumber = "1110", Type = AccountType.Asset, IsBankAccount = true,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public void Should_ReplaceDepositToWithPaymentMethod_When_Rendered()
    {
        var cut = Render<RecordIncome>();

        cut.Find("#paymentMethod");
        Assert.Empty(cut.FindAll("#depositAccount"));
        Assert.DoesNotContain("Deposit to", cut.Markup);
        Assert.Equal(PaymentMethod.Cash.ToString(), cut.Find("#paymentMethod").GetAttribute("value"));
    }

    [Fact]
    public void Should_PlaceMethodAfterAccountAndOrganisationBeforeDescription_When_Rendered()
    {
        var cut = Render<RecordIncome>();
        var markup = cut.Markup;

        Assert.True(markup.IndexOf("id=\"account\"", StringComparison.Ordinal)
                    < markup.IndexOf("id=\"paymentMethod\"", StringComparison.Ordinal));
        Assert.True(markup.IndexOf("id=\"paymentMethod\"", StringComparison.Ordinal)
                    < markup.IndexOf("id=\"incomeOrganisation\"", StringComparison.Ordinal));
        Assert.True(markup.IndexOf("id=\"incomeOrganisation\"", StringComparison.Ordinal)
                    < markup.IndexOf("id=\"description\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(PaymentMethod.Card)]
    [InlineData(PaymentMethod.ElectronicTransfer)]
    public void Should_ShowBankPickerOnly_When_MethodIsCardOrEft(PaymentMethod method)
    {
        SetBankAccounts(MakeBank(BankId, "Community Bank"));
        var cut = Render<RecordIncome>();

        cut.Find("#paymentMethod").Change(method.ToString());

        cut.Find("#bankAccount");
        Assert.Empty(cut.FindAll("#reference"));
        Assert.Equal(BankId.ToString(), cut.Find("#bankAccount").GetAttribute("value"));
    }

    [Theory]
    [InlineData(PaymentMethod.Check)]
    [InlineData(PaymentMethod.Other)]
    public void Should_ShowReferenceOnly_When_MethodIsChequeOrOther(PaymentMethod method)
    {
        var cut = Render<RecordIncome>();

        cut.Find("#paymentMethod").Change(method.ToString());

        cut.Find("#reference");
        Assert.Empty(cut.FindAll("#bankAccount"));
    }

    [Fact]
    public void Should_ShowNoBankWarning_When_CardChosenAndNoBankAccountExists()
    {
        var cut = Render<RecordIncome>();
        Assert.DoesNotContain("/finance/accounts", cut.Markup);

        cut.Find("#paymentMethod").Change(PaymentMethod.Card.ToString());

        Assert.Contains("/finance/accounts", cut.Markup);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash)]
    [InlineData(PaymentMethod.Check)]
    public async Task Should_SaveWithoutBank_When_CashOrChequeAndNoBankAccountExists(PaymentMethod method)
    {
        var cut = Render<RecordIncome>();
        cut.Find("#paymentMethod").Change(method.ToString());
        if (method == PaymentMethod.Check)
            cut.Find("#reference").Change(" 1042 ");
        await cut.Find("#amount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "75" });

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _incomeService.Received(1).RecordIncomeAsync(
            Arg.Is<RecordIncomeRequest>(r => r!.PaymentMethod == method && r.BankAccountId == null
                && r.Reference == (method == PaymentMethod.Check ? "1042" : null)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_SendBankAccount_When_CardSubmitted()
    {
        SetBankAccounts(MakeBank(BankId, "Community Bank"));
        var cut = Render<RecordIncome>();
        cut.Find("#paymentMethod").Change(PaymentMethod.Card.ToString());
        await cut.Find("#amount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "75" });

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _incomeService.Received(1).RecordIncomeAsync(
            Arg.Is<RecordIncomeRequest>(r => r!.PaymentMethod == PaymentMethod.Card && r.BankAccountId == BankId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RefuseSave_When_CardChosenWithNoBankAccount()
    {
        var cut = Render<RecordIncome>();
        cut.Find("#paymentMethod").Change(PaymentMethod.Card.ToString());
        await cut.Find("#amount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "75" });

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _incomeService.DidNotReceive().RecordIncomeAsync(
            Arg.Any<RecordIncomeRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Should_PrefixAmountWithConfiguredSymbol_When_Rendered()
    {
        var cut = Render<RecordIncome>();

        Assert.Contains(MoneyFormatter.Symbol, cut.Find(".input-group-text").TextContent);
    }

    // --- Helpers ---

    private static Account MakeAccount(Guid id, string name) => new()
    {
        Id = id, Name = name, Type = AccountType.Income,
        AccountNumber = "4000", IsSystem = false, SortOrder = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
