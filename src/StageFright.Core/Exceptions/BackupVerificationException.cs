namespace StageFright.Core.Exceptions;

/// <summary>
/// Thrown when a freshly-created backup file fails its post-write self-check — it cannot be read
/// back, its recorded statistics are internally inconsistent, or a per-record-type count does not
/// match the source data (FR-021–FR-024). The file is left on disk for diagnosis and MUST NOT be
/// relied upon.
/// </summary>
public sealed class BackupVerificationException : Exception
{
    /// <summary>Human-readable description of every read-back / count mismatch found.</summary>
    public IReadOnlyList<string> Discrepancies { get; }

    /// <summary>The written backup file (kept on disk even though verification failed).</summary>
    public string FilePath { get; }

    /// <summary>UTC timestamp of the failure.</summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>Correlation identifier for cross-layer tracing.</summary>
    public Guid CorrelationId { get; } = Guid.NewGuid();

    public BackupVerificationException(
        string message,
        string filePath,
        IReadOnlyList<string> discrepancies,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FilePath = filePath;
        Discrepancies = discrepancies;
    }
}
