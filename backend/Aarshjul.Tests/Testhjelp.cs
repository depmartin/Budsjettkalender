using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Tests;

/// <summary>
/// Bygger en <see cref="AppDbContext"/> mot SQLite in-memory (åpen tilkobling holdes i live),
/// slik at synlighetstestene kjører mot ekte EF-oversettelse uten ekstern database.
/// </summary>
public sealed class Testdatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public Testdatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new AppDbContext(options);
        Db.Database.EnsureCreated();
    }

    public AppDbContext Db { get; }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}

/// <summary>TimeProvider med fast «nå», for deterministiske dato-/historikktester.</summary>
public sealed class FastKlokke(DateOnly idag) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
        => new(idag.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
