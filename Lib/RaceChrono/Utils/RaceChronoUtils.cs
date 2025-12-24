using System.Globalization;
using System.Text;

namespace ACRCBridge.Lib.RaceChrono.Utils;

internal static class RaceChronoUtils
{
    private static readonly Encoding Encoding = Encoding.ASCII;
    internal static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    internal static string NmeaTimeOfDay(DateTime utc)
    {
        // NMEA time-of-day: hhmmss.sss
        return utc.ToString("HHmmss.fff", Culture);
    }

    internal static (string Field, char Hem) NmeaLat(double latDeg)
    {
        var hem = latDeg >= 0 ? 'N' : 'S';
        latDeg = Math.Abs(latDeg);
        var deg = (int)Math.Floor(latDeg);
        var minutes = (latDeg - deg) * 60.0;
        // ddmm.mmm
        var field = string.Format(Culture, "{0:00}{1:00.000}", deg, minutes);
        return (field, hem);
    }

    internal static (string Field, char Hem) NmeaLon(double lonDeg)
    {
        var hem = lonDeg >= 0 ? 'E' : 'W';
        lonDeg = Math.Abs(lonDeg);
        var deg = (int)Math.Floor(lonDeg);
        var minutes = (lonDeg - deg) * 60.0;
        // dddmm.mmm
        var field = string.Format(Culture, "{0:000}{1:00.000}", deg, minutes);
        return (field, hem);
    }

    internal static byte[] ToNmeaLine(string payload)
    {
        var checksum = NmeaChecksumHex(payload);
        var sentence = "$" + payload + "*" + checksum + "\r\n";
        return Encoding.GetBytes(sentence);
    }

    private static string NmeaChecksumHex(string payload)
    {
        // XOR of all bytes in payload (everything between '$' and '*')
        var bytes = Encoding.GetBytes(payload);
        byte checksum = 0;
        foreach (var b in bytes)
        {
            checksum ^= b;
        }

        return checksum.ToString("X2");
    }
}