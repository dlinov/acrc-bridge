namespace ACRCBridge.Lib;

public interface ITelemetryPublisher
{
    public event Action<string>? Status;
}