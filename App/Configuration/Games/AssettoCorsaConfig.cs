namespace ACRCBridge.App.Configuration.Games;

internal sealed record AssettoCorsaConfig(
    string Host,
    int Port,
    bool InvertClutch,
    TimeSpan HandshakeWaitTimeout,
    TimeSpan HandshakeRetryTimeout,
    TimeSpan IdleTimeout);
