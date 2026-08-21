using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Shared;

/// <summary>
/// The one bordered-list rendering convention every list box in the app follows (FR-007
/// of spec 017) — a read-only display when <see cref="OnRemove"/> is unset, or an
/// interactive list with a per-row remove affordance when it's supplied. Internally
/// scrolls (see app.css's <c>.bordered-list-box</c>) so a long list stays contained
/// instead of pushing the rest of its host tab out of view.
/// </summary>
public partial class BorderedListBox<TItem> : ComponentBase
{
    [Parameter, EditorRequired] public IReadOnlyList<TItem> Items { get; set; } = Array.Empty<TItem>();
    [Parameter, EditorRequired] public RenderFragment<TItem> RowTemplate { get; set; } = null!;

    /// <summary>Unset (null) renders a read-only list; set, each row gets a remove button.</summary>
    [Parameter] public EventCallback<TItem>? OnRemove { get; set; }

    [Parameter] public string EmptyText { get; set; } = "Nothing added yet.";

    private async Task HandleRemoveAsync(TItem item)
    {
        if (OnRemove is { } callback)
            await callback.InvokeAsync(item);
    }
}
