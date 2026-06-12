namespace StageFright.Core.Exceptions;

/// <summary>
/// Thrown when a backup import file is invalid, incomplete, version-incompatible, or corrupted.
/// </summary>
public sealed class ImportException : Exception
{
    /// <summary>The entity type or section involved in the import failure.</summary>
    public string EntityType { get; }

    /// <summary>The entity identifier, if the failure concerns a specific record.</summary>
    public Guid? EntityId { get; }

    /// <summary>Description of the import step that failed.</summary>
    public string OperationContext { get; }

    /// <summary>UTC timestamp of the failure.</summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>Correlation identifier for cross-layer tracing.</summary>
    public Guid CorrelationId { get; } = Guid.NewGuid();

    public ImportException(string message, string entityType = "Backup", string operationContext = "Import", Guid? entityId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EntityType = entityType;
        EntityId = entityId;
        OperationContext = operationContext;
    }
}
