namespace ACRCBridge.App.Configuration.Bridge;

internal sealed record BridgeConfig(int Port, string? BindAddress)
{
    /// <summary>
    /// Optional RC3 slot -> channel map (Bridge:Rc3Channels). Slots are d1, d2 and a1..a15;
    /// values are Assetto Corsa channel names or "None". When omitted, built-in defaults apply.
    /// </summary>
    public Dictionary<string, string>? Rc3Channels { get; init; }
}