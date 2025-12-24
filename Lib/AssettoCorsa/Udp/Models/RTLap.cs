using System.Runtime.InteropServices;

namespace ACRCBridge.Lib.AssettoCorsa.Udp.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
struct RTLap
{
    public int CarIdentifierNumber;
    public int Lap;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string DriverName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string CarName;
    public int Time;
}