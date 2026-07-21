namespace ACRCBridge.Lib.Dto;

/// <summary>
/// Telemetry channels sourced from the Assetto Corsa shared-memory physics/static blocks.
/// These are only available when the bridge runs on the same machine as Assetto Corsa.
/// </summary>
public readonly record struct AcPhysicsExtras(
    float SteerAngle,
    float Fuel,
    float FuelPercent,
    float TyreTempFL,
    float TyreTempFR,
    float TyreTempRL,
    float TyreTempRR,
    float TyrePressureFL,
    float TyrePressureFR,
    float TyrePressureRL,
    float TyrePressureRR,
    float BrakeTempFL,
    float BrakeTempFR,
    float BrakeTempRL,
    float BrakeTempRR,
    float BrakeTempMax,
    float GyroXDegPerSec,
    float GyroYDegPerSec,
    float GyroZDegPerSec,
    float HeadingDeg,
    float PitchDeg,
    float RollDeg);
