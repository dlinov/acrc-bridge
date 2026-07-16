using System.Net.Sockets;
using System.Text;
using ACRCBridge.Lib.Dto;
using ACRCBridge.Lib.RaceChrono;

namespace ACRCBridge.Lib.Tests.RaceChrono;

[TestClass]
public sealed class RaceChronoPublisherTests
{
    [TestMethod]
    public async Task StartAsync_WhenCarUpdateIsPublished_SendsOrderedTelemetryBatch()
    {
        var telemetryListener = new TestTelemetryListener();
        var publisher = new RaceChronoPublisher(0, telemetryListener, "127.0.0.1");
        var clientConnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        publisher.Status += status =>
        {
            if (status.Contains("TCP client connected", StringComparison.Ordinal))
            {
                clientConnected.TrySetResult(true);
            }
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var publisherTask = publisher.StartAsync(cts.Token);

        try
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(publisher.LocalEndpoint, cts.Token);
            await clientConnected.Task.WaitAsync(cts.Token);

            telemetryListener.Publish(CreateCarUpdate());

            using var reader = new StreamReader(
                client.GetStream(),
                Encoding.ASCII,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);

            var rmc = await reader.ReadLineAsync(cts.Token);
            var gga = await reader.ReadLineAsync(cts.Token);
            var rc3 = await reader.ReadLineAsync(cts.Token);

            Assert.IsNotNull(rmc);
            Assert.IsNotNull(gga);
            Assert.IsNotNull(rc3);
            StringAssert.StartsWith(rmc, "$GPRMC,");
            StringAssert.StartsWith(gga, "$GPGGA,");
            StringAssert.StartsWith(rc3, "$RC3,");

            var rmcFields = rmc.Split(',');
            Assert.AreEqual("5824.14166", rmcFields[3]);
            Assert.AreEqual("N", rmcFields[4]);
            Assert.AreEqual("02426.88336", rmcFields[5]);
            Assert.AreEqual("E", rmcFields[6]);

            var ggaFields = gga.Split(',');
            Assert.AreEqual("5824.14166", ggaFields[2]);
            Assert.AreEqual("N", ggaFields[3]);
            Assert.AreEqual("02426.88336", ggaFields[4]);
            Assert.AreEqual("E", ggaFields[5]);

            var rc3Fields = rc3.Split(',');
            Assert.AreEqual("0.200", rc3Fields[3], "AC horizontal G must be published as RC3 X/lateral acceleration.");
            Assert.AreEqual("0.100", rc3Fields[4], "AC vertical G must be published as RC3 Y/vertical acceleration.");
            Assert.AreEqual("0.300", rc3Fields[5], "AC frontal G must be published as RC3 Z/longitudinal acceleration.");
            Assert.AreEqual("5000.000", rc3Fields[9], "Engine RPM must be published in the RC3 RPM/Digital 1 field.");
            Assert.AreEqual("4.000", rc3Fields[10], "Gear must be published in the RC3 Digital 2 field.");
            AssertNmeaChecksum(rc3);

            telemetryListener.Publish(CreateCarUpdate() with { Longitude = 24.448256f });

            var eastboundRmc = await reader.ReadLineAsync(cts.Token);
            var eastboundGga = await reader.ReadLineAsync(cts.Token);
            var eastboundRc3 = await reader.ReadLineAsync(cts.Token);
            Assert.IsNotNull(eastboundRmc);
            Assert.IsNotNull(eastboundGga);
            Assert.IsNotNull(eastboundRc3);
            StringAssert.StartsWith(eastboundGga, "$GPGGA,");
            StringAssert.StartsWith(eastboundRc3, "$RC3,");

            var eastboundRmcFields = eastboundRmc.Split(',');
            Assert.AreEqual("90.0", eastboundRmcFields[8], "Eastbound movement must produce a 90-degree course.");
        }
        finally
        {
            await cts.CancelAsync();
            await publisherTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    private static CarUpdate CreateCarUpdate()
    {
        return new CarUpdate(
            SpeedKmh: 100,
            EngineRpm: 5000,
            Gear: 4,
            LapTime: 60_000,
            LastLap: 61_000,
            BestLap: 59_000,
            LapCount: 3,
            Gas: 0.75f,
            Brake: 0,
            Clutch: 1,
            Latitude: 58.402361,
            Altitude: 5,
            Longitude: 24.448056,
            PosNormalized: 0.25f,
            GamePosX: -299.5362f,
            GamePosY: 0.45f,
            GamePosZ: -132.2299f,
            Slope: 0,
            AccGVertical: 0.1f,
            AccGHorizontal: 0.2f,
            AccGFrontal: 0.3f);
    }

    private static void AssertNmeaChecksum(string sentence)
    {
        var separatorIndex = sentence.LastIndexOf('*');
        Assert.IsGreaterThan(1, separatorIndex, "Sentence must contain a checksum separator.");
        var payload = sentence[1..separatorIndex];
        byte checksum = 0;
        foreach (var character in payload)
        {
            checksum ^= (byte)character;
        }

        Assert.AreEqual(checksum.ToString("X2"), sentence[(separatorIndex + 1)..]);
    }

    private sealed class TestTelemetryListener : ITelemetryListener
    {
        public event Action<string>? Status
        {
            add { }
            remove { }
        }

        public event Action<ConnectionInfo>? Connected
        {
            add { }
            remove { }
        }

        public event Action<CarUpdate>? CarUpdate;

        public event Action<LapEvent>? LapEvent
        {
            add { }
            remove { }
        }

        public event Action<Exception>? Error
        {
            add { }
            remove { }
        }

        public void Publish(CarUpdate update)
        {
            CarUpdate?.Invoke(update);
        }
    }
}