using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Shared;

/// <summary>
/// Shared add-account experience (FR-012/FR-016) used both by the standalone Chart of
/// Accounts page (immediate-create <see cref="OnSubmit"/>) and the setup wizard's Chart
/// of Accounts tab (deferred-queue <see cref="OnSubmit"/>) — this component knows nothing
/// about persistence, only field markup and validation, per research.md's Dependency
/// Inversion decision. Duplicate-name checking (blank/length via DataAnnotations,
/// case-insensitive duplicate against <see cref="ExistingNames"/>) lives here since it's
/// identical regardless of caller — only what happens after a valid submit differs.
/// </summary>
public partial class AddAccountForm : ComponentBase
{
    [Parameter, EditorRequired] public EventCallback<NewAccountModel> OnSubmit { get; set; }
    [Parameter] public string SubmitButtonText { get; set; } = "Add Account";

    /// <summary>Case-insensitive duplicate-check set. The standalone page passes real
    /// account names; the wizard tab passes real names union already-queued names.</summary>
    [Parameter] public IReadOnlyList<string> ExistingNames { get; set; } = Array.Empty<string>();

    private NewAccountModel _model = new();
    private string? _duplicateError;
    private bool _submitting;

    private async Task HandleValidSubmitAsync()
    {
        _duplicateError = null;
        var trimmedName = _model.Name!.Trim();

        if (ExistingNames.Any(n => string.Equals(n, trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            _duplicateError = $"An account named '{trimmedName}' already exists.";
            return;
        }

        _submitting = true;
        try
        {
            _model.Name = trimmedName;
            await OnSubmit.InvokeAsync(_model);
            _model = new NewAccountModel();
        }
        finally
        {
            _submitting = false;
        }
    }
}
