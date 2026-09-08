using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Layout;

/// <summary>
/// Deliberately chrome-free layout (spec 030, US1 / FR-007). Wraps <c>@Body</c> in the same
/// <see cref="CultureProvider"/> / <see cref="ThemeProvider"/> cascade as <see cref="ShellLayout"/>
/// but renders no <c>&lt;nav class="shell-sidebar"&gt;</c>, no sidebar links and no theme toggle —
/// a terminal screen mounted under it has no navigation surface at all. Used only by
/// <see cref="Pages.Setup.RestartRequiredScreen"/>; every other route stays on
/// <see cref="ShellLayout"/>.
/// </summary>
public partial class BlankLayout : LayoutComponentBase
{
}
