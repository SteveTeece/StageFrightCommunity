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
/// bUnit tests for ReconciliationWorkspace — summary cards, balanced/unbalanced
/// finalise gating, persistent checkbox ticks, finalised read-only state,
/// draft deletion navigation, and error surfacing.
/// </summary>
public class ReconciliationWorkspaceTests : LocalizedTestContext
{
    private readonly IBankReconciliationService _service = Substitute.For<IBankReconciliationService>();

    private static readonly Guid ReconciliationId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly DateTime StatementDate = new(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

    public ReconciliationWorkspaceTests()
    {
        Services.AddSingleton(_service);
    }

    // --- Rendering ---

    [Fact]
    public void Should_ShowNotFoundMessage_When_ReconciliationDoesNotExist()
    {
        _service.GetWorkspaceAsync(ReconciliationId, Arg.Any<CancellationToken>())
            .Returns<Task<ReconciliationWorkspaceView>>(
                _ => throw new EntityNotFoundException(nameof(BankReconciliation), ReconciliationId, "test"));

        var cut = RenderWorkspace();

        Assert.Contains("Reconciliation not found", cut.Markup);
    }

    [Fact]
    public void Should_RenderSummaryCards_When_DraftLoads()
    {
        SetupWorkspace(MakeView(statementClosing: 250m, opening: 100m, clearedDebit: 200m, clearedCredit: 50m));

        var cut = RenderWorkspace();

        Assert.Contains("$250.00", cut.Find("#summary-statement").TextContent);
        Assert.Contains("$100.00", cut.Find("#summary-opening").TextContent);
        Assert.Contains("$150.00", cut.Find("#summary-cleared").TextContent);
        Assert.Contains("$0.00", cut.Find("#summary-difference").TextContent);
    }

    [Fact]
    public void Should_ShowErrorAlert_When_LoadingFails()
    {
        _service.GetWorkspaceAsync(ReconciliationId, Arg.Any<CancellationToken>())
            .Returns<Task<ReconciliationWorkspaceView>>(_ => throw new InvalidOperationException("boom"));

        var cut = RenderWorkspace();

        Assert.Contains("Failed to load reconciliation", cut.Find(".alert-danger").TextContent);
    }

    // --- Finalise gating ---

    [Fact]
    public void Should_EnableFinalise_When_DifferenceIsZero()
    {
        SetupWorkspace(MakeView(statementClosing: 150m, clearedDebit: 150m));

        var cut = RenderWorkspace();

        Assert.False(cut.Find("#finalise-button").HasAttribute("disabled"));
    }

    [Fact]
    public void Should_DisableFinalise_When_DifferenceIsNonZero()
    {
        SetupWorkspace(MakeView(statementClosing: 150m, clearedDebit: 100m));

        var cut = RenderWorkspace();

        Assert.True(cut.Find("#finalise-button").HasAttribute("disabled"));
    }

    [Fact]
    public void Should_CallFinalise_When_FinaliseClicked()
    {
        SetupWorkspace(MakeView(statementClosing: 150m, clearedDebit: 150m));

        var cut = RenderWorkspace();
        cut.Find("#finalise-button").Click();

        _service.Received(1).FinaliseAsync(ReconciliationId, Arg.Any<CancellationToken>());
    }

    // --- Ticks ---

    [Fact]
    public void Should_RenderCheckedTick_When_TransactionIsCleared()
    {
        SetupWorkspace(MakeView(statementClosing: 150m, clearedDebit: 150m, unclearedDebit: 30m));

        var cut = RenderWorkspace();
        var ticks = cut.FindAll(".reconciliation-tick");

        Assert.Equal(2, ticks.Count);
        Assert.Single(ticks, t => t.HasAttribute("checked"));
    }

    [Fact]
    public void Should_CallToggleClear_When_TickChanged()
    {
        var view = MakeView(statementClosing: 150m, unclearedDebit: 150m);
        SetupWorkspace(view);

        var cut = RenderWorkspace();
        cut.Find(".reconciliation-tick").Change(true);

        _service.Received(1).ToggleClearAsync(
            ReconciliationId, view.Transactions[0].Transaction.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Should_ShowError_When_ToggleClearIsRejected()
    {
        var view = MakeView(statementClosing: 150m, unclearedDebit: 150m);
        SetupWorkspace(view);
        _service.ToggleClearAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new ReconciliationException(
                "This transaction is already cleared by another reconciliation.",
                nameof(ReconciliationLine), "test"));

        var cut = RenderWorkspace();
        cut.Find(".reconciliation-tick").Change(true);

        Assert.Contains("already cleared", cut.Find(".alert-danger").TextContent);
    }

    // --- Finalised read-only state ---

    [Fact]
    public void Should_RenderReadOnly_When_ReconciliationIsFinalised()
    {
        SetupWorkspace(MakeView(statementClosing: 150m, clearedDebit: 150m, status: ReconciliationStatus.Finalised));

        var cut = RenderWorkspace();

        Assert.Contains("Finalised", cut.Markup);
        Assert.Empty(cut.FindAll("#finalise-button"));
        Assert.Empty(cut.FindAll("#delete-draft-button"));
        Assert.All(cut.FindAll(".reconciliation-tick"), t => Assert.True(t.HasAttribute("disabled")));
    }

    // --- Delete draft ---

    [Fact]
    public void Should_DeleteDraftAndNavigate_When_DeleteClicked()
    {
        SetupWorkspace(MakeView(statementClosing: 150m, clearedDebit: 100m));

        var cut = RenderWorkspace();
        cut.Find("#delete-draft-button").Click();

        _service.Received(1).DeleteDraftAsync(ReconciliationId, Arg.Any<CancellationToken>());
        var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        Assert.EndsWith("/finance/reconciliation", nav.Uri);
    }

    [Fact]
    public void Should_ShowEmptyState_When_NoTransactionsExist()
    {
        SetupWorkspace(MakeView(statementClosing: 0m));

        var cut = RenderWorkspace();

        Assert.Contains("No transactions on this account", cut.Markup);
    }

    // --- Helpers ---

    private IRenderedComponent<ReconciliationWorkspace> RenderWorkspace() =>
        Render<ReconciliationWorkspace>(ps => ps.Add(p => p.Id, ReconciliationId));

    private void SetupWorkspace(ReconciliationWorkspaceView view)
    {
        _service.GetWorkspaceAsync(ReconciliationId, Arg.Any<CancellationToken>())
            .Returns(view);
    }

    private static ReconciliationWorkspaceView MakeView(
        decimal statementClosing, decimal opening = 0m,
        decimal clearedDebit = 0m, decimal clearedCredit = 0m, decimal unclearedDebit = 0m,
        ReconciliationStatus status = ReconciliationStatus.Draft)
    {
        var transactions = new List<ReconciliationTransactionView>();
        if (clearedDebit != 0m)
            transactions.Add(new ReconciliationTransactionView { Transaction = MakeTransaction(debit: clearedDebit), IsCleared = true });
        if (clearedCredit != 0m)
            transactions.Add(new ReconciliationTransactionView { Transaction = MakeTransaction(credit: clearedCredit), IsCleared = true });
        if (unclearedDebit != 0m)
            transactions.Add(new ReconciliationTransactionView { Transaction = MakeTransaction(debit: unclearedDebit), IsCleared = false });

        var clearedTotal = clearedDebit - clearedCredit;

        return new ReconciliationWorkspaceView
        {
            Reconciliation = new BankReconciliation
            {
                Id = ReconciliationId,
                AccountId = AccountId,
                StatementDate = StatementDate,
                StatementClosingBalance = statementClosing,
                OpeningBalance = opening,
                Status = status
            },
            Account = new Account
            {
                Id = AccountId, Name = "Operating Account", Type = AccountType.Asset,
                AccountNumber = "1110", IsBankAccount = true
            },
            Transactions = transactions,
            ClearedTotal = clearedTotal,
            Difference = statementClosing - (opening + clearedTotal)
        };
    }

    private static Transaction MakeTransaction(decimal debit = 0m, decimal credit = 0m) => new()
    {
        Id = Guid.NewGuid(),
        AccountId = AccountId,
        Date = StatementDate.AddDays(-3),
        DebitAmount = debit,
        CreditAmount = credit,
        GLAccount = "1110",
        Description = debit != 0m ? "Deposit" : "Payment",
        CreatedAt = DateTime.UtcNow
    };
}
