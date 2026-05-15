# Architecture Guide

## Overview

StageFright Community uses a **Vertical Slice Module Architecture** where each feature is self-contained, independently testable, and capable of being developed and deployed in isolation. This guide explains the architectural patterns, principles, and how to implement new features following this model.

---

## Vertical Slice Architecture

### What is a Vertical Slice?

A vertical slice is a complete, thin slice of functionality from top to bottom: Domain → Application → Infrastructure → UI. Each module (slice) is independent and handles a complete user-facing feature or business capability.

```
Domain Layer:        Member entity, business rules
                     ↓
Application Layer:   MemberService, request/response models
                     ↓
Infrastructure Layer: MemberRepository, database access
                     ↓
UI Layer:            MembersPage, components
                     ↓
Dashboard:           MembersDashboardTile
```

### Why Vertical Slices?

✅ **Independence** — Each slice can be developed, tested, and deployed independently  
✅ **Clarity** — Clear ownership: each module owns its full business capability  
✅ **Testability** — Isolated testing without cross-module complexity  
✅ **Scalability** — New features added as new slices; no core bloat  
✅ **Maintainability** — Changes localized to one slice; low risk of side effects  

### Module Structure

Every module lives in its own folder and contains all required layers:

```
src/Features/[ModuleName]/
├── Domain/
│   ├── Entities/
│   │   └── [Entity].cs
│   ├── ValueObjects/
│   ├── Events/
│   └── [Contracts].cs          # Published interfaces
│
├── Application/
│   ├── Services/
│   │   └── [Service].cs        # Business logic orchestrators
│   ├── Handlers/
│   │   └── [Handler].cs        # Command/event handlers
│   ├── DTOs/
│   │   └── [Request|Response].cs
│   └── Interfaces/
│       └── I[Service].cs       # Contracts published to other modules
│
├── Infrastructure/
│   ├── Repositories/
│   │   └── [Entity]Repository.cs  # Data access
│   ├── DataAccess/
│   │   └── [Entity]DbConfiguration.cs  # EF Core config
│   └── ExternalServices/       # Third-party integrations
│       └── [Service]Client.cs
│
├── UI/
│   ├── Pages/
│   │   └── [Feature]Page.razor
│   ├── Components/
│   │   └── [Component].razor   # Reusable UI components
│   └── Models/
│       └── [ViewModel].cs      # UI-specific models
│
├── Tests/
│   ├── Unit/
│   │   ├── Services/
│   │   └── [ServiceName]Tests.cs
│   ├── Integration/
│   │   ├── Repositories/
│   │   └── [RepositoryName]IntegrationTests.cs
│   └── UI/
│       └── [PageName]UITests.cs
│
├── DashboardTile.cs            # Dashboard tile provider
└── README.md                   # Module documentation
```

---

## Layer Responsibilities

### Domain Layer (`Domain/`)

**Purpose**: Encapsulates business logic and rules, independent of any framework or technology.

**Responsibilities**:
- Entities with business behavior
- Value objects (immutable data)
- Business rules and constraints
- Domain events
- Published contracts (interfaces)

**Rules**:
- ✅ Domain classes should be "fat models" with behavior
- ✅ Use value objects for complex data
- ✅ Raise domain events for important facts
- ✅ Can reference other Domain layers (published contracts only)
- ❌ No framework dependencies (EF Core, Serilog, etc.)
- ❌ No Application or Infrastructure imports
- ❌ No data access logic

**Example**:

```csharp
// ✅ GOOD: Rich domain model
public class Member
{
    public int Id { get; set; }
    public string Name { get; set; }
    public Email Email { get; set; }           // Value object
    public MemberStatus Status { get; set; }   // Enum
    public decimal OutstandingFees { get; private set; }
    
    public void RecordFeePayment(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive");
        if (OutstandingFees < amount) throw new InvalidOperationException("Overpayment");
        
        OutstandingFees -= amount;
        DomainEvents.Add(new FeePaymentRecordedEvent(this.Id, amount));
    }
}

// ✅ GOOD: Value object
public record Email
{
    public string Value { get; }
    
    public Email(string value)
    {
        if (!IsValidEmail(value)) throw new ArgumentException("Invalid email");
        Value = value;
    }
}
```

### Application Layer (`Application/`)

**Purpose**: Orchestrates business logic, coordinates between Domain and Infrastructure layers.

**Responsibilities**:
- Service classes that use repositories and Domain logic
- DTOs (Data Transfer Objects) for inbound/outbound data
- Request/response models
- Business orchestration
- Published service interfaces

**Rules**:
- ✅ Services are thin orchestrators
- ✅ Use dependency injection for repositories and external services
- ✅ Translate exceptions at boundaries
- ✅ Create DTOs to decouple from Domain entities
- ✅ Can reference Domain and Infrastructure layers
- ❌ No direct UI logic
- ❌ No entity framework queries (use repositories)
- ❌ No hardcoded business rules (use Domain entities)

**Example**:

```csharp
// ✅ GOOD: Service orchestrates Domain + Infrastructure
public interface IMemberService
{
    Task<MemberDto> CreateMemberAsync(CreateMemberRequest request);
    Task<List<MemberDto>> GetActiveMembersAsync();
}

public class MemberService : IMemberService
{
    private readonly IMemberRepository _repository;
    private readonly ILogger<MemberService> _logger;
    
    public MemberService(IMemberRepository repository, ILogger<MemberService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    public async Task<MemberDto> CreateMemberAsync(CreateMemberRequest request)
    {
        try
        {
            var email = new Email(request.Email);
            var member = new Member { Name = request.Name, Email = email };
            
            var created = await _repository.AddAsync(member);
            _logger.LogInformation("Member created: {MemberId}", created.Id);
            
            return new MemberDto 
            { 
                Id = created.Id, 
                Name = created.Name, 
                Email = created.Email.Value 
            };
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to create member");
            throw new PersistenceException("Failed to create member", innerException: ex);
        }
    }
}

// ✅ GOOD: DTO decouples from Domain
public class MemberDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Status { get; set; }
}
```

### Infrastructure Layer (`Infrastructure/`)

**Purpose**: Implements data access and external service integration.

**Responsibilities**:
- Repository implementations for data access
- Entity Framework configuration
- External API clients
- File system access
- Exception translation at boundaries

**Rules**:
- ✅ Repositories implement interfaces defined in Application
- ✅ Translate raw exceptions to custom exceptions
- ✅ Use Entity Framework Core for queries
- ✅ Can reference Application and Domain
- ❌ No business logic (that's in Domain/Application)
- ❌ No UI logic
- ❌ No service orchestration

**Example**:

```csharp
// ✅ GOOD: Repository with exception translation
public class MemberRepository : IMemberRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MemberRepository> _logger;
    
    public MemberRepository(ApplicationDbContext context, ILogger<MemberRepository> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public async Task<Member> AddAsync(Member member)
    {
        try
        {
            _context.Members.Add(member);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Member added: {MemberId}", member.Id);
            return member;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error adding member");
            throw new PersistenceException("Failed to add member", innerException: ex);
        }
    }
    
    public async Task<Member> GetByIdAsync(int id)
    {
        try
        {
            var member = await _context.Members.FirstOrDefaultAsync(m => m.Id == id);
            return member ?? throw new EntityNotFoundException("Member", id);
        }
        catch (DbException ex)
        {
            throw new PersistenceException("Failed to query members", innerException: ex);
        }
    }
}
```

### UI Layer (`UI/`)

**Purpose**: Presents information and handles user interaction.

**Responsibilities**:
- Blazor pages and components
- User interaction handling
- Validation for UI input
- Navigation
- Component presentation logic

**Rules**:
- ✅ Inject services from Application layer
- ✅ Use components for reusability
- ✅ Handle errors gracefully for users
- ✅ CSS isolation for styling
- ✅ Can reference Application layer only
- ❌ No business logic (use services)
- ❌ No direct database access
- ❌ No hardcoded data

**Example**:

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
        <EmptyState Message="No members found" />
    }
    else
    {
        <MembersTable Members="members" OnDelete="HandleDelete" />
    }
</div>

@code {
    private List<MemberDto> members;
    private string errorMessage;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            members = await MemberService.GetActiveMembersAsync();
        }
        catch (EntityNotFoundException)
        {
            members = new();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load members");
            errorMessage = "Failed to load members. Please try again.";
        }
    }

    private async Task HandleDelete(int memberId)
    {
        try
        {
            await MemberService.DeleteMemberAsync(memberId);
            members = await MemberService.GetActiveMembersAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete member");
            errorMessage = "Failed to delete member";
        }
    }
}

<style scoped>
.members-container {
    padding: 16px;
}
</style>
```

---

## Communication Between Modules

### Intra-Module Communication

Within a module, all layers communicate directly:

```
MembersPage (UI)
    ↓ (injects)
MemberService (Application)
    ↓ (uses)
MemberRepository (Infrastructure)
    ↓ (queries)
Member (Domain)
```

### Inter-Module Communication

Between modules, use published Application interfaces **only**:

```
EventScheduling Module → Events Module
    ↓
Injects IEventService (Application layer)
    ↓
Does NOT import
  - Event Infrastructure
  - Event UI Components
  - Event private services
```

**Example: EventScheduling scheduling an event**

```csharp
// ✅ GOOD: EventScheduling injects published interface
public class EventSchedulingService
{
    private readonly IEventService _eventService;
    
    public EventSchedulingService(IEventService eventService)
    {
        _eventService = eventService; // Published interface
    }
    
    public async Task ScheduleRehearsalAsync(RehearsalRequest request)
    {
        var eventDto = await _eventService.CreateEventAsync(
            new CreateEventRequest { /* ... */ }
        );
    }
}

// ❌ BAD: Direct Infrastructure import
public class EventSchedulingService
{
    private readonly EventRepository _repository; // Private!
    // This breaks the module boundary
}
```

### Cross-Module Data Flow

```
Input: EventSchedulingService needs to create an event

1. EventScheduling calls IEventService.CreateEventAsync(createRequest)
   (Published Application interface)

2. EventService orchestrates:
   - Validates input
   - Creates Event entity (Domain)
   - Saves via EventRepository (Infrastructure)

3. Returns EventDto (DTO for external consumption)

4. EventScheduling receives EventDto and proceeds

Result: Clean boundary, no implementation leakage
```

---

## Dependency Injection Setup

### Module Registration

Each module registers its services in a setup extension:

```csharp
// Features/Members/DependencyInjection.cs
namespace StageFright.Features.Members;

public static class DependencyInjection
{
    public static IServiceCollection AddMembersModule(
        this IServiceCollection services)
    {
        // Domain services (if any)
        
        // Application services
        services.AddScoped<IMemberService, MemberService>();
        
        // Infrastructure
        services.AddScoped<IMemberRepository, MemberRepository>();
        
        return services;
    }
}

// Program.cs
builder.Services
    .AddMembersModule()
    .AddEventSchedulingModule()
    .AddFinancialTrackingModule();
```

### Cross-Module Dependencies

Inject published interfaces from other modules:

```csharp
// EventScheduling/Application/Services/EventSchedulingService.cs
public class EventSchedulingService
{
    public EventSchedulingService(
        IEventService eventService,      // From Events module
        IMemberRepository memberRepository) // From Members module
    {
        _eventService = eventService;
        _memberRepository = memberRepository;
    }
}
```

---

## Error Handling & Exception Translation

### Exception Translation at Boundaries

Translate raw exceptions to custom exceptions at architectural boundaries:

```
Infrastructure Layer (catches raw exceptions)
    ↓ (translates)
    ↓
Application Layer (sees only custom exceptions)
    ↓
UI Layer (handles custom exceptions)
```

**Example: Repository translates database exception**

```csharp
// Infrastructure/Repositories/MemberRepository.cs
public class MemberRepository : IMemberRepository
{
    public async Task<Member> GetByIdAsync(int id)
    {
        try
        {
            return await _context.Members.FindAsync(id)
                ?? throw new EntityNotFoundException("Member", id);
        }
        catch (EntityNotFoundException)
        {
            throw; // Custom exception, let it through
        }
        catch (DbException ex)
        {
            // Raw database exception → custom exception
            throw new PersistenceException("Failed to query member", innerException: ex);
        }
    }
}

// Application/Services/MemberService.cs
public class MemberService : IMemberService
{
    public async Task<MemberDto> GetMemberAsync(int id)
    {
        try
        {
            var member = await _memberRepository.GetByIdAsync(id);
            return new MemberDto { /* ... */ };
        }
        catch (EntityNotFoundException ex)
        {
            // Custom exception from repository
            _logger.LogWarning(ex, "Member not found: {MemberId}", id);
            throw; // Propagate to UI
        }
    }
}

// UI/Pages/MemberPage.razor
@code {
    protected override async Task OnInitializedAsync()
    {
        try
        {
            member = await MemberService.GetMemberAsync(id);
        }
        catch (EntityNotFoundException)
        {
            errorMessage = "Member not found";
        }
        catch (PersistenceException)
        {
            errorMessage = "Failed to load member. Please try again.";
        }
    }
}
```

---

## Data Models

### Domain Entities

Entities represent core business concepts with business behavior:

```csharp
public class Member
{
    public int Id { get; set; }
    public string Name { get; set; }
    public Email Email { get; set; }
    public MemberStatus Status { get; set; }
    
    // Soft delete fields
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string DeletedBy { get; set; }
    
    // Navigation properties
    public ICollection<EventAttendance> Attendances { get; set; }
    public ICollection<FeePayment> Payments { get; set; }
    
    // Business methods
    public void RecordAttendance(Event @event)
    {
        if (IsDeleted) throw new InvalidOperationException("Cannot record for deleted member");
        Attendances.Add(new EventAttendance { Member = this, Event = @event });
    }
}
```

### DTOs (Data Transfer Objects)

DTOs decouple Domain entities from external consumers:

```csharp
// Request (inbound)
public class CreateMemberRequest
{
    public string Name { get; set; }
    public string Email { get; set; }
}

// Response (outbound)
public class MemberDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Status { get; set; }
    public decimal OutstandingFees { get; set; }
}
```

---

## Testing Strategy

### Unit Tests (Single Layer)

Test a single service/repository in isolation with mocked dependencies:

```csharp
[Fact]
public async Task Should_ReturnActiveMembersOnly_When_GetActiveMembersAsync_Called()
{
    // Arrange
    var mockRepository = new Mock<IMemberRepository>();
    mockRepository
        .Setup(r => r.GetActiveMembersAsync())
        .ReturnsAsync(new List<Member>
        {
            new() { Id = 1, Name = "John", Status = MemberStatus.Active }
        });
    
    var service = new MemberService(mockRepository.Object, new Mock<ILogger<MemberService>>().Object);
    
    // Act
    var result = await service.GetActiveMembersAsync();
    
    // Assert
    Assert.Single(result);
    Assert.Equal("John", result[0].Name);
}
```

### Integration Tests (Multiple Layers)

Test with real repositories and in-memory database:

```csharp
[Fact]
public async Task Should_CreateAndRetrieveMember_When_UsingSameTransaction()
{
    // Arrange
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase("test-db")
        .Options;
    
    using var context = new ApplicationDbContext(options);
    var repository = new MemberRepository(context, new Mock<ILogger<MemberRepository>>().Object);
    
    // Act
    var member = new Member { Name = "John", Email = new Email("john@example.com") };
    await repository.AddAsync(member);
    
    var retrieved = await repository.GetByIdAsync(member.Id);
    
    // Assert
    Assert.NotNull(retrieved);
    Assert.Equal("John", retrieved.Name);
}
```

### UI Tests (Component)

Test Blazor components with bUnit:

```csharp
[Fact]
public void Should_DisplayMembersList_When_MembersProvided()
{
    // Arrange
    var members = new List<MemberDto>
    {
        new() { Id = 1, Name = "John", Email = "john@example.com" }
    };
    
    // Act
    var cut = RenderComponent<MembersTable>(
        ComponentParameter.CreateParameter("Members", members)
    );
    
    // Assert
    cut.Find("table tbody tr").TextContent.Should().Contain("John");
}
```

---

## Dashboard Tiles

Each module defines how it appears on the dashboard:

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

---

## Settings System

The Settings page (`/settings`) is a core application feature where configuration and module preferences are managed through a tabbed interface.

### Settings Architecture

- **Core Application Settings Tab**: Built-in tab with organization, fee, and membership configuration
- **Module Settings Tabs**: Each module MAY provide a settings tab for module-specific configuration
- **Tab Registry**: Settings tabs discovered and registered at startup via DI
- **Tab Isolation**: Each tab manages its own UI, validation, and persistence

### Application Settings Tab

The core application provides built-in settings:

```csharp
public class ApplicationSettings
{
    public string OrganizationName { get; set; }
    public decimal AnnualMembershipFee { get; set; }
    public decimal RehearsalFee { get; set; }
    public DateTime MembershipRenewalDueDate { get; set; } // e.g., September 1
    public int MembershipRenewalGracePeriodDays { get; set; }
}
```

### Module Settings Tab Implementation

Modules that need configuration provide a settings tab:

```csharp
// Module publishes ISettingsTabProvider interface
public interface ISettingsTabProvider
{
    string TabTitle { get; }      // Display title (e.g., "Members")
    string TabIcon { get; }       // Icon name
    int DisplayOrder { get; }     // Tab order in settings page
    
    Type SettingsComponentType { get; }  // Blazor component for settings UI
    
    Task<ISettingsTab> GetSettingsAsync();
    Task<ValidationResult> ValidateAsync(ISettingsTab settings);
    Task SaveAsync(ISettingsTab settings);
}

// Module implements provider
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
        if (settings is not MembersSettings ms)
            return ValidationResult.Invalid("Invalid settings type");
        
        if (ms.AutoArchiveInactiveDays < 1)
            return ValidationResult.Invalid("Days must be at least 1");
        
        return ValidationResult.Valid();
    }
    
    public async Task SaveAsync(ISettingsTab settings)
    {
        await _settingsService.SaveAsync((MembersSettings)settings);
    }
}

// Module provides Blazor component for tab content
// Features/Members/UI/Components/MembersSettingsTab.razor
@implements IAsyncDisposable
@inject IMembersSettingsTabProvider SettingsProvider
@inject ILogger<MembersSettingsTab> Logger

<EditForm Model="@settings" OnValidSubmit="@HandleSave">
    <DataAnnotationsValidator />
    
    <div class="form-group">
        <label>Default Member Status</label>
        <InputSelect @bind-Value="settings.DefaultMemberStatus" class="form-control">
            <option>Active</option>
            <option>Inactive</option>
        </InputSelect>
        <ValidationMessage For="@(() => settings.DefaultMemberStatus)" />
    </div>
    
    <div class="form-group">
        <label>Auto-Archive Inactive After (days)</label>
        <InputNumber @bind-Value="settings.AutoArchiveInactiveDays" class="form-control" />
        <ValidationMessage For="@(() => settings.AutoArchiveInactiveDays)" />
    </div>
    
    @if (errorMessage != null)
    {
        <div class="alert alert-danger">@errorMessage</div>
    }
    
    <div class="button-group">
        <button type="button" class="btn btn-secondary" @onclick="OnCancel">Cancel</button>
        <button type="submit" class="btn btn-primary">Save Settings</button>
    </div>
</EditForm>

@code {
    [CascadingParameter]
    private SettingsPage ParentPage { get; set; }
    
    private MembersSettings settings;
    private string errorMessage;

    protected override async Task OnInitializedAsync()
    {
        settings = (MembersSettings)await SettingsProvider.GetSettingsAsync();
    }

    private async Task HandleSave()
    {
        var result = await SettingsProvider.ValidateAsync(settings);
        if (!result.IsValid)
        {
            errorMessage = result.ErrorMessage;
            return;
        }

        try
        {
            await SettingsProvider.SaveAsync(settings);
            Logger.LogInformation("Members settings saved");
            await ParentPage.ShowSuccessMessage("Members settings saved successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save members settings");
            errorMessage = "Failed to save settings. Please try again.";
        }
    }

    private void OnCancel()
    {
        ParentPage?.OnTabCancel();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        // Cleanup
    }
}
```

### Settings Page Tab Layout

```
┌─────────────────────────────────────────────────────────┐
│ Settings                                                │
├─────────────────────────────────────────────────────────┤
│ │ Application │ Members │ Events │ Finances │ ...      │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  Application Settings                                  │
│  ┌─────────────────────────────────────────────────┐  │
│  │ Organization Name: [____________]                │  │
│  │ Annual Membership Fee: $[____]                   │  │
│  │ Rehearsal Fee: $[____]                           │  │
│  │ Membership Renewal Due Date: [____-____]         │  │
│  │                                                  │  │
│  │ [Cancel]  [Save Settings]                        │  │
│  └─────────────────────────────────────────────────┘  │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

### Module Settings Registration

Register settings tabs in module DependencyInjection:

```csharp
// Features/Members/DependencyInjection.cs
public static IServiceCollection AddMembersModule(this IServiceCollection services)
{
    // ... other registrations ...
    
    // Settings tab registration
    services.AddScoped<IMembersSettingsTabProvider, MembersSettingsTabProvider>();
    services.AddScoped<ISettingsTabProvider>(sp => 
        sp.GetRequiredService<IMembersSettingsTabProvider>());
    
    return services;
}

// Program.cs - All ISettingsTabProvider implementations auto-discovered
builder.Services.Scan(scan => scan
    .FromAssemblies(typeof(Program).Assembly)
    .AddClasses(classes => classes.AssignableTo(typeof(ISettingsTabProvider)))
    .AsImplementedInterfaces()
    .WithScopedLifetime());
```

### Settings Tab Lifecycle

1. **Page Load**: `/settings` route loads SettingsPage component
2. **Tab Discovery**: Application discovers all registered `ISettingsTabProvider` instances
3. **Tab Rendering**: Tabs rendered with tab headers sorted by `DisplayOrder`
4. **Tab Selection**: User clicks tab, content component created and loaded
5. **Data Load**: Tab component calls `SettingsProvider.GetSettingsAsync()`
6. **Edit**: User modifies settings in form
7. **Validation**: On save, form validates locally and calls `ValidateAsync()`
8. **Persistence**: If valid, calls `SaveAsync()` to persist changes
9. **Feedback**: User sees success/error message

### Settings Persistence

Settings are persisted through the module's infrastructure layer:

```csharp
// Features/Members/Infrastructure/MembersSettingsRepository.cs
public class MembersSettingsRepository : IMembersSettingsRepository
{
    private readonly ApplicationDbContext _context;
    
    public async Task<MembersSettings> GetSettingsAsync()
    {
        // Load from database or return defaults
        return await _context.MembersSettings.FirstOrDefaultAsync()
            ?? MembersSettings.CreateDefaults();
    }
    
    public async Task SaveAsync(MembersSettings settings)
    {
        var existing = await _context.MembersSettings.FirstOrDefaultAsync();
        
        if (existing == null)
        {
            _context.MembersSettings.Add(settings);
        }
        else
        {
            existing.DefaultMemberStatus = settings.DefaultMemberStatus;
            existing.AutoArchiveInactiveDays = settings.AutoArchiveInactiveDays;
        }
        
        await _context.SaveChangesAsync();
    }
}
```

---

## Navigation Menu System

The main navigation menu is modular and extensible. Each module defines its own menu items, which appear in the application's main navigation bar. Settings always appears as the final menu item.

### Menu Architecture

- **Centralized Discovery**: Menu items are auto-discovered via `IMenuItemProvider` at application startup
- **Module Ownership**: Each module registers only its own menu items
- **Hierarchical Support**: Menu items can have sub-items for feature grouping
- **Visual Enhancement**: Optional icons distinguish menu items visually
- **Dynamic Badges**: Real-time notification badges (e.g., pending count)
- **Ordering**: Modules control their display order; Settings reserved for last

### Menu Item Interface

```csharp
// Application/Navigation/IMenuItemProvider.cs (published interface)
public interface IMenuItemProvider
{
    string ModuleName { get; }          // e.g., "Members"
    int DisplayOrder { get; }           // Module-level order (1, 2, 3...)
    
    IReadOnlyList<MenuItem> GetMenuItems();
}

public class MenuItem
{
    public string Title { get; set; }              // "Members", "Upcoming Events"
    public string Route { get; set; }             // "/members", "/events/upcoming"
    public string Icon { get; set; }              // Optional: "users", "calendar"
    public int DisplayOrder { get; set; }         // Item order within module
    public List<MenuItem> SubItems { get; set; }  // Optional sub-menu
    public string BadgeText { get; set; }         // Optional: "5", "3"
    public bool IsActive { get; set; }            // Computed: is current page?
}
```

### Module Menu Implementation Example

```csharp
// Features/Members/UI/MembersMenuItemProvider.cs
public class MembersMenuItemProvider : IMenuItemProvider
{
    private readonly IMemberService _memberService;
    
    public string ModuleName => "Members";
    public int DisplayOrder => 1;  // First module in menu
    
    public MembersMenuItemProvider(IMemberService memberService)
    {
        _memberService = memberService;
    }
    
    public IReadOnlyList<MenuItem> GetMenuItems()
    {
        // Compute badge text dynamically
        var pendingCount = _memberService.GetPendingApprovalCountAsync().Result;
        var pendingBadge = pendingCount > 0 ? pendingCount.ToString() : null;
        
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
                        BadgeText = pendingBadge,
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

// Features/Events/UI/EventsMenuItemProvider.cs
public class EventsMenuItemProvider : IMenuItemProvider
{
    public string ModuleName => "Events";
    public int DisplayOrder => 2;  // Second module in menu
    
    public IReadOnlyList<MenuItem> GetMenuItems()
    {
        return new List<MenuItem>
        {
            new()
            {
                Title = "Events",
                Route = "/events",
                Icon = "calendar",
                DisplayOrder = 1,
                SubItems = new List<MenuItem>
                {
                    new() { Title = "Upcoming", Route = "/events/upcoming", DisplayOrder = 1 },
                    new() { Title = "Past", Route = "/events/past", DisplayOrder = 2 },
                    new() { Title = "Create Event", Route = "/events/new", DisplayOrder = 3 }
                }
            }
        };
    }
}
```

### Menu Registration

Register in module's DependencyInjection:

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

// Program.cs - Auto-discover all IMenuItemProvider implementations
builder.Services.Scan(scan => scan
    .FromAssemblies(typeof(Program).Assembly)
    .AddClasses(classes => classes.AssignableTo(typeof(IMenuItemProvider)))
    .AsImplementedInterfaces()
    .WithScopedLifetime());
```

### Menu Rendering Order

The application renders menu items in this order:

```
1. Dashboard (core, always first)
2. Members (DisplayOrder: 1)
3. Events (DisplayOrder: 2)
4. Finances (DisplayOrder: 3)
... [other modules by DisplayOrder] ...
N. Settings (core, always last)
```

Sub-items within each module are ordered by their `DisplayOrder`.

### Main Navigation Component

```razor
@* App/Layout/MainLayout.razor *@
@using StageFright.Application.Navigation
@inject IEnumerable<IMenuItemProvider> MenuProviders

<nav class="main-navigation">
    <div class="nav-brand">
        <span>StageFright</span>
    </div>
    
    <ul class="nav-menu">
        @* Dashboard always first *@
        <li class="nav-item">
            <a href="/dashboard" class="nav-link">
                <i class="icon icon-home"></i>
                <span>Dashboard</span>
            </a>
        </li>
        
        @* Module menu items sorted by DisplayOrder *@
        @foreach (var provider in MenuProviders
            .OrderBy(p => p.DisplayOrder))
        {
            @foreach (var item in provider.GetMenuItems()
                .OrderBy(m => m.DisplayOrder))
            {
                <li class="nav-item">
                    <a href="@item.Route" 
                       class="nav-link @(item.IsActive ? "active" : "")"
                       title="@item.Title">
                        @if (!string.IsNullOrEmpty(item.Icon))
                        {
                            <i class="icon icon-@item.Icon"></i>
                        }
                        <span>@item.Title</span>
                        @if (!string.IsNullOrEmpty(item.BadgeText))
                        {
                            <span class="badge">@item.BadgeText</span>
                        }
                    </a>
                    
                    @* Sub-menu items *@
                    @if (item.SubItems?.Count > 0)
                    {
                        <ul class="nav-submenu">
                            @foreach (var sub in item.SubItems
                                .OrderBy(s => s.DisplayOrder))
                            {
                                <li class="nav-subitem">
                                    <a href="@sub.Route" 
                                       class="nav-sublink @(sub.IsActive ? "active" : "")"
                                       title="@sub.Title">
                                        @sub.Title
                                        @if (!string.IsNullOrEmpty(sub.BadgeText))
                                        {
                                            <span class="badge">@sub.BadgeText</span>
                                        }
                                    </a>
                                </li>
                            }
                        </ul>
                    }
                </li>
            }
        }
        
        @* Settings always last *@
        <li class="nav-item nav-settings">
            <a href="/settings" class="nav-link">
                <i class="icon icon-cog"></i>
                <span>Settings</span>
            </a>
        </li>
    </ul>
</nav>

@code {
    protected override void OnInitialized()
    {
        // Update IsActive flag for all items based on current route
        foreach (var provider in MenuProviders)
        {
            UpdateMenuItemStates(provider.GetMenuItems());
        }
    }
    
    private void UpdateMenuItemStates(IReadOnlyList<MenuItem> items)
    {
        // Compare item routes with current route and set IsActive
        // Implementation depends on routing context
    }
}
```

### Menu Item Guidelines

**Icon Usage**:
- Use common, recognizable icons (e.g., "users", "calendar", "dollar-sign")
- Icons are optional but recommended for visual distinction
- See [UI_COMPONENT_STYLE_GUIDE.md](#icon-library) for complete icon reference

**Badge Usage**:
- Badges display dynamic counts (e.g., "5" pending items)
- Compute badge values dynamically; never hardcode
- Clear badges when count reaches zero

**Sub-menu Depth**:
- Limit to 2 levels: menu item → sub-items
- Avoid deeply nested menus; prioritize flat hierarchy
- Group related features under one parent item

**Route Isolation**:
- Each module owns its route prefix (e.g., `/members/*`, `/events/*`)
- Avoid route conflicts between modules
- Use route parameters for entity IDs (e.g., `/members/edit/123`)

**Menu Isolation Rules**:
- Modules MUST NOT depend on menu items from other modules
- Menu contributions MUST be independent
- Do NOT modify or remove menu items from other modules
- Settings menu is reserved; modules cannot add items after Settings

---

## Best Practices

### ✅ DO

- Keep modules focused on a single business capability
- Use dependency injection for all service dependencies
- Create interfaces for all public services
- Translate exceptions at layer boundaries
- Write tests for all business logic
- Use value objects for complex data
- Isolate modules; communicate through interfaces only
- Document public module interfaces

### ❌ DON'T

- Import Infrastructure layer from other modules
- Create circular dependencies
- Use static state or service locators
- Mix business logic into UI or Infrastructure
- Hardcode configuration or sensitive data
- Create god objects (too many responsibilities)
- Forget to mock external dependencies in tests

---

## Example: Adding a New Module

### 1. Create Folder Structure

```bash
mkdir -p src/Features/RehearsalScheduling/{Domain,Application,Infrastructure,UI,Tests}
```

### 2. Create Domain Layer

```csharp
// Domain/Entities/Rehearsal.cs
public class Rehearsal
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime ScheduledDate { get; set; }
    public TimeSpan Duration { get; set; }
    
    public void Reschedule(DateTime newDate)
    {
        if (newDate <= DateTime.Now) throw new ArgumentException("Date must be in future");
        ScheduledDate = newDate;
    }
}

// Domain/IRehearsalRepository.cs
public interface IRehearsalRepository
{
    Task<Rehearsal> AddAsync(Rehearsal rehearsal);
    Task<List<Rehearsal>> GetUpcomingAsync();
}
```

### 3. Create Application Layer

```csharp
// Application/Services/IRehearsalService.cs
public interface IRehearsalService
{
    Task<RehearsalDto> CreateRehearsalAsync(CreateRehearsalRequest request);
    Task<List<RehearsalDto>> GetUpcomingRehearalsAsync();
}

// Application/Services/RehearsalService.cs
public class RehearsalService : IRehearsalService
{
    private readonly IRehearsalRepository _repository;
    
    public RehearsalService(IRehearsalRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<RehearsalDto> CreateRehearsalAsync(CreateRehearsalRequest request)
    {
        var rehearsal = new Rehearsal 
        { 
            Name = request.Name,
            ScheduledDate = request.ScheduledDate,
            Duration = request.Duration
        };
        
        await _repository.AddAsync(rehearsal);
        
        return new RehearsalDto
        {
            Id = rehearsal.Id,
            Name = rehearsal.Name,
            ScheduledDate = rehearsal.ScheduledDate
        };
    }
}
```

### 4. Create Infrastructure Layer

```csharp
// Infrastructure/Repositories/RehearsalRepository.cs
public class RehearsalRepository : IRehearsalRepository
{
    private readonly ApplicationDbContext _context;
    
    public async Task<Rehearsal> AddAsync(Rehearsal rehearsal)
    {
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();
        return rehearsal;
    }
}
```

### 5. Create UI Layer

```razor
@* UI/Pages/RehearalsPage.razor *@
@page "/rehearsals"
@inject IRehearsalService RehearsalService

<PageHeader Title="Rehearsals" />

@foreach (var rehearsal in rehearsals ?? new())
{
    <RehearsalCard Rehearsal="rehearsal" />
}

@code {
    private List<RehearsalDto> rehearsals;
    
    protected override async Task OnInitializedAsync()
    {
        rehearsals = await RehearsalService.GetUpcomingRehearalsAsync();
    }
}
```

### 6. Create DashboardTile

```csharp
// DashboardTile.cs
public class RehearsalsDashboardTile : IDashboardTile
{
    public string Title => "Upcoming Rehearsals";
    public int Order => 2;
    
    // ... implementation
}
```

### 7. Register Module

```csharp
// DependencyInjection.cs
public static IServiceCollection AddRehearsalSchedulingModule(this IServiceCollection services)
{
    services.AddScoped<IRehearsalService, RehearsalService>();
    services.AddScoped<IRehearsalRepository, RehearsalRepository>();
    return services;
}

// Program.cs
builder.Services.AddRehearsalSchedulingModule();
```

---

## Summary

The Vertical Slice Architecture provides:

- **Modularity** — Each feature is independent
- **Clarity** — Clear ownership and responsibility
- **Testability** — Isolated testing at each layer
- **Scalability** — New features added as new modules
- **Maintainability** — Changes localized to one module

Follow the pattern, respect module boundaries, and use published interfaces for communication.

---

For more details, see:
- [Contributing Guide](../CONTRIBUTING.md)
- [UI Component Style Guide](UI_COMPONENT_STYLE_GUIDE.md)
- [Constitution](..\.specify\memory\constitution.md)
