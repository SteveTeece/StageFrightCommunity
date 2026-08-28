using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>
/// First-run display-language step (spec 027, US3 / FR-013). A plain <c>&lt;select&gt;</c> of
/// the runtime-discovered shipped languages by endonym — consistent with the wizard's theme
/// dropdown (FR-022 exception) since v1 has no in-session live switch. Pre-selects the
/// FR-023-resolved default and writes the choice onto <see cref="SetupFormModel.LanguageCode"/>,
/// which <c>SetupWizard</c> carries into <c>SetupRequest</c> at Finish.
/// </summary>
public partial class LanguageSelectionTab : ComponentBase
{
    [Inject] private IStringLocalizer<SetupResource> L { get; set; } = null!;
    [Inject] private ISupportedLanguagesCatalog Languages { get; set; } = null!;
    [Inject] private ILanguageProvider LanguageProvider { get; set; } = null!;

    [Parameter] public SetupFormModel Model { get; set; } = null!;

    private string _selectedLanguageCode = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        if (!string.IsNullOrWhiteSpace(Model.LanguageCode))
        {
            _selectedLanguageCode = Model.LanguageCode!;
            return;
        }

        // No prior choice this session — pre-select the FR-023 default (explicit → OS language
        // → en-AU). During setup there is no Settings row, so this resolves to the OS language
        // when a matching set ships, otherwise en-AU.
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
        Model.LanguageCode = resolved;
    }

    private void HandleLanguageChanged(ChangeEventArgs e)
    {
        var selected = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(selected))
            return;

        _selectedLanguageCode = selected;
        Model.LanguageCode = selected;
    }
}
