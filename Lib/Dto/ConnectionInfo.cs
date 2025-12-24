namespace ACRCBridge.Lib.Dto;

public readonly record struct ConnectionInfo(
    string DriverName,
    string CarName,
    string TrackName,
    string TrackConfig,
    int ServerIdentifier,
    int ServerVersion);




