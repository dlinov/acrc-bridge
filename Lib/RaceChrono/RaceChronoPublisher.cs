using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ACRCBridge.Lib.Dto;
using ACRCBridge.Lib.RaceChrono.GPS;
using ACRCBridge.Lib.RaceChrono.RC3;
using ACRCBridge.Lib.RaceChrono.Utils;

namespace ACRCBridge.Lib.RaceChrono;

public sealed class RaceChronoPublisher : ITelemetryPublisher
{
    private readonly ITelemetryListener _telemetryListener;
    private readonly TcpListener _tcpListener;
    private readonly ConcurrentDictionary<EndPoint, TcpClient> _clients = new();
    private readonly RC3Serializer _rc3Serializer;
    private readonly GpggaSerializer _gpggaSerializer;
    private readonly GprmcSerializer _gprmcSerializer;
    private int _started;

    public RaceChronoPublisher(int port, ITelemetryListener telemetryListener)
    {
        _telemetryListener = telemetryListener;
        // Note: keep the listener alive for the lifetime of this publisher.
        // TODO: is IDisposable needed here?
        _tcpListener = new TcpListener(IPAddress.Any, port);
        var culture = RaceChronoUtils.Culture;
        _rc3Serializer = new RC3Serializer(culture);
        _gpggaSerializer = new GpggaSerializer(culture);
        _gprmcSerializer = new GprmcSerializer(culture);
    }

    public event Action<string>? Status;

    public Task Start(CancellationToken token)
    {
        Status?.Invoke("Starting RaceChrono telemetry publisher");
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            const string errorMsg = "RaceChrono telemetry publisher had already started";
            Status?.Invoke($"ERROR: {errorMsg}");
            throw new InvalidOperationException(errorMsg);
        }

        _telemetryListener.CarUpdate += BroadcastCarUpdate;
        Status?.Invoke("RaceChrono telemetry publisher hooked to telemetry events.");
        _tcpListener.Start();
        Status?.Invoke("Awaiting RaceChrono connection at " + _tcpListener.LocalEndpoint);

        // Accept clients in the background.
        return Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    TcpClient client;
                    try
                    {
                        client = await _tcpListener.AcceptTcpClientAsync(token);
                        var clientEndpoint = client.Client.RemoteEndPoint;
                        if (clientEndpoint != null)
                        {
                            _clients.AddOrUpdate(
                                clientEndpoint,
                                _ => client,
                                (_, oldClient) =>
                                {
                                    try
                                    {
                                        oldClient.Dispose();
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(e);
                                    }
                                    return client;
                                });
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    var remote = client.Client.RemoteEndPoint?.ToString() ?? "(unknown - will not be saved)";
                    Status?.Invoke($"{DateTime.Now} TCP client connected: {remote}");
                }
            }
            catch (Exception ex)
            {
                Status?.Invoke("TCP accept loop error: " + ex.Message);
            }
            finally
            {
                try
                {
                    _tcpListener.Stop();
                }
                catch
                {
                    // ignored
                }
            }
        }, token);
    }

    private void BroadcastCarUpdate(CarUpdate update)
    {
        // RaceChrono merges GPS (NMEA) and IMU (RC3) by timestamps.
        // Send both sentence types over the same TCP stream.
        var nowUtc = DateTime.UtcNow;

        // GPS: use update.PosX/PosZ as lat/lon (ACUdpReader already converts them).
        var lat = (double)update.PosX;
        var lon = (double)update.PosZ;

        var rmc = _gprmcSerializer.Serialize(nowUtc, lat, lon, speedKmh: update.SpeedKmh, courseDeg: 0);
        // TODO: altitudeMeters can be derived from PosY if needed.
        var gga = _gpggaSerializer.Serialize(nowUtc, lat, lon, altitudeMeters: 0, fixQuality: 1, satellites: 8, hdop: 1.0);
        var rc3 = _rc3Serializer.Serialize(update, nowUtc, mixedWithNmea: true);

        // Fire-and-forget: don't block the telemetry listener thread.
        _ = BroadcastToSubscribedClientsAsync(rmc);
        _ = BroadcastToSubscribedClientsAsync(gga);
        _ = BroadcastToSubscribedClientsAsync(rc3);
    }

    private async Task BroadcastToSubscribedClientsAsync(byte[] data)
    {
        foreach (var kvp in _clients)
        {
            var client = kvp.Value;
            try
            {
                await client.Client.SendAsync(data).ConfigureAwait(false);
            }
            catch
            {
                // Ignore per-client send failures.
            }
        }
    }
}


