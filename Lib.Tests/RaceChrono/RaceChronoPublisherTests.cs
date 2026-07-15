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
            Assert.AreEqual("5824.142", rmcFields[3]);
            Assert.AreEqual("N", rmcFields[4]);
            Assert.AreEqual("02426.883", rmcFields[5]);
            Assert.AreEqual("E", rmcFields[6]);

            var ggaFields = gga.Split(',');
            Assert.AreEqual("5824.142", ggaFields[2]);
            Assert.AreEqual("N", ggaFields[3]);
            Assert.AreEqual("02426.883", ggaFields[4]);
            Assert.AreEqual("E", ggaFields[5]);
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
            Latitude: 58.402361f,
            Altitude: 5,
            Longitude: 24.448056f,
            PosNormalized: 0.25f,
            GamePosX: -299.5362f,
            GamePosY: 0.45f,
            GamePosZ: -132.2299f,
            Slope: 0,
            AccGVertical: 0.1f,
            AccGHorizontal: 0.2f,
            AccGFrontal: 0.3f);
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