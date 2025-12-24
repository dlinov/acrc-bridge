using System.Globalization;
using System.Text;
using ACRCBridge.Lib.RaceChrono.Utils;

namespace ACRCBridge.Lib.RaceChrono.GPS;

public class GprmcSerializer(CultureInfo culture)
{
    public byte[] Serialize(DateTime utcNow, double latDeg, double lonDeg, float speedKmh, float courseDeg)
    {
        // $GPRMC,hhmmss.sss,A,llll.lll,N,yyyyy.yyy,E,knots,course,ddmmyy,,,A*CS
        // var culture = TelemetryCulture;
        var time = RaceChronoUtils.NmeaTimeOfDay(utcNow);
        var date = utcNow.ToString("ddMMyy", culture);

        var (latField, latHem) = RaceChronoUtils.NmeaLat(latDeg);
        var (lonField, lonHem) = RaceChronoUtils.NmeaLon(lonDeg);

        var speedKnots = speedKmh / 1.852f;

        var payload = string.Format(
            culture,
            "GPRMC,{0},A,{1},{2},{3},{4},{5:0.0},{6:0.0},{7},,,A",
            time,
            latField,
            latHem,
            lonField,
            lonHem,
            speedKnots,
            courseDeg,
            date);

        return RaceChronoUtils.ToNmeaLine(payload);
    }
}