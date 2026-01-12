using ACRCBridge.App;
using ACRCBridge.App.Configuration;
using ACRCBridge.App.Dashboard;
using ACRCBridge.App.LearnTrack;
using ACRCBridge.Lib;
using ACRCBridge.Lib.AssettoCorsa.Udp;
using ACRCBridge.Lib.Coordinates;
using ACRCBridge.Lib.RaceChrono;
using Microsoft.Extensions.Configuration;

// TODO: Add command-line arguments for:
// - choice of data collection method (memory-mapped file vs. UDP)
// - writing collected data to a file (TODO: how to replay this correctly?)

Console.WriteLine("Starting Assetto Corsa - RaceChrono bridge. Press Ctrl+C to stop at any time...");
var cliArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
var appMode = AppMode.Bridge;
if (AppHelpers.HasArg(cliArgs, "--learn-track"))
{
    appMode = AppMode.LearnTrack;
}

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

var acTelemetryListener = new ACUdpReader(
    acHost: acConfig.Host,
    acPort: acConfig.Port,
    invertClutch: acConfig.InvertClutch,
    handshakeWaitTimeout: acConfig.HandshakeWaitTimeout,
    handshakeRetryTimeout: acConfig.HandshakeRetryTimeout,
    idleTimeout: acConfig.IdleTimeout,
    coordinateConverters: convertersCollection);
var rcTelemetryPublisher = new RaceChronoPublisher(bridgePort, acTelemetryListener);

var readerTask = acTelemetryListener.StartAsync(cts.Token);
var pubTask = rcTelemetryPublisher.StartAsync(cts.Token);
#pragma warning disable CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
var uiTask = appMode switch
#pragma warning restore CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
{
    AppMode.Bridge =>
        RunDashboardAsync(acTelemetryListener, rcTelemetryPublisher),
    AppMode.LearnTrack =>
        RunLearnTrackAsync(acTelemetryListener),
};

try
{
    await Task.WhenAll(readerTask, pubTask, uiTask);
}
catch (OperationCanceledException)
{
    // expected
}

return;

Task RunDashboardAsync(ITelemetryListener telemetryListener, ITelemetryPublisher telemetryPublisher)
{
    var state = new DashboardState();
    telemetryListener.Status += msg => state.SetStatus(msg);
    telemetryPublisher.Status += msg => state.SetServerStatus(msg);
    // RaceChronoPublisher reports issues via Status (it implements ITelemetryPublisher), so we don't subscribe to Error here.
    telemetryListener.Error += ex => state.SetStatus($"ERROR: {ex.Message}");
    telemetryListener.Connected += info => state.SetConnection(info);
    telemetryListener.CarUpdate += update => state.SetCar(update);
    telemetryListener.LapEvent += lap => state.SetLap(lap);
    return Dashboard.RunDashboardAsync(state, cts.Token);
}

Task RunLearnTrackAsync(ITelemetryListener telemetryListener)
{
    var trackWizard = new LearnTrackWizard(telemetryListener, acConfig.HandshakeRetryTimeout);
    return trackWizard.RunLearnTrackAsync(cts.Token);
}
