using Microsoft.EntityFrameworkCore;

namespace LP.Umbraco.LegacyFeatureConverter.Data;

/// <summary>
/// SQL Server-specific DbContext for the Legacy Feature Converter.
/// Used for generating SQL Server migrations and at runtime when SQL Server is the configured provider.
/// </summary>
public class LegacyFeatureConverterDbContextSqlServer : LegacyFeatureConverterDbContext
{
    /// <summary>
    /// Initializes a new instance with SQL Server-specific options.
    /// </summary>
    /// <param name="options">The SQL Server-configured DbContext options.</param>
    public LegacyFeatureConverterDbContextSqlServer(
        DbContextOptions<LegacyFeatureConverterDbContextSqlServer> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Only configure if not already configured (for design-time migration generation)
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=TempMigrations;Trusted_Connection=True;");
        }
    }
}
