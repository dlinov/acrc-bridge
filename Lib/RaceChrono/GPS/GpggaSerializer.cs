using System.Globalization;
using ACRCBridge.Lib.RaceChrono.Utils;

namespace ACRCBridge.Lib.RaceChrono.GPS;

public class GpggaSerializer(CultureInfo culture)
{
    public byte[] Serialize(
        DateTime utcNow,
        double latDeg,
        double lonDeg,
        double altitudeMeters,
        int fixQuality,
        int satellites,
        double hdop)
    {
        // $GPGGA,hhmmss.sss,lat,N,lon,E,fix,sats,hdop,alt,M,0.0,M,,*CS
        var time = RaceChronoUtils.NmeaTimeOfDay(utcNow);

        var (latField, latHem) = RaceChronoUtils.NmeaLat(latDeg);
        var (lonField, lonHem) = RaceChronoUtils.NmeaLon(lonDeg);

        var payload = string.Format(
            culture,
            "GPGGA,{0},{1},{2},{3},{4},{5},{6:00},{7:0.00},{8:0.0},M,0.0,M,,",
            time,
            latField,
            latHem,
            lonField,
            lonHem,
            fixQuality,
            satellites,
            hdop,
            altitudeMeters);

        return RaceChronoUtils.ToNmeaLine(payload);
    }
}