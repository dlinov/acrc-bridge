namespace ACRCBridge.Lib.Dto;

public readonly record struct CarUpdate(
    float SpeedKmh,
    float EngineRpm,
    int Gear,
    int LapTime,
    int LastLap,
    int BestLap,
    int LapCount,
    float Gas,
    float Brake,
    float Clutch,
    float Longitude,
    float Altitude,
    float Latitude,
    float PosNormalized,
    float GamePosX,
    float GamePosY,
    float GamePosZ,
    float Slope,
    float AccGVertical,
    float AccGHorizontal,
    float AccGFrontal);
    