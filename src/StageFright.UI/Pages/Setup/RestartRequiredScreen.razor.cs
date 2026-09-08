using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Setup;

/// <summary>
/// Terminal screen shown after a successful restore (spec 030, US1 / FR-007). Rendered under the
/// chrome-free <see cref="Layout.BlankLayout"/> — no sidebar, no nav — and renders only a
/// "close and reopen the application" instruction with no continue / go-to-dashboard / retry
/// control of its own, so a post-restore user cannot proceed on pre-restore in-memory state. The
/// application never relaunches itself; the next launch routes normally (a restored
/// <c>Settings</c> row makes <c>IsSetupCompleteAsync()</c> true → <c>/dashboard</c>).
/// </summary>
public partial class RestartRequiredScreen : ComponentBase
{
    [Inject] private IStringLocalizer<SetupResource> L { get; set; } = null!;
}
