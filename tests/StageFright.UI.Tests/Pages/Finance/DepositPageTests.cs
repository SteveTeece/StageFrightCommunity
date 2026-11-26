using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Finance;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for DepositPage (spec 033 US3): the cheques / cash / total split on every tick and keystroke,
/// the preselected bank and cash available, the disabled cash box, refusal messages and the no-bank message.
/// </summary>
public class DepositPageTests : LocalizedTestContext
{
    private readonly IBankingService _banking = Substitute.For<IBankingService>();
    private static readonly Guid BankId = Guid.NewGuid();
    private static readonly AwaitingDepositItem Cheque1 = Item("Alice Smith", "CHQ 1", 50m);
    private static readonly AwaitingDepositItem Cheque2 = Item("Town Hall", "CHQ 2", 120m);

    public DepositPageTests()
    {
        Services.AddSingleton(_banking);
        SetOverview();
    }

    private void SetOverview(decimal cashAvailable = 80m, IReadOnlyList<Account>? banks = null)
    {
        _banking.GetOverviewAsync(Arg.Any<CancellationToken>()).Returns(new BankingOverview
        {
            AwaitingItems = [Cheque1, Cheque2],
            UndepositedTotal = 170m,
            PettyCashBalance = cashAvailable,
            CashAvailableToday = cashAvailable,
            DepositBankAccounts = banks ?? [Bank(BankId, "Community Bank")]
        });
    }

    private static Account Bank(Guid id, string name) => new()
    {
        Id = id, Name = name, AccountNumber = "1110", Type = AccountType.Asset, IsBankAccount = true,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static AwaitingDepositItem Item(string payer, string reference, decimal amount) => new()
    {
        Id = Guid.NewGuid(), ReceivedDate = new DateTime(2026, 3, 3), Source = ReceivedItemSource.MemberPayment,
        PayerName = payer, Reference = reference, Amount = amount
    };

    [Fact]
    public void Should_SplitTotalIntoChequesAndCash_When_ItemsTickedAndCashTyped()
    {
        var cut = Render<DepositPage>();
        Assert.Equal(MoneyFormatter.Format(0m), cut.Find("#totalDeposit").TextContent);

        cut.Find($"#deposit-item-{Cheque1.Id}").Change(true);
        Assert.Equal(MoneyFormatter.Format(50m), cut.Find("#totalCheques").TextContent);
        Assert.Equal(MoneyFormatter.Format(50m), cut.Find("#totalDeposit").TextContent);

        cut.Find("#depositCash").Input("25");
        Assert.Equal(MoneyFormatter.Format(25m), cut.Find("#totalCash").TextContent);
        Assert.Equal(MoneyFormatter.Format(75m), cut.Find("#totalDeposit").TextContent);

        cut.Find("#select-all-deposit-items").Change(true);
        Assert.Equal(MoneyFormatter.Format(170m), cut.Find("#totalCheques").TextContent);
        Assert.Equal(MoneyFormatter.Format(195m), cut.Find("#totalDeposit").TextContent);
    }

    [Fact]
    public void Should_PreselectSingleBankAndShowCashAvailable_When_Loaded()
    {
        var cut = Render<DepositPage>();

        Assert.Equal(BankId.ToString(), cut.Find("#depositBank").GetAttribute("value"));
        Assert.Equal($"Cash available: {MoneyFormatter.Format(80m)}", cut.Find("#cashAvailableHelp").TextContent.Trim());
    }

    [Fact]
    public void Should_NotPreselectBank_When_SeveralBanksExist()
    {
        SetOverview(80m, [Bank(BankId, "Community Bank"), Bank(Guid.NewGuid(), "Savings")]);

        var cut = Render<DepositPage>();

        Assert.Equal(Guid.Empty.ToString(), cut.Find("#depositBank").GetAttribute("value"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public void Should_DisableCashBox_When_CashAvailableIsZeroOrLess(int cashAvailable)
    {
        SetOverview(cashAvailable);

        var cut = Render<DepositPage>();

        Assert.True(cut.Find("#depositCash").HasAttribute("disabled"));
    }

    [Fact]
    public void Should_EnableCashBox_When_CashAvailable()
    {
        var cut = Render<DepositPage>();

        Assert.False(cut.Find("#depositCash").HasAttribute("disabled"));
    }

    [Fact]
    public void Should_ReloadCashAvailable_When_DateChanged()
    {
        _banking.GetCashAvailableAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(20m);
        var cut = Render<DepositPage>();

        cut.Find("#depositDate").Change("2026-03-01");

        Assert.Equal($"Cash available: {MoneyFormatter.Format(20m)}", cut.Find("#cashAvailableHelp").TextContent.Trim());
    }

    [Fact]
    public void Should_DisableSave_When_NothingChosen()
    {
        var cut = Render<DepositPage>();

        Assert.True(cut.Find("#saveDeposit").HasAttribute("disabled"));

        cut.Find($"#deposit-item-{Cheque1.Id}").Change(true);

        Assert.False(cut.Find("#saveDeposit").HasAttribute("disabled"));
    }

    [Fact]
    public void Should_SendTickedItemsAndCashAndReturnToBanking_When_Saved()
    {
        var cut = Render<DepositPage>();
        var nav = Services.GetRequiredService<NavigationManager>();
        cut.Find($"#deposit-item-{Cheque2.Id}").Change(true);
        cut.Find("#depositCash").Input("30");

        cut.Find("form").Submit();

        _banking.Received(1).RecordDepositAsync(
            Arg.Is<RecordDepositRequest>(r => r!.BankAccountId == BankId && r.CashAmount == 30m
                && r.ReceivedItemIds.Count == 1 && r.ReceivedItemIds[0] == Cheque2.Id),
            Arg.Any<CancellationToken>());
        Assert.EndsWith("/finance/banking", nav.Uri);
    }

    [Fact]
    public void Should_PostOnce_When_FormSubmittedTwiceWhileSaving()
    {
        var pending = new TaskCompletionSource<BankDeposit>();
        _banking.RecordDepositAsync(Arg.Any<RecordDepositRequest>(), Arg.Any<CancellationToken>())
            .Returns(pending.Task);
        var cut = Render<DepositPage>();
        cut.Find("#depositCash").Input("30");

        cut.Find("form").Submit();
        cut.Find("form").Submit();

        _banking.Received(1).RecordDepositAsync(Arg.Any<RecordDepositRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Should_ShowRefusalMessageAndStay_When_ServiceRefusesDeposit()
    {
        _banking.RecordDepositAsync(Arg.Any<RecordDepositRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BankDeposit>(new ValidationException(
                "The cash amount is more than the cash available ($10.00).", "BankDeposit", "RecordDepositAsync")));
        var cut = Render<DepositPage>();
        var nav = Services.GetRequiredService<NavigationManager>();
        var before = nav.Uri;
        cut.Find("#depositCash").Input("500");

        cut.Find("form").Submit();

        Assert.Equal("The cash amount is more than the cash available ($10.00).", cut.Find("[role=alert]").TextContent.Trim());
        Assert.Equal(before, nav.Uri);
    }

    [Fact]
    public void Should_ShowClosedPeriodMessage_When_DepositDatedInClosedPeriod()
    {
        _banking.RecordDepositAsync(Arg.Any<RecordDepositRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BankDeposit>(new ClosedPeriodException("closed", "BankDeposit", "RecordDepositAsync")));
        var cut = Render<DepositPage>();
        cut.Find($"#deposit-item-{Cheque1.Id}").Change(true);

        cut.Find("form").Submit();

        Assert.Contains("closed financial period", cut.Find("[role=alert]").TextContent);
    }

    [Fact]
    public void Should_ShowGenericSaveError_When_UnexpectedFailure()
    {
        _banking.RecordDepositAsync(Arg.Any<RecordDepositRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BankDeposit>(new DataAccessException("disk full", "BankDeposit", "RecordDepositAsync")));
        var cut = Render<DepositPage>();
        cut.Find($"#deposit-item-{Cheque1.Id}").Change(true);

        cut.Find("form").Submit();

        Assert.Equal("Failed to record deposit: disk full", cut.Find("[role=alert]").TextContent.Trim());
    }

    [Fact]
    public void Should_ShowNoBankMessageAndNoForm_When_NoBankAccountExists()
    {
        SetOverview(80m, []);

        var cut = Render<DepositPage>();

        Assert.Contains("/finance/accounts", cut.Markup);
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public void Should_ReturnToBanking_When_Cancelled()
    {
        var cut = Render<DepositPage>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find("#cancelDeposit").Click();

        Assert.EndsWith("/finance/banking", nav.Uri);
    }

    [Fact]
    public void Should_ShowLoadError_When_OverviewFails()
    {
        _banking.GetOverviewAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BankingOverview>(new DataAccessException("down", "Account", "GetOverview")));

        var cut = Render<DepositPage>();

        Assert.Equal("Failed to load banking: down", cut.Find("[role=alert]").TextContent.Trim());
    }
}
