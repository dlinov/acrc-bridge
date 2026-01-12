namespace ACRCBridge.App.Configuration.Games;

internal sealed record AssettoCorsaConfig(
    string Host,
    int Port,
    TimeSpan HandshakeTimeout,
    TimeSpan IdleTimeout);
