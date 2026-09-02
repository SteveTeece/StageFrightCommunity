using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using StageFright.UI.Layout;

namespace StageFright.UI.Tests.Layout;

/// <summary>
/// bUnit tests for <see cref="CultureProvider.Switch"/> (spec 029) — the in-session live
/// language switch. Culture is process-wide static state, so every test saves and restores it
/// to avoid bleeding into other tests sharing the same thread.
/// </summary>
public sealed class CultureProviderTests : BunitContext
{
    [Fact]
    public async Task Switch_UpdatesCurrentCultureProperty()
    {
        using var _ = new CultureRestorer();
        var cut = Render<CultureProvider>(p => p.AddChildContent("<span>content</span>"));
        var newCulture = CultureInfo.GetCultureInfo("fr-FR");

        await cut.InvokeAsync(() => cut.Instance.Switch(newCulture));

        Assert.Equal("fr-FR", cut.Instance.CurrentCulture.Name);
    }

    [Fact]
    public async Task Switch_SetsTheProcessWideCultureGlobals()
    {
        using var _ = new CultureRestorer();
        var cut = Render<CultureProvider>(p => p.AddChildContent("<span>content</span>"));
        var newCulture = CultureInfo.GetCultureInfo("fr-FR");

        await cut.InvokeAsync(() => cut.Instance.Switch(newCulture));

        // Switch assigns the two process-wide globals — and only those. A render that reads
        // CultureInfo.CurrentUICulture with no per-context override (the real renderer's state
        // once MauiProgram stops pinning one) resolves straight through to them.
        Assert.Equal("fr-FR", CultureInfo.DefaultThreadCurrentCulture?.Name);
        Assert.Equal("fr-FR", CultureInfo.DefaultThreadCurrentUICulture?.Name);
    }

    [Fact]
    public async Task Switch_DoesNotPinAPerContextOverride_SoLaterGlobalChangesStillTakeEffect()
    {
        using var _ = new CultureRestorer();
        var cut = Render<CultureProvider>(p => p.AddChildContent("<span>content</span>"));

        await cut.InvokeAsync(() => cut.Instance.Switch(CultureInfo.GetCultureInfo("fr-FR")));

        // Regression guard for the spec 029 T036 defect: if Switch pinned CultureInfo.CurrentUICulture
        // as an AsyncLocal per-context override, that override would shadow every subsequent
        // DefaultThreadCurrentUICulture read on the same context — freezing the UI language until a
        // process restart. Proof it does not: a direct change to the global is still observed.
        await cut.InvokeAsync(() =>
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal("de-DE", CultureInfo.CurrentCulture.Name);
            Assert.Equal("de-DE", CultureInfo.CurrentUICulture.Name);
        });
    }

    [Fact]
    public async Task Switch_RerendersDescendant_ThatReadsCurrentCultureAsCascadingValue()
    {
        using var _ = new CultureRestorer();
        var cut = Render<CultureProvider>(p => p.AddChildContent<CultureConsumer>());
        var newCulture = CultureInfo.GetCultureInfo("fr-FR");

        await cut.InvokeAsync(() => cut.Instance.Switch(newCulture));

        Assert.Equal("fr-FR", cut.Find(".culture-consumer").TextContent);
    }

    /// <summary>Reads <see cref="CultureProvider.CurrentCulture"/> as a cascading value, exactly
    /// like a real page's <c>@L["Key"]</c>/<c>MoneyFormatter.Format</c> call sites would.</summary>
    private sealed class CultureConsumer : ComponentBase
    {
        [CascadingParameter] public CultureProvider? CultureProvider { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "culture-consumer");
            builder.AddContent(2, CultureProvider?.CurrentCulture.Name);
            builder.CloseElement();
        }
    }

}
