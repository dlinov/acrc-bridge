// Struct layout adapted from https://github.com/mdjarv/assettocorsasharedmemory (MIT License, © Mathias Djärv).
using System.Runtime.InteropServices;

namespace ACRCBridge.Lib.AssettoCorsa.SharedMemory;

/// <summary>
/// Assetto Corsa shared-memory static block (<c>Local\acpmf_static</c>).
/// Only the leading fields required by the bridge are declared; the block is read via a
/// fixed-size view that matches this layout up to and including <see cref="TyreRadius"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
internal struct StaticInfo
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string SMVersion;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string ACVersion;

    public int NumberOfSessions;
    public int NumCars;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string CarModel;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string Track;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string PlayerName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string PlayerSurname;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string PlayerNick;

    public int SectorCount;
    public float MaxTorque;
    public float MaxPower;
    public int MaxRpm;
    public float MaxFuel;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] SuspensionMaxTravel;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreRadius;
}
