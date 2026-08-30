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
    public void Switch_UpdatesCurrentCultureProperty()
    {
        using var _ = new CultureRestorer();
        var cut = Render<CultureProvider>(p => p.AddChildContent("<span>content</span>"));
        var newCulture = CultureInfo.GetCultureInfo("fr-FR");

        cut.InvokeAsync(() => cut.Instance.Switch(newCulture));

        Assert.Equal("fr-FR", cut.Instance.CurrentCulture.Name);
    }

    [Fact]
    public void Switch_SetsProcessCultureInfo()
    {
        using var _ = new CultureRestorer();
        var cut = Render<CultureProvider>(p => p.AddChildContent("<span>content</span>"));
        var newCulture = CultureInfo.GetCultureInfo("fr-FR");

        cut.InvokeAsync(() => cut.Instance.Switch(newCulture));

        Assert.Equal("fr-FR", CultureInfo.CurrentCulture.Name);
        Assert.Equal("fr-FR", CultureInfo.CurrentUICulture.Name);
        Assert.Equal("fr-FR", CultureInfo.DefaultThreadCurrentCulture?.Name);
        Assert.Equal("fr-FR", CultureInfo.DefaultThreadCurrentUICulture?.Name);
    }

    [Fact]
    public void Switch_RerendersDescendant_ThatReadsCurrentCultureAsCascadingValue()
    {
        using var _ = new CultureRestorer();
        var cut = Render<CultureProvider>(p => p.AddChildContent<CultureConsumer>());
        var newCulture = CultureInfo.GetCultureInfo("fr-FR");

        cut.InvokeAsync(() => cut.Instance.Switch(newCulture));

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

    /// <summary>Saves the four static <see cref="CultureInfo"/> slots <see cref="CultureProvider.Switch"/> writes and restores them on dispose.</summary>
    private sealed class CultureRestorer : IDisposable
    {
        private readonly CultureInfo _currentCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _currentUiCulture = CultureInfo.CurrentUICulture;
        private readonly CultureInfo? _defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        private readonly CultureInfo? _defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _currentCulture;
            CultureInfo.CurrentUICulture = _currentUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = _defaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = _defaultUiCulture;
        }
    }
}
