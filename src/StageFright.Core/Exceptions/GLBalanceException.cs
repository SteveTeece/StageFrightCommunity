namespace StageFright.Core.Exceptions;

/// <summary>
/// Thrown when a GL transaction pair does not balance (Σdebits ≠ Σcredits).
/// Triggers immediate rollback of the enclosing unit-of-work transaction.
/// </summary>
public sealed class GLBalanceException : Exception
{
    /// <summary>The entity type being processed when the imbalance was detected.</summary>
    public string EntityType { get; }

    /// <summary>The entity identifier involved in the unbalanced operation.</summary>
    public Guid? EntityId { get; }

    /// <summary>Description of the financial operation that caused the imbalance.</summary>
    public string OperationContext { get; }

    /// <summary>UTC timestamp of the failure.</summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>Correlation identifier for cross-layer tracing.</summary>
    public Guid CorrelationId { get; } = Guid.NewGuid();

    public GLBalanceException(string message, string entityType, string operationContext, Guid? entityId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EntityType = entityType;
        EntityId = entityId;
        OperationContext = operationContext;
    }
}
