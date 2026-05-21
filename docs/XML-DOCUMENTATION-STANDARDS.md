---
title: StageFright Community — XML Documentation Standards
author: Architecture Team
date: 2026-05-19
version: 1.0
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

## File Organization (Constitution §3.2.1, §4.5)

Before writing XML documentation, every new class, interface, record, struct, or enum MUST be placed in its own dedicated file. This is a **mandatory, non-negotiable requirement** (see CONTRIBUTING.md and docs/ARCHITECTURE.md for detailed rules).

**File Organization Rules**:
- ✅ One type per file (exceptions: private nested types, compiler-generated types)
- ✅ File name matches type name exactly (e.g., `MemberService.cs` for `class MemberService`)
- ✅ Types separated by responsibility:
  - Service interface separate from service implementation
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

**Current Core Enums**:
- `MemberStatus` — Active/Inactive participation status
- `CategoryType` — Income/Expense classification
- `FeeType` — Annual/Attendance/Other
- `PaymentMethod` — Cash/Check/Card/ElectronicTransfer/Other
- `PaymentType` — Annual/Attendance/Other
- `Theme` — Dark/Light UI theme
- `AuditAction` — Create/Update/Delete audit actions

## Mandatory Requirements

### Scope: ALWAYS Document These

#### 1. Public Classes and Structs

**REQUIRED**: Every public class and struct MUST have a summary documenting:
- What the class represents (in one sentence)
- When/why to use it (context)
- Key responsibility (if related to SOLID principles)

```csharp
/// <summary>
/// Provides repository operations for Member entities, including create, read, update, and soft-delete.
/// Implements filtering by member status (Active/Inactive/Archived) and effective date-based queries
/// for historical reporting.
/// </summary>
public class MemberRepository : BaseRepository<Member>, IMemberRepository
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
/// <exception cref="ArgumentNullException">Thrown when memberId is null or empty GUID.</exception>
/// <exception cref="DataAccessException">
/// Thrown when database query fails (connection lost, corrupted data, permission denied).
/// Check inner exception for underlying database error details.
/// </exception>
/// <remarks>
/// FIFO allocation requires ordering by FeeDate, not by creation time.
/// Fees are immutable (per FR-005); this query returns a snapshot at query time.
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
/// <summary>
/// Gets or sets the member's full legal name (required).
/// Used for display, reports, and audit trails.
/// </summary>
/// <value>
/// The member's name as a non-empty string (max 255 characters).
/// Must not be null or whitespace when the member entity is persisted to the database.
/// </value>
public string Name
{
    get => _name ?? string.Empty;
    set => _name = value?.Trim() ?? string.Empty;
}

/// <summary>
/// Gets the member's calculated age in years.
/// </summary>
/// <value>
/// Age derived from DateOfBirth using floor((today - DOB) / 365.25).
/// Returns null if DateOfBirth is not set.
/// Always positive; negative ages are impossible.
/// </value>
public int? Age => DateOfBirth.HasValue ? CalculateAge(DateOfBirth.Value) : null;
```

#### 5. Public Enums and Enum Values

**REQUIRED**: Every enum and enum value MUST have a summary.

```csharp
/// <summary>
/// Represents a member's participation status in the organization.
/// Status is separate from archival (IsDeleted flag) per Constitution §3.5.
/// </summary>
public enum MemberStatus
{
    /// <summary>Member is actively participating. Fees apply (annual + attendance).</summary>
    Active = 0,

    /// <summary>Member exists but is not participating. No fees accrue.</summary>
    Inactive = 1
}
```

#### 6. Public Constants

**REQUIRED**: Every public constant MUST have a summary.

```csharp
/// <summary>Default maximum age range (in years) for member age validation.</summary>
private const int DefaultMaxAge = 150;

/// <summary>Payment method: cash payments (hand-counted tender).</summary>
public const string PaymentMethodCash = "Cash";
```

### Scope: OPTIONAL (Recommended but Not Enforced)

- **Internal/Private Methods**: Recommended for complex algorithms; not required.
  - Use regular `//` comments for implementation notes
- **Test Code**: Test class public test methods should have summary; test setup/arrange/act/assert do not require XML comments
- **Auto-Properties with Obvious Names**: May use minimal summary if purpose is self-evident
  - Example: `public string Email { get; set; }` might use simple summary without elaborate examples

### Scope: NOT DOCUMENTED (No XML Comments Required)

- Method implementations, local variables, loop bodies
- Compiler-generated code (`partial` implementations, code-behind)
- Private fields with obvious names (use `//` comment if needed for clarity)

## Format and Content Guidelines

### Structure: Use Only These XML Tags

Use these standard XML comment tags:

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
- `/// <summary>Validates member age against configured minimum age (default 0 years).</summary>`
- `/// <summary>Applies a payment toward member's outstanding balance using FIFO allocation.</summary>`

**BAD** ❌:
- `/// <summary>Gets fees.</summary>` (too vague)
- `/// <summary>This method retrieves unpaid annual fees for the current year.</summary>` (redundant "This method")
- `/// <summary>Retrieves all unpaid annual fees for the current year. Also checks if the member is active.</summary>` (explain too much; keep to one sentence)

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
/// <param name="fromDate">date</param>
/// <param name="toDate">date</param>
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

**BAD** ❌:
```csharp
/// <returns>Payment records.</returns>
```

#### 4. Exceptions: Explain When/Why Thrown

**GOOD** ✅:
```csharp
/// <exception cref="ValidationException">
/// Thrown when member age (calculated from DateOfBirth) is below the configured
/// Minimum Member Age in Settings. Use GetSettingsAsync() to check minimum before
/// attempting to create the member.
/// </exception>
/// <exception cref="DataAccessException">
/// Thrown when database query fails due to connection loss or corrupted data.
/// Check inner exception details for root cause.
/// </exception>
```

**BAD** ❌:
```csharp
/// <exception cref="Exception">Something went wrong.</exception>
```

#### 5. Remarks: Extended Explanation (Use Sparingly)

Use `<remarks>` for:
- Implementation notes or performance characteristics
- Architectural decisions affecting the API
- Related methods that should be called together
- Caveats or gotchas

**GOOD** ✅:
```csharp
/// <remarks>
/// FIFO allocation requires ordering by FeeDate. This method returns fees in
/// chronological order (oldest first) to support payment allocation logic.
/// See also: Payment.AllocateAsync() which consumes this collection.
/// </remarks>
```

**BAD** ❌:
```csharp
/// <remarks>This retrieves the fees.</remarks>
```

### Special Cases

#### Case 1: Inheritance and Overrides

When overriding a method, decide:
- **If behavior is identical to base class**: Use `<inheritdoc />`
- **If behavior is overridden**: Document the override-specific behavior

```csharp
/// <inheritdoc />
/// <remarks>This override includes archived members in the query results.</remarks>
public override async Task<IEnumerable<Member>> GetAllAsync()
{
    // Implementation
}
```

#### Case 2: Generic Types/Methods

Document type parameters:

```csharp
/// <summary>
/// Base repository providing generic CRUD operations for domain entities.
/// </summary>
/// <typeparam name="TEntity">The domain entity type managed by this repository.</typeparam>
public abstract class BaseRepository<TEntity> : IRepository<TEntity> where TEntity : class
{
}
```

#### Case 3: Nullable Reference Types

Use nullable annotations in summary:

```csharp
/// <summary>
/// Gets or sets optional member notes.
/// </summary>
/// <value>Notes as a string, or null if no notes provided.</value>
public string? Notes { get; set; }
```

#### Case 4: Async Methods

Mention async behavior in remarks if not obvious:

```csharp
/// <summary>
/// Asynchronously retrieves all unpaid fees for a member from the database.
/// </summary>
/// <remarks>This method is async and must be awaited to retrieve data from the database.</remarks>
public async Task<IEnumerable<Fee>> GetUnpaidAsync(Guid memberId)
{
    // Implementation
}
```

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

**Rejection Criteria**: Code MUST NOT merge if:
- Public types lack `<summary>`
- Public methods lack `<summary>`, `<param>`, `<returns>`, or `<exception cref="..."/>`
- Documentation is inaccurate or contradicts implementation

## IDE Integration and Tools

### Visual Studio / VS Code

- **IntelliSense Tooltips**: As you type, XML comments appear in IDE tooltips
- **Go-to-Definition**: Press F12 or Ctrl-Click to see method documentation
- **Quick Info**: Hover over a symbol to see documentation pop-up

### StyleCop Analyzers (Optional)

StyleCop.Analyzers can be configured to warn on missing XML documentation:

```
SA1600: Elements should be documented
SA1601: Partial elements should be documented
SA1602: Enumeration items should be documented
```

Configure in `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.SA1600.severity = warning
```

### Documentation Generation

Use `docfx` to generate static HTML documentation from XML comments:

```bash
docfx docfx.json
```

Outputs professional API documentation suitable for public release.

## Examples: Complete Annotated Code

### Example 1: Service Class with Validation

```csharp
/// <summary>
/// Provides age calculation and validation services for member registration.
/// Enforces age range constraints and reports validation errors clearly.
/// </summary>
public class AgeCalculationService
{
    private const int DefaultMaxAge = 150;
    private const int DefaultMinAge = 0;

    /// <summary>
    /// Calculates a person's age in years based on their date of birth.
    /// </summary>
    /// <param name="dateOfBirth">
    /// The person's date of birth (must be a past date, typically at least 1-2 days ago).
    /// </param>
    /// <param name="maxAgeRange">
    /// Maximum allowed age in years (default 150). Throws if calculated age exceeds this value.
    /// </param>
    /// <param name="minAge">
    /// Minimum required age in years (default 0). Throws if calculated age is below this value.
    /// </param>
    /// <returns>
    /// Calculated age in years as an integer. Uses formula: floor((today - DOB) / 365.25).
    /// </returns>
    /// <exception cref="ValidationException">
    /// Thrown if calculated age is below minAge or exceeds maxAgeRange.
    /// Exception message is specific: "Age {calculated_age} is below minimum required age {minAge}."
    /// </exception>
    public int CalculateAge(DateTime dateOfBirth, int maxAgeRange = DefaultMaxAge, int minAge = DefaultMinAge)
    {
        var today = DateTime.Today;
        var age = today.Year - dateOfBirth.Year;

        if (dateOfBirth.Date > today.AddYears(-age))
            age--;

        if (age < minAge)
            throw new ValidationException($"Age {age} is below minimum required age {minAge}.");

        if (age > maxAgeRange)
            throw new ValidationException($"Age {age} exceeds maximum allowed age range {maxAgeRange}.");

        return age;
    }
}
```

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

    /// <summary>
    /// Updates only the Notes field on an existing payment.
    /// Amount, Date, PaymentMethod, PaymentType, and Category fields are immutable and locked.
    /// </summary>
    /// <param name="paymentId">The payment's unique identifier.</param>
    /// <param name="notes">New notes text (may be empty or null).</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the payment record is not found in the database.
    /// </exception>
    /// <exception cref="DataAccessException">Thrown when database update fails.</exception>
    /// <remarks>
    /// Modifying notes triggers an UpdatedAt timestamp update.
    /// If UpdatedAt differs from CreatedAt, only Notes was modified (audit trail indicator).
    /// </remarks>
    Task UpdateNotesAsync(Guid paymentId, string notes);
}

/// <summary>
/// Repository implementation for Payment entity.
/// Enforces immutability: only Notes field can be edited after creation.
/// All payments are linked to GL transaction pairs for accounting integrity.
/// </summary>
public class PaymentRepository : BaseRepository<Payment>, IPaymentRepository
{
    /// <inheritdoc />
    public async Task<IEnumerable<Payment>> GetByMemberAsync(Guid memberId)
    {
        return await _dbSet
            .Where(p => p.MemberId == memberId)
            .OrderByDescending(p => p.Date)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task UpdateNotesAsync(Guid paymentId, string notes)
    {
        var payment = await GetByIdAsync(paymentId);
        if (payment == null)
            throw new InvalidOperationException($"Payment with ID {paymentId} not found.");

        payment.Notes = notes;
        payment.UpdatedAt = DateTime.UtcNow;
        await UpdateAsync(payment);
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
| **Repeating obvious** | `/// <summary>Gets the ID.</summary> public Guid Id { get; }` | Wastes reviewer time; obvious from property name | Acceptable for property; focus on complex types |

## References

- [Microsoft: XML Documentation Comments (C#)](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/namespaces)
- [Recommended XML Documentation Tags](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags)
- [IntelliSense from Code Comments](https://learn.microsoft.com/en-us/visualstudio/ide/create-xml-documentation-comments)

---

**Document Version**: 1.0  
**Last Updated**: 2026-05-19  
**Status**: Active (mandatory from project start)
