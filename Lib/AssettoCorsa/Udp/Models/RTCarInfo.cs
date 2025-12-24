using System.Runtime.InteropServices;

namespace ACRCBridge.Lib.AssettoCorsa.Udp.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
struct RTCarInfo
{
    // Reference client treats this as utf-16le length 4 bytes (2 chars)
    // Offset 0..3
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2)]
    public string Identifier;

    // Offset 4..7
    public int Size;

    // Offsets 8..19
    public float SpeedKmh;
    public float SpeedMph;
    public float SpeedMs;

    // Offsets 20..25 (int8)
    public byte IsAbsEnabled;
    public byte IsAbsInAction;
    public byte IsTcInAction;
    public byte IsTcEnabled;
    public byte IsInPit;
    public byte IsEngineLimiterOn;

    // Offsets 26..27 (skip 2 bytes in reference parser)
    public byte Padding0;
    public byte Padding1;

    // Offsets 28..39
    public float AccGVertical;
    public float AccGHorizontal;
    public float AccGFrontal;

    // Offsets 40..55
    public int LapTime;
    public int LastLap;
    public int BestLap;
    public int LapCount;

    // Offsets 56..75
    public float Gas;
    public float Brake;
    public float Clutch;
    public float EngineRPM;
    public float Steer;

    // Offset 76..79
    public int Gear;

    // Offset 80..83
    public float CgHeight;

    // Offset 84..99
    public float WheelAngularSpeed1;
    public float WheelAngularSpeed2;
    public float WheelAngularSpeed3;
    public float WheelAngularSpeed4;

    // Offset 100..115
    public float SlipAngle1;
    public float SlipAngle2;
    public float SlipAngle3;
    public float SlipAngle4;

    // Offset 116..131
    public float SlipAngleContactPatch1;
    public float SlipAngleContactPatch2;
    public float SlipAngleContactPatch3;
    public float SlipAngleContactPatch4;

    // Offset 132..147
    public float SlipRatio1;
    public float SlipRatio2;
    public float SlipRatio3;
    public float SlipRatio4;

    // Offset 148..163
    public float TyreSlip1;
    public float TyreSlip2;
    public float TyreSlip3;
    public float TyreSlip4;

    // Offset 164..179
    public float NdSlip1;
    public float NdSlip2;
    public float NdSlip3;
    public float NdSlip4;

    // Offset 180..195
    public float Load1;
    public float Load2;
    public float Load3;
    public float Load4;

    // Offset 196..211
    public float Dy1;
    public float Dy2;
    public float Dy3;
    public float Dy4;

    // Offset 212..227
    public float Mz1;
    public float Mz2;
    public float Mz3;
    public float Mz4;

    // Offset 228..243
    public float TyreDirtyLevel1;
    public float TyreDirtyLevel2;
    public float TyreDirtyLevel3;
    public float TyreDirtyLevel4;

    // Offset 244..259
    public float CamberRad1;
    public float CamberRad2;
    public float CamberRad3;
    public float CamberRad4;

    // Offset 260..275
    public float TyreRadius1;
    public float TyreRadius2;
    public float TyreRadius3;
    public float TyreRadius4;

    // Offset 276..291
    public float TyreLoadedRadius1;
    public float TyreLoadedRadius2;
    public float TyreLoadedRadius3;
    public float TyreLoadedRadius4;

    // Offset 292..307
    public float SuspensionHeight1;
    public float SuspensionHeight2;
    public float SuspensionHeight3;
    public float SuspensionHeight4;

    // Offset 308..315
    public float CarPositionNormalized;
    public float CarSlope;

    // Offset 316..327
    public float CarCoordinatesX;
    public float CarCoordinatesY;
    public float CarCoordinatesZ;
}