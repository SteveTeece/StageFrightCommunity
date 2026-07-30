# Contract: Member Service (StageFright.Core → StageFright.UI boundary)

**Feature**: `011-member-firstname-lastname` | **Phase**: 1 (Design)

This is a desktop MAUI Blazor Hybrid application with no external network API — the relevant
"interface exposed to another system" is the internal module contract `IMemberService`
(`src/StageFright.Core/Contracts/IMemberService.cs`), which `StageFright.UI` (via DI) and other
modules (e.g. `StageFright.Reports` providers, `StageFright.Core/Modules/Finance`) depend on.
Per the plan-template guidance, this document captures that boundary's contract for this feature
rather than a REST/CLI schema, since none exists in this codebase.

## `IMemberService` — method signatures unchanged

```csharp
public interface IMemberService
{
    Task<Member> CreateAsync(CreateMemberRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateMemberRequest request, CancellationToken ct = default);
    Task<Member?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Member>> GetByStatusAsync(MemberStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<Member>> GetArchivedAsync(CancellationToken ct = default);
    Task InactivateAsync(Guid id, CancellationToken ct = default);
    Task ActivateAsync(Guid id, CancellationToken ct = default);
    Task ArchiveAsync(Guid id, CancellationToken ct = default);
    Task RestoreAsync(Guid id, CancellationToken ct = default);
}
```

No method is added, removed, or renamed. This feature only changes the **shape of the request
DTOs** two of these methods accept, and the **shape of the `Member` entity** every method returns.

## `CreateMemberRequest` — breaking DTO change

```csharp
// Before
public sealed record CreateMemberRequest
{
    public string Name { get; init; } = string.Empty;
    public string StreetAddress { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public DateTime JoinDate { get; init; }
    public DateTime? DateOfBirth { get; init; }
}

// After
public sealed record CreateMemberRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string StreetAddress { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public DateTime JoinDate { get; init; }
    public DateTime? DateOfBirth { get; init; }
}
```

**Preconditions** (enforced by `MemberValidationService.Validate(CreateMemberRequest, Settings)`):
- `FirstName` and `LastName` each non-null, non-whitespace, ≤ 100 characters after trim.
- All other precondition rules unchanged (street address required, email format when provided,
  date-of-birth range via `AgeCalculationService`).

**Postconditions**: `CreateAsync` returns a `Member` with `FirstName`/`LastName` set from the
trimmed request values, `Id` newly generated, `Status = Active`, and an audit-trail `Create`
entry logged (unchanged behavior).

## `UpdateMemberRequest` — breaking DTO change

Same shape change as `CreateMemberRequest` (`Name` → `FirstName` + `LastName`).

**Preconditions**: Same four checks as `CreateAsync`, applied via
`MemberValidationService.Validate(UpdateMemberRequest, Settings)`; target member must exist
(`EntityNotFoundException` otherwise, unchanged).

**Postconditions** (new behavior — closes an existing gap, see research.md Decision 8):
`UpdateAsync` now captures `oldFirstName`/`oldLastName` before mutation and logs them via
`oldValue:`/`newValue:` on the `AuditTrailService.LogAsync` call, where today it logs an `Update`
action with no old/new values at all.

## `Member` entity — response shape change

Every method returning `Member`/`IReadOnlyList<Member>` now returns entities with:
- `FirstName` (string, was part of `Name`)
- `LastName` (string, was part of `Name`)
- `FullName` (computed, new — `"{FirstName} {LastName}"`)
- `SortableFullName` (computed, new — `"{LastName}, {FirstName}"`)
- No `Name` property (removed).

Consumers (`StageFright.UI`, `StageFright.Reports` providers, `StageFright.Core/Modules/Finance`)
must be updated to stop referencing `Member.Name` and use `FullName`/`SortableFullName` per the
context table in [data-model.md](./../data-model.md)'s "Consumer inventory" section — this is a
compile-time-enforced contract change (removing `Name` breaks the build for any unmigrated call
site), which is the intended mechanism for ensuring no consumer is missed.

## Out of scope for this contract

- `IMemberRepository`/`MemberRepository` — no signature change (no `Name`-specific methods exist
  today to change; see research.md inventory).
- `IReportProvider` (`StageFright.Plugins.Contracts`) — no interface change; only the three
  concrete provider implementations' internal use of `Member.Name` changes, not the contract
  they implement.
