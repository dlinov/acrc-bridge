using System.Globalization;
using System.Text;
using ACRCBridge.Lib.Dto;
using ACRCBridge.Lib.RaceChrono.RC3;

namespace ACRCBridge.Lib.Tests.RaceChrono.RC3;

[TestClass]
public sealed class RC3SerializerTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly DateTime SampleUtc = new(2024, 1, 2, 3, 4, 5, 678, DateTimeKind.Utc);

    // Field indices inside a split RC3 sentence (index 0 is the "RC3" tag).
    private const int AccXField = 3;
    private const int AccYField = 4;
    private const int AccZField = 5;
    private const int GyroXField = 6;

    [TestMethod]
    public void Serialize_WithDefaultMap_UsesNewDefaultChannelLayout()
    {
        var serializer = new RC3Serializer(Culture);
        var data = CreateSampleCarUpdate();

        var fields = SerializeToFields(serializer, data);

        // Acceleration axes are fixed and are never remappable.
        Assert.AreEqual(Fmt(data.AccGHorizontal), fields[AccXField]);
        Assert.AreEqual(Fmt(data.AccGVertical), fields[AccYField]);
        Assert.AreEqual(Fmt(data.AccGFrontal), fields[AccZField]);

        // Gyro comes from shared memory; the sample has no physics, so it stays zero.
        Assert.AreEqual("0.000", fields[GyroXField]);
        Assert.AreEqual("0.000", fields[GyroXField + 1]);
        Assert.AreEqual("0.000", fields[GyroXField + 2]);

        // Non-physics slots keep UDP-sourced values.
        Assert.AreEqual(Fmt(data.EngineRpm), SlotField(fields, "d1"));
        Assert.AreEqual(Fmt(data.Gear), SlotField(fields, "d2"));
        Assert.AreEqual(Fmt(data.SpeedKmh), SlotField(fields, "a1"));
        Assert.AreEqual(Fmt(data.Gas), SlotField(fields, "a2"));
        Assert.AreEqual(Fmt(data.Brake), SlotField(fields, "a3"));
        Assert.AreEqual(Fmt(data.Clutch), SlotField(fields, "a4"));

        // GPS and lap timing are no longer in the default map; slots a5..a15 are shared-memory
        // channels that emit a constant zero when no physics snapshot is present.
        foreach (var slot in new[] { "a5", "a6", "a7", "a8", "a9", "a10", "a11", "a12", "a13", "a14", "a15" })
        {
            Assert.AreEqual("0.000", SlotField(fields, slot), $"Physics slot '{slot}' must be zero without a snapshot.");
        }
    }

    [TestMethod]
    public void Serialize_WithDefaultMapAndPhysics_RoutesSharedMemoryChannels()
    {
        var serializer = new RC3Serializer(Culture);
        var physics = CreateSamplePhysics();
        var data = CreateSampleCarUpdate(physics);

        var fields = SerializeToFields(serializer, data);

        // Gyro is fed from the shared-memory angular velocity (fields 6/7/8).
        Assert.AreEqual(Fmt(physics.GyroXDegPerSec), fields[GyroXField]);
        Assert.AreEqual(Fmt(physics.GyroYDegPerSec), fields[GyroXField + 1]);
        Assert.AreEqual(Fmt(physics.GyroZDegPerSec), fields[GyroXField + 2]);

        // Non-physics slots are unaffected.
        Assert.AreEqual(Fmt(data.EngineRpm), SlotField(fields, "d1"));
        Assert.AreEqual(Fmt(data.SpeedKmh), SlotField(fields, "a1"));

        // Shared-memory channels populate the freed analog slots.
        Assert.AreEqual(Fmt(physics.SteerAngle), SlotField(fields, "a5"));
        Assert.AreEqual(Fmt(physics.Fuel), SlotField(fields, "a6"));
        Assert.AreEqual(Fmt(physics.TyreTempFL), SlotField(fields, "a7"));
        Assert.AreEqual(Fmt(physics.TyrePressureFL), SlotField(fields, "a11"));
        Assert.AreEqual(Fmt(physics.BrakeTempMax), SlotField(fields, "a15"));
    }

    [TestMethod]
    public void Serialize_WithExplicitDefaultMap_MatchesBuiltInDefaults()
    {
        var defaults = new RC3Serializer(Culture);
        var configured = new RC3Serializer(Culture, BuildDefaultMap());
        var data = CreateSampleCarUpdate();

        var defaultBytes = defaults.Serialize(data, SampleUtc, mixedWithNmea: true);
        var configuredBytes = configured.Serialize(data, SampleUtc, mixedWithNmea: true);

        CollectionAssert.AreEqual(defaultBytes, configuredBytes);
    }

    [TestMethod]
    public void Serialize_WithNoneAndOmittedSlots_OutputsConstantZero()
    {
        // Providing a map makes it authoritative: "None" and every omitted slot become zero.
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["d1"] = "None",
        };
        var serializer = new RC3Serializer(Culture, map);
        var data = CreateSampleCarUpdate();

        var fields = SerializeToFields(serializer, data);

        foreach (var slot in AllSlots())
        {
            Assert.AreEqual("0.000", SlotField(fields, slot), $"Slot '{slot}' should output constant zero.");
        }
    }

    [TestMethod]
    public void Serialize_WithCustomMap_RoutesChannelAndZeroesUnlistedSlots()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a5"] = "SpeedKmh",
        };
        var serializer = new RC3Serializer(Culture, map);
        var data = CreateSampleCarUpdate();

        var fields = SerializeToFields(serializer, data);

        Assert.AreEqual(Fmt(data.SpeedKmh), SlotField(fields, "a5"));
        Assert.AreEqual("0.000", SlotField(fields, "d1"), "Unlisted slot must default to zero when a map is provided.");
        Assert.AreEqual("0.000", SlotField(fields, "a1"));
    }

    [TestMethod]
    public void Serialize_WithDifferentlyCasedSlotAndChannel_IsCaseInsensitive()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["A1"] = "enginerpm",
        };
        var serializer = new RC3Serializer(Culture, map);
        var data = CreateSampleCarUpdate();

        var fields = SerializeToFields(serializer, data);

        Assert.AreEqual(Fmt(data.EngineRpm), SlotField(fields, "a1"));
    }

    [TestMethod]
    public void Constructor_WithUnknownSlot_ThrowsArgumentException()
    {
        var map = new Dictionary<string, string> { ["zz1"] = "SpeedKmh" };

        Assert.ThrowsExactly<ArgumentException>(() => { _ = new RC3Serializer(Culture, map); });
    }

    [TestMethod]
    public void Constructor_WithUnknownChannel_ThrowsArgumentException()
    {
        var map = new Dictionary<string, string> { ["a1"] = "Foobar" };

        Assert.ThrowsExactly<ArgumentException>(() => { _ = new RC3Serializer(Culture, map); });
    }

    private static string Fmt(float value)
    {
        return value.ToString("0.000", Culture);
    }

    private static string[] SerializeToFields(RC3Serializer serializer, CarUpdate data)
    {
        var bytes = serializer.Serialize(data, SampleUtc, mixedWithNmea: true);
        var sentence = Encoding.ASCII.GetString(bytes);
        var start = sentence.IndexOf('$') + 1;
        var end = sentence.LastIndexOf('*');
        var payload = sentence[start..end];
        return payload.Split(',');
    }

    private static string SlotField(string[] fields, string slot)
    {
        // Sentence layout: [0]=RC3 [1]=time [2]=count [3..5]=accel [6..8]=gyro
        // [9]=d1 [10]=d2 [11..25]=a1..a15.
        var index = slot switch
        {
            "d1" => 9,
            "d2" => 10,
            _ => 10 + int.Parse(slot[1..], Culture),
        };
        return fields[index];
    }

    private static IEnumerable<string> AllSlots()
    {
        yield return "d1";
        yield return "d2";
        for (var i = 1; i <= 15; i++)
        {
            yield return "a" + i.ToString(Culture);
        }
    }

    private static Dictionary<string, string> BuildDefaultMap()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["d1"] = "EngineRpm",
            ["d2"] = "Gear",
            ["a1"] = "SpeedKmh",
            ["a2"] = "Gas",
            ["a3"] = "Brake",
            ["a4"] = "Clutch",
            ["a5"] = "SteerAngle",
            ["a6"] = "Fuel",
            ["a7"] = "TyreTempFL",
            ["a8"] = "TyreTempFR",
            ["a9"] = "TyreTempRL",
            ["a10"] = "TyreTempRR",
            ["a11"] = "TyrePressureFL",
            ["a12"] = "TyrePressureFR",
            ["a13"] = "TyrePressureRL",
            ["a14"] = "TyrePressureRR",
            ["a15"] = "BrakeTempMax",
        };
    }

    private static CarUpdate CreateSampleCarUpdate(AcPhysicsExtras? physics = null)
    {
        return new CarUpdate(
            SpeedKmh: 111.5f,
            EngineRpm: 5000f,
            Gear: 4,
            LapTime: 60_000,
            LastLap: 61_000,
            BestLap: 59_000,
            LapCount: 3,
            Gas: 0.75f,
            Brake: 0.25f,
            Clutch: 0.5f,
            Latitude: 58.402361,
            Altitude: 12.5f,
            Longitude: 24.448056,
            PosNormalized: 0.3f,
            GamePosX: -299.5362f,
            GamePosY: 0.45f,
            GamePosZ: -132.2299f,
            Slope: -1.5f,
            AccGVertical: 0.1f,
            AccGHorizontal: 0.2f,
            AccGFrontal: 0.3f,
            Physics: physics);
    }

    private static AcPhysicsExtras CreateSamplePhysics()
    {
        return new AcPhysicsExtras(
            SteerAngle: -12.5f,
            Fuel: 33.3f,
            FuelPercent: 66.6f,
            TyreTempFL: 80.1f,
            TyreTempFR: 81.2f,
            TyreTempRL: 82.3f,
            TyreTempRR: 83.4f,
            TyrePressureFL: 27.5f,
            TyrePressureFR: 27.6f,
            TyrePressureRL: 26.5f,
            TyrePressureRR: 26.6f,
            BrakeTempFL: 250.5f,
            BrakeTempFR: 255.5f,
            BrakeTempRL: 200.5f,
            BrakeTempRR: 205.5f,
            BrakeTempMax: 255.5f,
            GyroXDegPerSec: 1.5f,
            GyroYDegPerSec: -2.5f,
            GyroZDegPerSec: 3.5f,
            HeadingDeg: 45.0f,
            PitchDeg: -1.0f,
            RollDeg: 2.0f);
    }
}
