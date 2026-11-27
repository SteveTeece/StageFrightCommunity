using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Finance;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for DepositDetailPage (spec 033 US5): the cash and cheques with their total, the two-step reverse,
/// no reverse once reversed, and the refusal, closed-period and error messages.
/// </summary>
public class DepositDetailPageTests : LocalizedTestContext
{
    private const string FinalisedReason = "This deposit is part of a finalised bank reconciliation and cannot be reversed.";
    private readonly IBankingService _banking = Substitute.For<IBankingService>();
    private static readonly Guid DepositId = Guid.NewGuid();

    public DepositDetailPageTests()
    {
        Services.AddSingleton(_banking);
        _banking.GetDepositAsync(DepositId, Arg.Any<CancellationToken>()).Returns(Recorded());
    }

    private static DepositDetail Recorded() => new()
    {
        Id = DepositId, Date = new DateTime(2026, 3, 4), BankAccountName = "Community Bank", CashAmount = 40m, Total = 160m,
        CanReverse = true,
        Items =
        [
            Item("Alice Smith", "CHQ 1", 50m),
            Item("Town Hall", "CHQ 2", 70m)
        ]
    };

    private static DepositDetail Reversed() => new()
    {
        Id = DepositId, Date = new DateTime(2026, 3, 4), BankAccountName = "Community Bank", CashAmount = 40m, Total = 160m,
        IsReversed = true, ReversedAt = new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc),
        CannotReverseReason = "This deposit has already been reversed."
    };

    private static AwaitingDepositItem Item(string? payer, string? reference, decimal amount,
        ReceivedItemSource source = ReceivedItemSource.MemberPayment) => new()
    {
        Id = Guid.NewGuid(), ReceivedDate = new DateTime(2026, 3, 2), Source = source,
        PayerName = payer, Reference = reference, Amount = amount
    };

    private IRenderedComponent<DepositDetailPage> RenderPage() =>
        Render<DepositDetailPage>(ps => ps.Add(p => p.Id, DepositId));

    private void ReverseThrows(Exception ex) =>
        _banking.ReverseDepositAsync(DepositId, Arg.Any<CancellationToken>()).Returns(Task.FromException(ex));

    private static void ReverseInTwoSteps(IRenderedComponent<DepositDetailPage> cut)
    {
        cut.Find("#reverseDeposit").Click();
        cut.Find("#confirmReverse").Click();
    }

    [Fact]
    public void Should_ListCashAndEachItemWithTotal_When_DepositOpened()
    {
        var cut = RenderPage();

        Assert.Equal("Community Bank", cut.Find("#depositBank").TextContent);
        Assert.Equal(MoneyFormatter.Format(40m), cut.Find("#depositCash").TextContent);
        Assert.Equal(MoneyFormatter.Format(160m), cut.Find("#depositTotal").TextContent);
        Assert.Equal(MoneyFormatter.Format(120m), cut.Find("#itemsTotal").TextContent);
        foreach (var text in new[] { "Alice Smith", "CHQ 1", "Town Hall", "CHQ 2", MoneyFormatter.Format(70m) })
            Assert.Contains(text, cut.Markup);
        Assert.Equal("Recorded", cut.Find("#depositStatus").TextContent);
    }

    [Fact]
    public void Should_ShowOpeningBalanceLabel_When_ItemHasNoPayer()
    {
        _banking.GetDepositAsync(DepositId, Arg.Any<CancellationToken>()).Returns(new DepositDetail
        {
            Id = DepositId, Date = new DateTime(2026, 3, 4), BankAccountName = "Community Bank", Total = 300m, CanReverse = true,
            Items = [Item(null, null, 300m, ReceivedItemSource.OpeningBalance)]
        });

        var cut = RenderPage();

        Assert.Contains("Opening balance", cut.Markup);
    }

    [Fact]
    public void Should_AskForConfirmationAndPostNothing_When_ReverseClickedOnce()
    {
        var cut = RenderPage();

        cut.Find("#reverseDeposit").Click();

        Assert.Contains("Reverse this deposit?", cut.Find("#reverseConfirmText").TextContent);
        Assert.Empty(cut.FindAll("#reverseDeposit"));
        _banking.DidNotReceive().ReverseDepositAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Should_ReverseAndShowReversedStatus_When_Confirmed()
    {
        _banking.GetDepositAsync(DepositId, Arg.Any<CancellationToken>()).Returns(Recorded(), Reversed());
        var cut = RenderPage();

        ReverseInTwoSteps(cut);

        _banking.Received(1).ReverseDepositAsync(DepositId, Arg.Any<CancellationToken>());
        Assert.Equal("Deposit reversed.", cut.Find("#depositMessage").TextContent);
        Assert.StartsWith("Reversed", cut.Find("#depositStatus").TextContent);
        Assert.Contains("Reversed on " + new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc).ToLocalTime().ToShortDateString(),
            cut.Find("#depositStatus").TextContent);
        Assert.Empty(cut.FindAll("#reverseDeposit"));
        Assert.Empty(cut.FindAll("[role=alert]"));
    }

    [Fact]
    public void Should_PostNothingAndOfferReverseAgain_When_ConfirmationCancelled()
    {
        var cut = RenderPage();
        cut.Find("#reverseDeposit").Click();

        cut.Find("#cancelReverse").Click();

        Assert.Empty(cut.FindAll("#confirmReverse"));
        Assert.Single(cut.FindAll("#reverseDeposit"));
        _banking.DidNotReceive().ReverseDepositAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Should_ReverseOnce_When_ConfirmClickedTwiceWhileReversing()
    {
        var pending = new TaskCompletionSource();
        _banking.ReverseDepositAsync(DepositId, Arg.Any<CancellationToken>()).Returns(pending.Task);
        var cut = RenderPage();
        cut.Find("#reverseDeposit").Click();

        cut.Find("#confirmReverse").Click();
        Assert.True(cut.Find("#confirmReverse").HasAttribute("disabled"));
        cut.Find("#confirmReverse").Click();

        _banking.Received(1).ReverseDepositAsync(DepositId, Arg.Any<CancellationToken>());
        pending.SetResult();
    }

    [Fact]
    public void Should_OfferNoReverseAndShowReason_When_AlreadyReversed()
    {
        _banking.GetDepositAsync(DepositId, Arg.Any<CancellationToken>()).Returns(Reversed());

        var cut = RenderPage();

        Assert.Empty(cut.FindAll("#reverseDeposit"));
        Assert.StartsWith("Reversed", cut.Find("#depositStatus").TextContent);
        Assert.Equal("This deposit has already been reversed.", cut.Find("#cannotReverseReason").TextContent);
    }

    [Fact]
    public void Should_OfferNoReverseAndShowReason_When_ClearedInFinalisedReconciliation()
    {
        _banking.GetDepositAsync(DepositId, Arg.Any<CancellationToken>()).Returns(new DepositDetail
        {
            Id = DepositId, Date = new DateTime(2026, 3, 4), BankAccountName = "Community Bank", Total = 160m,
            CanReverse = false, CannotReverseReason = FinalisedReason
        });

        var cut = RenderPage();

        Assert.Empty(cut.FindAll("#reverseDeposit"));
        Assert.Equal(FinalisedReason, cut.Find("#cannotReverseReason").TextContent);
    }

    [Fact]
    public void Should_ShowRefusalAndReload_When_ServiceRefusesReversal()
    {
        _banking.GetDepositAsync(DepositId, Arg.Any<CancellationToken>()).Returns(Recorded(), new DepositDetail
        {
            Id = DepositId, Date = new DateTime(2026, 3, 4), BankAccountName = "Community Bank", Total = 160m,
            CanReverse = false, CannotReverseReason = FinalisedReason
        });
        ReverseThrows(new ValidationException(FinalisedReason, "BankDeposit", "ReverseDepositAsync"));
        var cut = RenderPage();

        ReverseInTwoSteps(cut);

        Assert.Equal(FinalisedReason, cut.Find("[role=alert]").TextContent.Trim());
        Assert.Empty(cut.FindAll("#confirmReverse"));
        Assert.Empty(cut.FindAll("#reverseDeposit"));
    }

    [Fact]
    public void Should_ShowClosedPeriodMessage_When_ReversalDatedInClosedPeriod()
    {
        ReverseThrows(new ClosedPeriodException("closed", "JournalEntry", "ReverseDepositAsync"));
        var cut = RenderPage();

        ReverseInTwoSteps(cut);

        Assert.Equal(
            "This transaction is dated in a closed financial period and cannot be posted. Change the date to a period that is still open.",
            cut.Find("[role=alert]").TextContent.Trim());
    }

    [Fact]
    public void Should_ReloadWithoutError_When_ReversalLosesRaceToAnother()
    {
        _banking.GetDepositAsync(DepositId, Arg.Any<CancellationToken>()).Returns(Recorded(), Reversed());
        ReverseThrows(new DataIntegrityException("already stamped", "BankDeposit", "MarkReversedAsync"));
        var cut = RenderPage();

        ReverseInTwoSteps(cut);

        Assert.Empty(cut.FindAll("[role=alert]"));
        Assert.StartsWith("Reversed", cut.Find("#depositStatus").TextContent);
    }

    [Fact]
    public void Should_ShowGenericReverseError_When_DataAccessFails()
    {
        ReverseThrows(new DataAccessException("disk full", "BankDeposit", "ReverseDepositAsync"));
        var cut = RenderPage();

        ReverseInTwoSteps(cut);

        Assert.Equal("Failed to reverse deposit: disk full", cut.Find("[role=alert]").TextContent.Trim());
    }

    [Fact]
    public void Should_ShowLoggedErrorTextNotExceptionText_When_ReversalFailsWithFrameworkException()
    {
        ReverseThrows(new InvalidOperationException("SQLite Error 5: database is locked"));
        var cut = RenderPage();

        ReverseInTwoSteps(cut);

        Assert.Equal("Failed to reverse deposit: an unexpected error occurred. The details have been logged.",
            cut.Find("[role=alert]").TextContent.Trim());
        Assert.Single(cut.FindAll("#reverseDeposit"));
    }

    [Fact]
    public void Should_ShowLegacyNoteAndNoReverse_When_LegacyDeposit()
    {
        _banking.GetDepositAsync(DepositId, Arg.Any<CancellationToken>()).Returns(new DepositDetail
        {
            Id = DepositId, Date = new DateTime(2025, 6, 1), BankAccountName = "Community Bank", CashAmount = 80m, Total = 80m,
            IsLegacy = true
        });

        var cut = RenderPage();

        Assert.Equal("Recorded before cheque tracking was added; it cannot be reversed here.", cut.Find("#legacyNote").TextContent);
        Assert.Empty(cut.FindAll("#reverseDeposit"));
    }

    [Fact]
    public void Should_ShowNotFoundAndBackLink_When_DepositUnknown()
    {
        _banking.GetDepositAsync(DepositId, Arg.Any<CancellationToken>()).Returns((DepositDetail?)null);

        var cut = RenderPage();

        Assert.Equal("That deposit could not be found.", cut.Find("#depositNotFound").TextContent);
        Assert.Equal("/finance/banking", cut.Find("#backToBanking").GetAttribute("href"));
    }

    [Fact]
    public void Should_ShowLoadErrorAndNoNotFound_When_DepositFailsToLoad()
    {
        _banking.GetDepositAsync(DepositId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DepositDetail?>(new DataAccessException("disk unavailable", "BankDeposit", "GetDeposit")));

        var cut = RenderPage();

        Assert.Equal("Failed to load banking: disk unavailable", cut.Find("[role=alert]").TextContent.Trim());
        Assert.Empty(cut.FindAll("#depositNotFound"));
    }
}
