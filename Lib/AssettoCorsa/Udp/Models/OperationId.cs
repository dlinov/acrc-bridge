namespace ACRCBridge.Lib.AssettoCorsa.Udp.Models;

enum OperationId : int
{
    HANDSHAKE = 0,
    SUBSCRIBE_UPDATE = 1,
    SUBSCRIBE_SPOT = 2,
    DISMISS = 3,
}