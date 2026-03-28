using LP.Umbraco.LegacyFeatureConverter.Backoffice;
using LP.Umbraco.LegacyFeatureConverter.Converters;
using LP.Umbraco.LegacyFeatureConverter.Converters.MediaPicker;
using LP.Umbraco.LegacyFeatureConverter.Converters.NestedContent;
using LP.Umbraco.LegacyFeatureConverter.Data;
using LP.Umbraco.LegacyFeatureConverter.Data.Migrations;
using LP.Umbraco.LegacyFeatureConverter.Infrastructure.Queue;
using LP.Umbraco.LegacyFeatureConverter.Infrastructure.Services;
using LP.Umbraco.LegacyFeatureConverter.Services;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;

namespace LP.Umbraco.LegacyFeatureConverter.Composers;

/// <summary>
/// Main composer that registers all Legacy Feature Converter services with the DI container.
/// Handles database provider detection, service registration, converter discovery,
/// background task, and backoffice UI registration.
/// </summary>
public class LegacyConverterComposer : IComposer
{
    /// <summary>
    /// Registers all services, converters, and infrastructure with the Umbraco DI container.
    /// </summary>
    /// <param name="builder">The Umbraco builder.</param>
    public void Compose(IUmbracoBuilder builder)
    {
        // === Database (EF Core) ===
        RegisterDatabase(builder);

        // === Services ===
        builder.Services.AddScoped<IConversionHistoryService, ConversionHistoryService>();
        builder.Services.AddScoped<IConversionQueueService, ConversionQueueService>();
        builder.Services.AddScoped<IConverterService, ConverterService>();

        // === Built-in converters (auto-discovered via IEnumerable<IPropertyConverter>) ===
        builder.Services.AddScoped<IPropertyConverter, NestedContentConverter>();
        builder.Services.AddScoped<IPropertyConverter, MediaPickerConverter>();

        // === Background task (queue processing) ===
        builder.Services.AddHostedService<ConversionBackgroundTask>();

        // === Database migration on startup ===
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification,
            LegacyFeatureConverterDatabaseMigration>();

        // === Backoffice UI ===
        builder.ManifestFilters().Append<LegacyConverterManifestFilter>();
    }

    /// <summary>
    /// Detects the database provider from connection strings and registers
    /// the appropriate EF Core DbContext variant.
    /// </summary>
    /// <param name="builder">The Umbraco builder.</param>
    private static void RegisterDatabase(IUmbracoBuilder builder)
    {
        // Read the provider name from Umbraco's raw config key.
        // Umbraco stores this as "umbracoDbDSN_ProviderName" — standard ConnectionStrings model
        // binding does NOT map this automatically, so we read it directly.
        var providerName = builder.Config["ConnectionStrings:umbracoDbDSN_ProviderName"]
            ?? "Microsoft.Data.Sqlite";

        if (providerName.Contains("SqlClient", StringComparison.OrdinalIgnoreCase))
        {
            // SQL Server
            builder.Services.AddUmbracoDbContext<LegacyFeatureConverterDbContextSqlServer>(
                (serviceProvider, optionsBuilder) =>
                {
                    optionsBuilder.UseUmbracoDatabaseProvider(serviceProvider);
                });

            builder.Services.AddScoped<LegacyFeatureConverterDbContext>(sp =>
                sp.GetRequiredService<LegacyFeatureConverterDbContextSqlServer>());
        }
        else
        {
            // SQLite
            builder.Services.AddUmbracoDbContext<LegacyFeatureConverterDbContextSqlite>(
                (serviceProvider, optionsBuilder) =>
                {
                    optionsBuilder.UseUmbracoDatabaseProvider(serviceProvider);
                });

            builder.Services.AddScoped<LegacyFeatureConverterDbContext>(sp =>
                sp.GetRequiredService<LegacyFeatureConverterDbContextSqlite>());
        }
    }
}
