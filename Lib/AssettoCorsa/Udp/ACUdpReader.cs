using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ACRCBridge.Lib.Dto;
using ACRCBridge.Lib.AssettoCorsa.Udp.Models;
using ACRCBridge.Lib.Coordinates;

namespace ACRCBridge.Lib.AssettoCorsa.Udp;

public class ACUdpReader(GeoConvertersCollection coordinateConverters) : ITelemetryListener
{
    private const int ACServerPort = 9996; // AC listens on this port

    public static int ExpectedHandshakeResponseSize => Marshal.SizeOf<HandshakeResponse>();
    public static int ExpectedRTCarInfoSize => Marshal.SizeOf<RTCarInfo>();
    public static int ExpectedRTLapSize => Marshal.SizeOf<RTLap>();

    public event Action<string>? Status;
    public event Action<ConnectionInfo>? Connected;
    public event Action<CarUpdate>? CarUpdate;
    public event Action<LapEvent>? LapEvent;
    public event Action<Exception>? Error;
    
    private static readonly IPEndPoint ACEndPoint = new(IPAddress.Loopback, ACServerPort);

    public Task Start(CancellationToken token)
    {
        return Task.Run(async () => await ReadLoopAsync(token), token);
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        Status?.Invoke("Starting AC UDP listener...");
        // Bind to any available port, don't use 9996 as that's what AC uses
        using var udpClient = new UdpClient();

        // Fix for "An existing connection was forcibly closed by the remote host" on Windows
        // This happens when the previous send resulted in an ICMP Port Unreachable message
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            const int SIO_UDP_CONNRESET = -1744830452;
            udpClient.Client.IOControl(SIO_UDP_CONNRESET, [0], null);
            // TODO: should udpServer be added here as well?
        }

        var trackName = string.Empty;

        try
        {
            // 1. Handshake Loop
            while (!token.IsCancellationRequested)
            {
                await SendHandshakeAsync(udpClient, ACEndPoint);

                try
                {
                    // Wait for response with 1 second timeout
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeoutCts.CancelAfter(1000);

                    var handshakeResult = await udpClient.ReceiveAsync(timeoutCts.Token);
                    var response = Deserialize<HandshakeResponse>(handshakeResult.Buffer);
                    trackName = response.TrackName;

                    Connected?.Invoke(new ConnectionInfo(
                        DriverName: response.DriverName,
                        CarName: response.CarName,
                        TrackName: response.TrackName,
                        TrackConfig: response.TrackConfig,
                        ServerIdentifier: response.Identifier,
                        ServerVersion: response.Version));

                    Status?.Invoke("Connected.");
                    break; // Connected!
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    Status?.Invoke("No response from AC. Retrying in 5s...");
                    await Task.Delay(5000, token);
                }
            }

            if (token.IsCancellationRequested) return;

            // 3. Subscribe to updates
            Status?.Invoke("Subscribing to updates...");
            await SendSubscribeUpdateAsync(udpClient, ACEndPoint);
            await SendSubscribeSpotAsync(udpClient, ACEndPoint);
            Status?.Invoke("Subscribed.");

            // 4. Process updates
            while (!token.IsCancellationRequested)
            {
                var result = await udpClient.ReceiveAsync(token);

                if (result.Buffer.Length == Marshal.SizeOf<RTCarInfo>())
                {
                    var info = Deserialize<RTCarInfo>(result.Buffer);
                    var coordinatesConverter = coordinateConverters.GetConverter(trackName);
                    var gps = coordinatesConverter.FromGameCoordinates(
                        info.CarCoordinatesX,
                        info.CarCoordinatesZ);
                    info.CarCoordinatesX = (float)gps.Latitude;
                    info.CarCoordinatesZ = (float)gps.Longitude;
                    CarUpdate?.Invoke(new CarUpdate(
                        SpeedKmh: info.SpeedKmh,
                        EngineRpm: info.EngineRPM,
                        Gear: info.Gear,
                        LapTime: info.LapTime,
                        LastLap: info.LastLap,
                        BestLap: info.BestLap,
                        LapCount: info.LapCount,
                        Gas: info.Gas,
                        Brake: info.Brake,
                        Clutch: info.Clutch,
                        PosX: info.CarCoordinatesX,
                        PosY: info.CarCoordinatesY,
                        PosZ: info.CarCoordinatesZ,
                        Slope: info.CarSlope,
                        PosNormalized: info.CarPositionNormalized,
                        AccGVertical: info.AccGVertical,
                        AccGHorizontal: info.AccGHorizontal,
                        AccGFrontal: info.AccGFrontal));
                }
                else if (result.Buffer.Length == Marshal.SizeOf<RTLap>())
                {
                    var lap = Deserialize<RTLap>(result.Buffer);
                    LapEvent?.Invoke(new LapEvent(
                        CarIdentifierNumber: lap.CarIdentifierNumber,
                        Lap: lap.Lap,
                        DriverName: lap.DriverName,
                        CarName: lap.CarName,
                        Time: lap.Time));
                }
                else
                {
                    Status?.Invoke($"Unknown data length: {result.Buffer.Length} bytes");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when token is cancelled
            // Try to dismiss connection
            try 
            {
                await SendSubscribeDismissAsync(udpClient, ACEndPoint);
            }
            catch { /* Ignore errors during shutdown */ }
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
            Status?.Invoke($"Error: {ex.Message}");
        }

        Status?.Invoke("Stopped AC UDP listener 🏁");
    }

    private Task SendHandshakeAsync(UdpClient udp, IPEndPoint endPoint)
    {
        Status?.Invoke($"Sending handshake to {endPoint}...");
        return SendOperationAsync(udp, endPoint, (int)OperationId.HANDSHAKE);
    }

    private static Task SendSubscribeUpdateAsync(UdpClient udp, IPEndPoint endPoint)
    {        
        return SendOperationAsync(udp, endPoint, (int)OperationId.SUBSCRIBE_UPDATE);
    }

    private static Task SendSubscribeSpotAsync(UdpClient udp, IPEndPoint endPoint)
    {
        return SendOperationAsync(udp, endPoint, (int)OperationId.SUBSCRIBE_SPOT);
    }

    private static Task SendSubscribeDismissAsync(UdpClient udp, IPEndPoint endPoint)
    {
        return SendOperationAsync(udp, endPoint, (int)OperationId.DISMISS);
    }

    private static async Task SendOperationAsync(UdpClient udp, IPEndPoint endPoint, int operationId)
    {
        var handshake = new Handshake { Identifier = 1, Version = 1, OperationId = operationId };
        var buffer = Serialize(handshake);
        await udp.SendAsync(buffer, buffer.Length, endPoint);
    }

    private static byte[] Serialize<T>(T data) where T : struct
    {
        var size = Marshal.SizeOf(data);
        var buffer = new byte[size];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(data, handle.AddrOfPinnedObject(), false);
        }
        finally
        {
            handle.Free();
        }
        return buffer;
    }

    private static T Deserialize<T>(byte[] data) where T : struct
    {
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }
}
