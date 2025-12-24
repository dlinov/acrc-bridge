using ACRCBridge.Lib.Dto;

namespace ACRCBridge.Lib.Coordinates;

public sealed class GeoConverter
{
    private readonly float _originAcX;
    private readonly float _originAcZ;

    private readonly GpsCoordinate _originGps;
    private readonly double _earthRadius;
    private readonly double _lonRadius;

    private readonly double _scale;
    private readonly double _cosRot;
    private readonly double _sinRot;

    private GeoConverter(ReferencePoint originPoint, double scale, double rotRad, double earthRadius)
    {
        _originAcX = originPoint.X;
        _originAcZ = originPoint.Z;

        _originGps = originPoint.GpsCoordinate;
        _scale = scale;
        _earthRadius = earthRadius;

        _lonRadius = _earthRadius * Math.Cos(_originGps.Latitude * Math.PI / 180.0);

        _cosRot = Math.Cos(rotRad);
        _sinRot = Math.Sin(rotRad);
    }

    /// <summary>
    /// Creates a converter from two reference pairs mapping AC (X,Z) coordinates to GPS coordinates.
    /// Assumptions/contract:
    /// - AC points are in meters in an X/Z plane.
    /// - GPS points are close enough to use a local tangent-plane (ENU) approximation.
    /// - The transform is: ENU = scale * R(theta) * (ac - ac0), then added to gps0.
    /// </summary>
    internal static GeoConverter FromTwoReferencePoints(
        ReferencePoint point0,
        ReferencePoint point1,
        double earthRadiusMeters = 6378137.0)
    {
        // AC delta vector
        double dax = point1.X - point0.X;
        double daz = point1.Z - point0.Z;
        var aLen = Math.Sqrt(dax * dax + daz * daz);
        if (aLen < 1e-6)
        {
            throw new ArgumentException("AC reference points are identical/too close; can't derive scale/rotation.");
        }

        // GPS delta vector in local ENU meters around gps0
        var dLatRad = (point1.GpsCoordinate.Latitude - point0.GpsCoordinate.Latitude) * Math.PI / 180.0;
        var dLonRad = (point1.GpsCoordinate.Longitude - point0.GpsCoordinate.Longitude) * Math.PI / 180.0;

        var lonRadius = earthRadiusMeters * Math.Cos(point0.GpsCoordinate.Latitude * Math.PI / 180.0);
        var east = dLonRad * lonRadius;
        var north = dLatRad * earthRadiusMeters;

        var gLen = Math.Sqrt(east * east + north * north);
        if (gLen < 1e-6)
        {
            throw new ArgumentException("GPS reference points are identical/too close; can't derive scale/rotation.");
        }

        var scale = gLen / aLen;

        // Axis convention:
        // - ENU uses +East, +North.
        // - In many AC tracks, X behaves like East, but Z grows "south" (opposite of North).
        // So we map AC (X,Z) to an intermediate (E,S) plane with S = +Z.
        // Then convert to ENU by rotating (E,S) onto (East,North) and flipping the S/N sign.
        // Practically: treat the AC vector as (eastAc = dax, northAc = -daz).
        double eastAc = dax;
        double northAc = -daz;

        // Find rotation so that R(theta) * (eastAc,northAc) aligns with (east,north).
        var angleAc = Math.Atan2(northAc, eastAc);
        var angleGps = Math.Atan2(north, east);
        var rotRad = angleGps - angleAc;

        return new GeoConverter(point0, scale, rotRad, earthRadiusMeters);
    }

    public GpsCoordinate FromGameCoordinates(float acX, float acZ)
    {
        // Always use the AC reference origin from the calibration pair.
        double x = acX - _originAcX;
        double z = acZ - _originAcZ;

        // Apply the same axis convention as calibration: (eastAc = x, northAc = -z)
        var eastAc = x;
        var northAc = -z;

        // Rotate and scale -> local ENU in meters.
        var eastRot = eastAc * _cosRot - northAc * _sinRot;
        var northRot = eastAc * _sinRot + northAc * _cosRot;

        var east = _scale * eastRot;
        var north = _scale * northRot;

        // Convert meters to lat/lon degrees around GPS origin.
        var dLat = north / _earthRadius;
        var dLon = east / _lonRadius;

        var lat = _originGps.Latitude + dLat * 180.0 / Math.PI;
        var lon = _originGps.Longitude + dLon * 180.0 / Math.PI;

        return new GpsCoordinate(lat, lon);
    }

    // Porsche Ring track converter. Choice of reference points is arbitrary but should cover a reasonable distance.
    internal static readonly GeoConverter PorscheRing = FromTwoReferencePoints(
        point0: new ReferencePoint(0.0f, 0.0f, new GpsCoordinate(58.401111, 24.453306)), // AC track origin
        point1: new ReferencePoint(-299.5362f, -132.2299f, new GpsCoordinate(58.402361, 24.448056)) // AC start line
    );

    internal static readonly GeoConverter Spa = FromTwoReferencePoints(
        point0: new ReferencePoint(0.0f, 0.0f, new GpsCoordinate(50.437591, 5.969755)), // AC track origin
        point1: new ReferencePoint(598.888700f, 825.427600f, new GpsCoordinate(50.429761, 5.977091)) // End of Kemmel plus two turns, end of curb/water sink
    );
}
