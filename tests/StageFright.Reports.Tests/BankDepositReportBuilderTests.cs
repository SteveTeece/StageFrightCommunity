using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Providers;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for BankDepositReportBuilder (issue #399): the printable single-deposit report lists each
/// received item, a cash row only when cash was banked, the deposit total, and a subtitle naming
/// the description, date, bank account and status.
/// </summary>
public class BankDepositReportBuilderTests
{
    private readonly BankDepositReportBuilder _sut = new(RealLocalizer.Instance);

    private static AwaitingDepositItem Item(string? payer, string? reference, decimal amount,
        ReceivedItemSource source = ReceivedItemSource.MemberPayment) => new()
    {
        Id = Guid.NewGuid(), ReceivedDate = new DateTime(2026, 3, 2), Source = source,
        PayerName = payer, Reference = reference, Amount = amount
    };

    private static DepositDetail Deposit(decimal cash, bool reversed = false, string? description = "March takings",
        params AwaitingDepositItem[] items) => new()
    {
        Id = Guid.NewGuid(), Date = new DateTime(2026, 3, 4), Description = description, BankAccountName = "Community Bank",
        CashAmount = cash, Items = items, Total = cash + items.Sum(i => i.Amount), IsReversed = reversed
    };

    [Fact]
    public void Should_ListItemsCashAndTotal_When_DepositHasBoth()
    {
        var report = _sut.Build(Deposit(40m, items: [Item("Alice Smith", "CHQ 1", 50m), Item("Town Hall", null, 70m)]));

        Assert.Equal("Bank Deposit", report.Title);
        Assert.Equal("March takings — deposited 4 March 2026 to Community Bank (Recorded)", report.SubTitle);
        Assert.Equal(["Received", "Payer", "Reference", "Amount"], report.Columns.Select(c => c.Header));
        var rows = Assert.Single(report.Sections).Rows;
        Assert.Equal(3, rows.Count);
        Assert.Equal(["2026-03-02", "Alice Smith", "CHQ 1", MoneyFormatter.Format(50m)], rows[0].Cells);
        Assert.Equal(["2026-03-02", "Town Hall", string.Empty, MoneyFormatter.Format(70m)], rows[1].Cells);
        Assert.Equal([string.Empty, "Cash", string.Empty, MoneyFormatter.Format(40m)], rows[2].Cells);
        Assert.Equal([string.Empty, "Deposit total", string.Empty, MoneyFormatter.Format(160m)], report.GrandTotal!.Cells);
        Assert.True(report.GrandTotal.IsEmphasized);
    }

    [Fact]
    public void Should_OmitCashRow_When_NoCashBanked()
    {
        var report = _sut.Build(Deposit(0m, items: [Item("Alice Smith", "CHQ 1", 50m)]));

        var row = Assert.Single(report.Sections[0].Rows);
        Assert.Equal("Alice Smith", row.Cells[1]);
    }

    [Fact]
    public void Should_ListOnlyCash_When_LegacyDepositHasNoItems()
    {
        var report = _sut.Build(Deposit(70m));

        var row = Assert.Single(report.Sections[0].Rows);
        Assert.Equal("Cash", row.Cells[1]);
        Assert.Equal(MoneyFormatter.Format(70m), report.GrandTotal!.Cells[3]);
    }

    [Fact]
    public void Should_ShowReversedStatus_When_DepositReversed()
    {
        var report = _sut.Build(Deposit(40m, reversed: true));

        Assert.EndsWith("(Reversed)", report.SubTitle);
    }

    [Fact]
    public void Should_UseTitleInSubtitle_When_DescriptionMissing()
    {
        var report = _sut.Build(Deposit(40m, description: " "));

        Assert.StartsWith("Bank Deposit — deposited", report.SubTitle);
    }

    [Fact]
    public void Should_LabelOpeningBalancePayer_When_OpeningBalanceItemHasNoPayer()
    {
        var report = _sut.Build(Deposit(0m, items: [Item(null, null, 300m, ReceivedItemSource.OpeningBalance)]));

        Assert.Equal("Opening balance", report.Sections[0].Rows[0].Cells[1]);
    }

    [Fact]
    public void Should_LeavePayerBlank_When_IncomeItemHasNoPayer()
    {
        var report = _sut.Build(Deposit(0m, items: [Item(null, null, 30m, ReceivedItemSource.IncomeEntry)]));

        Assert.Equal(string.Empty, report.Sections[0].Rows[0].Cells[1]);
    }
}
