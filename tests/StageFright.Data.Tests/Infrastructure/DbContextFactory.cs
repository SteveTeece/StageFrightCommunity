using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StageFright.Data;

namespace StageFright.Data.Tests.Infrastructure;

/// <summary>
/// Creates StageFrightDbContext instances backed by a shared SQLite in-memory connection.
/// Each test should call CreateContext() to get a fresh context sharing the same in-memory DB.
/// Dispose the factory to release the connection.
/// </summary>
public sealed class DbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public DbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public StageFrightDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new StageFrightDbContext(options);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
