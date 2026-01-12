using ACRCBridge.Lib.Dto;

namespace ACRCBridge.App.Dashboard;

internal sealed class DashboardState
{
    private readonly Lock _lock = new();

    private string _status = "";
    private string _serverStatus = "";
    private ConnectionInfo? _connection;
    private CarUpdate? _car;
    private LapEvent? _lap;

    public void SetStatus(string status)
    {
        lock (_lock) _status = status;
    }

    public void SetServerStatus(string serverStatus)
    {
        lock (_lock) _serverStatus = serverStatus;
    }

    public void SetConnection(ConnectionInfo connectionInfo)
    {
        lock (_lock)
        {
            _connection = connectionInfo;
            if (connectionInfo.IsConnected) return;
            _car = null;
            _lap = null;
        }
    }

    public void SetCar(CarUpdate update)
    {
        lock (_lock) _car = update;
    }

    public void SetLap(LapEvent lap)
    {
        lock (_lock) _lap = lap;
    }

    public DashboardSnapshot Snapshot()
    {
        lock (_lock)
        {
            return new DashboardSnapshot(
                Status: _status,
                ServerStatus: _serverStatus,
                Connection: _connection,
                Car: _car,
                Lap: _lap);
        }
    }
}