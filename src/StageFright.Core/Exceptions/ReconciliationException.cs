namespace StageFright.Core.Exceptions;

/// <summary>
/// Thrown when a bank reconciliation operation violates the reconciliation lifecycle
/// (e.g. modifying or deleting a finalised reconciliation, finalising with a non-zero
/// difference, opening a second draft for the same account).
/// </summary>
public sealed class ReconciliationException : Exception
{
    /// <summary>The entity type being processed when the violation was detected.</summary>
    public string EntityType { get; }

    /// <summary>The entity identifier involved in the failed operation.</summary>
    public Guid? EntityId { get; }

    /// <summary>Description of the reconciliation operation that failed.</summary>
    public string OperationContext { get; }

    /// <summary>UTC timestamp of the failure.</summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>Correlation identifier for cross-layer tracing.</summary>
    public Guid CorrelationId { get; } = Guid.NewGuid();

    public ReconciliationException(string message, string entityType, string operationContext, Guid? entityId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EntityType = entityType;
        EntityId = entityId;
        OperationContext = operationContext;
    }
}
