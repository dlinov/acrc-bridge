namespace ACRCBridge.Lib.Dto;

public readonly record struct ConnectionInfo(
    bool IsConnected,
    string DriverName,
    string CarName,
    string TrackName,
    string TrackConfig,
    int ServerIdentifier,
    int ServerVersion)
{
    public static readonly ConnectionInfo Disconnected = new(
        IsConnected: false,
        DriverName: "",
        CarName: "",
        TrackName: "",
        TrackConfig: "",
        ServerIdentifier: 0,
        ServerVersion: 0);
}




