using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Finance;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for CashWithdrawalPage (spec 033 US4): the required bank and amount, the single bank preselected,
/// the no-bank message, the request sent on save, and the refusal and error messages.
/// </summary>
public class CashWithdrawalPageTests : LocalizedTestContext
{
    private readonly IBankingService _banking = Substitute.For<IBankingService>();
    private static readonly Guid BankId = Guid.NewGuid();

    public CashWithdrawalPageTests()
    {
        Services.AddSingleton(_banking);
        SetBanks(Bank(BankId, "Community Bank"));
    }

    private void SetBanks(params Account[] banks) =>
        _banking.GetOverviewAsync(Arg.Any<CancellationToken>()).Returns(new BankingOverview { DepositBankAccounts = banks });

    private static Account Bank(Guid id, string name) => new()
    {
        Id = id, Name = name, AccountNumber = "1110", Type = AccountType.Asset, IsBankAccount = true,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public void Should_PreselectBank_When_OnlyOneBankExists()
    {
        var cut = Render<CashWithdrawalPage>();

        Assert.Equal(BankId.ToString(), cut.Find("#withdrawalBank").GetAttribute("value"));
    }

    [Fact]
    public void Should_RequireBankAndPostNothing_When_SeveralBanksAndNoneChosen()
    {
        SetBanks(Bank(BankId, "Community Bank"), Bank(Guid.NewGuid(), "Savings"));
        var cut = Render<CashWithdrawalPage>();
        Assert.Equal(Guid.Empty.ToString(), cut.Find("#withdrawalBank").GetAttribute("value"));
        cut.Find("#withdrawalAmount").Input("50");

        cut.Find("form").Submit();

        Assert.Equal("Choose a bank account other than Petty Cash.", cut.Find("#withdrawalBankError").TextContent.Trim());
        _banking.DidNotReceive().RecordWithdrawalAsync(Arg.Any<RecordCashWithdrawalRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-5")]
    public void Should_RequirePositiveAmountAndPostNothing_When_AmountMissingOrNotAboveZero(string amount)
    {
        var cut = Render<CashWithdrawalPage>();
        cut.Find("#withdrawalAmount").Input(amount);

        cut.Find("form").Submit();

        Assert.Equal("Amount must be greater than zero.", cut.Find("#withdrawalAmountError").TextContent.Trim());
        _banking.DidNotReceive().RecordWithdrawalAsync(Arg.Any<RecordCashWithdrawalRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Should_SendRequestAndReturnToBanking_When_Saved()
    {
        var cut = Render<CashWithdrawalPage>();
        var nav = Services.GetRequiredService<NavigationManager>();
        cut.Find("#withdrawalDate").Change("2026-03-07");
        cut.Find("#withdrawalAmount").Input("200");
        cut.Find("#withdrawalPurpose").Change("  Opening night float  ");

        cut.Find("form").Submit();

        _banking.Received(1).RecordWithdrawalAsync(
            Arg.Is<RecordCashWithdrawalRequest>(r => r!.BankAccountId == BankId && r.Amount == 200m
                && r.Date == new DateTime(2026, 3, 7) && r.Purpose == "Opening night float"),
            Arg.Any<CancellationToken>());
        Assert.EndsWith("/finance/banking", nav.Uri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_SendDefaultDescription_When_PurposeLeftBlank(string purpose)
    {
        var cut = Render<CashWithdrawalPage>();
        cut.Find("#withdrawalAmount").Input("40");
        cut.Find("#withdrawalPurpose").Change(purpose);

        cut.Find("form").Submit();

        _banking.Received(1).RecordWithdrawalAsync(
            Arg.Is<RecordCashWithdrawalRequest>(r => r!.Purpose == "Cash withdrawal"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Should_PostOnce_When_FormSubmittedTwiceWhileSaving()
    {
        var pending = new TaskCompletionSource<JournalEntry>();
        _banking.RecordWithdrawalAsync(Arg.Any<RecordCashWithdrawalRequest>(), Arg.Any<CancellationToken>())
            .Returns(pending.Task);
        var cut = Render<CashWithdrawalPage>();
        cut.Find("#withdrawalAmount").Input("40");

        cut.Find("form").Submit();
        cut.Find("form").Submit();

        _banking.Received(1).RecordWithdrawalAsync(Arg.Any<RecordCashWithdrawalRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Should_ShowRefusalMessageAndStay_When_ServiceRefusesWithdrawal()
    {
        _banking.RecordWithdrawalAsync(Arg.Any<RecordCashWithdrawalRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<JournalEntry>(new ValidationException(
                "The purpose can be at most 200 characters.", "JournalEntry", "RecordWithdrawalAsync")));
        var cut = Render<CashWithdrawalPage>();
        var nav = Services.GetRequiredService<NavigationManager>();
        var before = nav.Uri;
        cut.Find("#withdrawalAmount").Input("40");

        cut.Find("form").Submit();

        Assert.Equal("The purpose can be at most 200 characters.", cut.Find("[role=alert]").TextContent.Trim());
        Assert.Equal(before, nav.Uri);
    }

    [Fact]
    public void Should_ShowClosedPeriodMessage_When_WithdrawalDatedInClosedPeriod()
    {
        _banking.RecordWithdrawalAsync(Arg.Any<RecordCashWithdrawalRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<JournalEntry>(new ClosedPeriodException("closed", "JournalEntry", "RecordWithdrawalAsync")));
        var cut = Render<CashWithdrawalPage>();
        cut.Find("#withdrawalAmount").Input("40");

        cut.Find("form").Submit();

        Assert.Contains("closed financial period", cut.Find("[role=alert]").TextContent);
    }

    [Fact]
    public void Should_ShowGenericSaveError_When_UnexpectedFailure()
    {
        _banking.RecordWithdrawalAsync(Arg.Any<RecordCashWithdrawalRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<JournalEntry>(new DataAccessException("disk full", "JournalEntry", "RecordWithdrawalAsync")));
        var cut = Render<CashWithdrawalPage>();
        cut.Find("#withdrawalAmount").Input("40");

        cut.Find("form").Submit();

        Assert.Equal("Failed to record withdrawal: disk full", cut.Find("[role=alert]").TextContent.Trim());
    }

    [Fact]
    public void Should_ShowLoggedErrorTextNotExceptionText_When_SaveFailsWithFrameworkException()
    {
        _banking.RecordWithdrawalAsync(Arg.Any<RecordCashWithdrawalRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<JournalEntry>(new InvalidOperationException("SQLite Error 5: database is locked")));
        var cut = Render<CashWithdrawalPage>();
        cut.Find("#withdrawalAmount").Input("40");

        cut.Find("form").Submit();

        Assert.Equal("Failed to record withdrawal: an unexpected error occurred. The details have been logged.",
            cut.Find("[role=alert]").TextContent.Trim());
    }

    [Fact]
    public void Should_ShowLoadError_When_OverviewFailsToLoad()
    {
        _banking.GetOverviewAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BankingOverview>(new DataAccessException("disk unavailable", "Account", "GetOverview")));

        var cut = Render<CashWithdrawalPage>();

        Assert.Contains("Failed to load banking: disk unavailable", cut.Markup);
    }

    [Fact]
    public void Should_ShowNoBankMessageAndNoForm_When_NoBankAccountExists()
    {
        SetBanks();

        var cut = Render<CashWithdrawalPage>();

        Assert.Contains("/finance/accounts", cut.Markup);
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public void Should_ReturnToBanking_When_Cancelled()
    {
        var cut = Render<CashWithdrawalPage>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find("#cancelWithdrawal").Click();

        Assert.EndsWith("/finance/banking", nav.Uri);
    }
}
