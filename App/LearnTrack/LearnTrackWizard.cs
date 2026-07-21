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
    private ConnectionInfo? _connectionInfo;
    private CarUpdate? _carUpdate;

    public LearnTrackWizard(
        ITelemetryListener telemetryListener,
        TimeSpan connectionTimeout)
    {
        _telemetryListener = telemetryListener;
        _connectionTimeout = connectionTimeout;
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
        Console.WriteLine("- drive your car to a known location on the track and press Enter to capture it");
        Console.WriteLine("- then provide that location's real-world GPS latitude, longitude and height");
        Console.WriteLine("Pick two points that are far apart for the most accurate mapping.");
        Console.WriteLine("After entering both points, you'll get a JSON snippet to add to appsettings.json.");
        Console.WriteLine("Awaiting for game telemetry...");
        await AwaitGameTelemetryAsync(token);

        var trackName = _connectionInfo?.TrackName.Replace("%", "") ?? "[unknown_track]";
        Console.WriteLine("Game telemetry received. Track detected: " + trackName);
        var point0 = CaptureReferencePoint("Point0");
        if (point0 is null)
        {
            Console.WriteLine("Telemetry lost. Exiting.");
            return EmptyResponse;
        }

        var point1 = CaptureReferencePoint("Point1");
        if (point1 is null)
        {
            Console.WriteLine("Telemetry lost. Exiting.");
            return EmptyResponse;
        }

        // Build the exact DTO shape used in config: Dictionary<string, TrackReferencePoints>
        var singleTrackDictionary = new Dictionary<string, TrackReferencePoints>
        {
            [trackName] = new(Point0: point0, Point1: point1)
        };
        var json = JsonSerializer.Serialize(
            singleTrackDictionary,
            typeof(Dictionary<string, TrackReferencePoints>),
            LearnTrackJsonContext.Default);

        Console.WriteLine();
        Console.WriteLine("Add this under appsettings.json -> Tracks (merge with existing):");
        Console.WriteLine(json);

        // Small UX: allow user to copy result before exiting.
        Console.WriteLine();
        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();
        return json;
    }

    /// <summary>
    /// Captures one reference point: waits for the user to park the car at a known
    /// location and press Enter (snapshotting the in-game position at that instant),
    /// then reads that location's real-world GPS coordinates.
    /// </summary>
    private ReferencePoint? CaptureReferencePoint(string name)
    {
        Console.WriteLine();
        Console.WriteLine($"{name}: drive to a spot whose real-world GPS you know, stop the car there,");
        Console.Write("then press Enter to capture its in-game position...");
        Console.ReadLine();

        var carUpdate = _carUpdate;
        if (carUpdate is null)
        {
            return null;
        }

        var acX = carUpdate.Value.GamePosX;
        var acY = carUpdate.Value.GamePosY;
        var acZ = carUpdate.Value.GamePosZ;
        Console.WriteLine("  Captured in-game position: X={0}, Y={1}, Z={2}", acX, acY, acZ);

        var lat = PromptDouble("  GPS Latitude: ");
        var lon = PromptDouble("  GPS Longitude: ");
        var height = PromptDouble("  GPS Height (meters): ");
        Console.WriteLine(
            "{0} recorded: AC X={1}, Y={2}, Z={3}; GPS {4}, {5}, {6}m",
            name, acX, acY, acZ, lat, lon, height);

        return new ReferencePoint(acX, acY, acZ, new GpsCoordinate(lat, lon, height));
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
