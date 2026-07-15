using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Channels;
using ACRCBridge.Lib.Dto;
using ACRCBridge.Lib.RaceChrono.GPS;
using ACRCBridge.Lib.RaceChrono.RC3;
using ACRCBridge.Lib.RaceChrono.Utils;

namespace ACRCBridge.Lib.RaceChrono;

public sealed class RaceChronoPublisher : ITelemetryPublisher
{
    private readonly ITelemetryListener _telemetryListener;
    private readonly TcpListener _tcpListener;
    private readonly ConcurrentDictionary<TcpClient, byte> _clients = new();
    private readonly Channel<TelemetryBatch> _outbound = Channel.CreateBounded<TelemetryBatch>(
        new BoundedChannelOptions(16)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
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

        _tcpListener = new TcpListener(_bindAddress, port);
        var culture = RaceChronoUtils.Culture;
        _rc3Serializer = new RC3Serializer(culture);
        _gpggaSerializer = new GpggaSerializer(culture);
        _gprmcSerializer = new GprmcSerializer(culture);
    }

    public event Action<string>? Status;

    internal IPEndPoint LocalEndpoint => (IPEndPoint)_tcpListener.LocalEndpoint;

    public async Task StartAsync(CancellationToken token)
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
        try
        {
            _tcpListener.Start();
        }
        catch
        {
            _telemetryListener.CarUpdate -= BroadcastCarUpdate;
            throw;
        }
        Status?.Invoke("awaiting RaceChrono connection at " + GetConnectHint());

        using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var acceptTask = AcceptClientsAsync(stopCts.Token);
        var broadcastTask = BroadcastTelemetryAsync(stopCts.Token);

        try
        {
            var completedTask = await Task.WhenAny(acceptTask, broadcastTask).ConfigureAwait(false);
            await completedTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Status?.Invoke("RaceChrono publisher error: " + ex.Message);
        }
        finally
        {
            await stopCts.CancelAsync().ConfigureAwait(false);
            _tcpListener.Stop();

            try
            {
                await Task.WhenAll(acceptTask, broadcastTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopCts.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Status?.Invoke("RaceChrono publisher shutdown error: " + ex.Message);
            }

            _outbound.Writer.TryComplete();
            _telemetryListener.CarUpdate -= BroadcastCarUpdate;
            DisposeClients();
            Status?.Invoke("Stopped RaceChrono telemetry publisher");
        }
    }

    private async Task AcceptClientsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var client = await _tcpListener.AcceptTcpClientAsync(token).ConfigureAwait(false);
            if (!_clients.TryAdd(client, 0))
            {
                client.Dispose();
                continue;
            }

            var remote = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            Status?.Invoke($"{DateTime.Now} TCP client connected: {remote}");
        }
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

        var lat = (double)update.Latitude;
        var lon = (double)update.Longitude;
        var altitude = (double)update.Altitude;

        var rmc = _gprmcSerializer.Serialize(nowUtc, lat, lon, speedKmh: update.SpeedKmh, courseDeg: 0);
        var gga = _gpggaSerializer.Serialize(nowUtc, lat, lon, altitudeMeters: altitude, fixQuality: 1, satellites: 8, hdop: 1.0);
        var rc3 = _rc3Serializer.Serialize(update, nowUtc, mixedWithNmea: true);

        _outbound.Writer.TryWrite(new TelemetryBatch(rmc, gga, rc3));
    }

    private async Task BroadcastTelemetryAsync(CancellationToken token)
    {
        await foreach (var batch in _outbound.Reader.ReadAllAsync(token).ConfigureAwait(false))
        {
            var sendTasks = _clients.Keys
                .Select(client => SendBatchAsync(client, batch, token));
            await Task.WhenAll(sendTasks).ConfigureAwait(false);
        }
    }

    private async Task SendBatchAsync(TcpClient client, TelemetryBatch batch, CancellationToken token)
    {
        try
        {
            var stream = client.GetStream();
            await stream.WriteAsync(batch.Rmc, token).ConfigureAwait(false);
            await stream.WriteAsync(batch.Gga, token).ConfigureAwait(false);
            await stream.WriteAsync(batch.Rc3, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (_clients.TryRemove(client, out _))
            {
                var remote = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                client.Dispose();
                Status?.Invoke($"TCP client disconnected: {remote} ({ex.Message})");
            }
        }
    }

    private void DisposeClients()
    {
        foreach (var client in _clients.Keys)
        {
            if (_clients.TryRemove(client, out _))
            {
                client.Dispose();
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

    private readonly record struct TelemetryBatch(byte[] Rmc, byte[] Gga, byte[] Rc3);
}
