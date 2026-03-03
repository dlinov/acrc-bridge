using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
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
    private readonly IPAddress _bindAddress;
    private readonly int _port;
    private int _started;

    public RaceChronoPublisher(int port, ITelemetryListener telemetryListener, string? bindAddress = null)
    {
        _telemetryListener = telemetryListener;
        _bindAddress = ParseBindAddress(bindAddress);
        _port = port;

        // Note: keep the listener alive for the lifetime of this publisher.
        // TODO: is IDisposable needed here?
        _tcpListener = new TcpListener(_bindAddress, port);
        var culture = RaceChronoUtils.Culture;
        _rc3Serializer = new RC3Serializer(culture);
        _gpggaSerializer = new GpggaSerializer(culture);
        _gprmcSerializer = new GprmcSerializer(culture);
    }

    public event Action<string>? Status;

    public Task StartAsync(CancellationToken token)
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
        Status?.Invoke("awaiting RaceChrono connection at " + GetConnectHint());

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

    private string GetConnectHint()
    {
        var ips = ResolveAdvertisedAddresses();
        var note = IsAnyAddress(_bindAddress)
            ? "(listening on all interfaces)"
            : string.Empty;

        var ipsAsString = ips.Count switch
        {
            1 => ips[0].ToString(),
            > 1 => $"[{string.Join(", ", ips)}]",
            _ => string.Empty
        };
        return $"{ipsAsString}:{_port} {note}".TrimEnd();
    }

    private void BroadcastCarUpdate(CarUpdate update)
    {
        // RaceChrono merges GPS (NMEA) and IMU (RC3) by timestamps.
        // Send both sentence types over the same TCP stream.
        var nowUtc = DateTime.UtcNow;

        var lat = (double)update.Longitude;
        var lon = (double)update.Latitude;
        var altitude = (double)update.Altitude;

        var rmc = _gprmcSerializer.Serialize(nowUtc, lat, lon, speedKmh: update.SpeedKmh, courseDeg: 0);
        var gga = _gpggaSerializer.Serialize(nowUtc, lat, lon, altitudeMeters: altitude, fixQuality: 1, satellites: 8, hdop: 1.0);
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

    private static IPAddress ParseBindAddress(string? bindAddress)
    {
        if (string.IsNullOrWhiteSpace(bindAddress) || bindAddress == "0.0.0.0")
        {
            return IPAddress.Any;
        }

        if (!IPAddress.TryParse(bindAddress, out var ipAddress) || ipAddress.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new InvalidOperationException(
                $"Invalid Bridge:BindAddress '{bindAddress}'. Use IPv4 (e.g. 0.0.0.0, 127.0.0.1, 192.168.x.x).");
        }

        return ipAddress;
    }

    private static bool IsAnyAddress(IPAddress address)
    {
        return address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any);
    }

    private List<IPAddress> ResolveAdvertisedAddresses()
    {
        if (!IsAnyAddress(_bindAddress))
        {
            return [_bindAddress];
        }

        var ips = GetUnicastIps().ToList();
        return ips.Count > 0 ? ips : [IPAddress.Loopback];
    }

    private static IEnumerable<IPAddress> GetUnicastIps()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                         ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                         ni.GetIPProperties().UnicastAddresses.Count > 0)
            .OrderByDescending(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            .ThenByDescending(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet);
        //var result = new List<IPAddress>();

        foreach (var nic in interfaces)
        {
            var props = nic.GetIPProperties();
            foreach (var unicast in props.UnicastAddresses)
            {
                var ip = unicast.Address;
                if (ip.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                if (IPAddress.IsLoopback(ip))
                {
                    continue;
                }

                // Ignore APIPA addresses (169.254.x.x) because they are usually not useful for phone/tablet clients.
                var bytes = ip.GetAddressBytes();
                if (bytes[0] == 169 && bytes[1] == 254)
                {
                    continue;
                }

                yield return ip;
            }
        }
    }
}
