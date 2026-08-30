using System.Globalization;

namespace StageFright.UI.Tests;

/// <summary>
/// Saves the four static <see cref="CultureInfo"/> slots <see cref="StageFright.UI.Layout.CultureProvider.Switch"/>
/// writes and restores them on dispose. Culture is process-wide static state (not per-test), so
/// any test that triggers a live culture switch — directly via <c>CultureProvider.Switch</c>, or
/// indirectly by clicking a confirm/save button that calls it — must wrap the call in one of
/// these or the mutation leaks into every other test sharing the run (xUnit reuses threads across
/// test classes), breaking unrelated English-text/date-format assertions elsewhere in the suite.
/// </summary>
public sealed class CultureRestorer : IDisposable
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
