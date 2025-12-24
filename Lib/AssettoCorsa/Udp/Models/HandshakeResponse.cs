using System.Runtime.InteropServices;

namespace ACRCBridge.Lib.AssettoCorsa.Udp.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
struct HandshakeResponse
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string CarName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string DriverName;
    public int Identifier;
    public int Version;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string TrackName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string TrackConfig;
}
