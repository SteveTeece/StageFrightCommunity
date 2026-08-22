---
title: StageFright Community — XML Documentation Standards
author: Architecture Team
date: 2026-08-22
version: 1.1
status: Active
---

# XML Documentation Standards

## Overview

This document establishes mandatory XML documentation (triple-slash comments: `///`) standards for the StageFright Community codebase. XML documentation provides:

- **IntelliSense Support**: IDE tooltips display documentation while typing
- **Go-to-Definition Context**: Developers see documentation when navigating to types/methods
- **External Documentation**: Automated tools extract XML comments to generate API documentation
- **Code Review Clarity**: Reviewers understand intent without reading implementation details
- **Maintenance**: Future developers understand purpose, parameters, and expected behavior

`StageFright.App.csproj` sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>` (with `CS1591`, "missing XML comment for publicly visible type or member", suppressed there since `StageFright.App` is a composition-root project with few public APIs) — the library projects (`StageFright.Core`, `StageFright.Data`, `StageFright.UI`, `StageFright.Reports`, `StageFright.Plugins.Contracts`) are where this standard is enforced in practice.

## File Organization (Constitution §3.2.1, §4.5)

Before writing XML documentation, every new class, interface, record, struct, or enum MUST be placed in its own dedicated file. This is a **mandatory, non-negotiable requirement** (see `CONTRIBUTING.md` and `docs/ARCHITECTURE.md` for detailed rules).

**File Organization Rules**:
- ✅ One type per file (exceptions: private nested types, compiler-generated types)
- ✅ File name matches type name exactly (e.g., `MemberService.cs` for `class MemberService`)
- ✅ Types separated by responsibility:
  - Service interface separate from service implementation (interfaces live in `StageFright.Core/Contracts/`; implementations in `StageFright.Core/Modules/<ModuleName>/`)
  - DTOs separate from domain entities
  - Request/response models in their own files
  - Enums and value objects in dedicated files

**Why This Matters**:
- Prevents accidental violations of Single Responsibility Principle
- Makes XML documentation more meaningful (one type per file = one clear responsibility)
- Improves IDE navigation (Go to Definition goes to the right file)
- Reduces merge conflicts in version control

**Code Review Consequence**: Any PR with multiple types in a single file will be rejected during review. This is non-negotiable.

## Enum Organization

**Enum Placement**: All common enums (used across features or core business logic) belong in `StageFright.Core.Enums`. Enums are considered foundational, cross-cutting types like entities and should be centralized to:
- Prevent duplication and inconsistency
- Enable easy import across the application
- Establish a single source of truth for all enumeration types

**Current Core Enums** (`StageFright.Core/Enums/`, 13 total):
- `AccountType` — chart-of-accounts classification (replaced the old `CategoryType`/`Category` model entirely — see the `ConvertCategoriesToAccounts` migration)
- `AuditAction` — Create/Update/Delete audit actions
- `FeeType` — fee classification (annual, attendance, other)
- `JournalEntryType` — general-journal entry classification
- `MemberStatus` — Active/Inactive participation status
- `PaymentMethod` — Cash/Check/Card/ElectronicTransfer/Other
- `PaymentType` — payment classification
- `PlatformThemePreference` — device-level theme preference source
- `ReconciliationStatus` — bank reconciliation workflow state
- `ReportColumnAlignment` — report-rendering column alignment
- `ReportFilterType` — report filter input type
- `TaxCode` — generic sales-tax code (spec `016-generic-sales-tax`)
- `Theme` — Dark/Light UI theme

## Mandatory Requirements

### Scope: ALWAYS Document These

#### 1. Public Classes and Structs

**REQUIRED**: Every public class and struct MUST have a summary documenting:
- What the class represents (in one sentence)
- When/why to use it (context)
- Key responsibility (if related to SOLID principles)

```csharp
/// <summary>
/// A member of the performing arts group. Supports Active/Inactive status transitions
/// and soft-delete (archival). Financial history is retained on archive.
/// </summary>
public class Member
{
    // Implementation
}
```

#### 2. Public Interfaces

**REQUIRED**: Every public interface MUST have a summary describing the contract.

```csharp
/// <summary>
/// Defines the contract for Member repository operations including CRUD, status filtering,
/// and historical queries by effective date.
/// </summary>
public interface IMemberRepository : IRepository<Member>
{
    // Members
}
```

#### 3. Public Methods and Constructors

**REQUIRED**: Every public method/constructor MUST document:
- **`<summary>`**: What the method does (verb phrase)
- **`<param>`**: Each parameter's purpose and constraints
- **`<returns>`**: Return value including null/empty/error cases
- **`<exception cref="..."/>`**: Each exception that can be thrown

```csharp
/// <summary>
/// Retrieves all unpaid fees for a specific member, ordered by fee date (oldest first).
/// Used for FIFO payment allocation and Member Account Summary reporting.
/// </summary>
/// <param name="memberId">The member's unique identifier (non-empty GUID).</param>
/// <returns>
/// An enumerable of unpaid Fee records. Returns an empty collection if the member has no unpaid fees.
/// Does not include archived fees. Ordered by FeeDate ascending (oldest first).
/// </returns>
/// <exception cref="DataAccessException">
/// Thrown when database query fails (connection lost, corrupted data, permission denied).
/// Check inner exception for underlying database error details.
/// </exception>
/// <remarks>
/// FIFO allocation requires ordering by FeeDate, not by creation time.
/// Fees are immutable; this query returns a snapshot at query time.
/// </remarks>
public async Task<IEnumerable<Fee>> GetUnpaidAsync(Guid memberId)
{
    // Implementation
}
```

#### 4. Public Properties and Indexers

**REQUIRED**: Every public property MUST document:
- **`<summary>`**: What the property represents
- **`<value>`**: Type and constraints on valid values
- **Nullability**: When value can be null

```csharp
/// <summary>Given name of the member.</summary>
/// <value>Required, non-empty string, max 100 characters.</value>
public string FirstName { get; set; } = string.Empty;

/// <summary>Optional phone number.</summary>
/// <value>Format validated when provided; null when not supplied.</value>
public string? Phone { get; set; }
```

#### 5. Public Enums and Enum Values

**REQUIRED**: Every enum and enum value MUST have a summary.

```csharp
/// <summary>
/// Pre-set size a dashboard tile can render at. <see cref="OneByOne"/> is the default,
/// matching every tile's current rendered size.
/// </summary>
public enum DashboardTileSize
{
    /// <summary>1 column x 1 row (default).</summary>
    OneByOne,

    /// <summary>2 columns x 1 row (double width).</summary>
    OneByTwo,

    /// <summary>1 column x 2 rows (double height).</summary>
    TwoByOne,

    /// <summary>2 columns x 2 rows (double width and height).</summary>
    TwoByTwo
}
```

#### 6. Public Constants

**REQUIRED**: Every public constant MUST have a summary.

```csharp
/// <summary>Default maximum age range (in years) for member age validation.</summary>
private const int DefaultMaxAge = 150;
```

### Scope: OPTIONAL (Recommended but Not Enforced)

- **Internal/Private Methods**: Recommended for complex algorithms; not required.
  - Use regular `//` comments for implementation notes
- **Test Code**: Test class public test methods should have a summary; test setup/arrange/act/assert do not require XML comments
- **Auto-Properties with Obvious Names**: May use minimal summary if purpose is self-evident

### Scope: NOT DOCUMENTED (No XML Comments Required)

- Method implementations, local variables, loop bodies
- Compiler-generated code (`partial` implementations, code-behind)
- Private fields with obvious names (use `//` comment if needed for clarity)
- `.razor.cs` code-behind's Blazor lifecycle overrides (`OnInitializedAsync`, `OnParametersSet`, etc.) when behavior is the framework default

## Format and Content Guidelines

### Structure: Use Only These XML Tags

| Tag | Purpose | Required? |
|-----|---------|-----------|
| `<summary>` | Brief description (1-3 sentences) | **ALWAYS** |
| `<param>` | Parameter description | For all public methods |
| `<returns>` | Return value description | For all methods returning non-void |
| `<value>` | Property value description | For public properties |
| `<exception cref="..."/>` | Exception that can be thrown | For all exceptions thrown |
| `<remarks>` | Extended explanation, usage patterns | OPTIONAL (use sparingly) |
| `<example>` | Code example | OPTIONAL (for complex APIs) |
| `<see cref="..."/>` | Link to related type/member | OPTIONAL (create IDE links) |
| `<seealso cref="..."/>` | See also (related type/member) | OPTIONAL |
| `<typeparam>` | Generic type parameter | For generic types/methods |
| `<inheritdoc />` | Inherit documentation from base class | When behavior unchanged |

### Content Guidelines

#### 1. Summary: Concise and Specific

**GOOD** ✅:
- `/// <summary>Retrieves all unpaid annual fees for the current year.</summary>`
- `/// <summary>Applies a payment toward member's outstanding balance using FIFO allocation.</summary>`

**BAD** ❌:
- `/// <summary>Gets fees.</summary>` (too vague)
- `/// <summary>This method retrieves unpaid annual fees for the current year.</summary>` (redundant "This method")

#### 2. Parameters: Describe Constraints and Context

**GOOD** ✅:
```csharp
/// <param name="memberId">The member's unique identifier (non-null GUID).</param>
/// <param name="fromDate">Start of query range, inclusive.</param>
/// <param name="toDate">End of query range, inclusive. Must be >= fromDate.</param>
```

**BAD** ❌:
```csharp
/// <param name="memberId">id</param>
```

#### 3. Return Values: Include Edge Cases

**GOOD** ✅:
```csharp
/// <returns>
/// Enumerable of Payment records for the specified member within the date range.
/// Returns empty collection if no payments found.
/// Records ordered by date descending (most recent first).
/// </returns>
```

#### 4. Exceptions: Explain When/Why Thrown

Every custom exception in `StageFright.Core/Exceptions/` shares the same constructor shape — `(message, entityType, operationContext, entityId = null, innerException = null)` — so document what drives `message`, not the constructor plumbing:

```csharp
/// <exception cref="ValidationException">
/// Thrown when the calculated age is below <c>minimumMemberAge</c> or exceeds
/// <c>maxAgeRangeYears</c>. Both bounds come from application Settings.
/// </exception>
/// <exception cref="DataAccessException">
/// Thrown when the database query fails due to connection loss or corrupted data.
/// Check inner exception details for root cause.
/// </exception>
```

**BAD** ❌:
```csharp
/// <exception cref="Exception">Something went wrong.</exception>
```

#### 5. Remarks: Extended Explanation (Use Sparingly)

Use `<remarks>` for implementation notes, architectural decisions, related methods that should be called together, or caveats/gotchas.

**GOOD** ✅:
```csharp
/// <remarks>
/// FIFO allocation requires ordering by FeeDate. This method returns fees in
/// chronological order (oldest first) to support payment allocation logic.
/// </remarks>
```

### Special Cases

#### Case 1: Inheritance and Overrides

```csharp
/// <inheritdoc />
/// <remarks>This override includes archived members in the query results.</remarks>
public override async Task<IEnumerable<Member>> GetAllAsync()
{
    // Implementation
}
```

#### Case 2: Generic Types/Methods

```csharp
/// <summary>
/// Generic IRepository implementation. Translates EF Core exceptions to domain exceptions
/// before they leave the DAL boundary.
/// </summary>
/// <typeparam name="TEntity">The domain entity type managed by this repository.</typeparam>
public class BaseRepository<TEntity> : IRepository<TEntity> where TEntity : class
{
}
```

#### Case 3: Nullable Reference Types

```csharp
/// <summary>Optional member notes.</summary>
/// <value>Notes as a string, or null if no notes provided.</value>
public string? Notes { get; set; }
```

#### Case 4: Async Methods

Mention async behavior in `<remarks>` only if not obvious from the `Task`/`Task<T>` return type and method name.

## Known Pitfall: Misplaced Comments Break the Build

A misplaced `///` doc comment throws a compiler error, not a warning — this has bitten the codebase before (see `CLAUDE.md`'s Known Gotchas):

- A `///` comment attached to one parameter inside a multi-line record's positional-parameter list, or one containing a bare `&`, throws `CS1587`/`CS1570`.
- Adding even one `<param>` tag to a member's doc comment makes the compiler require `<param>` tags for **all** of that member's parameters (`CS1573`). For a record/method with many parameters, a plain `//` comment next to the parameter you actually want to explain is simpler than fully documenting every parameter just to satisfy the compiler.

## Code Review Checklist

Reviewers MUST verify:

- [ ] All public classes have `<summary>` documenting purpose
- [ ] All public methods have `<summary>`, `<param>` for all parameters, `<returns>`, and `<exception cref="..."/>`
- [ ] All public properties have `<summary>` and `<value>`
- [ ] All enum values have `<summary>`
- [ ] Exceptions documented match actual exceptions thrown in implementation
- [ ] Parameter descriptions are accurate and include constraints
- [ ] Return value descriptions include null/empty cases
- [ ] Summary is concise (1-3 sentences) and specific
- [ ] No stale/outdated documentation (matches current implementation)
- [ ] No obvious facts repeated (avoid "This method" preamble)
- [ ] No partial `<param>` tags on a multi-parameter member (all-or-nothing, per CS1573 above)

**Rejection Criteria**: Code MUST NOT merge if:
- Public types lack `<summary>`
- Public methods lack `<summary>`, `<param>`, `<returns>`, or `<exception cref="..."/>`
- Documentation is inaccurate or contradicts implementation

## IDE Integration and Tools

### Visual Studio / VS Code

- **IntelliSense Tooltips**: As you type, XML comments appear in IDE tooltips
- **Go-to-Definition**: Press F12 or Ctrl-Click to see method documentation
- **Quick Info**: Hover over a symbol to see documentation pop-up

### Documentation Generation

Use `docfx` to generate static HTML documentation from XML comments:

```bash
docfx docfx.json
```

## Examples: Complete Annotated Code

### Example 1: Service Class with Validation (real code — `StageFright.Core/Modules/Members/AgeCalculationService.cs`)

```csharp
/// <summary>
/// Calculates a member's age in completed years.
/// Handles Feb-29 birthdays in non-leap years by treating Mar 1 as the anniversary.
/// </summary>
public class AgeCalculationService
{
    /// <summary>
    /// Returns age in whole years, or null when dob is null.
    /// </summary>
    public int? Calculate(DateTime? dob, DateTime today)
    {
        // Implementation
    }

    /// <summary>
    /// Validates a date-of-birth value against system constraints.
    /// No-ops when dob is null (DOB is optional).
    /// </summary>
    public void ValidateDateOfBirth(DateTime? dob, DateTime today, int maxAgeRangeYears, int minimumMemberAge)
    {
        // Throws ValidationException("Member", nameof(ValidateDateOfBirth)) on any constraint violation
    }
}
```

Note the deliberately minimal `<param>` usage here — per the CS1573 pitfall above, a method with several parameters and a self-explanatory summary is documented with prose in the summary rather than forcing every parameter into its own `<param>` tag.

### Example 2: Repository Interface and Implementation

```csharp
/// <summary>
/// Defines the contract for Payment repository operations.
/// Payments are immutable financial records linked to GL transactions.
/// </summary>
public interface IPaymentRepository : IRepository<Payment>
{
    /// <summary>
    /// Retrieves all payments for a specific member.
    /// </summary>
    /// <param name="memberId">The member's unique identifier (non-empty GUID).</param>
    /// <returns>
    /// Enumerable of Payment records ordered by date descending (most recent first).
    /// Returns empty collection if member has no payments.
    /// </returns>
    /// <exception cref="DataAccessException">Thrown when database query fails.</exception>
    Task<IEnumerable<Payment>> GetByMemberAsync(Guid memberId);
}

/// <summary>
/// Repository implementation for Payment entity.
/// Enforces immutability: financial records are never edited or deleted after creation.
/// All payments are linked to GL transaction pairs for accounting integrity.
/// </summary>
public class PaymentRepository : BaseRepository<Payment>, IPaymentRepository
{
    /// <inheritdoc />
    public async Task<IEnumerable<Payment>> GetByMemberAsync(Guid memberId)
    {
        // Implementation
    }
}
```

## Common Mistakes to Avoid

| Mistake | Example | Problem | Fix |
|---------|---------|---------|-----|
| **Missing parameter docs** | `/// <summary>Creates a member.</summary> public void Create(string name, string address) { }` | Reviewers don't know what `name` and `address` are for | Add `<param>` tags for all parameters |
| **Vague summaries** | `/// <summary>Gets data.</summary>` | Unhelpful to developers | Be specific: "Retrieves all unpaid fees for a member" |
| **Inaccurate docs** | Documentation says "returns null if not found" but code returns empty collection | Misleading; causes bugs | Update docs to match implementation |
| **Stale docs** | Method refactored but docs not updated | Developers follow wrong guidance | Always update docs when changing behavior |
| **"This" preamble** | `/// <summary>This method creates a member.</summary>` | Redundant with method name | Remove: "Creates a member." is sufficient |
| **Missing exceptions** | Method throws `ValidationException` but not documented | Callers don't know to handle exception | Document all exceptions with `<exception cref="..."/>` |
| **Partial `<param>` tags** | One `<param>` added to a 4-parameter method | Triggers CS1573 for the other three | Either document all parameters or use prose in `<summary>`/`//` instead |
| **Repeating obvious** | `/// <summary>Gets the ID.</summary> public Guid Id { get; }` | Wastes reviewer time; obvious from property name | Acceptable for property; focus on complex types |

## References

- [Microsoft: XML Documentation Comments (C#)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/)
- [Recommended XML Documentation Tags](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags)
- [IntelliSense from Code Comments](https://learn.microsoft.com/en-us/visualstudio/ide/create-xml-documentation-comments)
- [ARCHITECTURE.md](ARCHITECTURE.md) — exception hierarchy and layer boundaries these comments document

---

**Document Version**: 1.1
**Last Updated**: 2026-08-22
**Status**: Active (mandatory from project start)
