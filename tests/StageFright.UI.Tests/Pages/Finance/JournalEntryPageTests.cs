using Bunit;
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
/// bUnit tests for JournalEntryPage — dynamic line list, debit-clears-credit per row,
/// running totals with balance badge, save gating, and posting via IGeneralJournalService.
/// </summary>
public class JournalEntryPageTests : BunitContext
{
    private readonly IGeneralJournalService _journalService = Substitute.For<IGeneralJournalService>();

    private static readonly Guid ExpenseAccountId = Guid.NewGuid();
    private static readonly Guid CashAccountId = Guid.NewGuid();

    public JournalEntryPageTests()
    {
        Services.AddSingleton(_journalService);

        _journalService.GetJournalAccountsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                new()
                {
                    Id = CashAccountId, Name = "Cash on Hand", Type = AccountType.Asset,
                    AccountNumber = "1100", IsSystem = true, IsBankAccount = true,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Id = ExpenseAccountId, Name = "Hall Hire", Type = AccountType.Expense,
                    AccountNumber = "6000",
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            });
    }

    // --- Rendering ---

    [Fact]
    public void Should_RenderTwoEmptyLines_When_PageLoads()
    {
        var cut = Render<JournalEntryPage>();

        Assert.Equal(2, cut.FindAll(".journal-account").Count);
    }

    [Fact]
    public void Should_ShowOutOfBalanceBadge_When_NothingEntered()
    {
        var cut = Render<JournalEntryPage>();

        Assert.Contains("Out of balance", cut.Find(".journal-balance-badge").TextContent);
    }

    [Fact]
    public void Should_DisableSave_When_JournalIsEmpty()
    {
        var cut = Render<JournalEntryPage>();

        Assert.True(cut.Find(".journal-save").HasAttribute("disabled"));
    }

    // --- Line management ---

    [Fact]
    public void Should_AddLine_When_AddLineClicked()
    {
        var cut = Render<JournalEntryPage>();

        cut.Find(".journal-add-line").Click();

        Assert.Equal(3, cut.FindAll(".journal-account").Count);
    }

    [Fact]
    public void Should_DisableRemove_When_OnlyTwoLinesRemain()
    {
        var cut = Render<JournalEntryPage>();

        Assert.All(cut.FindAll(".journal-remove-line"), b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void Should_RemoveLine_When_RemoveClickedWithMoreThanTwoLines()
    {
        var cut = Render<JournalEntryPage>();
        cut.Find(".journal-add-line").Click();

        cut.FindAll(".journal-remove-line")[2].Click();

        Assert.Equal(2, cut.FindAll(".journal-account").Count);
    }

    // --- Debit-clears-credit ---

    [Fact]
    public void Should_ClearCredit_When_DebitEnteredOnSameRow()
    {
        var cut = Render<JournalEntryPage>();

        cut.FindAll(".journal-credit")[0].Change("50");
        cut.FindAll(".journal-debit")[0].Change("100");

        Assert.Equal("100.00", cut.Find(".journal-total-debits").TextContent);
        Assert.Equal("0.00", cut.Find(".journal-total-credits").TextContent);
    }

    [Fact]
    public void Should_ClearDebit_When_CreditEnteredOnSameRow()
    {
        var cut = Render<JournalEntryPage>();

        cut.FindAll(".journal-debit")[0].Change("100");
        cut.FindAll(".journal-credit")[0].Change("50");

        Assert.Equal("0.00", cut.Find(".journal-total-debits").TextContent);
        Assert.Equal("50.00", cut.Find(".journal-total-credits").TextContent);
    }

    // --- Balance badge + totals ---

    [Fact]
    public void Should_ShowBalancedBadge_When_DebitsEqualCredits()
    {
        var cut = Render<JournalEntryPage>();

        cut.FindAll(".journal-debit")[0].Change("100");
        cut.FindAll(".journal-credit")[1].Change("100");

        Assert.Contains("Balanced", cut.Find(".journal-balance-badge").TextContent);
    }

    [Fact]
    public void Should_ShowOutOfBalanceAmount_When_TotalsDiffer()
    {
        var cut = Render<JournalEntryPage>();

        cut.FindAll(".journal-debit")[0].Change("100");
        cut.FindAll(".journal-credit")[1].Change("60");

        Assert.Contains("40", cut.Find(".journal-balance-badge").TextContent);
    }

    // --- Save gating ---

    [Fact]
    public void Should_DisableSave_When_BalancedButNoDescription()
    {
        var cut = Render<JournalEntryPage>();
        EnterBalancedLines(cut);

        Assert.True(cut.Find(".journal-save").HasAttribute("disabled"));
    }

    [Fact]
    public void Should_DisableSave_When_BalancedButAccountMissing()
    {
        var cut = Render<JournalEntryPage>();
        cut.Find("#journalDescription").Change("Correction");
        cut.FindAll(".journal-debit")[0].Change("100");
        cut.FindAll(".journal-credit")[1].Change("100");
        cut.FindAll(".journal-account")[0].Change(ExpenseAccountId.ToString());

        Assert.True(cut.Find(".journal-save").HasAttribute("disabled"));
    }

    [Fact]
    public void Should_EnableSave_When_BalancedWithAccountsAndDescription()
    {
        var cut = Render<JournalEntryPage>();
        cut.Find("#journalDescription").Change("Correction");
        EnterBalancedLines(cut);

        Assert.False(cut.Find(".journal-save").HasAttribute("disabled"));
    }

    // --- Posting ---

    [Fact]
    public void Should_PostJournal_When_SaveClicked()
    {
        var cut = Render<JournalEntryPage>();
        cut.Find("#journalDescription").Change("Correction");
        EnterBalancedLines(cut);

        cut.Find(".journal-save").Click();

        _journalService.Received(1).RecordJournalAsync(
            Arg.Is<RecordJournalRequest>(r =>
                r.Description == "Correction"
                && r.Lines.Count == 2
                && r.Lines.Sum(l => l.DebitAmount) == 100m
                && r.Lines.Sum(l => l.CreditAmount) == 100m),
            Arg.Any<CancellationToken>());
        Assert.Contains("posted successfully", cut.Markup);
    }

    [Fact]
    public void Should_ShowError_When_PostingFails()
    {
        _journalService.RecordJournalAsync(Arg.Any<RecordJournalRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new ValidationException("The journal is out of balance — total debits must equal total credits.", "JournalEntry", "RecordJournalAsync"));

        var cut = Render<JournalEntryPage>();
        cut.Find("#journalDescription").Change("Correction");
        EnterBalancedLines(cut);

        cut.Find(".journal-save").Click();

        Assert.Contains("Failed to post journal", cut.Markup);
    }

    [Fact]
    public void Should_ResetForm_When_RecordAnotherClicked()
    {
        var cut = Render<JournalEntryPage>();
        cut.Find("#journalDescription").Change("Correction");
        EnterBalancedLines(cut);
        cut.Find(".journal-save").Click();

        cut.FindAll("button").First(b => b.TextContent.Contains("Record Another")).Click();

        Assert.Equal(2, cut.FindAll(".journal-account").Count);
        Assert.Equal("0.00", cut.Find(".journal-total-debits").TextContent);
    }

    [Fact]
    public void Should_ShowErrorAlert_When_AccountLoadFails()
    {
        _journalService.GetJournalAccountsAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<Account>>>(_ => throw new InvalidOperationException("boom"));

        var cut = Render<JournalEntryPage>();

        Assert.Contains("Failed to load accounts", cut.Markup);
    }

    // --- Helpers ---

    /// <summary>Fills both default rows: DR Hall Hire 100 / CR Cash 100.</summary>
    private static void EnterBalancedLines(IRenderedComponent<JournalEntryPage> cut)
    {
        cut.FindAll(".journal-account")[0].Change(ExpenseAccountId.ToString());
        cut.FindAll(".journal-debit")[0].Change("100");
        cut.FindAll(".journal-account")[1].Change(CashAccountId.ToString());
        cut.FindAll(".journal-credit")[1].Change("100");
    }
}
