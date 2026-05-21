using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Shared;

public partial class MainLayout : IAsyncDisposable
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await ValueTask.CompletedTask;
    }
}
