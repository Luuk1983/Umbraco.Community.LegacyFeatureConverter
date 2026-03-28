using LP.Umbraco.LegacyFeatureConverter.Models;
using Microsoft.EntityFrameworkCore;

namespace LP.Umbraco.LegacyFeatureConverter.Data;

/// <summary>
/// Base DbContext for the Legacy Feature Converter package.
/// Manages conversion history, log entries, and the persistent conversion queue.
/// Uses provider-specific derived contexts for SQL Server and SQLite.
/// </summary>
public class LegacyFeatureConverterDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance for direct DI resolution.
    /// </summary>
    /// <param name="options">The DbContext options configured by the DI container.</param>
    public LegacyFeatureConverterDbContext(DbContextOptions<LegacyFeatureConverterDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Initializes a new instance for derived classes.
    /// Allows derived classes to pass their specific <c>DbContextOptions&lt;DerivedType&gt;</c> to the base.
    /// </summary>
    /// <param name="options">The DbContext options from the derived class.</param>
    protected LegacyFeatureConverterDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the conversion history records.
    /// </summary>
    public DbSet<ConversionHistory> ConversionHistories { get; set; } = null!;

    /// <summary>
    /// Gets or sets the conversion log entries.
    /// </summary>
    public DbSet<ConversionLogEntry> ConversionLogs { get; set; } = null!;

    /// <summary>
    /// Gets or sets the conversion queue items.
    /// </summary>
    public DbSet<QueueItem> QueueItems { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureConversionHistory(modelBuilder);
        ConfigureConversionLogEntry(modelBuilder);
        ConfigureQueueItem(modelBuilder);
    }

    /// <summary>
    /// Configures the <see cref="ConversionHistory"/> entity mapping.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    private static void ConfigureConversionHistory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversionHistory>(entity =>
        {
            entity.ToTable("LegacyFeatureConverterHistory");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .IsRequired();

            entity.Property(e => e.StartedAt)
                .HasColumnName("StartedAt")
                .IsRequired();

            entity.Property(e => e.CompletedAt)
                .HasColumnName("CompletedAt");

            entity.Property(e => e.ConverterType)
                .HasColumnName("ConverterType")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.IsTestRun)
                .HasColumnName("IsTestRun")
                .IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("Status")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.SelectedDocumentTypes)
                .HasColumnName("SelectedDocumentTypes")
                .HasMaxLength(int.MaxValue);

            entity.Property(e => e.TotalDocumentTypes)
                .HasColumnName("TotalDocumentTypes")
                .HasDefaultValue(0)
                .IsRequired();

            entity.Property(e => e.TotalDataTypes)
                .HasColumnName("TotalDataTypes")
                .HasDefaultValue(0)
                .IsRequired();

            entity.Property(e => e.TotalContentNodes)
                .HasColumnName("TotalContentNodes")
                .HasDefaultValue(0)
                .IsRequired();

            entity.Property(e => e.SuccessCount)
                .HasColumnName("SuccessCount")
                .HasDefaultValue(0)
                .IsRequired();

            entity.Property(e => e.FailureCount)
                .HasColumnName("FailureCount")
                .HasDefaultValue(0)
                .IsRequired();

            entity.Property(e => e.SkippedCount)
                .HasColumnName("SkippedCount")
                .HasDefaultValue(0)
                .IsRequired();

            entity.Property(e => e.Summary)
                .HasColumnName("Summary")
                .HasMaxLength(int.MaxValue);

            entity.Property(e => e.PerformingUserKey)
                .HasColumnName("PerformingUserKey")
                .IsRequired();

            // One-to-many: ConversionHistory -> ConversionLogEntry (cascade delete)
            entity.HasMany(h => h.LogEntries)
                .WithOne(l => l.ConversionHistory)
                .HasForeignKey(l => l.ConversionHistoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// Configures the <see cref="ConversionLogEntry"/> entity mapping.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    private static void ConfigureConversionLogEntry(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversionLogEntry>(entity =>
        {
            entity.ToTable("LegacyFeatureConverterLog");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .IsRequired();

            entity.Property(e => e.ConversionHistoryId)
                .HasColumnName("ConversionHistoryId")
                .IsRequired();

            entity.Property(e => e.Timestamp)
                .HasColumnName("Timestamp")
                .IsRequired();

            entity.Property(e => e.Level)
                .HasColumnName("Level")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.ItemType)
                .HasColumnName("ItemType")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.ItemName)
                .HasColumnName("ItemName")
                .HasMaxLength(500);

            entity.Property(e => e.ItemKey)
                .HasColumnName("ItemKey")
                .HasMaxLength(200);

            entity.Property(e => e.Message)
                .HasColumnName("Message")
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(e => e.Details)
                .HasColumnName("Details")
                .HasMaxLength(int.MaxValue);

            entity.Property(e => e.StackTrace)
                .HasColumnName("StackTrace")
                .HasMaxLength(int.MaxValue);

            // Indexes for faster queries
            entity.HasIndex(e => e.ConversionHistoryId)
                .HasDatabaseName("IX_LegacyFeatureConverterLog_HistoryId");

            entity.HasIndex(e => e.Timestamp)
                .HasDatabaseName("IX_LegacyFeatureConverterLog_Timestamp");
        });
    }

    /// <summary>
    /// Configures the <see cref="QueueItem"/> entity mapping.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    private static void ConfigureQueueItem(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QueueItem>(entity =>
        {
            entity.ToTable("LegacyFeatureConverterQueue");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .IsRequired();

            entity.Property(e => e.SerializedOptions)
                .HasColumnName("SerializedOptions")
                .HasMaxLength(int.MaxValue)
                .IsRequired();

            entity.Property(e => e.QueuedAt)
                .HasColumnName("QueuedAt")
                .IsRequired();

            entity.Property(e => e.StartedAt)
                .HasColumnName("StartedAt");

            entity.Property(e => e.CompletedAt)
                .HasColumnName("CompletedAt");

            entity.Property(e => e.Status)
                .HasColumnName("Status")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.ConversionHistoryId)
                .HasColumnName("ConversionHistoryId");

            // Index on Status for efficient queue polling
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_LegacyFeatureConverterQueue_Status");

            // Index on QueuedAt for FIFO ordering
            entity.HasIndex(e => e.QueuedAt)
                .HasDatabaseName("IX_LegacyFeatureConverterQueue_QueuedAt");
        });
    }
}
