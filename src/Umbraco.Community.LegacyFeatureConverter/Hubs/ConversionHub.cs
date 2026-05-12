using Microsoft.AspNetCore.SignalR;

namespace Umbraco.Community.LegacyFeatureConverter.Hubs;

/// <summary>
/// SignalR hub for real-time conversion progress updates.
/// The hub itself is empty — all communication is server-to-client
/// via <see cref="IHubContext{ConversionHub}"/> in the background task.
/// </summary>
public class ConversionHub : Hub<IConversionHubClient>
{
}
