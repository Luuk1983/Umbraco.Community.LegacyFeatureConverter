using Microsoft.EntityFrameworkCore;

namespace Umbraco.Community.LegacyFeatureConverter.Data;

/// <summary>
/// SQLite-specific DbContext for the Legacy Feature Converter.
/// Used for generating SQLite migrations and at runtime when SQLite is the configured provider.
/// </summary>
public class LegacyFeatureConverterDbContextSqlite : LegacyFeatureConverterDbContext
{
    /// <summary>
    /// Initializes a new instance with SQLite-specific options.
    /// </summary>
    /// <param name="options">The SQLite-configured DbContext options.</param>
    public LegacyFeatureConverterDbContextSqlite(
        DbContextOptions<LegacyFeatureConverterDbContextSqlite> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Only configure if not already configured (for design-time migration generation)
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=temp.db");
        }
    }
}
