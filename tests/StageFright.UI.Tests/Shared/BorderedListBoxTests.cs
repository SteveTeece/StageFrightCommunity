using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using StageFright.UI.Shared;

namespace StageFright.UI.Tests.Shared;

/// <summary>
/// bUnit tests for BorderedListBox — the app-wide list-box rendering convention (FR-007
/// of spec 017): each item renders via RowTemplate, OnRemove unset is read-only, set
/// gives each row a remove affordance, and the container always carries the
/// ".bordered-list-box" class app.css uses for the contained-scroll treatment.
/// </summary>
public class BorderedListBoxTests : LocalizedTestContext
{
    private static readonly RenderFragment<string> RowTemplate =
        item => builder => builder.AddContent(0, item);

    [Fact]
    public void RendersEachItem_ViaRowTemplate()
    {
        var cut = Render<BorderedListBox<string>>(p => p
            .Add(x => x.Items, new[] { "Alpha", "Beta" })
            .Add(x => x.RowTemplate, RowTemplate));

        Assert.Contains("Alpha", cut.Markup);
        Assert.Contains("Beta", cut.Markup);
    }

    [Fact]
    public void OnRemoveUnset_RendersReadOnly_NoRemoveButtons()
    {
        var cut = Render<BorderedListBox<string>>(p => p
            .Add(x => x.Items, new[] { "Alpha" })
            .Add(x => x.RowTemplate, RowTemplate));

        Assert.Equal("list", cut.Find(".bordered-list-box").GetAttribute("role"));
        Assert.Empty(cut.FindAll(".bordered-list-box-remove"));
    }

    [Fact]
    public void OnRemoveSet_RendersRemoveButton_PerRow()
    {
        var cut = Render<BorderedListBox<string>>(p => p
            .Add(x => x.Items, new[] { "Alpha", "Beta" })
            .Add(x => x.RowTemplate, RowTemplate)
            .Add(x => x.OnRemove, EventCallback.Factory.Create<string>(this, _ => { })));

        Assert.Equal("listbox", cut.Find(".bordered-list-box").GetAttribute("role"));
        Assert.Equal(2, cut.FindAll(".bordered-list-box-remove").Count);
    }

    [Fact]
    public void ClickingRemove_InvokesCallback_WithTheClickedItem()
    {
        string? removed = null;
        var cut = Render<BorderedListBox<string>>(p => p
            .Add(x => x.Items, new[] { "Alpha", "Beta" })
            .Add(x => x.RowTemplate, RowTemplate)
            .Add(x => x.OnRemove, EventCallback.Factory.Create<string>(this, item => removed = item)));

        cut.FindAll(".bordered-list-box-remove")[1].Click();

        Assert.Equal("Beta", removed);
    }

    [Fact]
    public void EmptyItems_ShowsEmptyText()
    {
        var cut = Render<BorderedListBox<string>>(p => p
            .Add(x => x.Items, Array.Empty<string>())
            .Add(x => x.RowTemplate, RowTemplate)
            .Add(x => x.EmptyText, "Nothing queued yet."));

        Assert.Contains("Nothing queued yet.", cut.Markup);
        Assert.Empty(cut.FindAll(".bordered-list-box-row"));
    }

    [Fact]
    public void DefaultEmptyText_IsUsed_WhenNotSpecified()
    {
        var cut = Render<BorderedListBox<string>>(p => p
            .Add(x => x.Items, Array.Empty<string>())
            .Add(x => x.RowTemplate, RowTemplate));

        Assert.Contains("Nothing added yet.", cut.Markup);
    }
}
