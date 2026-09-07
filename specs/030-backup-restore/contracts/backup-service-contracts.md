# Contract — Backup service & seams (C#)

Namespaces: `StageFright.Core.Contracts`, `StageFright.Core.Exceptions`, `StageFright.Core.Modules.Settings`, `StageFright.Core.Modules.Settings.Backup`, `StageFright.Core.Enums`.

---

## `IBackupService` (changed)

```csharp
public interface IBackupService
{
    /// Serialises a full snapshot to a temp/working stream, then hands it to the
    /// destination picker (native Save As, pre-filled with the FR-010 default name),
    /// reads the written file back from disk, verifies its per-record-type counts
    /// against the just-captured live data and the file's own recorded counts,
    /// writes an AuditAction.Export audit entry, and returns the result.
    /// Throws BackupVerificationException if the read-back or any count check fails
    /// (the file is left on disk for diagnosis). Throws OperationCanceledException
    /// if the user cancels the Save dialog. Throws DataAccessException on write/IO failure
    /// (no partial file left behind).
    Task<BackupVerificationResult> CreateBackupAsync(CancellationToken ct = default);

    /// Unchanged signature. Now also: rejects a file whose SchemaVersion is newer than
    /// BackupSchema.CurrentSchemaVersion on any component (incl. a newer same-major build);
    /// writes the pre-restore recovery copy to IRecoveryCopyStore's directory for EVERY
    /// restore (first-run included); upserts the three new entity types.
    /// Throws ImportException on corrupt/incomplete/incompatible file (DB untouched).
    Task ImportAsync(string filePath, CancellationToken ct = default);

    /// Unchanged. Reads + validates a file without touching the DB; returns counts,
    /// creation date, originating version for the restore-confirmation summary.
    Task<BackupManifest> GetManifestAsync(string filePath, CancellationToken ct = default);
}
```

Notes:
- The pre-existing `Task ExportAsync(string filePath, CancellationToken ct)` is retained (used internally by `ImportAsync` to write the recovery copy, and by tests). It now also throws `BackupVerificationException` when its own read-back fails, so a recovery copy is never silently bad. `CreateBackupAsync` is the user-facing entry point that adds the Save dialog + audit.
- `GetManifestAsync` and `ImportAsync` share the count-computation helper with `CreateBackupAsync`'s verification so the restore-confirmation numbers and the post-write-check numbers are produced by one code path (FR-022).

## `IBackupRepository` (changed — doc only, signatures stable)

```csharp
public interface IBackupRepository
{
    /// Now also reads the three added collections — JournalEntries (no query filter),
    /// BankReconciliations and ReconciliationLines (both IgnoreQueryFilters — archived
    /// drafts included).
    Task<BackupSnapshot> GetFullSnapshotAsync(CancellationToken ct = default);

    /// PK-upsert of every collection in the snapshot, inside the caller's IUnitOfWork
    /// transaction. FK-safe order (see data-model.md §5): JournalEntries before Transactions;
    /// Accounts + Transactions before BankReconciliations/ReconciliationLines;
    /// ReconciliationLines last before Settings/AuditTrailEntries.
    Task UpsertSnapshotAsync(BackupSnapshot snapshot, CancellationToken ct = default);
}
```

## `IBackupDestinationPicker` (new)

```csharp
namespace StageFright.Core.Contracts;

/// Native "Save As" seam. Implemented per-platform (MAUI). Never throws for a user
/// cancel — returns a Cancelled outcome. Any platform error is wrapped as
/// DataAccessException by the implementation before it crosses back into Core.
public interface IBackupDestinationPicker
{
    /// Shows the OS-native Save dialog pre-filled with <paramref name="suggestedFileName"/>
    /// (e.g. "Cool Choir backup 2026-09-07.sfbak"), writes <paramref name="content"/> to the
    /// chosen location, and returns the final absolute path — or Cancelled.
    Task<BackupDestinationResult> SaveAsync(string suggestedFileName, Stream content, CancellationToken ct = default);
}

/// One class per file. The positional flag is `WasCancelled`, not `Cancelled`, so it does not
/// collide with the static `Cancelled` factory property (CS0102).
public sealed record BackupDestinationResult(bool WasCancelled, string? FilePath)
{
    public static BackupDestinationResult Cancelled { get; } = new(true, null);
    public static BackupDestinationResult Saved(string path) => new(false, path);
}
```

MAUI implementation `MauiBackupDestinationPicker` (in `StageFright.App`) delegates to `CommunityToolkit.Maui.Storage.FileSaver.Default.SaveAsync(...)` and returns `result.FilePath` on `IsSuccessful`, `Cancelled` when the user dismisses the dialog.

## `IRecoveryCopyStore` (new)

```csharp
namespace StageFright.Core.Contracts;

/// Where BackupService.ImportAsync writes the pre-restore recovery copy (FR-015).
/// Matches the never-throw shape of ILanguagePreferenceStore.
public interface IRecoveryCopyStore
{
    /// An existing, writable directory for recovery-copy .sfbak files. Created on demand.
    string GetRecoveryDirectory();
}
```

MAUI implementation `MauiRecoveryCopyStore` returns `Path.Combine(FileSystem.AppDataDirectory, "recovery")` (directory created if missing). Tests supply a temp-dir fake.

---

## `BackupVerificationException` (new — `StageFright.Core/Exceptions/`)

```csharp
public sealed class BackupVerificationException : Exception
{
    public IReadOnlyList<string> Discrepancies { get; }
    public string FilePath { get; }
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public Guid CorrelationId { get; } = Guid.NewGuid();

    public BackupVerificationException(string message, string filePath,
        IReadOnlyList<string> discrepancies, Exception? innerException = null)
        : base(message, innerException) { FilePath = filePath; Discrepancies = discrepancies; }
}
```

Constructor shape matches the other custom exceptions (`ImportException`).

## `BackupVerificationResult` (new — `.../Backup/`)

`sealed record BackupVerificationResult(bool Passed, IReadOnlyList<string> Discrepancies, string FilePath)` — see `data-model.md` §6.

## `BackupSchema` (new — `StageFright.Core`, static)

```csharp
public static class BackupSchema
{
    public const string CurrentSchemaVersion = "1.2.0";

    /// false if fileVersion is null/empty/unparseable OR strictly greater than
    /// CurrentSchemaVersion on any semver component; true otherwise (equal or older).
    public static bool IsRestorable(string? fileVersion);
}
```

## `BackupFileNameBuilder` (new — `StageFright.Core.Modules.Settings`, static)

```csharp
public static class BackupFileNameBuilder
{
    public const string FallbackBaseName = "StageFright"; // when the org name is blank/whitespace

    /// Returns "<sanitised org> backup <yyyy-MM-dd>.sfbak".
    /// - the literal word "backup" is always present (Verbatim Constraint)
    /// - Path.GetInvalidFileNameChars() are removed; runs of whitespace collapse to one space; trimmed
    /// - a blank/whitespace org name yields "StageFright backup <yyyy-MM-dd>.sfbak"
    /// - date is formatted with CultureInfo.InvariantCulture "yyyy-MM-dd" (host-locale independent)
    public static string Build(string? organisationName, DateOnly date);
}
```

## `AuditAction` — values used (enum already defines both)

- `AuditAction.Export` — written by `BackupService` after a verified backup (FR-017). *Currently unused in the codebase; this feature is its first use.*
- `AuditAction.Import` — already written by `ImportAsync`; entry lands inside the restore transaction so it belongs to the restored dataset.
