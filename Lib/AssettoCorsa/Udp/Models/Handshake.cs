using System.Runtime.InteropServices;

namespace ACRCBridge.Lib.AssettoCorsa.Udp.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct Handshake
{
    public int Identifier;
    public int Version;
    public int OperationId;
}
