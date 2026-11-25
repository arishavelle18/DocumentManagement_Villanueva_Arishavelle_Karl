using DocumentManagement.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DocumentManagement.Tests.Infrastructure;

/// <summary>
/// A clean SQLite in-memory database for handler unit tests. The connection stays open
/// for the lifetime of this object, so several AppDbContext instances can share it
/// (e.g. one for the handler, a fresh one to verify what was actually persisted).
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteTestDatabase()
    {
        _connection.Open();
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptors)
            .Options;

        return new AppDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}

/// <summary>Simulates a database failure on SaveChanges without needing a broken database.</summary>
public sealed class FailingSaveChangesInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default) =>
        throw new DbUpdateException("Simulated database failure.");
}
