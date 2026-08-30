using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.UI.Layout;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Setup;

/// <summary>
/// First-run language-selection screen (spec 029, US1) — shown before the setup wizard on a
/// clean install (routed here by <c>App.razor.cs</c> whenever setup is incomplete and no
/// language preference has been recorded yet). Confirming records the choice via
/// <see cref="ILanguagePreferenceStore"/>, applies it to the running session immediately via
/// <see cref="CultureProvider.Switch"/> (no restart), then continues into the setup wizard. In
/// Debug builds, a "Load sample data" option (added by US3) can instead seed the database and
/// open straight on the dashboard.
/// </summary>
public partial class FirstRunLanguageScreen : ComponentBase
{
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private ISupportedLanguagesCatalog Languages { get; set; } = null!;
    [Inject] private ILanguageProvider LanguageProvider { get; set; } = null!;
    [Inject] private ILanguagePreferenceStore LanguagePreferenceStore { get; set; } = null!;
    [Inject] private IStringLocalizer<SetupResource> L { get; set; } = null!;

    [CascadingParameter] private CultureProvider? CultureProvider { get; set; }

    private string _selectedLanguageCode = string.Empty;
    private bool _confirming;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        // Pre-select the FR-002 default (explicit → OS language → en-AU). Pre-setup, this
        // resolution ladder has no database or recorded preference to consult, so it reduces to
        // "OS language when shipped, else en-AU" — identical to the deleted LanguageSelectionTab.
        string resolved;
        try
        {
            var culture = await LanguageProvider.ResolveStartupCultureAsync();
            resolved = Languages.Find(culture.Name)?.CultureCode ?? Languages.Default.CultureCode;
        }
        catch
        {
            resolved = Languages.Default.CultureCode;
        }

        _selectedLanguageCode = resolved;
    }

    private void HandleLanguageChanged(ChangeEventArgs e)
    {
        var selected = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(selected))
            return;

        _selectedLanguageCode = selected;
    }

    private async Task HandleConfirmAsync()
    {
        _errorMessage = null;
        _confirming = true;

        try
        {
            LanguagePreferenceStore.Set(_selectedLanguageCode);
            CultureProvider?.Switch(CultureInfo.GetCultureInfo(_selectedLanguageCode));

            Nav.NavigateTo("/setup");
        }
        finally
        {
            _confirming = false;
        }
    }
}
