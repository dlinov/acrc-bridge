using ACRCBridge.Lib.Dto;

namespace ACRCBridge.ConsoleApp.Dashboard;

internal readonly record struct DashboardSnapshot(
    string Status,
    string ServerStatus,
    ConnectionInfo? Connection,
    CarUpdate? Car,
    LapEvent? Lap,
    int ExpectedHandshakeResponseSize,
    int ExpectedRTCarInfoSize,
    int ExpectedRTLapSize);
