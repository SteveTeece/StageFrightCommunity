using Microsoft.EntityFrameworkCore.Storage;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;

namespace StageFright.Data;

/// <summary>
/// Wraps every financial operation in a database transaction.
/// On exception the transaction is rolled back before re-throwing.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly StageFrightDbContext _db;

    public UnitOfWork(StageFrightDbContext db)
    {
        _db = db;
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default)
    {
        IDbContextTransaction? tx = null;
        try
        {
            tx = await _db.Database.BeginTransactionAsync(ct);
            await operation(ct);
            await tx.CommitAsync(ct);
        }
        catch (GLBalanceException)
        {
            if (tx is not null) await SafeRollbackAsync(tx);
            throw;
        }
        catch (Exception ex) when (ex is not DataAccessException and not ValidationException)
        {
            if (tx is not null) await SafeRollbackAsync(tx);
            throw new DataAccessException(ex.Message, "UnitOfWork", nameof(ExecuteInTransactionAsync), null, ex);
        }
        catch
        {
            if (tx is not null) await SafeRollbackAsync(tx);
            throw;
        }
        finally
        {
            tx?.Dispose();
        }
    }

    private static async Task SafeRollbackAsync(IDbContextTransaction tx)
    {
        try { await tx.RollbackAsync(); } catch { /* best-effort */ }
    }
}
