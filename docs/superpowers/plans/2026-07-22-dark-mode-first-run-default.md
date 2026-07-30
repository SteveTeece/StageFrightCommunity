# Dark Mode First-Run Default Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Default the app to Dark mode on first run (following the OS/device theme preference where available, falling back to Dark), let the user override that default with a toggle switch on the Setup Wizard itself, and keep persisting the chosen theme across sessions as it already does today.

**Architecture:** A new `IDeviceThemePreferenceProvider` abstraction (implemented in `StageFright.App` via MAUI's `Application.Current.RequestedTheme`, since `StageFright.Core` has no MAUI dependency) feeds `ThemeProvider`'s pre-setup fallback. `ThemeProvider`'s resulting `CurrentTheme` is surfaced as a toggle switch directly on the Setup Wizard and flows into `SetupRequest.Theme`, which `SetupService.InitializeAsync` persists verbatim — no separate default computation inside `SetupService`.

**Tech Stack:** .NET 10 / MAUI Blazor Hybrid, xUnit, NSubstitute, bUnit.

**Related issue:** GitHub issue #248 ("Default to Dark Mode") — reference it in commits; close it once the full build and test suite are green.

## Global Constraints

- One class per file; private nested types are the only exception (CLAUDE.md "Key rules").
- Every `.razor` component pairs with a `.razor.cs` code-behind; `@code` blocks in `.razor` files are prohibited (CLAUDE.md "Key rules").
- Every reachable code path (success, validation failure, exception, boundary/null) must have automated tests before merge; test names follow `Should_[ExpectedBehavior]_When_[Condition]` or the existing file's established naming style (CLAUDE.md "Key rules"). `MauiDeviceThemePreferenceProvider` is the sole exception — a one-line MAUI API passthrough in a project (`StageFright.App`) that has no test project today, consistent with existing composition-root code (`MauiProgram.cs`, `App.xaml.cs`).
- Run `dotnet build` and the full test suite (without `--no-build`) after making code changes, and report build/test results before considering the task complete (CLAUDE.md "Build & Test Verification").
- Commit all changed and new files at the end of the task, staged with exact filenames (not `git add -A`/`.`), following the existing commit message style (CLAUDE.md "Git / Commit Workflow").

---

### Task 1: `IDeviceThemePreferenceProvider` abstraction + `ThemeProvider` OS-preference fallback

**Files:**
- Create: `src/StageFright.Core/Enums/PlatformThemePreference.cs`
- Create: `src/StageFright.Core/Contracts/IDeviceThemePreferenceProvider.cs`
- Modify: `src/StageFright.UI/Layout/ThemeProvider.razor.cs`
- Test: `tests/StageFright.UI.Tests/Layout/ThemeProviderTests.cs`
- Test: `tests/StageFright.UI.Tests/Layout/ShellLayoutTests.cs`

**Interfaces:**
- Produces: `enum StageFright.Core.Enums.PlatformThemePreference { Unspecified, Light, Dark }`
- Produces: `interface StageFright.Core.Contracts.IDeviceThemePreferenceProvider { PlatformThemePreference GetPreference(); }`
- Produces: `ThemeProvider` now falls back to `PlatformThemePreference`-derived `Theme` (Dark when `Unspecified`) instead of a hardcoded `Theme.Light`, whenever `Settings` is null.

- [ ] **Step 1: Create the `PlatformThemePreference` enum**

```csharp
namespace StageFright.Core.Enums;

/// <summary>
/// The OS/device's own light-or-dark preference, as reported by the host platform.
/// Mirrors MAUI's AppTheme without StageFright.Core taking a MAUI dependency.
/// </summary>
public enum PlatformThemePreference
{
    /// <summary>The platform did not report a preference (or none is available).</summary>
    Unspecified,

    /// <summary>The platform requests a light theme.</summary>
    Light,

    /// <summary>The platform requests a dark theme.</summary>
    Dark
}
```

- [ ] **Step 2: Create the `IDeviceThemePreferenceProvider` interface**

```csharp
using StageFright.Core.Enums;

namespace StageFright.Core.Contracts;

/// <summary>Reads the host platform's light/dark theme preference.</summary>
public interface IDeviceThemePreferenceProvider
{
    /// <summary>Returns the platform's current theme preference.</summary>
    PlatformThemePreference GetPreference();
}
```

- [ ] **Step 3: Write the failing bUnit tests for `ThemeProvider`'s new fallback behavior**

Replace the two existing Light-fallback tests in `tests/StageFright.UI.Tests/Layout/ThemeProviderTests.cs` (`Renders_DataBsTheme_Light_ByDefault_WhenSettingsNull` and `CurrentTheme_IsLight_WhenSettingsNull`) and add the `IDeviceThemePreferenceProvider` mock to the test class. Replace the whole file with:

```csharp
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.UI.Layout;

namespace StageFright.UI.Tests.Layout;

/// <summary>
/// bUnit tests for ThemeProvider — verifies data-bs-theme attribute changes on toggle,
/// the OS-preference-driven fallback (Dark when unspecified), and preference persistence
/// via SettingsService.
/// </summary>
public class ThemeProviderTests : BunitContext
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IDeviceThemePreferenceProvider _deviceThemeProvider = Substitute.For<IDeviceThemePreferenceProvider>();

    public ThemeProviderTests()
    {
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(_deviceThemeProvider);
        _deviceThemeProvider.GetPreference().Returns(PlatformThemePreference.Unspecified);
    }

    // --- Default theme (no Settings row yet — pre-setup fallback) ---

    [Theory]
    [InlineData(PlatformThemePreference.Light, "light")]
    [InlineData(PlatformThemePreference.Dark, "dark")]
    [InlineData(PlatformThemePreference.Unspecified, "dark")]
    public void Renders_DataBsTheme_FromDevicePreference_WhenSettingsNull(PlatformThemePreference preference, string expectedAttr)
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns((Settings?)null);
        _deviceThemeProvider.GetPreference().Returns(preference);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span class='child'>Content</span>"));

        var attr = cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme");
        Assert.Equal(expectedAttr, attr);
    }

    [Fact]
    public async Task Renders_DataBsTheme_Light_WhenSettingsThemeIsLight()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(Theme.Light));
        _settingsService.SaveAsync(Arg.Any<StageFright.Core.Entities.Settings>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span class='child'>Content</span>"));

        await cut.InvokeAsync(() => cut.Instance.ToggleAsync()); // toggle to dark
        await cut.InvokeAsync(() => cut.Instance.ToggleAsync()); // toggle back to light

        var attr = cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme");
        Assert.Equal("light", attr);
    }

    [Fact]
    public void Renders_DataBsTheme_Dark_WhenSettingsThemeIsDark()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(Theme.Dark));

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span class='child'>Content</span>"));

        var attr = cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme");
        Assert.Equal("dark", attr);
    }

    // --- Toggle ---

    [Fact]
    public async Task Toggle_ChangesTheme_FromLight_ToDark()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(Theme.Light));
        _settingsService.SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span>Content</span>"));

        Assert.Equal("light", cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme"));

        await cut.InvokeAsync(() => cut.Instance.ToggleAsync());

        Assert.Equal("dark", cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme"));
    }

    [Fact]
    public async Task Toggle_ChangesTheme_FromDark_ToLight()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(Theme.Dark));
        _settingsService.SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span>Content</span>"));

        Assert.Equal("dark", cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme"));

        await cut.InvokeAsync(() => cut.Instance.ToggleAsync());

        Assert.Equal("light", cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme"));
    }

    // --- Persistence ---

    [Fact]
    public async Task Toggle_PersistsTheme_ViaSaveAsync()
    {
        var settings = MakeSettings(Theme.Light);
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(settings);
        _settingsService.SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span>Content</span>"));

        await cut.InvokeAsync(() => cut.Instance.ToggleAsync());

        await _settingsService.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s.Theme == Theme.Dark),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Toggle_WhenSettingsNull_DoesNotCallSaveAsync()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns((Settings?)null);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span>Content</span>"));

        await cut.InvokeAsync(() => cut.Instance.ToggleAsync());

        await _settingsService.DidNotReceive().SaveAsync(
            Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    }

    // --- Cascading value ---

    [Fact]
    public void ExposesItself_AsCascadingValue_SoChildrenCanAccess()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns((Settings?)null);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span>Content</span>"));

        // The ThemeProvider instance is accessible as the component instance
        Assert.NotNull(cut.Instance);
        Assert.IsType<ThemeProvider>(cut.Instance);
    }

    // --- CurrentTheme ---

    [Theory]
    [InlineData(PlatformThemePreference.Light, Theme.Light)]
    [InlineData(PlatformThemePreference.Dark, Theme.Dark)]
    [InlineData(PlatformThemePreference.Unspecified, Theme.Dark)]
    public void CurrentTheme_FollowsDevicePreference_WhenSettingsNull(PlatformThemePreference preference, Theme expectedTheme)
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns((Settings?)null);
        _deviceThemeProvider.GetPreference().Returns(preference);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span>Content</span>"));

        Assert.Equal(expectedTheme, cut.Instance.CurrentTheme);
    }

    [Fact]
    public async Task CurrentTheme_UpdatesAfterToggle()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(Theme.Light));
        _settingsService.SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span>Content</span>"));

        Assert.Equal(Theme.Light, cut.Instance.CurrentTheme);

        await cut.InvokeAsync(() => cut.Instance.ToggleAsync());

        Assert.Equal(Theme.Dark, cut.Instance.CurrentTheme);
    }

    // --- Helpers ---

    private static Settings MakeSettings(Theme theme) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationName = "Test Org",
        AnnualFee = 50m,
        AttendanceFee = 5m,
        MembershipRenewalMonth = 1,
        Theme = theme,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
```

- [ ] **Step 4: Run the new/changed tests to verify they fail**

Run: `dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~ThemeProviderTests"`
Expected: FAIL — compile error (`IDeviceThemePreferenceProvider` not registered as a service `ThemeProvider` requires yet) or, once it compiles, `Renders_DataBsTheme_FromDevicePreference_WhenSettingsNull` and `CurrentTheme_FollowsDevicePreference_WhenSettingsNull` failing for the `Dark`/`Unspecified` cases because `ThemeProvider` still hardcodes `Theme.Light`.

- [ ] **Step 5: Update `ThemeProvider.razor.cs` to use the device preference fallback**

Replace `src/StageFright.UI/Layout/ThemeProvider.razor.cs` with:

```csharp
using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;

namespace StageFright.UI.Layout;

/// <summary>
/// Cascading component that owns the current UI theme.
/// Wraps content in an element with data-bs-theme="light"|"dark" so Bootstrap 5.3
/// applies the correct colour scheme to all descendants.
/// Reads the initial theme from Settings on mount; when no Settings row exists yet
/// (pre-setup), falls back to the device's OS theme preference, defaulting to Dark
/// when that preference is unavailable. Exposes ToggleAsync to persist changes.
/// </summary>
public partial class ThemeProvider : ComponentBase
{
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private IDeviceThemePreferenceProvider DeviceThemePreferenceProvider { get; set; } = null!;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    private Theme _currentTheme = Theme.Dark;

    /// <summary>The active theme — read by ShellLayout, GeneralSettingsTab, and SetupWizard.</summary>
    public Theme CurrentTheme => _currentTheme;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var settings = await SettingsService.GetAsync();
            _currentTheme = settings?.Theme ?? FallbackTheme();
        }
        catch (Exception)
        {
            _currentTheme = FallbackTheme();
        }
    }

    private Theme FallbackTheme() => DeviceThemePreferenceProvider.GetPreference() switch
    {
        PlatformThemePreference.Light => Theme.Light,
        PlatformThemePreference.Dark => Theme.Dark,
        _ => Theme.Dark
    };

    /// <summary>
    /// Toggles the theme between Light and Dark, persists the choice to Settings,
    /// and triggers a re-render so all cascaded consumers update.
    /// </summary>
    public async Task ToggleAsync()
    {
        _currentTheme = _currentTheme == Theme.Light ? Theme.Dark : Theme.Light;

        var settings = await SettingsService.GetAsync();
        if (settings is not null)
        {
            settings.Theme = _currentTheme;
            await SettingsService.SaveAsync(settings);
        }

        StateHasChanged();
    }
}
```

- [ ] **Step 6: Fix `ShellLayoutTests` — register the new required service**

`ShellLayout` renders `<ThemeProvider>` internally, so every `Render<ShellLayout>()` call in `tests/StageFright.UI.Tests/Layout/ShellLayoutTests.cs` now needs `IDeviceThemePreferenceProvider` registered in DI or the render will throw. The existing test `Should_RenderThemeToggleShowingLight_When_ThemeIsLight` asserts a Light fallback, so stub the preference as `Light` to keep that assertion meaningful (it now exercises "OS says Light" instead of "hardcoded Light").

In `tests/StageFright.UI.Tests/Layout/ShellLayoutTests.cs`, add the import and registration:

```csharp
using StageFright.Core.Enums; // add alongside the existing usings
```

```csharp
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IDeviceThemePreferenceProvider _deviceThemeProvider = Substitute.For<IDeviceThemePreferenceProvider>();

    public ShellLayoutTests()
    {
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(_deviceThemeProvider);
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns((Settings?)null);
        _deviceThemeProvider.GetPreference().Returns(PlatformThemePreference.Light);
    }
```

(This replaces the existing constructor and field declarations at the top of the class — everything else in the file is unchanged.)

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~ThemeProviderTests|FullyQualifiedName~ShellLayoutTests"`
Expected: PASS (all tests in both classes)

- [ ] **Step 8: Build the whole solution to confirm no other compile breaks**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

- [ ] **Step 9: Commit**

```bash
git add src/StageFright.Core/Enums/PlatformThemePreference.cs src/StageFright.Core/Contracts/IDeviceThemePreferenceProvider.cs src/StageFright.UI/Layout/ThemeProvider.razor.cs tests/StageFright.UI.Tests/Layout/ThemeProviderTests.cs tests/StageFright.UI.Tests/Layout/ShellLayoutTests.cs
git commit -m "Add device theme preference abstraction; ThemeProvider falls back to OS/Dark (#248)"
```

---

### Task 2: `SetupRequest.Theme` + `SetupService` persists the requested theme

**Files:**
- Modify: `src/StageFright.Core/Modules/Settings/SetupRequest.cs`
- Modify: `src/StageFright.Core/Modules/Settings/SetupService.cs:67`
- Modify: `tests/StageFright.Core.Tests/Modules/Settings/SetupServiceTests.cs`
- Modify: `tests/StageFright.Integration.Tests/Scenarios/V1_FirstRunSetupTests.cs`
- Modify: `tests/StageFright.Integration.Tests/Scenarios/V10_ThemeTests.cs`

**Interfaces:**
- Consumes: `StageFright.Core.Enums.Theme` (existing).
- Produces: `SetupRequest` gains a 9th positional parameter `Theme Theme`. `SetupService.InitializeAsync` persists `request.Theme` directly (no more hardcoded default and no dependency on `IDeviceThemePreferenceProvider` — that abstraction is only consumed by `ThemeProvider`, per Task 1).

- [ ] **Step 1: Write the failing unit test for `SetupService` persisting the requested theme**

Add to `tests/StageFright.Core.Tests/Modules/Settings/SetupServiceTests.cs`, directly below `InitializeAsync_SavesSettings_WithCorrectValues`:

```csharp
    [Theory]
    [InlineData(Theme.Light)]
    [InlineData(Theme.Dark)]
    public async Task InitializeAsync_PersistsRequestedTheme(Theme requestedTheme)
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        var request = ValidRequest() with { Theme = requestedTheme };
        await svc.InitializeAsync(request, Ct);

        await _settingsRepo.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s.Theme == requestedTheme),
            Arg.Any<CancellationToken>());
    }
```

Update the `ValidRequest()` helper at the bottom of the same file to supply the new field:

```csharp
    private static SetupRequest ValidRequest() => new(
        OrganizationName: "Test Org",
        Abn: "51824753556",
        AnnualFee: 75m,
        AttendanceFee: 5m,
        MembershipRenewalMonth: 1,
        IsGstRegistered: false,
        AnnualFeeGstCode: null,
        AttendanceFeeGstCode: null,
        Theme: Theme.Dark);
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/StageFright.Core.Tests/ --filter "FullyQualifiedName~SetupServiceTests"`
Expected: FAIL — compile error, `SetupRequest` does not have a `Theme` parameter/property yet.

- [ ] **Step 3: Add `Theme` to `SetupRequest`**

Replace `src/StageFright.Core/Modules/Settings/SetupRequest.cs` with:

```csharp
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Settings;

/// <summary>Input data for the first-run setup wizard.</summary>
public record SetupRequest(
    string OrganizationName,
    string Abn,
    decimal AnnualFee,
    decimal AttendanceFee,
    int MembershipRenewalMonth,
    bool IsGstRegistered,
    GstCode? AnnualFeeGstCode,
    GstCode? AttendanceFeeGstCode,
    Theme Theme);
```

- [ ] **Step 4: Fix the two other call sites that construct `SetupRequest` positionally**

In `tests/StageFright.Integration.Tests/Scenarios/V1_FirstRunSetupTests.cs`, update both positional constructions:

```csharp
        var request = new SetupRequest("Springfield Choir", "51824753556", 75m, 5m, 9, false, null, null, Core.Enums.Theme.Dark);
```

```csharp
    private static SetupRequest ValidRequest() =>
        new("Test Organisation", "51824753556", 60m, 4m, 1, false, null, null, Core.Enums.Theme.Dark);
```

- [ ] **Step 5: Update `SetupService.InitializeAsync` to persist the requested theme**

In `src/StageFright.Core/Modules/Settings/SetupService.cs`, change line 67 from:

```csharp
            Theme = Theme.Light,
```

to:

```csharp
            Theme = request.Theme,
```

- [ ] **Step 6: Run the `SetupService` tests to verify they pass**

Run: `dotnet test tests/StageFright.Core.Tests/ --filter "FullyQualifiedName~SetupServiceTests"`
Expected: PASS (all tests, including the new `InitializeAsync_PersistsRequestedTheme` theory)

- [ ] **Step 7: Rewrite the stale `V10_ThemeTests` default-theme test to actually exercise `SetupService`**

The existing `DefaultTheme_IsLight_AfterFirstRunSetup` test never called `SetupService` — it just seeded a `Settings` row directly and read it back, which no longer reflects how the default is produced. In `tests/StageFright.Integration.Tests/Scenarios/V10_ThemeTests.cs`, replace that one test:

```csharp
    [Theory]
    [InlineData(Theme.Light)]
    [InlineData(Theme.Dark)]
    public async Task DefaultTheme_MatchesRequestedTheme_AfterFirstRunSetup(Theme requestedTheme)
    {
        var svc = BuildSetupService();
        var request = new SetupRequest("Test Choir", "51824753556", 60m, 5m, 1, false, null, null, requestedTheme);

        await svc.InitializeAsync(request);

        var settings = await new SettingsRepository(_db).GetAsync();
        Assert.Equal(requestedTheme, settings!.Theme);
    }
```

Add the `SetupService` builder helper alongside the existing `BuildSettingsService()` helper in the same file:

```csharp
    private SetupService BuildSetupService()
    {
        var settingsRepo = new SettingsRepository(_db);
        var accountRepo = new AccountRepository(_db);
        var eventTypeRepo = new EventTypeRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditService = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        return new SetupService(settingsRepo, accountRepo, eventTypeRepo, auditService);
    }
```

- [ ] **Step 8: Run the integration tests to verify they pass**

Run: `dotnet test tests/StageFright.Integration.Tests/ --filter "FullyQualifiedName~V10_ThemeTests|FullyQualifiedName~V1_FirstRunSetupTests"`
Expected: PASS (all tests in both scenario files)

- [ ] **Step 9: Build the whole solution**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

- [ ] **Step 10: Commit**

```bash
git add src/StageFright.Core/Modules/Settings/SetupRequest.cs src/StageFright.Core/Modules/Settings/SetupService.cs tests/StageFright.Core.Tests/Modules/Settings/SetupServiceTests.cs tests/StageFright.Integration.Tests/Scenarios/V1_FirstRunSetupTests.cs tests/StageFright.Integration.Tests/Scenarios/V10_ThemeTests.cs
git commit -m "SetupService persists the requested theme instead of a hardcoded default (#248)"
```

---

### Task 3: Theme toggle switch on the Setup Wizard

**Files:**
- Modify: `src/StageFright.UI/Pages/Setup/SetupWizard.razor`
- Modify: `src/StageFright.UI/Pages/Setup/SetupWizard.razor.cs`
- Create: `tests/StageFright.UI.Tests/Pages/Setup/SetupWizardThemeTests.cs`

**Interfaces:**
- Consumes: `StageFright.UI.Layout.ThemeProvider` (via `[CascadingParameter]`, same pattern as `GeneralSettingsTab` and `ShellLayout`), `ThemeProvider.CurrentTheme` (`Theme`), `ThemeProvider.ToggleAsync()`.
- Produces: `SetupWizard` now includes `Theme: ThemeProvider?.CurrentTheme ?? Theme.Dark` when building the `SetupRequest` it passes to `ISetupService.InitializeAsync`.

- [ ] **Step 1: Write the failing bUnit tests for the toggle switch**

Create `tests/StageFright.UI.Tests/Pages/Setup/SetupWizardThemeTests.cs`:

```csharp
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Layout;
using StageFright.UI.Pages.Setup;

namespace StageFright.UI.Tests.Pages.Setup;

/// <summary>
/// bUnit tests for the theme toggle switch on the Setup Wizard (issue #248): the switch
/// reflects and controls the cascaded ThemeProvider's current theme, and whatever theme
/// is selected when the wizard is submitted flows into the SetupRequest.
/// </summary>
public class SetupWizardThemeTests : BunitContext
{
    private const string ValidAbn = "51824753556";

    private readonly ISetupService _setupService = Substitute.For<ISetupService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IDeviceThemePreferenceProvider _deviceThemeProvider = Substitute.For<IDeviceThemePreferenceProvider>();

    public SetupWizardThemeTests()
    {
        Services.AddSingleton(_setupService);
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(_deviceThemeProvider);
        _setupService.InitializeAsync(Arg.Any<SetupRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _deviceThemeProvider.GetPreference().Returns(PlatformThemePreference.Dark);
    }

    private IRenderedComponent<ThemeProvider> RenderWizard() =>
        Render<ThemeProvider>(p => p.AddChildContent<SetupWizard>());

    private static void AdvanceToReview(IRenderedFragment cut)
    {
        cut.Find("#orgName").Change("My Choir");
        cut.Find("#abn").Change(ValidAbn);
        cut.Find("#btn-next").Click(); // -> step 2
        cut.Find("#btn-next").Click(); // -> step 3
        cut.Find("#btn-next").Click(); // -> step 4 (Review & Finish)
    }

    [Fact]
    public void ThemeToggle_Renders_OnSetupWizard()
    {
        var cut = RenderWizard();

        cut.Find(".setup-theme-toggle [role=switch]");
    }

    [Fact]
    public void ThemeToggle_DefaultsToDevicePreference()
    {
        var cut = RenderWizard();

        Assert.Contains("Dark", cut.Find(".setup-theme-toggle").TextContent);
    }

    [Fact]
    public void ThemeToggle_TogglingSwitch_ChangesDisplayedTheme()
    {
        var cut = RenderWizard();

        cut.Find(".setup-theme-toggle [role=switch]").Click();

        Assert.Contains("Light", cut.Find(".setup-theme-toggle").TextContent);
    }

    [Fact]
    public async Task Finish_IncludesDefaultTheme_InSetupRequest_WhenToggleUntouched()
    {
        var cut = RenderWizard();
        AdvanceToReview(cut);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r.Theme == Theme.Dark),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finish_IncludesToggledTheme_InSetupRequest_WhenUserSwitchesToLight()
    {
        var cut = RenderWizard();
        cut.Find(".setup-theme-toggle [role=switch]").Click(); // Dark -> Light
        AdvanceToReview(cut);

        await cut.Find("form").SubmitAsync();

        await _setupService.Received(1).InitializeAsync(
            Arg.Is<SetupRequest>(r => r.Theme == Theme.Light),
            Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~SetupWizardThemeTests"`
Expected: FAIL — `.setup-theme-toggle` not found (the switch doesn't exist yet).

- [ ] **Step 3: Add the toggle switch to `SetupWizard.razor`**

In `src/StageFright.UI/Pages/Setup/SetupWizard.razor`, insert the toggle between the progress bar and the error alert (i.e. immediately after the `</div>` that closes the `progress` bar on line 17, before the `@if (_errorMessage is not null)` block):

```razor
    <div class="setup-theme-toggle d-flex align-items-center gap-2 mb-3">
        <span class="small text-muted">Theme:</span>
        <RadzenSwitch Name="setupThemeToggleSwitch"
                      Value="@(ThemeProvider?.CurrentTheme == Theme.Dark)"
                      Change="@(async (bool _) => await HandleThemeToggleAsync())" />
        <span class="small">@(ThemeProvider?.CurrentTheme == Theme.Dark ? "Dark" : "Light")</span>
    </div>
```

- [ ] **Step 4: Wire up `SetupWizard.razor.cs`**

Replace `src/StageFright.UI/Pages/Setup/SetupWizard.razor.cs` with:

```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Layout;

namespace StageFright.UI.Pages.Setup;

public partial class SetupWizard : ComponentBase
{
    [Inject] private IServiceProvider ServiceProvider { get; set; } = null!;

    [CascadingParameter] private ThemeProvider? ThemeProvider { get; set; }

    private readonly SetupFormModel _model = new();
    private EditContext _editContext = null!;
    private IDebugDataSeeder? _debugSeeder;
    private int _currentStep = 1;
    private bool _submitting;
    private bool _seedingInProgress;
    private bool _seedWithTestData;
    private string? _errorMessage;
    private string? _seedingProgress;

    protected override void OnInitialized()
    {
        _editContext = new EditContext(_model);

        // IDebugDataSeeder is only registered in Debug builds (MauiProgram.cs) — there is
        // never a database seed in Release, so resolve it optionally rather than requiring
        // it via [Inject], and hide the "Load sample data" checkbox when it's unavailable.
        _debugSeeder = ServiceProvider.GetService(typeof(IDebugDataSeeder)) as IDebugDataSeeder;
    }

    private void HandleNext()
    {
        if (_editContext.Validate() && _currentStep < 4)
            _currentStep++;
    }

    private void HandleBack()
    {
        if (_currentStep > 1)
            _currentStep--;
    }

    private void HandleGstToggleChanged()
    {
        if (!_model.IsGstRegistered)
        {
            _model.AnnualFeeGstCode = null;
            _model.AttendanceFeeGstCode = null;
        }
    }

    private async Task HandleThemeToggleAsync()
    {
        if (ThemeProvider is not null)
            await ThemeProvider.ToggleAsync();
    }

    private async Task HandleValidSubmitAsync()
    {
        _submitting = true;
        _errorMessage = null;

        try
        {
            var request = new SetupRequest(
                OrganizationName: _model.OrganizationName!,
                Abn: _model.Abn!,
                AnnualFee: _model.AnnualFee,
                AttendanceFee: _model.AttendanceFee,
                MembershipRenewalMonth: _model.MembershipRenewalMonth,
                IsGstRegistered: _model.IsGstRegistered,
                AnnualFeeGstCode: _model.AnnualFeeGstCode,
                AttendanceFeeGstCode: _model.AttendanceFeeGstCode,
                Theme: ThemeProvider?.CurrentTheme ?? Theme.Dark);

            await SetupService.InitializeAsync(request);

            if (_seedWithTestData && _debugSeeder is not null)
            {
                _seedingInProgress = true;
                try
                {
                    var progress = new Progress<string>(msg =>
                    {
                        _seedingProgress = msg;
                        InvokeAsync(StateHasChanged);
                    });
                    await Task.Run(() => _debugSeeder.SeedAsync(progress));
                }
                finally
                {
                    _seedingInProgress = false;
                }
            }

            Nav.NavigateTo("/dashboard");
        }
        catch (Core.Exceptions.ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch
        {
            _errorMessage = "An unexpected error occurred. Please try again.";
        }
        finally
        {
            _submitting = false;
        }
    }
}
```

- [ ] **Step 5: Run the new tests to verify they pass**

Run: `dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~SetupWizardThemeTests"`
Expected: PASS (all 5 tests)

- [ ] **Step 6: Run the full `SetupWizard` test suite to confirm no regressions**

Run: `dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~SetupWizard"`
Expected: PASS — `SetupWizardTests`, `SetupWizardNoSeederTests`, and `SetupWizardThemeTests` all green. (`SetupWizardTests` and `SetupWizardNoSeederTests` render `SetupWizard` without a `ThemeProvider` ancestor, so `ThemeProvider` stays null there and `Theme` falls back to `Theme.Dark` in the submitted request — none of their existing assertions check `SetupRequest.Theme`, so they're unaffected.)

- [ ] **Step 7: Build the whole solution**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

- [ ] **Step 8: Commit**

```bash
git add src/StageFright.UI/Pages/Setup/SetupWizard.razor src/StageFright.UI/Pages/Setup/SetupWizard.razor.cs tests/StageFright.UI.Tests/Pages/Setup/SetupWizardThemeTests.cs
git commit -m "Add theme toggle switch to the Setup Wizard (#248)"
```

---

### Task 4: MAUI implementation of `IDeviceThemePreferenceProvider`

**Files:**
- Create: `src/StageFright.App/MauiDeviceThemePreferenceProvider.cs`
- Modify: `src/StageFright.App/MauiProgram.cs:151` (inside `RegisterCoreServices`)

**Interfaces:**
- Consumes: `StageFright.Core.Contracts.IDeviceThemePreferenceProvider`, `StageFright.Core.Enums.PlatformThemePreference` (from Task 1); MAUI's `Application.Current.RequestedTheme` (`Microsoft.Maui.ApplicationModel.AppTheme`, available via MAUI's implicit global usings — see `App.xaml.cs`, which uses `Application`/`Window`/`DeviceDisplay` with no explicit `using` statements).
- Produces: `MauiDeviceThemePreferenceProvider`, registered as a DI singleton for `IDeviceThemePreferenceProvider`.

This class is a one-line MAUI API passthrough in `StageFright.App`, a project with no test coverage today (composition-root only, per CLAUDE.md) — no test is written for it, consistent with the rest of that project (e.g. `MauiProgram.cs`, `App.xaml.cs`). The branching logic it feeds (`Unspecified` → `Dark`, etc.) is already covered by `ThemeProviderTests` in Task 1.

- [ ] **Step 1: Create `MauiDeviceThemePreferenceProvider`**

```csharp
using StageFright.Core.Contracts;
using StageFright.Core.Enums;

namespace StageFright.App;

/// <summary>
/// Reads the OS/device's light-or-dark preference via MAUI's Application.Current.RequestedTheme.
/// </summary>
public class MauiDeviceThemePreferenceProvider : IDeviceThemePreferenceProvider
{
    public PlatformThemePreference GetPreference() => Application.Current?.RequestedTheme switch
    {
        AppTheme.Light => PlatformThemePreference.Light,
        AppTheme.Dark => PlatformThemePreference.Dark,
        _ => PlatformThemePreference.Unspecified
    };
}
```

- [ ] **Step 2: Register it in `MauiProgram.RegisterCoreServices`**

In `src/StageFright.App/MauiProgram.cs`, inside `RegisterCoreServices` (around line 151-159, right after `services.AddScoped<ISetupService, SetupService>();`), add:

```csharp
        services.AddSingleton<IDeviceThemePreferenceProvider, MauiDeviceThemePreferenceProvider>();
```

So that section reads:

```csharp
    private static void RegisterCoreServices(IServiceCollection services)
    {
        services.AddScoped<IAuditTrailService, AuditTrailService>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddSingleton<IDeviceThemePreferenceProvider, MauiDeviceThemePreferenceProvider>();
        services.AddScoped<AccountNumberAssignmentService>();

        // Settings service
        services.AddScoped<ISettingsService, SettingsService>();
```

- [ ] **Step 3: Build the whole solution (this project has no dedicated test project)**

Run: `dotnet build`
Expected: Build succeeded, 0 errors — confirms `Application.Current.RequestedTheme`/`AppTheme` resolve correctly under MAUI's implicit global usings and the DI registration compiles.

- [ ] **Step 4: Commit**

```bash
git add src/StageFright.App/MauiDeviceThemePreferenceProvider.cs src/StageFright.App/MauiProgram.cs
git commit -m "Wire up MAUI OS theme detection for first-run dark-mode default (#248)"
```

---

### Task 5: Full verification and issue closure

**Files:** None (verification-only task).

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test`
Expected: All test projects pass — `StageFright.Core.Tests`, `StageFright.Data.Tests`, `StageFright.UI.Tests`, `StageFright.Integration.Tests`, `StageFright.Reports.Tests`. Zero failures.

- [ ] **Step 2: Run a full solution build**

Run: `dotnet build`
Expected: Build succeeded, 0 errors, 0 new warnings introduced by this change.

- [ ] **Step 3: Manually confirm no other Light-default assumptions were missed**

Run: `grep -rn "Theme.Light" src/ tests/` (or the PowerShell equivalent `Select-String`) and review each hit — confirm every remaining occurrence is either (a) a legitimate "Light" branch (e.g. `Theme.Light => "light"` mappings, GeneralSettingsTab's toggle display), not a leftover *default*, or (b) a test that explicitly seeds/asserts Light as one of two theory cases, not an assumed default. Fix inline if anything is found; otherwise proceed.

- [ ] **Step 4: If everything is green, close GitHub issue #248**

Run:
```bash
gh issue close 248 --comment "Implemented: first-run default now follows the OS/device theme preference (falling back to Dark), the Setup Wizard has its own theme toggle switch, and the chosen theme continues to persist across sessions via the existing Settings.Theme round-trip. Full build and test suite green."
```

Expected: Issue #248 transitions to Closed, with the comment recorded.
