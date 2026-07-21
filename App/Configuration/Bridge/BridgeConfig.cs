namespace ACRCBridge.App.Configuration.Bridge;

/// <param name="Port">UDP port the bridge publishes RaceChrono telemetry on.</param>
/// <param name="BindAddress">Address to bind the publisher to (null = all interfaces).</param>
/// <param name="Rc3Channels">
/// Optional RC3 slot -> channel map (Bridge:Rc3Channels). Slots are d1, d2 and a1..a15;
/// values are Assetto Corsa channel names or "None". When omitted, built-in defaults apply.
/// </param>
internal sealed record BridgeConfig(
    int Port,
    string? BindAddress,
    Dictionary<string, string>? Rc3Channels = null);