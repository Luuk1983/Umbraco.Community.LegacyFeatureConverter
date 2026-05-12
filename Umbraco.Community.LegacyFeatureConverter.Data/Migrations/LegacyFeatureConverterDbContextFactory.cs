using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Umbraco.Community.LegacyFeatureConverter.Data.Migrations;

/// <summary>
/// Design-time factory for SQL Server migrations.
/// Used by the <c>dotnet ef migrations add</c> CLI command to create SQL Server-specific migrations.
/// </summary>
/// <example>
/// <code>
/// dotnet ef migrations add InitialCreate --context LegacyFeatureConverterDbContextSqlServer --output-dir Migrations/SqlServer
/// </code>
/// </example>
public class LegacyFeatureConverterDbContextSqlServerFactory
    : IDesignTimeDbContextFactory<LegacyFeatureConverterDbContextSqlServer>
{
    /// <summary>
    /// Creates a new SQL Server DbContext instance for design-time tooling.
    /// </summary>
    /// <param name="args">Command-line arguments (not used).</param>
    /// <returns>A configured SQL Server DbContext instance.</returns>
    public LegacyFeatureConverterDbContextSqlServer CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LegacyFeatureConverterDbContextSqlServer>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=TempMigrations;Trusted_Connection=True;");
        return new LegacyFeatureConverterDbContextSqlServer(optionsBuilder.Options);
    }
}

/// <summary>
/// Design-time factory for SQLite migrations.
/// Used by the <c>dotnet ef migrations add</c> CLI command to create SQLite-specific migrations.
/// </summary>
/// <example>
/// <code>
/// dotnet ef migrations add InitialCreate --context LegacyFeatureConverterDbContextSqlite --output-dir Migrations/Sqlite
/// </code>
/// </example>
public class LegacyFeatureConverterDbContextSqliteFactory
    : IDesignTimeDbContextFactory<LegacyFeatureConverterDbContextSqlite>
{
    /// <summary>
    /// Creates a new SQLite DbContext instance for design-time tooling.
    /// </summary>
    /// <param name="args">Command-line arguments (not used).</param>
    /// <returns>A configured SQLite DbContext instance.</returns>
    public LegacyFeatureConverterDbContextSqlite CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LegacyFeatureConverterDbContextSqlite>();
        optionsBuilder.UseSqlite("Data Source=temp.db");
        return new LegacyFeatureConverterDbContextSqlite(optionsBuilder.Options);
    }
}
