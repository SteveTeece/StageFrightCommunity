using Bunit;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Finance;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>bUnit tests for DepositItemSelectionGrid (spec 033 US3): select-all header and the selection-changed total.</summary>
public class DepositItemSelectionGridTests : LocalizedTestContext
{
    private static readonly AwaitingDepositItem First = Item("Alice Smith", "CHQ 1", 50m);
    private static readonly AwaitingDepositItem Second = Item("Town Hall", "CHQ 2", 120m);

    private static AwaitingDepositItem Item(string payer, string reference, decimal amount) => new()
    {
        Id = Guid.NewGuid(), ReceivedDate = new DateTime(2026, 3, 3), Source = ReceivedItemSource.MemberPayment,
        PayerName = payer, Reference = reference, Amount = amount
    };

    [Fact]
    public void Should_RenderUntickedRowsWithSelectAllHeader_When_ItemsAwait()
    {
        var cut = Render<DepositItemSelectionGrid>(p => p.Add(x => x.Items, [First, Second]));

        Assert.Single(cut.FindAll("input#select-all-deposit-items"));
        var rows = cut.FindAll("input[type=checkbox][id^='deposit-item-']");
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.False(r.HasAttribute("checked")));
        Assert.Contains("Alice Smith", cut.Markup);
        Assert.Contains("CHQ 2", cut.Markup);
    }

    [Fact]
    public void Should_RaiseTickedTotal_When_RowTicked()
    {
        decimal? total = null;
        var cut = Render<DepositItemSelectionGrid>(p => p
            .Add(x => x.Items, [First, Second])
            .Add(x => x.SelectionChanged, t => total = t));

        cut.Find($"#deposit-item-{Second.Id}").Change(true);

        Assert.Equal(120m, total);
        Assert.Equal([Second.Id], cut.Instance.GetSelectedItemIds());
    }

    [Fact]
    public void Should_TickEveryRowAndRaiseFullTotal_When_SelectAllTicked()
    {
        decimal? total = null;
        var cut = Render<DepositItemSelectionGrid>(p => p
            .Add(x => x.Items, [First, Second])
            .Add(x => x.SelectionChanged, t => total = t));

        cut.Find("#select-all-deposit-items").Change(true);

        Assert.Equal(170m, total);
        Assert.Equal(2, cut.Instance.GetSelectedItemIds().Count);

        cut.Find("#select-all-deposit-items").Change(false);

        Assert.Equal(0m, total);
        Assert.Empty(cut.Instance.GetSelectedItemIds());
    }

    [Fact]
    public void Should_DisableCheckboxes_When_ReadOnly()
    {
        var cut = Render<DepositItemSelectionGrid>(p => p
            .Add(x => x.Items, [First])
            .Add(x => x.ReadOnly, true));

        Assert.True(cut.Find("#select-all-deposit-items").HasAttribute("disabled"));
        Assert.True(cut.Find($"#deposit-item-{First.Id}").HasAttribute("disabled"));
    }

    [Fact]
    public void Should_ShowNothingAwaitingMessage_When_NoItems()
    {
        var cut = Render<DepositItemSelectionGrid>(p => p.Add(x => x.Items, []));

        Assert.Contains("No cheques or other items are awaiting deposit.", cut.Markup);
    }
}
