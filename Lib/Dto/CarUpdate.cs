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
    float PosX,
    float PosY,
    float PosZ,
    float PosNormalized,
    float Slope,
    float AccGVertical,
    float AccGHorizontal,
    float AccGFrontal);
    