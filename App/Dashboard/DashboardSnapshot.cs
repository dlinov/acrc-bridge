using ACRCBridge.Lib.Dto;

namespace ACRCBridge.App.Dashboard;

internal readonly record struct DashboardSnapshot(
    string Status,
    string ServerStatus,
    ConnectionInfo? Connection,
    CarUpdate? Car,
    LapEvent? Lap);
