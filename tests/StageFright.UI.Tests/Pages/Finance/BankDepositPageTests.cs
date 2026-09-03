using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Finance;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for BankDepositPage:
/// - Renders destination dropdown with eligible bank accounts (excludes Cash on Hand)
/// - Shows warning when the only bank account is Cash on Hand
/// - Amount &gt; 0 validation enforced client-side
/// - Destination account must be selected
/// - Submit calls IBankDepositService.RecordDepositAsync
/// - Success message displayed after save with Record Another option
/// </summary>
public class BankDepositPageTests : LocalizedTestContext
{
    private readonly IBankDepositService _bankDepositService = Substitute.For<IBankDepositService>();
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();
    private static readonly Guid BankAccountId = Guid.NewGuid();

    public BankDepositPageTests()
    {
        Services.AddSingleton(_bankDepositService);
        Services.AddSingleton(_accountService);

        _accountService.GetBankAccountsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                MakeAccount(SystemAccounts.CashId, "Cash on Hand", "1100", isSystem: true),
                MakeAccount(BankAccountId, "Operating Account", "1110")
            });

        _bankDepositService.RecordDepositAsync(Arg.Any<RecordBankDepositRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    // --- Rendering ---

    [Fact]
    public void Renders_PageTitle_RecordBankDeposit()
    {
        var cut = Render<BankDepositPage>();

        Assert.Contains("Record Bank Deposit", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Renders_ToAccountDropdown_ExcludingCashOnHand()
    {
        var cut = Render<BankDepositPage>();

        var select = cut.Find("#toAccount");
        Assert.Contains("Operating Account", select.InnerHtml);
        Assert.DoesNotContain("Cash on Hand", select.InnerHtml);
    }

    [Fact]
    public void Renders_FixedFromLabel_NotAPicker()
    {
        var cut = Render<BankDepositPage>();

        var from = cut.Find("#depositFrom");
        Assert.Equal("Cash on Hand", from.GetAttribute("value"));
        Assert.NotNull(from.GetAttribute("disabled"));
    }

    [Fact]
    public void Renders_AmountInput()
    {
        var cut = Render<BankDepositPage>();

        cut.Find("#depositAmount");
    }

    // --- No eligible destination state ---

    [Fact]
    public void WhenOnlyCashOnHandExists_ShowsWarning_AndNoForm()
    {
        _accountService.GetBankAccountsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                MakeAccount(SystemAccounts.CashId, "Cash on Hand", "1100", isSystem: true)
            });

        var cut = Render<BankDepositPage>();

        Assert.Empty(cut.FindAll("#toAccount"));
        Assert.Contains("bank account", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // --- Validation ---

    [Fact]
    public async Task Submit_WithZeroAmount_ShowsValidationError_DoesNotCallService()
    {
        var cut = Render<BankDepositPage>();

        await cut.Find("button.btn-primary").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("greater than zero", cut.Markup, StringComparison.OrdinalIgnoreCase);
        await _bankDepositService.DidNotReceive().RecordDepositAsync(
            Arg.Any<RecordBankDepositRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithNoDestinationSelected_ShowsValidationError_DoesNotCallService()
    {
        var cut = Render<BankDepositPage>();

        await cut.Find("#depositAmount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs
        {
            Value = "100.00"
        });

        await cut.Find("button.btn-primary").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("select the destination account", cut.Markup, StringComparison.OrdinalIgnoreCase);
        await _bankDepositService.DidNotReceive().RecordDepositAsync(
            Arg.Any<RecordBankDepositRequest>(), Arg.Any<CancellationToken>());
    }

    // --- Successful submission ---

    [Fact]
    public async Task Submit_WithValidData_CallsRecordDepositAsync()
    {
        var cut = Render<BankDepositPage>();

        await cut.Find("#depositAmount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs
        {
            Value = "150.00"
        });
        await cut.Find("#toAccount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs
        {
            Value = BankAccountId.ToString()
        });

        await cut.Find("button.btn-primary").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _bankDepositService.Received(1).RecordDepositAsync(
            Arg.Any<RecordBankDepositRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_Success_ShowsSuccessMessage_AndRecordAnotherButton()
    {
        var cut = Render<BankDepositPage>();

        await cut.Find("#depositAmount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs
        {
            Value = "75.50"
        });
        await cut.Find("#toAccount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs
        {
            Value = BankAccountId.ToString()
        });

        await cut.Find("button.btn-primary").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("recorded successfully", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Record Another", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // --- Helpers ---

    private static Account MakeAccount(Guid id, string name, string number, bool isSystem = false) => new()
    {
        Id = id, Name = name, Type = AccountType.Asset,
        AccountNumber = number, IsSystem = isSystem, IsBankAccount = true, SortOrder = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
