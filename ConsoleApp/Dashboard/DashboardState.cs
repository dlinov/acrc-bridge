using ACRCBridge.Lib.Dto;

namespace ACRCBridge.ConsoleApp.Dashboard;

internal sealed class DashboardState
{
    private readonly object _lockObj = new();

    private string _status = "";
    private string _serverStatus = "";
    private ConnectionInfo? _connection;
    private CarUpdate? _car;
    private LapEvent? _lap;

    public DashboardState(int ExpectedHandshakeResponseSize, int ExpectedRTCarInfoSize, int ExpectedRTLapSize)
    {
        this.ExpectedHandshakeResponseSize = ExpectedHandshakeResponseSize;
        this.ExpectedRTCarInfoSize = ExpectedRTCarInfoSize;
        this.ExpectedRTLapSize = ExpectedRTLapSize;
    }

    public int ExpectedHandshakeResponseSize { get; }
    public int ExpectedRTCarInfoSize { get; }
    public int ExpectedRTLapSize { get; }

    public void SetStatus(string status)
    {
        lock (_lockObj) _status = status;
    }

    public void SetServerStatus(string serverStatus)
    {
        lock (_lockObj) _serverStatus = serverStatus;
    }

    public void SetConnection(ConnectionInfo info)
    {
        lock (_lockObj) _connection = info;
    }

    public void SetCar(CarUpdate update)
    {
        lock (_lockObj) _car = update;
    }

    public void SetLap(LapEvent lap)
    {
        lock (_lockObj) _lap = lap;
    }

    public DashboardSnapshot Snapshot()
    {
        lock (_lockObj)
        {
            return new DashboardSnapshot(
                Status: _status,
                ServerStatus: _serverStatus,
                Connection: _connection,
                Car: _car,
                Lap: _lap,
                ExpectedHandshakeResponseSize: ExpectedHandshakeResponseSize,
                ExpectedRTCarInfoSize: ExpectedRTCarInfoSize,
                ExpectedRTLapSize: ExpectedRTLapSize);
        }
    }
}