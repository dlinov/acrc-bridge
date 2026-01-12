using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ACRCBridge.Lib.Dto;
using ACRCBridge.Lib.AssettoCorsa.Udp.Models;
using ACRCBridge.Lib.Coordinates;

namespace ACRCBridge.Lib.AssettoCorsa.Udp;

public sealed class ACUdpReader(
    string acHost,
    int acPort,
    TimeSpan handshakeTimeout,
    TimeSpan idleTimeout,
    GeoConvertersCollection coordinateConverters
) : ITelemetryListener
{
    private readonly IPEndPoint _acEndPoint = new(IPAddress.Parse(acHost), acPort);

    public static int ExpectedHandshakeResponseSize => Marshal.SizeOf<HandshakeResponse>();
    public static int ExpectedRTCarInfoSize => Marshal.SizeOf<RTCarInfo>();
    public static int ExpectedRTLapSize => Marshal.SizeOf<RTLap>();

    public event Action<string>? Status;
    public event Action<ConnectionInfo>? Connected;
    public event Action<CarUpdate>? CarUpdate;
    public event Action<LapEvent>? LapEvent;
    public event Action<Exception>? Error;

    public Task StartAsync(CancellationToken token) => ReadLoopAsync(token);

    private async Task ReadLoopAsync(CancellationToken token)
    {
        Status?.Invoke("Starting AC UDP listener...");

        // Session loop: handshake -> subscribe -> receive until idle/cancel -> dismiss -> repeat
        while (!token.IsCancellationRequested)
        {
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
            var connected = false;
            var shouldDismiss = false;

            try
            {
                // 1. Handshake Loop
                while (!token.IsCancellationRequested)
                {
                    await SendHandshakeAsync(udpClient, _acEndPoint).ConfigureAwait(false);

                    try
                    {
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                        timeoutCts.CancelAfter(handshakeTimeout);

                        var handshakeResult = await udpClient.ReceiveAsync(timeoutCts.Token).ConfigureAwait(false);
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
                        connected = true;
                        shouldDismiss = true;
                        break; // Connected!
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    {
                        Status?.Invoke("No response from AC. Retrying in 5s...");
                        await Task.Delay(5000, token).ConfigureAwait(false);
                    }
                }

                if (!connected || token.IsCancellationRequested)
                    continue;

                // 3. Subscribe to updates
                Status?.Invoke("Subscribing to updates...");
                await SendSubscribeUpdateAsync(udpClient, _acEndPoint).ConfigureAwait(false);
                await SendSubscribeSpotAsync(udpClient, _acEndPoint).ConfigureAwait(false);
                Status?.Invoke("Subscribed.");

                // 4. Process updates
                while (!token.IsCancellationRequested)
                {
                    var receiveTask = udpClient.ReceiveAsync(token).AsTask();
                    var timeoutTask = Task.Delay(idleTimeout, token);
                    var finished = await Task.WhenAny(receiveTask, timeoutTask).ConfigureAwait(false);

                    if (finished == timeoutTask)
                    {
                        // No data received for a while -> treat it as race stopped
                        Status?.Invoke($"No data for {idleTimeout.TotalSeconds:0}s. Reconnecting...");
                        break;
                    }

                    var result = await receiveTask.ConfigureAwait(false);
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
                // Expected when token is canceled
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
                Status?.Invoke($"Error: {ex.Message}");
            }
            finally
            {
                // If we successfully connected/subscribed, try to dismiss before reconnecting/shutting down.
                if (shouldDismiss)
                {
                    try
                    {
                        await SendSubscribeDismissAsync(udpClient, _acEndPoint).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignore errors during shutdown/reconnect.
                    }
                }
            }
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
        await udp.SendAsync(buffer, buffer.Length, endPoint).ConfigureAwait(false);
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
