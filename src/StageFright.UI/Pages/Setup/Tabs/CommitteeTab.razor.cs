using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Localization;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>Committee tab (US1/US5) — AGM month, seat count target, and a type-and-arrow-
/// button-to-add office-holder titles widget (a BorderedListBox with per-row remove),
/// replacing the old single-page wizard's comma-separated textbox (FR-009/FR-010/FR-011).
/// The queue itself lives in SetupWizard.razor.cs, same as the Chart of Accounts tab's
/// queue, since the Review tab also needs it.</summary>
public partial class CommitteeTab : ComponentBase
{
    [Parameter, EditorRequired] public SetupFormModel Model { get; set; } = null!;
    [Parameter, EditorRequired] public IReadOnlyList<string> QueuedTitles { get; set; } = Array.Empty<string>();
    [Parameter, EditorRequired] public EventCallback<string> OnAdd { get; set; }
    [Parameter, EditorRequired] public EventCallback<string> OnRemove { get; set; }

    [Inject] private IStringLocalizer<SetupResource> L { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private string _titleInput = string.Empty;
    private string? _error;

    private async Task HandleAddAsync()
    {
        var trimmed = _titleInput.Trim();
        if (trimmed.Length == 0)
        {
            _error = L["Setup_Committee_ErrorBlank"];
            return;
        }

        if (QueuedTitles.Any(t => string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            _error = Loc.Get<SetupResource>("Setup_Committee_ErrorDuplicate", trimmed);
            return;
        }

        _error = null;
        _titleInput = string.Empty;
        await OnAdd.InvokeAsync(trimmed);
    }

    private async Task HandleRemoveAsync(string title)
    {
        await OnRemove.InvokeAsync(title);
    }
}
