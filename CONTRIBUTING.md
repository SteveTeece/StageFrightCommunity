# Contributing to StageFright Community

Thank you for your interest in contributing! This guide explains how to develop features, maintain code quality, and follow our architectural standards. It stays in sync with the [Constitution](.specify/memory/constitution.md) and [Architecture Guide](docs/ARCHITECTURE.md) — when in doubt about a detail this file simplifies, those two are authoritative.

## Development Workflow

### 1. Feature Creation with Specifications

Every non-trivial feature starts with a specification. We use **Spec Kit** for structured feature development:

```bash
# Create a new feature specification
/speckit.specify My feature description here
```

This creates:
- Feature branch (e.g., `018-feature-name`)
- Specification directory under `specs/`
- Template files for planning and task generation

### 2. Feature Branch Naming

Branches follow the pattern: `NNN-descriptive-name`, incrementing from the highest existing spec number under `specs/`.

Examples: `016-generic-sales-tax`, `017-setup-wizard-tabs`.

### 3. Specification, Planning, and Implementation

For each feature:

```bash
# Draft the feature specification
/speckit.specify <feature description>

# Optional: Clarify ambiguous requirements
/speckit.clarify

# Generate implementation plan
/speckit.plan

# Generate task list
/speckit.tasks

# Implement tasks and create pull request
# When complete: /speckit.implement or manual implementation
```

All commands generate artifacts in `specs/<NNN-feature-name>/` (`spec.md`, `plan.md`, `tasks.md`, plus `data-model.md`/`contracts/`/`research.md` as needed). **When a code change touches behavior a spec doc describes, update that doc in the same task** — including small, presentation-only tweaks.

## Solution Structure (Layered, with Module Slices in Core)

This is **not** a per-module `Domain/Application/Infrastructure/UI` vertical slice. It's a layered solution — one project per architectural layer — with each business capability organized as a folder *inside* `StageFright.Core`. See [ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full picture; the essentials:

```
src/
├── StageFright.App/                    # MAUI Blazor Hybrid host — composition root only
│   └── MauiProgram.cs                  # ALL DI registration + startup sequence
├── StageFright.Core/
│   ├── Entities/                       # Domain entities (Guid PKs)
│   ├── Enums/                          # Shared enums
│   ├── Exceptions/                     # Custom exception hierarchy
│   ├── Contracts/                      # I<Service>/I<Entity>Repository interfaces
│   └── Modules/<ModuleName>/           # This module's services + request/response DTOs
│                                       # + its IMenuItemProvider
├── StageFright.Data/
│   └── Repositories/                   # ALL repositories, one per entity — centralized,
│                                       # NOT module-owned (see below)
├── StageFright.Plugins.Contracts/      # Extension-point interfaces, no dependencies
├── StageFright.Reports/                # Report pipeline (Providers/, Registry/, Rendering/)
└── StageFright.UI/
    ├── Pages/<ModuleName>/             # Paired .razor/.razor.cs pages
    ├── Modules/<ModuleName>/           # This module's IDashboardTileProvider
    ├── Shared/                         # BorderedListBox, ReportViewer, etc.
    └── Layout/                         # ShellLayout (sidebar nav), ThemeProvider
```

Current modules: `Agm`, `AuditTrail`, `Dashboard`, `Events`, `Finance`, `Members`, `Rehearsals`, `Settings`.

### Key Rules

1. **Repositories Are Centralized, Not Module-Owned**: every repository lives in `StageFright.Data/Repositories/`, implementing an interface from `StageFright.Core/Contracts/` — this is a deliberate, permanent deviation from pure vertical-slice ownership (keeps one `DbContext`/migration history for the shared SQLite database), not something to "fix" by moving repositories into `Modules/`.
2. **Dashboard-Tile Providers Live in `StageFright.UI`, Not Core**: a tile provider needs a Blazor component `Type` reference, which `StageFright.Core` intentionally can't have.
3. **No Cross-Module Imports**: a module injects another module's published interface from `StageFright.Core/Contracts/` — never its concrete service class, and never a `StageFright.Data` repository directly from UI code.
4. **No MediaTr or CQRS**: use direct service injection and explicit request/response models.
5. **Explicit DI Registration**: every service, repository, and core provider is registered by hand in `MauiProgram.cs` (`services.AddScoped<IX, X>()` per type) — there is no assembly-scanning/auto-discovery for in-solution types. Only plugin assemblies discovered from the `Plugins/` directory at runtime are registered reflectively (see [ARCHITECTURE.md § Plugin Discovery & Loading](docs/ARCHITECTURE.md#plugin-discovery--loading)).
6. **Dashboard Tiles**: modules that expose dashboard-visible functionality provide at least one `IDashboardTileProvider`.

### File Organization - Single Responsibility (MANDATORY)

**Every class, interface, record, struct, or enum MUST be in its own dedicated file.** This is a non-negotiable requirement enforced at code review and CI/CD.

**Rules**:
- ✅ One class per file maximum
- ✅ File name must exactly match the class/interface name (e.g., `MemberService.cs` for `class MemberService`)
- ✅ Each responsibility level gets separate files:
  - A service and its interface are in separate files (interface in `Contracts/`, implementation in `Modules/<Name>/`)
  - DTOs separate from domain entities
  - Request/response objects in dedicated files
  - Enums in their own files

**Exception**: Private nested types that serve a single purpose within their parent class may remain inline.

**Code Review**: PRs with multiple classes in a single file will be **rejected**. This is a blocking requirement.

**Example Structure**:
```
StageFright.Core/
├── Entities/Member.cs                  # only the Member entity
├── Enums/MemberStatus.cs               # only the MemberStatus enum
├── Contracts/IMemberService.cs         # only the IMemberService interface
├── Contracts/IMemberRepository.cs      # only the IMemberRepository interface
└── Modules/Members/
    ├── MemberService.cs                # only the MemberService class
    ├── CreateMemberRequest.cs          # only the CreateMemberRequest DTO
    └── MemberMenuItemProvider.cs       # only the menu-item provider

StageFright.Data/Repositories/
└── MemberRepository.cs                 # only the MemberRepository class
```

This enforces SOLID principles and keeps files focused and maintainable.

## Dashboard Tiles

Each module that exposes dashboard-visible functionality does so through `IDashboardTileProvider` (`StageFright.Plugins.Contracts`), implemented in `StageFright.UI/Modules/<ModuleName>/` (see Constitution §4.2):

```csharp
// StageFright.UI/Modules/Members/MembersDashboardTileProvider.cs
public class MembersDashboardTileProvider : IDashboardTileProvider
{
    public string TileId => "members-overview";
    public string Title => "Members";
    public string ModuleName => "Members";
    public int DisplayOrder => 1;
    public Type TileComponentType => typeof(MembersTile);
    public string? NavigateRoute => "/members";
    public string? ActionText => "View Members";

    // Optional — defaults to DashboardTileSize.OneByOne
    public DashboardTileSize TileSize => DashboardTileSize.OneByOne;

    public async Task<TileData> GetTileDataAsync(CancellationToken ct)
    {
        // Load and shape the data the tile component needs
    }
}
```

**Tile Sizing**: opt into `OneByOne` (default, 1×1), `OneByTwo` (2 cols × 1 row), `TwoByOne` (1 col × 2 rows), or `TwoByTwo` (2×2) via `TileSize`. The dashboard's CSS grid maps each to a `tile-size-1x1`/`1x2`/`2x1`/`2x2` class — no grid CSS changes needed to resize a tile.

**Failure Isolation**: tiles load in parallel; a throwing provider must render "Unable to load" without blocking the other tiles.

See [UI Component Style Guide § Dashboard Tiles](docs/UI_COMPONENT_STYLE_GUIDE.md#dashboard-tiles) for design patterns.

## Settings Tabs

The core application's own tabs (General, Tax, Committee, Event Types, Backup & Restore) are **hardcoded directly in `SettingsPage.razor`** — they are not contributed via `ISettingsTabProvider`. That interface exists solely for **plugin-contributed** tabs, rendered after the built-in ones; `SettingsPage.razor` resolves `IEnumerable<ISettingsTabProvider>` separately and skips a duplicate `TabKey` with a logged warning (see Constitution §4.3).

### Plugin Settings Tab Implementation

```csharp
// Real ISettingsTabProvider shape — no GetSettingsAsync/ValidateAsync/SaveAsync on the
// interface itself. Persistence and validation live entirely inside the tab's own
// Blazor component (SettingsComponentType).
public class MyPluginSettingsTabProvider : ISettingsTabProvider
{
    public string TabTitle => "My Plugin";
    public string TabIcon => "puzzle";
    public string TabKey => "my-plugin";       // deep-link: /settings?tab=my-plugin
    public int DisplayOrder => 100;             // plugin tabs: 100+; core tabs: 0–99
    public Type SettingsComponentType => typeof(MyPluginSettingsTab);
}

// MyPluginSettingsTab.razor.cs — owns its own load/validate/save, e.g. via an injected
// plugin-specific settings service. Follow the paired .razor/.razor.cs rule (see below).
```

### Core Application Settings (Not Module- or Plugin-Provided)

Part of the built-in General tab (Constitution §4.3):

- **Organization Name** — Name of the group/organization
- **Annual Membership Fee** — Annual fee amount
- **Rehearsal/Event Fee** — Per-rehearsal or per-event fee
- **Membership Renewal Due Date** — Month and day (e.g., "September 1")

## Navigation Menu Items

Modules that need navigation define an `IMenuItemProvider` in their own `StageFright.Core/Modules/<ModuleName>/` folder (Constitution §4.6). The shell renders these as a **fixed vertical sidebar** — not a top nav bar — with Settings always last.

### Menu Item Implementation (the real contract — no `IsActive` field)

```csharp
// StageFright.Core/Modules/Members/MemberMenuItemProvider.cs
public class MemberMenuItemProvider : IMenuItemProvider
{
    public string ModuleName => "Members";
    public int DisplayOrder => 1;

    public IReadOnlyList<MenuItem> GetMenuItems()
    {
        return new List<MenuItem>
        {
            new()
            {
                Title = "Members",
                Route = "/members",
                Icon = "users",
                DisplayOrder = 1
            }
        };
    }
}
```

`MenuItem`'s real properties: `Title`, `Route`, `Icon`, `ShortLabel`, `DisplayOrder`, `SubItems`, `BadgeText`. There is **no `IsActive` field** — the shell computes active-state from the current route at render time, it isn't stored on the model.

### Menu Item Registration

Register explicitly in `MauiProgram.RegisterCoreServices` — one line per provider, no scanning:

```csharp
services.AddSingleton<IMenuItemProvider, MemberMenuItemProvider>();
```

### Important Rules

- **Settings Always Last**: never add menu items after Settings
- **Module Order**: use `DisplayOrder` to control module placement relative to other modules
- **Icon Guidelines**: sidebar icons are Bootstrap Icons inlined as CSS masks (see [UI_COMPONENT_STYLE_GUIDE.md](docs/UI_COMPONENT_STYLE_GUIDE.md#icons))
- **No Hardcoding**: compute badge text dynamically (e.g., pending count)
- **Route Prefixes**: each module owns its route prefix (e.g., `/members/*`, `/events/*`)
- **Sub-menu Depth**: 2 levels maximum (menu item → sub-items)

## Testing Requirements

**Every reachable code path MUST be tested before merge.**

This includes:
- ✅ Success paths
- ✅ Validation failures
- ✅ Exception/error handling
- ✅ Boundary conditions (null, empty, min/max)
- ✅ State transitions
- ✅ All UI user journeys

Test frameworks: **xUnit v3**, **bUnit** for Blazor components, **NSubstitute** for mocking — there is no Moq dependency in the solution; use `Substitute.For<T>()`, not `Mock<T>`.

### Test Organization

```
tests/
├── StageFright.Core.Tests/         # Unit tests — services, domain logic
├── StageFright.Data.Tests/         # Integration tests — real SQLite connection
├── StageFright.UI.Tests/           # bUnit component tests
├── StageFright.Integration.Tests/  # Cross-layer user-journey tests
├── StageFright.Reports.Tests/      # Report provider + PDF/CSV renderer tests
└── StageFright.TestPlugin/         # Sample plugin fixture for the discovery pipeline
```

### Test Naming Convention

```csharp
// Unit/integration tests
[Fact]
public void Should_[ExpectedBehavior]_When_[Condition]() { }

// Integration tests specifically
[Fact]
public async Task Should_[ExpectedBehavior]_When_[Condition]_Integration() { }
```

### Writing Tests

**Tests must be deterministic** — no timing dependencies, use explicit waits.

Example unit test (NSubstitute, not Moq):
```csharp
[Fact]
public void Should_ThrowValidationException_When_EmailIsInvalid()
{
    // Arrange
    var repository = Substitute.For<IMemberRepository>();
    var service = new MemberService(repository /*, other deps */);
    var request = new CreateMemberRequest { Email = "invalid-email" };

    // Act & Assert
    Assert.Throws<ValidationException>(() => service.Create(request));
}
```

Example bUnit component test:
```csharp
[Fact]
public void Should_DisplayMembersList_When_ComponentRendered()
{
    var memberService = Substitute.For<IMemberService>();
    memberService.GetActiveMembersAsync(Arg.Any<CancellationToken>())
        .Returns(new List<Member> { new() { FirstName = "John", LastName = "Doe" } });

    using var ctx = new TestContext();
    ctx.Services.AddSingleton(memberService);
    var cut = ctx.RenderComponent<MemberList>();

    Assert.Contains("John", cut.Markup);
}
```

## UI Component Development

### Design Principles

All UI must follow the **UI Component Style Guide** ([docs/UI_COMPONENT_STYLE_GUIDE.md](docs/UI_COMPONENT_STYLE_GUIDE.md)) — the "Midnight Glass" design system:

- **Clean & Simple** — minimal visual clutter, tokens over hardcoded colors
- **Compact Layouts** — Bootstrap spacing utility classes, not bespoke CSS
- **Consistent Components** — `RadzenDataGrid` for tables, `BorderedListBox` for bordered lists, `RadzenSwitch` for toggles — never a hand-rolled equivalent
- **Accessible** — keyboard and screen-reader support

### Blazor Component Structure

Every component is a paired `.razor`/`.razor.cs` file — `@code { }` blocks in `.razor` are prohibited:

```razor
@* Pages/Members/MemberList.razor *@
@page "/members"

<div class="d-flex align-items-center justify-content-between mb-2">
    <h1 class="h3 mb-0">Members</h1>
    <button class="btn btn-primary btn-sm" @onclick="AddMember">Add Member</button>
</div>

@if (_loading)
{
    <p role="status" aria-live="polite">Loading members…</p>
}
else
{
    <RadzenDataGrid Data="@_members" TItem="Member" AllowSorting="true" AllowPaging="true"
                     PageSize="15" class="rz-shadow-0">
        <Columns>
            <RadzenDataGridColumn TItem="Member" Property="SortableFullName" Title="Name" />
        </Columns>
    </RadzenDataGrid>
}
```

```csharp
// Pages/Members/MemberList.razor.cs
public partial class MemberList
{
    [Inject] private IMemberService MemberService { get; set; } = default!;
    [Inject] private ILogger<MemberList> Logger { get; set; } = default!;

    private List<Member> _members = new();
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _members = (await MemberService.GetActiveMembersAsync(CancellationToken.None)).ToList();
        }
        catch (DataAccessException ex)
        {
            Logger.LogError(ex, "Failed to load members");
        }
        finally
        {
            _loading = false;
        }
    }

    private void AddMember() { /* ... */ }
}
```

### CSS & Styling

- Global stylesheet (`StageFright.App/wwwroot/app.css`) is the default home for CSS — layout, typography, `--sf-*` design tokens, Bootstrap overrides, and anything shared across two or more components
- `.razor.css` CSS isolation is added only when a component needs styles genuinely scoped to it
- Never hardcode a color — reference an `--sf-*` token

## Custom Exceptions

All exceptions crossing architectural boundaries must be one of the project-defined custom exceptions in `StageFright.Core/Exceptions/`:

```
ConcurrencyException, DataAccessException, DataIntegrityException, DuplicateEntityException,
EntityNotFoundException, GLBalanceException, ImportException, PluginLoadException,
ReconciliationException, ValidationException
```

Every one of them shares a single constructor shape:

```csharp
public sealed class DataAccessException : Exception
{
    public string EntityType { get; }
    public Guid? EntityId { get; }
    public string OperationContext { get; }
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public Guid CorrelationId { get; } = Guid.NewGuid();

    public DataAccessException(string message, string entityType, string operationContext,
        Guid? entityId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EntityType = entityType;
        EntityId = entityId;
        OperationContext = operationContext;
    }
}
```

Don't invent a new exception type with a different constructor shape — add a case to an existing type's `OperationContext`/message instead, or propose a new type that follows the same shape.

### Boundary Translation

**Translate raw framework exceptions to custom exceptions at architectural boundaries:**

```csharp
// ✅ GOOD: Translation at repository boundary
public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
{
    try
    {
        var entry = await _db.Set<TEntity>().AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        return entry.Entity;
    }
    catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
    {
        throw new DuplicateEntityException($"A {typeof(TEntity).Name} with these values already exists.",
            typeof(TEntity).Name, nameof(AddAsync), null, ex);
    }
    catch (Exception ex) when (ex is not DuplicateEntityException and not DataAccessException)
    {
        throw new DataAccessException(ex.Message, typeof(TEntity).Name, nameof(AddAsync), null, ex);
    }
}

// ❌ BAD: Raw exception leaks to Application layer
public async Task<Member?> GetByIdAsync(Guid id)
{
    return await _db.Members.FindAsync(id);  // any raw EF exception leaks unwrapped
}
```

## Soft Delete & Data Preservation

### Soft Delete Rules

**All application data must use soft delete** (except financial records, which are immutable) — real fields, matching `Member`:

```csharp
public class Member
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    // Soft-delete fields
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
```

### Financial Data Immutability

**`Fee`, `Payment`, and `Transaction` NEVER soft-delete or hard-delete** — they carry no soft-delete fields at all. To correct an error, create a reversing GL transaction pair; never edit or delete the original record.

## Logging & Observability

### Structured Logging with Serilog

```csharp
public async Task<Member> CreateMemberAsync(CreateMemberRequest request, CancellationToken ct)
{
    try
    {
        _logger.LogInformation("Creating new member: {MemberName}", request.FirstName + " " + request.LastName);

        var member = new Member { FirstName = request.FirstName, LastName = request.LastName };
        var created = await _repository.AddAsync(member, ct);

        _logger.LogInformation("Member created successfully: {MemberId}", created.Id);
        return created;
    }
    catch (ValidationException ex)
    {
        _logger.LogWarning(ex, "Validation failed for member creation: {Reason}", ex.Message);
        throw;
    }
}
```

### Never Log Sensitive Data

❌ **Don't log:**
- Passwords or tokens
- Payment card information
- Personal identification numbers

✅ **Do log:**
- Operation context (what, when, who)
- Error classifications
- Correlation IDs for tracing

## Code Review Checklist

Before submitting a pull request, verify:

- [ ] `dotnet build` and the full `dotnet test` suite are green (without `--no-build`)
- [ ] Code-path coverage complete (success, failure, exceptions, boundaries)
- [ ] Custom exceptions used at boundaries (one of the real ten types, correct constructor shape)
- [ ] Soft-delete implemented where required (not on `Fee`/`Payment`/`Transaction`/`JournalEntry`/`AuditTrailEntry`/`ReconciliationLine`/`CommitteeTerm`)
- [ ] No raw framework exceptions leak across layers
- [ ] UI follows the [UI Component Style Guide](docs/UI_COMPONENT_STYLE_GUIDE.md) (RadzenDataGrid/BorderedListBox/RadzenSwitch, design tokens)
- [ ] File organization follows §4.1/§4.5 of the [Constitution](.specify/memory/constitution.md) (module folder in Core, centralized repository in Data)
- [ ] No hardcoded values or magic strings
- [ ] Logging implemented for observability
- [ ] Comments explain WHY, not WHAT
- [ ] Any touched `specs/<feature>/` doc updated alongside the code change
- [ ] Commit messages are clear and descriptive

## Pull Request Process

> **Target branch**: All pull requests must be opened against `dev`, not `master`. PRs targeting `master` will be rejected.

1. **Branch**: Create feature branch from `dev`
2. **Implement**: Follow the layered/module-slice pattern (§4.1 of the Constitution), write tests for every reachable path
3. **Commit**: Use clear, descriptive commit messages
   ```
   feat(members): Add soft-delete support for members

   - Add IsDeleted, DeletedAt, DeletedBy fields to Member entity
   - Filter inactive members from queries by default
   - Add unit and integration tests for soft-delete behavior
   ```
4. **Push**: Push to origin
5. **PR**: Create pull request with:
   - Reference to feature specification (if applicable)
   - Description of changes
   - Test coverage summary
   - Screenshots for UI changes
6. **Review**: Address review feedback
7. **Merge**: Squash merge to `dev`

## Frequently Asked Questions

### Q: How do I create a new module?

See [ARCHITECTURE.md § Adding a New Module](docs/ARCHITECTURE.md#adding-a-new-module) for the concrete file-by-file walkthrough — in short: entity (Core/Entities) → contract (Core/Contracts) → repository (Data/Repositories) → migration → module service + menu provider (Core/Modules/<Name>) → UI pages + dashboard tile (UI/Pages, UI/Modules/<Name>) → explicit DI registration in `MauiProgram.cs` → tests in each matching test project.

### Q: What if my feature needs to communicate with another module?

Inject the other module's published interface from `StageFright.Core/Contracts/`, not its concrete service class or its repository:

```csharp
// ✅ GOOD: inject the published interface
public class EventService
{
    public EventService(IMemberRepository memberRepository) { }
}

// ❌ BAD: import a concrete type from another module or reach into Data directly
public class EventService
{
    public EventService(MemberService memberService) { }   // concrete cross-module import
}
```

### Q: How do I test async operations?

Use `xUnit` and `Task`-based tests, passing `TestContext.Current.CancellationToken` rather than `CancellationToken.None`/`default`:

```csharp
[Fact]
public async Task Should_LoadMembersAsync_When_Called()
{
    var repository = Substitute.For<IMemberRepository>();
    var service = new MemberService(repository);

    var result = await service.GetActiveMembersAsync(TestContext.Current.CancellationToken);

    Assert.NotEmpty(result);
}
```

### Q: What's the difference between Unit and Integration tests?

- **Unit** (`StageFright.Core.Tests`): a single service in isolation, dependencies mocked with NSubstitute
- **Integration** (`StageFright.Data.Tests`, `_Integration` suffix): a real `StageFrightDbContext` against a real SQLite connection — not EF Core's `UseInMemoryDatabase` provider, so SQLite-specific behavior (transactions, unique constraints) is exercised for real

### Q: How do I handle errors in Blazor components?

```razor
@if (!string.IsNullOrEmpty(_errorMessage))
{
    <div class="alert alert-danger">@_errorMessage</div>
}
```

```csharp
protected override async Task OnInitializedAsync()
{
    try
    {
        _data = await Service.LoadDataAsync(CancellationToken.None);
    }
    catch (ValidationException ex)
    {
        _errorMessage = $"Invalid input: {ex.Message}";
    }
    catch (EntityNotFoundException)
    {
        _errorMessage = "Item not found";
    }
    catch (DataAccessException ex)
    {
        Logger.LogError(ex, "Unexpected error loading data");
        _errorMessage = "An unexpected error occurred. Please try again.";
    }
}
```

## Resources

- [Constitution](.specify/memory/constitution.md) — Governance framework
- [Architecture Guide](docs/ARCHITECTURE.md) — Detailed architecture patterns
- [Setup Guide](docs/SETUP.md) — Developer environment setup
- [UI Component Style Guide](docs/UI_COMPONENT_STYLE_GUIDE.md) — Design standards
- [XML Documentation Standards](docs/XML-DOCUMENTATION-STANDARDS.md) — `///` comment requirements
- [.NET MAUI Documentation](https://learn.microsoft.com/en-us/dotnet/maui/)
- [Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/)

## Questions?

- Check existing issues and discussions
- Review specification and plan artifacts under `specs/` for design decisions
- Ask questions in GitHub Discussions
