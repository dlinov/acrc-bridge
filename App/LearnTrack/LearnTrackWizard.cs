using System.Globalization;
using System.Text.Json;
using ACRCBridge.Lib;
using ACRCBridge.Lib.Dto;

namespace ACRCBridge.App.LearnTrack;

public sealed class LearnTrackWizard
{
    private const string EmptyResponse = "{}";
    private readonly ITelemetryListener _telemetryListener;
    private readonly TimeSpan _connectionTimeout;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private ConnectionInfo? _connectionInfo;
    private CarUpdate? _carUpdate;

    public LearnTrackWizard(
        ITelemetryListener telemetryListener,
        TimeSpan connectionTimeout)
    {
        _telemetryListener = telemetryListener;
        _connectionTimeout = connectionTimeout;
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        _connectionInfo = null;
        _carUpdate = null;
        _telemetryListener.Connected += OnConnected;
        _telemetryListener.CarUpdate += OnCarUpdate;
    }

    public async Task<string> RunLearnTrackAsync(CancellationToken token)
    {
        // This mode doesn’t talk to the game; it only collects reference points and prints JSON.
        Console.WriteLine("Track learning mode (--learn-track)");
        Console.WriteLine("You'll enter 2 reference points. For each point you need to:");
        Console.WriteLine("- drive your car to a known location on the track in the game");
        Console.WriteLine("- provide GPS latitude and longitude of the location");
        Console.WriteLine("After entering both points, you'll get a JSON snippet to add to appsettings.json.");
        Console.WriteLine("Awaiting for game telemetry...");
        await AwaitGameTelemetryAsync(token);

        var trackName = _connectionInfo?.TrackName.Replace("%", "") ?? "[unknown_track]";
        Console.WriteLine("Game telemetry received. Track detected: " + trackName);
        Console.WriteLine("Drive the car to Point0 and provide its GPS coordinates when arrived:");
        var carUpdate0 = _carUpdate;
        if (carUpdate0 is null)
        {
            Console.WriteLine("Telemetry lost. Exiting.");
            return EmptyResponse;
        }
        var p0AcX = carUpdate0.Value.PosX;
        var p0AcZ = carUpdate0.Value.PosZ;
        var p0Lat = PromptDouble("  GPS Latitude: ");
        var p0Lon = PromptDouble("  GPS Longitude: ");
        Console.WriteLine("Point0 recorded: AC: X={0}, Z={1}; GPS: {2}, {3}", p0AcX, p0AcZ, p0Lat, p0Lon);

        var carUpdate1 = _carUpdate;
        if (carUpdate1 is null)
        {
            Console.WriteLine("Telemetry lost. Exiting.");
            return EmptyResponse;
        }
        Console.WriteLine("Drive the car to Point1 and provide its GPS coordinates when arrived:");
        var p1AcX = carUpdate1.Value.PosX;
        var p1AcZ = carUpdate1.Value.PosZ;
        var p1Lat = PromptDouble("  GPS Latitude: ");
        var p1Lon = PromptDouble("  GPS Longitude: ");
        Console.WriteLine("Point1 recorded: AC: X={0}, Z={1}; GPS: {2}, {3}", p1AcX, p1AcZ, p1Lat, p1Lon);

        // Build the exact DTO shape used in config: Dictionary<string, TrackReferencePoints>
        var singleTrackDictionary = new Dictionary<string, TrackReferencePoints>
        {
            [trackName] = new(
                Point0: new ReferencePoint(p0AcX, p0AcZ, new GpsCoordinate(p0Lat, p0Lon)),
                Point1: new ReferencePoint(p1AcX, p1AcZ, new GpsCoordinate(p1Lat, p1Lon)))
        };
        var json = JsonSerializer.Serialize(singleTrackDictionary, _jsonSerializerOptions);

        Console.WriteLine();
        Console.WriteLine("Add this under appsettings.json -> TracksCoordinates (merge with existing):");
        Console.WriteLine(json);

        // Small UX: allow user to copy result before exiting.
        Console.WriteLine();
        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();
        return json;
    }
    
    private void OnConnected(ConnectionInfo connectionInfo)
    {
        _connectionInfo = connectionInfo;
    }

    private void OnCarUpdate(CarUpdate carUpdate)
    {
        _carUpdate = carUpdate;
    }

    private Task AwaitGameTelemetryAsync(CancellationToken token)
    {
        return Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                if (_connectionInfo is { IsConnected: true } && _carUpdate is not null)
                {
                    return;
                }

                await Task.Delay(_connectionTimeout, token);
            }
        }, token);
    }

    private static string Prompt(string label)
    {
        while (true)
        {
            Console.Write(label);
            var line = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(line))
                return line.Trim();
        }
    }

    private static double PromptDouble(string label)
    {
        while (true)
        {
            var s = Prompt(label);
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;

            Console.WriteLine("Invalid number. Use '.' as decimal separator (InvariantCulture). Try again.");
        }
    }
}
