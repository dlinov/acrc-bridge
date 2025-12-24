namespace ACRCBridge.Lib.Dto;

public readonly record struct LapEvent(
    int CarIdentifierNumber,
    int Lap,
    string DriverName,
    string CarName,
    int Time);
