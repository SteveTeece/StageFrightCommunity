namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>
/// Outcome of the post-write self-check performed when a backup file is created (data-model §6).
/// <see cref="BackupService"/> only ever returns this with <see cref="Passed"/> =
/// <see langword="true"/>; a verification failure throws
/// <see cref="Core.Exceptions.BackupVerificationException"/> instead, so a UI drives its failure
/// state from the caught exception's discrepancies, not from a <c>Passed = false</c> result.
/// </summary>
/// <param name="Passed">True only when the file was read back and every record-type count matched.</param>
/// <param name="Discrepancies">Human-readable mismatches; empty when <paramref name="Passed"/> is true.</param>
/// <param name="FilePath">The written backup file.</param>
public sealed record BackupVerificationResult(bool Passed, IReadOnlyList<string> Discrepancies, string FilePath);
