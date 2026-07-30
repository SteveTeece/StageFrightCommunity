using Bunit;

namespace StageFright.UI.Tests;

/// <summary>
/// Shared bUnit base class for tests that render a RadzenDataGrid. RadzenDataGrid's
/// OnAfterRenderAsync invokes the JS interop call "Radzen.createDataGrid", which returns an
/// IJSObjectReference handle (later used to dispose event listeners). bUnit requires
/// SetupModule (not a plain Setup&lt;IJSObjectReference&gt;) to mock calls with that return type.
/// The module is set to Loose mode so any later calls on the returned handle (e.g. "dispose")
/// resolve to defaults instead of throwing.
/// </summary>
public abstract class RadzenGridTestContext : BunitContext
{
    protected RadzenGridTestContext()
    {
        JSInterop.SetupModule("Radzen.createDataGrid", _ => true).Mode = JSRuntimeMode.Loose;
    }
}
