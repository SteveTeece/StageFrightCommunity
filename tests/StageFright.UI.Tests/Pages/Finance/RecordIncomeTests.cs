using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
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
public class RecordIncomeTests : BunitContext
{
    private readonly IIncomeEntryService _incomeService = Substitute.For<IIncomeEntryService>();
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private static readonly Guid AccountId = Guid.NewGuid();

    public RecordIncomeTests()
    {
        Services.AddSingleton(_incomeService);
        Services.AddSingleton(_accountService);
        Services.AddSingleton(_settingsService);

        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings
            {
                Id = Guid.NewGuid(), OrganizationName = "Test Choir",
                AnnualFee = 50m, AttendanceFee = 10m,
                MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
                MinimumMemberAge = 0, SchemaVersion = "1.1.0",
                IsGstRegistered = false,
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

    // --- Helpers ---

    private static Account MakeAccount(Guid id, string name) => new()
    {
        Id = id, Name = name, Type = AccountType.Income,
        AccountNumber = "4000", IsSystem = false, SortOrder = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
