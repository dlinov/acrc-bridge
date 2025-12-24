namespace ACRCBridge.ConsoleApp.Dashboard;

internal static class Dashboard
{
    const int TitleRow = 0;
    const int StatusRow = 1;
    const int ConnectionRow = 2;
    const int ServerStatusRow = 3;
    const int ExpectedSizesRow = 4;
    const int SpeedRow = 5;
    const int RpmGearRow = 6;
    const int PositionRow = 7;
    const int GasRow = 8;
    const int BrakeRow = 9;
    const int ClutchRow = 10;
    const int LapRow2 = 11;
    const int LapRow = 12;
    const int DashboardRowCount = LapRow + 1;

    public static async Task RunDashboardAsync(DashboardState state, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var oldCursorVisible = Console.CursorVisible;
        Console.CursorVisible = false;

        // Clear whole screen (cls) once before drawing the fixed dashboard area.
        // Doing this every frame would flicker.
        Console.Clear();

        // Reserve a small fixed area; redraw in-place.
        for (var i = 0; i < DashboardRowCount; i++)
        {
            Console.WriteLine();
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        while (await timer.WaitForNextTickAsync(token))
        {
            var snapshot = state.Snapshot();
            Render(snapshot);
        }

        Console.CursorVisible = oldCursorVisible;
        Console.Clear();
    }

    static void Render(DashboardSnapshot s)
    {
        var width = Math.Max(40, Console.WindowWidth);

        WriteAt(TitleRow, "ACRCBridge Live Telemetry".PadRight(width));
        WriteAt(StatusRow, $"AC Status: {s.Status}".PadRight(width));
        WriteAt(ServerStatusRow, $"RC Status: {s.ServerStatus}".PadRight(width));

        var conn = s.Connection;
        var connLine = conn is null
            ? "AC Connection: (waiting for Assetto Corsa...)"
            : $"AC Connection: {conn.Value.DriverName} | {conn.Value.CarName} | {conn.Value.TrackName} [{conn.Value.TrackConfig}]";
        WriteAt(ConnectionRow, connLine.PadRight(width));

        WriteAt(ExpectedSizesRow,
            $"Expected sizes: HandshakeResponse={s.ExpectedHandshakeResponseSize} RTCarInfo={s.ExpectedRTCarInfoSize} RTLap={s.ExpectedRTLapSize}"
                .PadRight(width));

        var car = s.Car;
        if (car is null)
        {
            WriteAt(SpeedRow, "Car: (no data yet)".PadRight(width));
            WriteAt(RpmGearRow, "".PadRight(width));
            WriteAt(PositionRow, $"Position: (no data yet)".PadRight(width));
            WriteAt(LapRow2, $"Current lap: (no data yet)".PadRight(width));
            WriteAt(GasRow, $"Gas: (no data yet)".PadRight(width));
            WriteAt(BrakeRow, $"Brake: (no data yet)".PadRight(width));
            WriteAt(ClutchRow, $"Clutch: (no data yet)".PadRight(width));
        }
        else
        {
            var carValue = car.Value;
            var speed = Math.Clamp(carValue.SpeedKmh, 0, 320);
            var bar = ProgressBar(speed / 320f, Math.Clamp(width - 25, 10, 60));
            var posX = carValue.PosX;
            var posY = carValue.PosY;
            var posZ = carValue.PosZ;
            var posNorm = carValue.PosNormalized;
            var slope = carValue.Slope;
            var lapCount = carValue.LapCount;
            var bestLap = FormatLapTimeMs(carValue.BestLap);
            var currLap = FormatLapTimeMs(carValue.LapTime);
            var prevLap = FormatLapTimeMs(carValue.LastLap);
            var gasBar = ProgressBar(carValue.Gas, Math.Clamp(width - 15, 10, 60));
            var brakeBar = ProgressBar(carValue.Brake, Math.Clamp(width - 15, 10, 60));
            var clutchBar = ProgressBar(carValue.Clutch, Math.Clamp(width - 15, 10, 60));
            WriteAt(SpeedRow, $"Speed: {carValue.SpeedKmh,6:F1} km/h {bar}".PadRight(width));
            WriteAt(RpmGearRow, $"RPM: {carValue.EngineRpm,6:F0}  Gear: {carValue.Gear,2}".PadRight(width));
            WriteAt(PositionRow,
                $"Position: X={posX} Y={posY} Z={posZ} | Normalized poisition: {posNorm} | Slope: {slope}"
                    .PadRight(width));
            WriteAt(LapRow2,
                $"Current lap: {currLap} | Best lap: {bestLap} | Previous lap: {prevLap} | Lap count: {lapCount}"
                    .PadRight(width));
            WriteAt(GasRow, $"Gas: {gasBar} / {carValue.Gas:P0}".PadRight(width));
            WriteAt(BrakeRow, $"Brake: {brakeBar} / {carValue.Brake:P0}".PadRight(width));
            WriteAt(ClutchRow, $"Clutch: {clutchBar} / {carValue.Clutch:P0}".PadRight(width));
        }

        var lap = s.Lap;
        if (lap is null)
        {
            WriteAt(LapRow, "Lap: (no lap events yet)".PadRight(width));
        }
        else
        {
            var lapValue = lap.Value;
            // Protocol uses int; keeping it raw here.
            WriteAt(LapRow,
                $"Lap: #{lapValue.Lap}  CarId: {lapValue.CarIdentifierNumber}  Time: {lapValue.Time}".PadRight(width));
        }
    }

    static string FormatLapTimeMs(int milliseconds)
    {
        if (milliseconds <= 0)
        {
            return "--:--.---";
        }

        var ts = TimeSpan.FromMilliseconds(milliseconds);
        var minutes = (int)ts.TotalMinutes;
        return $"{minutes:00}:{ts.Seconds:00}.{ts.Milliseconds:000}";
    }

    static string ProgressBar(float progress01, int width)
    {
        progress01 = Math.Clamp(progress01, 0f, 1f);
        var filled = (int)Math.Round(progress01 * width);
        return "[" + new string('#', filled) + new string('-', Math.Max(0, width - filled)) + "]";
    }

    static void WriteAt(int row, string text)
    {
        // Avoid exceptions when console is resized too small.
        if (row < 0) return;
        if (Console.BufferHeight <= row) return;

        Console.SetCursorPosition(0, row);
        Console.Write(text);
    }
}