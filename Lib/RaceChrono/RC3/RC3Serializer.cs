using System.Globalization;
using ACRCBridge.Lib.Dto;
using ACRCBridge.Lib.RaceChrono.Utils;

namespace ACRCBridge.Lib.RaceChrono.RC3;

public class RC3Serializer
{
    private const string NoneChannel = "None";

    // Placeholders: {0}=time, {1}=count, {2..4}=xacc/yacc/zacc, {5..7}=gyro,
    // {8}=d1, {9}=d2, {10..24}=a1..a15.
    private const string Rc3Format =
        "RC3,{0},{1},{2:0.000},{3:0.000},{4:0.000},{5:0.000},{6:0.000},{7:0.000},{8:0.000},{9:0.000},{10:0.000},{11:0.000},{12:0.000},{13:0.000},{14:0.000},{15:0.000},{16:0.000},{17:0.000},{18:0.000},{19:0.000},{20:0.000},{21:0.000},{22:0.000},{23:0.000},{24:0.000}";

    private static readonly Func<CarUpdate, float> Zero = _ => 0f;

    // Assetto Corsa channels that may be mapped to configurable RC3 slots. The acceleration and
    // gyro axes are intentionally fixed and are NOT part of this set.
    private static readonly IReadOnlyDictionary<string, Func<CarUpdate, float>> AcChannels =
        new Dictionary<string, Func<CarUpdate, float>>(StringComparer.OrdinalIgnoreCase)
        {
            ["SpeedKmh"] = data => data.SpeedKmh,
            ["EngineRpm"] = data => data.EngineRpm,
            ["Gear"] = data => data.Gear,
            ["Gas"] = data => data.Gas,
            ["Brake"] = data => data.Brake,
            ["Clutch"] = data => data.Clutch,
            ["Longitude"] = data => (float)data.Longitude,
            ["Altitude"] = data => data.Altitude,
            ["Latitude"] = data => (float)data.Latitude,
            ["PosNormalized"] = data => data.PosNormalized,
            ["Slope"] = data => data.Slope,
            ["LapTime"] = data => data.LapTime,
            ["LastLap"] = data => data.LastLap,
            ["BestLap"] = data => data.BestLap,
            ["LapCount"] = data => data.LapCount,
            // Shared-memory channels (0 unless SameMachineWithAC is on and AC is running).
            ["SteerAngle"] = data => data.Physics?.SteerAngle ?? 0f,
            ["Fuel"] = data => data.Physics?.Fuel ?? 0f,
            ["FuelPercent"] = data => data.Physics?.FuelPercent ?? 0f,
            ["TyreTempFL"] = data => data.Physics?.TyreTempFL ?? 0f,
            ["TyreTempFR"] = data => data.Physics?.TyreTempFR ?? 0f,
            ["TyreTempRL"] = data => data.Physics?.TyreTempRL ?? 0f,
            ["TyreTempRR"] = data => data.Physics?.TyreTempRR ?? 0f,
            ["TyrePressureFL"] = data => data.Physics?.TyrePressureFL ?? 0f,
            ["TyrePressureFR"] = data => data.Physics?.TyrePressureFR ?? 0f,
            ["TyrePressureRL"] = data => data.Physics?.TyrePressureRL ?? 0f,
            ["TyrePressureRR"] = data => data.Physics?.TyrePressureRR ?? 0f,
            ["BrakeTempFL"] = data => data.Physics?.BrakeTempFL ?? 0f,
            ["BrakeTempFR"] = data => data.Physics?.BrakeTempFR ?? 0f,
            ["BrakeTempRL"] = data => data.Physics?.BrakeTempRL ?? 0f,
            ["BrakeTempRR"] = data => data.Physics?.BrakeTempRR ?? 0f,
            ["BrakeTempMax"] = data => data.Physics?.BrakeTempMax ?? 0f,
            ["Heading"] = data => data.Physics?.HeadingDeg ?? 0f,
            ["Pitch"] = data => data.Physics?.PitchDeg ?? 0f,
            ["Roll"] = data => data.Physics?.RollDeg ?? 0f,
        };

    // Configurable RC3 slots, in output order (d1, d2, a1..a15).
    private static readonly string[] ConfigurableSlots =
        ["d1", "d2", "a1", "a2", "a3", "a4", "a5", "a6", "a7", "a8", "a9", "a10", "a11", "a12", "a13", "a14", "a15"];

    private static readonly HashSet<string> ConfigurableSlotSet =
        new(ConfigurableSlots, StringComparer.OrdinalIgnoreCase);

    // Default slot -> channel mapping, used only when Bridge:Rc3Channels is omitted entirely.
    // GPS is delivered losslessly via NMEA and RaceChrono computes lap timing itself, so those
    // channels are dropped from the default map; the freed analog slots carry shared-memory data.
    private static readonly IReadOnlyDictionary<string, string> DefaultChannelMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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

    private readonly CultureInfo _culture;
    private readonly Func<CarUpdate, float>[] _slotSelectors;

    // overflow is acceptable here: https://racechrono.com/article/2572
    // There is no way to reset the counter from outside. So please create a separate instance if needed.
    private ushort _count;

    /// <param name="culture">Culture used for numeric and time formatting.</param>
    /// <param name="channelMap">
    /// Optional slot -> channel map (Bridge:Rc3Channels). Valid slots are d1, d2 and a1..a15;
    /// each value must be an Assetto Corsa channel name or "None". When null, built-in defaults
    /// are used. When provided, it is authoritative: any slot not listed outputs a constant 0.
    /// Throws <see cref="ArgumentException"/> for an unknown slot or channel name.
    /// </param>
    public RC3Serializer(CultureInfo culture, IReadOnlyDictionary<string, string>? channelMap = null)
    {
        _culture = culture;
        _slotSelectors = BuildSlotSelectors(channelMap);
    }

    /// <summary>
    /// Serializes CarUpdate data into RC3 sentence.
    ///
    /// RC3:
    /// $RC3,[time],[count],[xacc],[yacc],[zacc],[gyrox],[gyroy],[gyroz],[rpm/d1],[d2],[a1]..[a15]*checksum\r\n
    /// IMPORTANT:
    /// When mixing with NMEA, include [time] and leave [count] empty.
    /// </summary>
    /// <param name="data">CarUpdate instance</param>
    /// <param name="utcNow">UTC time to use</param>
    /// <param name="mixedWithNmea">true if works in parallel with GPS info publishers</param>
    /// <returns></returns>
    public byte[] Serialize(CarUpdate data, DateTime utcNow, bool mixedWithNmea)
    {
        var time = RaceChronoUtils.NmeaTimeOfDay(utcNow);
        var count = mixedWithNmea ? "" : (++_count).ToString(_culture);

        var args = new object[25];
        args[0] = time;
        args[1] = count;

        // Acceleration in G (fixed axes, not remappable).
        args[2] = data.AccGHorizontal;
        args[3] = data.AccGVertical;
        args[4] = data.AccGFrontal;

        // Gyro (deg/s) from shared-memory angular velocity; 0 when physics data is unavailable.
        args[5] = data.Physics?.GyroXDegPerSec ?? 0f;
        args[6] = data.Physics?.GyroYDegPerSec ?? 0f;
        args[7] = data.Physics?.GyroZDegPerSec ?? 0f;

        // Configurable digital/analog slots: d1, d2, a1..a15.
        for (var i = 0; i < _slotSelectors.Length; i++)
        {
            args[8 + i] = _slotSelectors[i](data);
        }

        var payload = string.Format(_culture, Rc3Format, args);
        return RaceChronoUtils.ToNmeaLine(payload);
    }

    private static Func<CarUpdate, float>[] BuildSlotSelectors(IReadOnlyDictionary<string, string>? channelMap)
    {
        // Omitting Rc3Channels entirely keeps the defaults; otherwise the provided map is authoritative.
        IReadOnlyDictionary<string, string> effectiveMap;
        if (channelMap is null)
        {
            effectiveMap = DefaultChannelMap;
        }
        else
        {
            var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in channelMap)
            {
                if (!ConfigurableSlotSet.Contains(entry.Key))
                {
                    throw new ArgumentException(
                        $"Unknown RC3 slot '{entry.Key}'. Valid slots are: {string.Join(", ", ConfigurableSlots)}.",
                        nameof(channelMap));
                }

                normalized[entry.Key] = entry.Value;
            }

            effectiveMap = normalized;
        }

        var selectors = new Func<CarUpdate, float>[ConfigurableSlots.Length];
        for (var i = 0; i < ConfigurableSlots.Length; i++)
        {
            selectors[i] = ResolveSelector(ConfigurableSlots[i], effectiveMap);
        }

        return selectors;
    }

    private static Func<CarUpdate, float> ResolveSelector(string slot, IReadOnlyDictionary<string, string> effectiveMap)
    {
        if (!effectiveMap.TryGetValue(slot, out var channel)
            || string.IsNullOrWhiteSpace(channel)
            || string.Equals(channel, NoneChannel, StringComparison.OrdinalIgnoreCase))
        {
            return Zero;
        }

        if (!AcChannels.TryGetValue(channel, out var selector))
        {
            throw new ArgumentException(
                $"Unknown channel '{channel}' for RC3 slot '{slot}'. " +
                $"Valid channels are: {string.Join(", ", AcChannels.Keys)}, {NoneChannel}.");
        }

        return selector;
    }
}