using ACRCBridge.App.Configuration;
using ACRCBridge.App.Dashboard;
using ACRCBridge.Lib.RaceChrono;
using ACRCBridge.Lib.AssettoCorsa.Udp;
using ACRCBridge.Lib.Coordinates;
using Microsoft.Extensions.Configuration;

// TODO: Add command-line arguments for:
// - choice of data collection method (memory-mapped file vs. UDP)
// - configuration (e.g., publication port, timeouts, etc.)
// - writing collected data to a file (TODO: how to replay this correctly?)

Console.WriteLine("Starting Assetto Corsa - RaceChrono bridge. Press Ctrl+C to stop at any time...");

using var cts = new CancellationTokenSource();

// Handle Ctrl+C gracefully
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true; // Prevent immediate termination
    cts.Cancel();
};

// Load configuration (appsettings.json)
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .Build();
var (gamesConfig, bridgePort, trackConfigs) = AppConfig.Load(config);
var acConfig = gamesConfig.AssettoCorsa;
var trackDtos = trackConfigs.Select(kv => (kv.Key, kv.Value.AsDto)).ToDictionary();
var convertersCollection = new GeoConvertersCollection(trackDtos);

var state = new DashboardState(
    ExpectedHandshakeResponseSize: ACUdpReader.ExpectedHandshakeResponseSize,
    ExpectedRTCarInfoSize: ACUdpReader.ExpectedRTCarInfoSize,
    ExpectedRTLapSize: ACUdpReader.ExpectedRTLapSize);

var acTelemetryListener = new ACUdpReader(
    acConfig.Host,
    acConfig.Port,
    acConfig.HandshakeTimeout,
    acConfig.IdleTimeout,
    convertersCollection);
var rcTelemetryPublisher = new RaceChronoPublisher(bridgePort, acTelemetryListener);
acTelemetryListener.Status += msg => state.SetStatus(msg);
rcTelemetryPublisher.Status += msg => state.SetServerStatus(msg);
acTelemetryListener.Error += ex => state.SetStatus($"ERROR: {ex.Message}");
acTelemetryListener.Connected += info => state.SetConnection(info);
acTelemetryListener.CarUpdate += update => state.SetCar(update);
acTelemetryListener.LapEvent += lap => state.SetLap(lap);

var readerTask = acTelemetryListener.StartAsync(cts.Token);
var pubTask = rcTelemetryPublisher.StartAsync(cts.Token);
var uiTask = Dashboard.RunDashboardAsync(state, cts.Token);

try
{
    await Task.WhenAll(readerTask, pubTask, uiTask);
}
catch (OperationCanceledException)
{
    // expected
}
