using System.Globalization;
using ACRCBridge.Lib.Dto;
using ACRCBridge.Lib.RaceChrono.Utils;

namespace ACRCBridge.Lib.RaceChrono.RC3;

public class RC3Serializer(CultureInfo culture)
{
    // overflow is acceptable here: https://racechrono.com/article/2572
    // There is no way to reset the counter from outside. So please create a separate instance if needed.
    private ushort _count;

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
        var count = mixedWithNmea ? "" : (++_count).ToString(culture);

        // Acceleration in G
        var xAccG = data.AccGHorizontal;
        var yAccG = data.AccGVertical;
        var zAccG = data.AccGFrontal;

        // Gyro (deg/s) - not available in current CarUpdate DTO, output zeros for now.
        const float gyroX = 0f;
        const float gyroY = 0f;
        const float gyroZ = 0f;

        // Digital channels
        var rpm = data.EngineRpm;
        var d2 = (float)data.Gear;

        // Analog channels (a1..a15)
        var a1 = data.SpeedKmh;
        var a2 = data.Gas;
        var a3 = data.Brake;
        var a4 = data.Clutch;
        var a5 = data.PosX;
        var a6 = data.PosY;
        var a7 = data.PosZ;
        var a8 = data.PosNormalized;
        var a9 = data.Slope;
        var a10 = (float)data.LapTime;
        var a11 = (float)data.LastLap;
        var a12 = (float)data.BestLap;
        var a13 = (float)data.LapCount;
        const float a14 = 0f;
        const float a15 = 0f;

        // Total fields after RC3: time,count,xacc,yacc,zacc,gyrox,gyroy,gyroz,rpm,d2,a1..a15
        var payload = string.Format(
            culture,
            "RC3,{0},{1},{2:0.000},{3:0.000},{4:0.000},{5:0.000},{6:0.000},{7:0.000},{8:0.000},{9:0.000},{10:0.000},{11:0.000},{12:0.000},{13:0.000},{14:0.000},{15:0.000},{16:0.000},{17:0.000},{18:0.000},{19:0.000},{20:0.000},{21:0.000},{22:0.000},{23:0.000},{24:0.000}",
            time,
            count,
            xAccG,
            yAccG,
            zAccG,
            gyroX,
            gyroY,
            gyroZ,
            rpm,
            d2,
            a1,
            a2,
            a3,
            a4,
            a5,
            a6,
            a7,
            a8,
            a9,
            a10,
            a11,
            a12,
            a13,
            a14,
            a15);

        return RaceChronoUtils.ToNmeaLine(payload);
    }
}