using ACRCBridge.App.Configuration.Games;
using ACRCBridge.App.Configuration.Tracks;
using Microsoft.Extensions.Configuration;

namespace ACRCBridge.App.Configuration;

internal sealed record AppConfig(GamesConfig Games, int BridgePort, Dictionary<string, TrackConfig> Tracks)
{
    public static AppConfig Load(IConfiguration configuration)
    {
        return configuration.Get<AppConfig>() ?? throw new InvalidOperationException("Failed to bind configuration.");
    }
}