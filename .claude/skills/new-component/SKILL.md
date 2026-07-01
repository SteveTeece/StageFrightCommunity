---
name: new-component
description: Scaffold a paired .razor/.razor.cs Blazor component (StageFright.UI) that follows CLAUDE.md's mandatory code-behind rule. Use when adding a new page, shared component, or dialog to StageFright.UI.
---

# new-component

Scaffolds a new Blazor component as a **paired** `.razor` + `.razor.cs` file (and, only if the
component genuinely needs scoped styles, a `.razor.css`). CLAUDE.md prohibits `@code { }` blocks
inside `.razor` files — all C# logic belongs in the code-behind file. A `razor-codebehind-check`
PostToolUse hook enforces this automatically, but scaffolding it correctly the first time avoids
the round-trip.

## Usage

`/new-component <Name> <target-folder> [--page /route]`

Examples:
- `/new-component RehearsalCard Shared` → shared component, no route
- `/new-component EventCheckIn Pages/Events --page /events/check-in` → routable page

## Steps

1. **Determine the target directory and namespace.** Components live under
   `src/StageFright.UI/<target-folder>/`. The namespace is `StageFright.UI.<target-folder>` with
   `/` replaced by `.` (e.g. `Pages/Events` → `StageFright.UI.Pages.Events`). Look at an existing
   sibling component in the same folder (e.g. `Shared/ReactivationForgivenessDialog.razor` or
   `Pages/Members/MemberList.razor`) to match its `@using` imports and markup conventions before
   writing the new files.

2. **Create `<Name>.razor`.** No `@code` block. If it's a page, start with `@page "<route>"` and a
   `<PageTitle>`. Bind events with `@onclick="MethodName"` etc., calling methods that live in the
   code-behind partial class.

3. **Create `<Name>.razor.cs`** in the same directory:

   ```csharp
   using Microsoft.AspNetCore.Components;

   namespace StageFright.UI.<Namespace>;

   public partial class <Name> : ComponentBase
   {
       // [Parameter], [Inject], private fields, and lifecycle/event-handler methods go here.
   }
   ```

   Inject services via `[Inject] private I<Thing>Service ThingService { get; set; } = null!;`,
   matching the constructor-less DI pattern already used throughout `StageFright.UI`.

4. **Only add a `.razor.css`** if the component needs styles that are genuinely scoped to it —
   most styling belongs in `wwwroot/css/` per CLAUDE.md. Don't create one by default.

5. **Wire it up:**
   - If it's a page, no further registration is needed — the Blazor router picks up `@page`
     directives automatically.
   - If it's a dashboard tile, settings tab, or menu item, register it via the relevant
     `StageFright.Plugins.Contracts` interface (`IDashboardTileProvider`, `ISettingsTabProvider`,
     `IMenuItemProvider`) and add the provider to `MauiProgram.RegisterCoreServices`.

6. **Add tests.** Per CLAUDE.md's exhaustive-coverage rule, add a bUnit test in
   `tests/StageFright.UI.Tests/` named `Should_[ExpectedBehavior]_When_[Condition]` covering the
   component's render states (loading, empty, populated, error) and interactions.

7. Run `dotnet build` on `StageFright.UI` (the build-on-edit hook does this automatically after
   each edit) and `dotnet test tests/StageFright.UI.Tests/` before considering the component done.
