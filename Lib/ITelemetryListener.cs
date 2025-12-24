using ACRCBridge.Lib.Dto;

namespace ACRCBridge.Lib;

public interface ITelemetryListener
{
    public event Action<string>? Status;
    public event Action<ConnectionInfo>? Connected;
    public event Action<CarUpdate>? CarUpdate;
    public event Action<LapEvent>? LapEvent;
    public event Action<Exception>? Error;
}