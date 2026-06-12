namespace StageFright.Core.Contracts;

/// <summary>
/// Unit-of-work contract for atomic financial operations.
/// Every financial workflow (fee creation, attendance batch, payment + FIFO allocation,
/// reactivation forgiveness, import) runs inside ExecuteInTransactionAsync.
/// GL imbalance throws GLBalanceException and triggers automatic rollback.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Executes the operation inside a database transaction. On any exception the transaction
    /// is rolled back before the exception propagates.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default);
}
