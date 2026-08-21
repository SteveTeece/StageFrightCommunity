using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup.Tabs;

/// <summary>bUnit tests for CommitteeTab (US1/US5) — AGM month, seat count target, and
/// the type-and-"+"-to-add office-holder titles widget (BorderedListBox with per-row
/// remove) that replaced the old comma-separated textbox (FR-009/FR-010/FR-011).
/// Rendered inside an EditForm since AGM month/seat count still use Input* components
/// needing a cascaded EditContext (built manually — EditForm.ChildContent is generic).</summary>
public class CommitteeTabTests : BunitContext
{
    private IRenderedComponent<EditForm> RenderInForm(
        SetupFormModel model,
        IReadOnlyList<string>? queuedTitles = null,
        EventCallback<string>? onAdd = null,
        EventCallback<string>? onRemove = null) =>
        Render<EditForm>(p => p
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(_ => builder =>
            {
                builder.OpenComponent<CommitteeTab>(0);
                builder.AddComponentParameter(1, nameof(CommitteeTab.Model), model);
                builder.AddComponentParameter(2, nameof(CommitteeTab.QueuedTitles), queuedTitles ?? Array.Empty<string>());
                builder.AddComponentParameter(3, nameof(CommitteeTab.OnAdd), onAdd ?? EventCallback.Factory.Create<string>(this, _ => { }));
                builder.AddComponentParameter(4, nameof(CommitteeTab.OnRemove), onRemove ?? EventCallback.Factory.Create<string>(this, _ => { }));
                builder.CloseComponent();
            })));

    [Fact]
    public void RendersAgmMonthAndSeatCount_BoundToModel()
    {
        var model = new SetupFormModel();
        var cut = RenderInForm(model);

        cut.Find("#agmMonth").Change("10");
        cut.Find("#seatCountTarget").Change("8");

        Assert.Equal(10, model.CommitteeRenewalMonth);
        Assert.Equal(8, model.GeneralCommitteeSeatCountTarget);
    }

    [Fact]
    public void AgmMonthOptions_CoverAllTwelveMonths()
    {
        var cut = RenderInForm(new SetupFormModel());

        Assert.Equal(12, cut.Find("#agmMonth").Children.Length);
    }

    [Fact]
    public void ValidTitle_InvokesOnAdd_AndClearsInput()
    {
        string? added = null;
        var cut = RenderInForm(new SetupFormModel(),
            onAdd: EventCallback.Factory.Create<string>(this, t => added = t));

        cut.Find("#committee-role-input").Input("Publicity Officer");
        cut.Find("#committee-role-add-btn").Click();

        Assert.Equal("Publicity Officer", added);
        Assert.Equal(string.Empty, cut.Find("#committee-role-input").GetAttribute("value") ?? string.Empty);
    }

    [Fact]
    public void BlankTitle_IsRejected_OnAddNotInvoked()
    {
        var invoked = false;
        var cut = RenderInForm(new SetupFormModel(),
            onAdd: EventCallback.Factory.Create<string>(this, _ => invoked = true));

        cut.Find("#committee-role-input").Input("   ");
        cut.Find("#committee-role-add-btn").Click();

        Assert.False(invoked);
        Assert.Contains("Enter a title", cut.Markup);
    }

    [Fact]
    public void DuplicateTitle_IsRejected_CaseInsensitively_OnAddNotInvoked()
    {
        var invoked = false;
        var cut = RenderInForm(new SetupFormModel(),
            queuedTitles: new[] { "Publicity Officer" },
            onAdd: EventCallback.Factory.Create<string>(this, _ => invoked = true));

        cut.Find("#committee-role-input").Input("publicity officer");
        cut.Find("#committee-role-add-btn").Click();

        Assert.False(invoked);
        Assert.Contains("already been added", cut.Markup);
    }

    [Fact]
    public void QueuedTitles_RenderInBorderedListBox()
    {
        var cut = RenderInForm(new SetupFormModel(), queuedTitles: new[] { "Publicity Officer", "Webmaster" });

        Assert.Contains("Publicity Officer", cut.Markup);
        Assert.Contains("Webmaster", cut.Markup);
    }

    [Fact]
    public void EmptyQueue_ShowsEmptyText()
    {
        var cut = RenderInForm(new SetupFormModel());

        Assert.Contains("No additional titles added yet.", cut.Markup);
    }

    [Fact]
    public void ClickingRemove_InvokesOnRemove_WithTheClickedTitle()
    {
        string? removed = null;
        var cut = RenderInForm(new SetupFormModel(),
            queuedTitles: new[] { "Publicity Officer", "Webmaster" },
            onRemove: EventCallback.Factory.Create<string>(this, t => removed = t));

        cut.FindAll(".bordered-list-box-remove")[1].Click();

        Assert.Equal("Webmaster", removed);
    }

    [Fact]
    public void FiveTitles_AddableAndRemovable_NoDupesOrBlanksEverAppear()
    {
        // SC-004: simulates what SetupWizard does — each OnAdd/OnRemove grows/shrinks the
        // queue, and the tab is re-rendered with the updated list, same as a real session.
        var model = new SetupFormModel();
        var queue = new List<string>();
        var cut = RenderInForm(model, queue,
            onAdd: EventCallback.Factory.Create<string>(this, t => queue.Add(t)),
            onRemove: EventCallback.Factory.Create<string>(this, t => queue.Remove(t)));

        var tab = cut.FindComponent<CommitteeTab>();
        var titles = new[] { "Publicity Officer", "Webmaster", "Fundraising Officer", "Social Secretary", "Librarian" };
        foreach (var title in titles)
        {
            cut.Find("#committee-role-input").Input(title);
            cut.Find("#committee-role-add-btn").Click();
            tab.Render(p => p.Add(x => x.QueuedTitles, queue.ToList()));
        }

        Assert.Equal(5, queue.Count);
        Assert.Equal(5, queue.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(queue, string.IsNullOrWhiteSpace);
        foreach (var title in titles)
            Assert.Contains(title, cut.Markup);

        foreach (var title in titles)
        {
            cut.FindAll(".bordered-list-box-remove")[0].Click();
            tab.Render(p => p.Add(x => x.QueuedTitles, queue.ToList()));
        }

        Assert.Empty(queue);
        Assert.Contains("No additional titles added yet.", cut.Markup);
    }
}
