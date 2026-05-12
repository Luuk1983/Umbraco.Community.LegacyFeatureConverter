using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace LP.Umbraco.LegacyFeatureConverter.Data.Migrations;

/// <summary>
/// Handles automatic database migration when the Umbraco application starts.
/// Applies any pending EF Core migrations to create or update the Legacy Feature Converter tables.
/// Only runs at <see cref="RuntimeLevel.Run"/> to avoid interfering with Umbraco's own install/upgrade process.
/// </summary>
public class LegacyFeatureConverterDatabaseMigration(
    LegacyFeatureConverterDbContext dbContext,
    ILogger<LegacyFeatureConverterDatabaseMigration> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    /// <summary>
    /// Handles the application starting notification by applying pending EF Core migrations.
    /// </summary>
    /// <param name="notification">The Umbraco application starting notification containing runtime level info.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    public async Task HandleAsync(
        UmbracoApplicationStartingNotification notification,
        CancellationToken cancellationToken)
    {
        if (notification.RuntimeLevel != RuntimeLevel.Run)
        {
            logger.LogInformation(
                "Legacy Feature Converter: Skipping database migrations because runtime level is {RuntimeLevel}",
                notification.RuntimeLevel);
            return;
        }

        logger.LogInformation("Legacy Feature Converter: Running database migrations");

        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Legacy Feature Converter: Database migrations completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy Feature Converter: An error occurred while migrating the database");
            throw;
        }
    }
}
