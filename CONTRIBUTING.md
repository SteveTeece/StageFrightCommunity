# Contributing to StageFright Community

Thank you for your interest in contributing! This guide explains how to develop features, maintain code quality, and follow our architectural standards.

## Development Workflow

### 1. Feature Creation with Specifications

Every feature starts with a specification. We use **Spec Kit** for structured feature development:

```bash
# Create a new feature specification
/speckit.specify My feature description here
```

This creates:
- Feature branch (e.g., `003-feature-name`)
- Specification directory under `specs/`
- Template files for planning and task generation

### 2. Feature Branch Naming

Branches follow the pattern: `NNN-descriptive-name` or `YYYYMMDD-HHMMSS-descriptive-name`

Examples:
- `001-user-authentication`
- `002-financial-tracking`
- `003-event-scheduling`

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

All commands generate artifacts in `specs/[NNN-feature-name]/`.

## Module Structure (Vertical Slice)

Each feature is organized as a **vertical slice module** following this pattern:

```
src/Features/[ModuleName]/
├── Domain/
│   ├── Entities/
│   │   ├── [Entity].cs
│   │   └── ...
│   ├── ValueObjects/
│   ├── Events/
│   └── [DomainContracts].cs
│
├── Application/
│   ├── Services/
│   │   └── [ServiceName].cs
│   ├── Handlers/
│   ├── DTOs/
│   └── Interfaces/
│
├── Infrastructure/
│   ├── Repositories/
│   │   └── [Entity]Repository.cs
│   ├── DataAccess/
│   └── ExternalServices/
│
├── UI/
│   ├── Pages/
│   │   └── [PageName].razor
│   ├── Components/
│   │   └── [ComponentName].razor
│   └── Models/
│
├── Tests/
│   ├── Unit/
│   ├── Integration/
│   └── UI/
│
├── DashboardTile.cs           # Dashboard tile provider
└── README.md                  # Module documentation
```

### Key Rules

1. **No Cross-Module Imports**: Import only from `Domain/` folders (published interfaces)
2. **No MediaTr or CQRS**: Use direct service injection
3. **Standard Repository Pattern**: Repositories in Infrastructure layer
4. **Service Injection**: Use MAUI DI container for dependency resolution
5. **Dashboard Tiles**: Each module provides at least one dashboard tile

### File Organization - Single Responsibility (MANDATORY)

**Every class, interface, record, struct, or enum MUST be in its own dedicated file.** This is a non-negotiable requirement enforced at code review and CI/CD.

**Rules**:
- ✅ One class per file maximum
- ✅ File name must exactly match the class/interface name (e.g., `MemberService.cs` for `class MemberService`)
- ✅ Each responsibility level gets separate files:
  - Services and their interfaces in separate files
  - DTOs separate from domain entities
  - Request/response objects in dedicated files
  - Enums and value objects in their own files
  
**Exception**: Private nested types that serve a single purpose within their parent class may remain inline.

**Code Review**: PRs with multiple classes in a single file will be **rejected**. This is a blocking requirement.

**Example Structure**:
```
Domain/
├── Member.cs           # only the Member entity
├── MemberStatus.cs     # only the MemberStatus enum
├── MemberEmail.cs      # only the MemberEmail value object
└── IMemberRepository.cs # only the IMemberRepository interface

Application/
├── IMemberService.cs   # only the IMemberService interface
├── MemberService.cs    # only the MemberService class
├── CreateMemberRequest.cs  # only the CreateMemberRequest DTO
└── MemberDto.cs        # only the MemberDto

Infrastructure/
├── MemberRepository.cs # only the MemberRepository class
└── MemberRepositoryExtensions.cs # only the extensions
```

This enforces SOLID principles and keeps files focused and maintainable.

### Module Template

Every new module should include a `README.md`:

```markdown
# [ModuleName] Module

## Purpose
[Brief description of what this module does]

## Responsibilities
- [What this module owns]

## Dependencies
- [External dependencies]

## Dashboard Tiles
- [Tile 1]: [description]
- [Tile 2]: [description]

## Settings Tab (Optional)
- [If applicable] Settings tab configuration and fields

## Key Entities
- [Entity 1]: [description]
- [Entity 2]: [description]
```

## Dashboard Tiles

Each module exposes functionality on the dashboard through tiles (see Constitution §4.2):

```csharp
// Features/Members/DashboardTile.cs
public class MembersDashboardTile : IDashboardTile
{
    private readonly IMemberService _memberService;
    
    public string Title => "Members";
    public int Order => 1;
    public string Icon => "users";
    
    public MembersDashboardTile(IMemberService memberService)
    {
        _memberService = memberService;
    }
    
    public async Task<IDashboardTileContent> GetContentAsync()
    {
        var activeCount = await _memberService.GetActiveMemberCountAsync();
        var pendingFees = await _memberService.GetPendingFeeCountAsync();
        var recent = await _memberService.GetRecentMembersAsync(5);
        
        return new MembersTileContent
        {
            ActiveMembers = activeCount,
            PendingFees = pendingFees,
            RecentMembers = recent
        };
    }
}
```

**Tile Content**: Each tile should include:
- Summary metrics or status information
- Charts/graphs for data visualization
- Quick-action buttons for common tasks
- Recent activity feeds
- Status indicators

See [UI Component Style Guide](docs/UI_COMPONENT_STYLE_GUIDE.md#dashboard-tiles) for detailed tile design patterns.

## Settings Tabs

Modules MAY define a settings tab for configuration (see Constitution §4.3). Settings tabs appear in the application Settings page under `/settings`.

### Module Settings Tab Implementation

```csharp
// Features/Members/Application/ISettingsTabProvider.cs (published interface)
public interface IMembersSettingsTabProvider : ISettingsTabProvider
{
    // Inherits from ISettingsTabProvider
}

// Features/Members/DashboardTile.cs (implements provider)
public class MembersSettingsTabProvider : ISettingsTabProvider
{
    private readonly IMembersSettingsService _settingsService;
    
    public string TabTitle => "Members";
    public string TabIcon => "users";
    public int DisplayOrder => 2;
    
    public Type SettingsComponentType => typeof(MembersSettingsTab);
    
    public async Task<ISettingsTab> GetSettingsAsync()
    {
        return await _settingsService.GetSettingsAsync();
    }
    
    public async Task<ValidationResult> ValidateAsync(ISettingsTab settings)
    {
        return await _settingsService.ValidateAsync(settings);
    }
    
    public async Task SaveAsync(ISettingsTab settings)
    {
        await _settingsService.SaveAsync(settings);
    }
}

// Features/Members/UI/Components/MembersSettingsTab.razor
@implements IAsyncDisposable
@inject IMembersSettingsTabProvider SettingsProvider

<EditForm Model="@settings" OnValidSubmit="@HandleSave">
    <DataAnnotationsValidator />
    
    <div class="settings-group">
        <h3>Member Settings</h3>
        
        <div class="form-group">
            <label>Default Member Status</label>
            <InputSelect @bind-Value="settings.DefaultMemberStatus">
                <option value="Active">Active</option>
                <option value="Inactive">Inactive</option>
            </InputSelect>
            <ValidationMessage For="@(() => settings.DefaultMemberStatus)" />
        </div>
        
        <div class="form-group">
            <label>Auto-Archive Inactive Days</label>
            <InputNumber @bind-Value="settings.AutoArchiveInactiveDays" />
            <ValidationMessage For="@(() => settings.AutoArchiveInactiveDays)" />
        </div>
    </div>
    
    <div class="settings-actions">
        <button type="button" class="btn btn-secondary" @onclick="OnCancel">Cancel</button>
        <button type="submit" class="btn btn-primary">Save Settings</button>
    </div>
</EditForm>

@code {
    private MembersSettings settings;

    protected override async Task OnInitializedAsync()
    {
        settings = (MembersSettings)await SettingsProvider.GetSettingsAsync();
    }

    private async Task HandleSave()
    {
        var result = await SettingsProvider.ValidateAsync(settings);
        if (!result.IsValid)
        {
            // Show validation errors
            return;
        }

        await SettingsProvider.SaveAsync(settings);
        // Notify parent that save succeeded
    }

    private void OnCancel()
    {
        // Notify parent to close or revert
    }
}
```

### Module Settings Registration

Register settings tabs in module DependencyInjection:

```csharp
// Features/Members/DependencyInjection.cs
public static IServiceCollection AddMembersModule(this IServiceCollection services)
{
    services.AddScoped<IMemberService, MemberService>();
    services.AddScoped<IMembersSettingsTabProvider, MembersSettingsTabProvider>();
    
    // Register with settings system
    services.AddScoped<ISettingsTabProvider>(sp => 
        sp.GetRequiredService<IMembersSettingsTabProvider>());
    
    return services;
}
```

### Application Settings Tab

The core application provides built-in settings (Constitution §4.3):

- **Organization Name** — Name of the group/organization
- **Annual Membership Fee** — Annual fee amount
- **Rehearsal/Event Fee** — Per-rehearsal or per-event fee
- **Membership Renewal Due Date** — Month and day (e.g., "September 1")

These settings are NOT provided by any module; they're part of the core application.

## Navigation Menu Items

Modules SHOULD define menu items for feature navigation (see Constitution §4.5). Menu items appear in the main navigation, with Settings always last.

### Menu Item Implementation

```csharp
// Features/Members/Application/IMenuItemProvider.cs (published interface)
public interface IMembersMenuItemProvider : IMenuItemProvider
{
    // Inherits from IMenuItemProvider
}

// Features/Members/UI/MembersMenuItemProvider.cs (implements provider)
public class MembersMenuItemProvider : IMenuItemProvider
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
                DisplayOrder = 1,
                SubItems = new List<MenuItem>
                {
                    new() 
                    { 
                        Title = "Active Members", 
                        Route = "/members/list", 
                        DisplayOrder = 1 
                    },
                    new() 
                    { 
                        Title = "Pending Approval", 
                        Route = "/members/pending", 
                        BadgeText = "3",
                        DisplayOrder = 2 
                    },
                    new() 
                    { 
                        Title = "Add Member", 
                        Route = "/members/new", 
                        DisplayOrder = 3 
                    }
                }
            }
        };
    }
}
```

### Menu Item Registration

Register menu items in module DependencyInjection:

```csharp
// Features/Members/DependencyInjection.cs
public static IServiceCollection AddMembersModule(this IServiceCollection services)
{
    services.AddScoped<IMemberService, MemberService>();
    services.AddScoped<IMembersMenuItemProvider, MembersMenuItemProvider>();
    
    // Register with menu system
    services.AddScoped<IMenuItemProvider>(sp => 
        sp.GetRequiredService<IMembersMenuItemProvider>());
    
    return services;
}
```

### Menu Item Characteristics

- **Title**: Display text in the navigation menu
- **Route**: Target URL path (e.g., "/members/list")
- **Icon**: Optional icon name for visual representation (e.g., "users", "calendar", "dollar-sign")
- **DisplayOrder**: Order within module menu items (lower numbers first)
- **SubItems**: Optional child menu items for grouping related features
- **BadgeText**: Optional notification badge (e.g., "5" for pending items)

### Menu Item Examples

**Members Module** (primary navigation):
```
Members
├── Active Members
├── Pending Approval [3]
└── Add Member
```

**Events Module** (with icon):
```
📅 Events
├── Upcoming Events
├── Past Events
└── Create Event
```

**Finances Module** (hierarchical):
```
💰 Finances
├── Transactions
├── Reports
├── Invoices [2]
└── Income Summary
```

### Important Rules

- **Settings Always Last**: Never add menu items after Settings
- **Module Order**: Use `DisplayOrder` to control module placement relative to other modules
- **Icon Guidelines**: Use common, recognizable icons (see UI_COMPONENT_STYLE_GUIDE.md for icon reference)
- **No Hardcoding**: Compute badge text dynamically (e.g., pending count)
- **Route Prefixes**: Each module owns its route prefix (e.g., `/members/*`, `/events/*`)
- **Sub-menu Depth**: Avoid deeply nested sub-menus; 2 levels maximum is recommended

## Testing Requirements

**Every reachable code path MUST be tested before merge.**

This includes:
- ✅ Success paths
- ✅ Validation failures
- ✅ Exception/error handling
- ✅ Boundary conditions (null, empty, min/max)
- ✅ State transitions
- ✅ All UI user journeys

### Test Organization

```
tests/
├── StageFright.Tests/
│   ├── Unit/
│   │   └── [ModuleName]/
│   │       ├── Services/
│   │       ├── Repositories/
│   │       └── [ComponentName]Tests.cs
│   │
│   ├── Integration/
│   │   └── [ModuleName]/
│   │       ├── Services/
│   │       ├── Workflows/
│   │       └── [Scenario]IntegrationTests.cs
│   │
│   └── UI/
│       ├── Pages/
│       ├── Components/
│       └── [PageName]UITests.cs
```

### Test Naming Convention

```csharp
// Unit/Integration tests
[Fact]
public void Should_[ExpectedBehavior]_When_[Condition]() { }

// UI tests
[Fact]
public void Should_[ExpectedBehavior]_When_[UserAction]_UI() { }
```

### Writing Tests

**Test must be deterministic** — no timing dependencies, use explicit waits.

Example unit test:
```csharp
[Fact]
public void Should_ValidateMemberEmail_When_CreatingNewMember()
{
    // Arrange
    var service = new MemberService(mockRepository.Object);
    var member = new MemberCreateRequest { Email = "invalid-email" };

    // Act & Assert
    Assert.Throws<ValidationException>(() => service.CreateMember(member));
}
```

Example UI integration test:
```csharp
[Fact]
public async Task Should_DisplayMemberList_When_NavigatingToMembersPage()
{
    // Arrange
    var cut = RenderComponent<MembersPage>(
        ComponentParameter.CreateParameter("MemberService", mockMemberService.Object)
    );

    // Act - wait for data load
    await cut.InvokeAsync(() => cut.Instance.OnInitializedAsync());

    // Assert
    cut.MarkupMatches(@"
        <h1>Members</h1>
        <table>
            <tr><td>John Doe</td></tr>
        </table>
    ");
}
```

## UI Component Development

### Design Principles

All UI must follow the **UI Component Style Guide** ([docs/UI_COMPONENT_STYLE_GUIDE.md](docs/UI_COMPONENT_STYLE_GUIDE.md)):

- **Clean & Simple** — minimal visual clutter
- **Compact Layouts** — optimized information density
- **Modern Design** — professional, contemporary aesthetics
- **Consistent Language** — unified component library
- **Accessible** — keyboard and screen-reader support

### Blazor Component Structure

```razor
@* Pages/MembersPage.razor *@
@page "/members"
@inject IMemberService MemberService
@inject ILogger<MembersPage> Logger

<PageHeader Title="Members" />

<div class="members-container">
    @if (members == null)
    {
        <LoadingSpinner />
    }
    else if (members.Count == 0)
    {
        <EmptyState Message="No members found" ActionText="Add Member" OnAction="NavigateToCreate" />
    }
    else
    {
        <MembersTable Members="members" OnDelete="HandleDelete" />
    }
</div>

@code {
    private List<MemberDto> members;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            members = await MemberService.GetActiveMembers();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load members");
            // Show user-friendly error
        }
    }

    private async Task HandleDelete(int memberId)
    {
        // Implementation
    }

    private void NavigateToCreate() => NavigationManager.NavigateTo("/members/new");
}
```

### CSS & Styling

- Use CSS isolation: `MembersPage.razor.css`
- Follow BEM naming: `.members-container__list--active`
- Minimal utility classes; prefer semantic CSS
- Coordinate with UI component library (Radzen, custom components)

## Custom Exceptions

All exceptions crossing architectural boundaries must be project-defined custom exceptions.

### Exception Hierarchy

```csharp
public abstract class StageFrightException : Exception
{
    public string CorrelationId { get; }
    public DateTime Timestamp { get; }
}

public class ValidationException : StageFrightException
{
    public string FieldName { get; }
    public object AttemptedValue { get; }
}

public class EntityNotFoundException : StageFrightException
{
    public string EntityType { get; }
    public object EntityId { get; }
}

public class PersistenceException : StageFrightException
{
    public string Operation { get; }
}

// ... and others
```

### Boundary Translation

**Translate raw framework exceptions to custom exceptions at architectural boundaries:**

```csharp
// ✅ GOOD: Translation at repository boundary
public async Task<Member> GetMemberAsync(int id)
{
    try
    {
        return await _context.Members.FindAsync(id);
    }
    catch (DbUpdateException ex)
    {
        throw new PersistenceException("Failed to query members", innerException: ex);
    }
}

// ❌ BAD: Raw exception leaks to Application layer
public async Task<Member> GetMemberAsync(int id)
{
    return await _context.Members.FindAsync(id);  // DbUpdateException!
}
```

## Soft Delete & Data Preservation

### Soft Delete Rules

**All application data must use soft delete** (except financial records, which are immutable):

```csharp
public class Member
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    
    // Soft delete fields
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string DeletedBy { get; set; }
}

// Query filtering
var activeMembers = await _context.Members
    .Where(m => !m.IsDeleted)
    .ToListAsync();
```

### Financial Data Immutability

**Financial records NEVER soft-delete or hard-delete:**

```csharp
public class IncomeTransaction
{
    public int Id { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    
    // NO IsDeleted, NO DeletedAt
    // To correct errors: create reversing transactions
}
```

## Logging & Observability

### Structured Logging with Serilog

```csharp
private readonly ILogger<MemberService> _logger;

public void CreateMember(MemberCreateRequest request)
{
    try
    {
        _logger.LogInformation("Creating new member: {MemberName}", request.Name);
        
        var member = new Member { Name = request.Name, Email = request.Email };
        _repository.Add(member);
        
        _logger.LogInformation("Member created successfully: {MemberId}", member.Id);
    }
    catch (ValidationException ex)
    {
        _logger.LogWarning(ex, "Validation failed for member creation: {Reason}", ex.Message);
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error creating member");
        throw new PersistenceException("Failed to create member", innerException: ex);
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

- [ ] All tests pass locally
- [ ] Code-path coverage complete (success, failure, exceptions, boundaries)
- [ ] Custom exceptions used at boundaries
- [ ] Soft-delete implemented where required
- [ ] No raw framework exceptions leak across layers
- [ ] UI follows design guide (clean, simple, modern)
- [ ] Module structure follows vertical slice pattern
- [ ] No hardcoded values or magic strings
- [ ] Logging implemented for observability
- [ ] Comments explain WHY, not WHAT
- [ ] Commit messages are clear and descriptive

## Pull Request Process

> **Target branch**: All pull requests must be opened against `dev`, not `master`. PRs targeting `master` will be rejected.

1. **Branch**: Create feature branch from `dev`
2. **Implement**: Follow vertical slice pattern, write tests first
3. **Commit**: Use clear, descriptive commit messages
   ```
   feat(members): Add soft-delete support for members

   - Add IsDeleted, DeletedAt, DeletedBy fields to Member entity
   - Filter inactive members from queries by default
   - Add MemberStatus view for inactive member management
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

1. Create folder: `src/Features/[ModuleName]/`
2. Create subfolders: Domain/, Application/, Infrastructure/, UI/, Tests/
3. Create `DashboardTile.cs` to define how the module appears on dashboard
4. Create `README.md` describing the module
5. Implement following vertical slice pattern (no cross-module imports)

### Q: What if my feature needs to communicate with another module?

Use dependency injection to reference published interfaces from the other module's Domain layer, not private implementation details. Example:

```csharp
// ✅ GOOD: Inject published interface
public class EventSchedulingService
{
    public EventSchedulingService(IMemberRepository memberRepository) { }
}

// ❌ BAD: Direct import from Infrastructure
public class EventSchedulingService
{
    public EventSchedulingService(MemberRepository repository) { }
}
```

### Q: How do I test async operations?

Use `xUnit` and `Task`-based tests:

```csharp
[Fact]
public async Task Should_LoadMembersAsync_When_PageInitialized()
{
    // Arrange
    var service = new MemberService(mockRepository.Object);

    // Act
    var result = await service.GetActiveMembersAsync();

    // Assert
    Assert.NotEmpty(result);
}
```

### Q: What's the difference between Unit and Integration tests?

- **Unit**: Test a single class in isolation, mock dependencies
- **Integration**: Test multiple components together with realistic (in-memory) data

### Q: How do I handle errors in Blazor components?

```razor
@if (errorMessage != null)
{
    <ErrorAlert Message="@errorMessage" OnDismiss="() => errorMessage = null" />
}

@code {
    private string errorMessage;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            data = await Service.LoadData();
        }
        catch (ValidationException ex)
        {
            errorMessage = $"Invalid input: {ex.Message}";
        }
        catch (EntityNotFoundException ex)
        {
            errorMessage = "Item not found";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error");
            errorMessage = "An unexpected error occurred. Please try again.";
        }
    }
}
```

## Resources

- [Constitution](\.specify\memory\constitution.md) — Governance framework
- [Architecture Guide](docs/ARCHITECTURE.md) — Detailed architecture patterns
- [UI Component Style Guide](docs/UI_COMPONENT_STYLE_GUIDE.md) — Design standards
- [Spec Kit Documentation](\.specify\README.md) — Feature specification workflow
- [.NET MAUI Documentation](https://learn.microsoft.com/en-us/dotnet/maui/)
- [Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/)

## Questions?

- Check existing issues and discussions
- Review specification and plan artifacts for design decisions
- Ask questions in GitHub Discussions
