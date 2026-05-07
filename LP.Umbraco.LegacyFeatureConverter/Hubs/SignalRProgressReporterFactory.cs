using LP.Umbraco.LegacyFeatureConverter.Models;
using LP.Umbraco.LegacyFeatureConverter.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace LP.Umbraco.LegacyFeatureConverter.Hubs;

/// <summary>
/// Creates <see cref="SignalRProgressReporter"/> instances for broadcasting conversion
/// progress via SignalR. Implements <see cref="IProgressReporterFactory"/> to decouple
/// the Infrastructure layer from the SignalR hub types.
/// </summary>
public class SignalRProgressReporterFactory : IProgressReporterFactory
{
    private readonly IHubContext<ConversionHub, IConversionHubClient> _hubContext;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRProgressReporterFactory"/> class.
    /// </summary>
    /// <param name="hubContext">The typed SignalR hub context.</param>
    /// <param name="loggerFactory">Factory for creating loggers.</param>
    public SignalRProgressReporterFactory(
        IHubContext<ConversionHub, IConversionHubClient> hubContext,
        ILoggerFactory loggerFactory)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <inheritdoc />
    public IProgress<ConversionProgress> Create(Guid queueItemId)
    {
        return new SignalRProgressReporter(
            _hubContext,
            queueItemId,
            _loggerFactory.CreateLogger<SignalRProgressReporter>());
    }

    /// <inheritdoc />
    public async Task SendCompletedAsync(
        Guid queueItemId, ConversionStatus status, Guid? conversionHistoryId)
    {
        await _hubContext.Clients.All.ConversionCompleted(
            queueItemId, status, conversionHistoryId);
    }
}
