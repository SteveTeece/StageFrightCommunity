namespace StageFright.Core.Exceptions;

/// <summary>
/// Thrown when a financial transaction is dated on or before the organisation's
/// <c>Settings.ClosedThroughDate</c> — a reported prior period must not be altered by a
/// back-dated entry (spec 028, FR-016 / FR-017). Raised at the GL choke point
/// (<c>GLRepository.AddBalancedSetAsync</c>) before any row is written, so the enclosing
/// unit-of-work transaction rolls back leaving no business row and no ledger line.
/// A distinct signal, not a subclass of <see cref="GLBalanceException"/>.
/// </summary>
public sealed class ClosedPeriodException : Exception
{
    /// <summary>The entity type being processed when the closed-period posting was rejected.</summary>
    public string EntityType { get; }

    /// <summary>The entity identifier involved in the rejected operation.</summary>
    public Guid? EntityId { get; }

    /// <summary>Description of the financial operation that was rejected.</summary>
    public string OperationContext { get; }

    /// <summary>UTC timestamp of the rejection.</summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>Correlation identifier for cross-layer tracing.</summary>
    public Guid CorrelationId { get; } = Guid.NewGuid();

    public ClosedPeriodException(string message, string entityType, string operationContext, Guid? entityId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EntityType = entityType;
        EntityId = entityId;
        OperationContext = operationContext;
    }
}
