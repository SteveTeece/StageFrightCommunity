namespace StageFright.Core.Exceptions;

/// <summary>
/// Thrown when an optimistic concurrency conflict is detected (e.g., EF Core DbUpdateConcurrencyException).
/// </summary>
public sealed class ConcurrencyException : Exception
{
    /// <summary>The entity type involved in the concurrency conflict.</summary>
    public string EntityType { get; }

    /// <summary>The entity identifier that experienced the conflict.</summary>
    public Guid? EntityId { get; }

    /// <summary>Description of the operation that caused the conflict.</summary>
    public string OperationContext { get; }

    /// <summary>UTC timestamp of the failure.</summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>Correlation identifier for cross-layer tracing.</summary>
    public Guid CorrelationId { get; } = Guid.NewGuid();

    public ConcurrencyException(string message, string entityType, string operationContext, Guid? entityId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EntityType = entityType;
        EntityId = entityId;
        OperationContext = operationContext;
    }
}
