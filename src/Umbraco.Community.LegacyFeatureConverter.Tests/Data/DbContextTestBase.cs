using Umbraco.Community.LegacyFeatureConverter.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Data;

/// <summary>
/// Base class for tests that need an in-memory SQLite database.
/// Creates a fresh database for each test and disposes it afterward.
/// </summary>
public abstract class DbContextTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    protected readonly LegacyFeatureConverterDbContext DbContext;

    /// <summary>
    /// Initializes a new in-memory SQLite database and applies the schema.
    /// </summary>
    protected DbContextTestBase()
    {
        // Use a shared in-memory connection that stays open for the test lifetime
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LegacyFeatureConverterDbContext>()
            .UseSqlite(_connection)
            .Options;

        DbContext = new LegacyFeatureConverterDbContext(options);
        DbContext.Database.EnsureCreated();
    }

    /// <summary>
    /// Disposes the database context and connection.
    /// </summary>
    public void Dispose()
    {
        DbContext.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
