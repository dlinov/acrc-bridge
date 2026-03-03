using ACRCBridge.Lib.Dto;

namespace ACRCBridge.Lib.Coordinates;

public sealed class GeoConverter
{
    private const double EarthRadiusMeters = 6378137.0; // WGS84 equatorial radius
    private readonly float _originAcX;
    private readonly float _originAcZ;

    private readonly GpsCoordinate _originGps;
    private readonly double _earthRadius;
    private readonly double _lonRadius;

    private readonly double _hScale;
    private readonly double _heightScale;
    private readonly double _heightOffset;
    private readonly double _cosRot;
    private readonly double _sinRot;

    /// <summary>
    /// Creates a GeoConverter using two reference points.
    /// </summary>
    /// <param name="originPoint"></param>
    /// <param name="hScale">Coefficient to recalculate X and Z game coordinates</param>
    /// <param name="heightScale">Assuming H = aY + b, this is a</param>
    /// <param name="heightOffset">Assuming H = aY + b, this is b</param>
    /// <param name="rotRad"></param>
    /// <param name="earthRadius">Earth radius in meters. If not specified, taking equatorial radius</param>
    private GeoConverter(
        ReferencePoint originPoint,
        double hScale,
        double heightScale,
        double heightOffset,
        double rotRad,
        double earthRadius)
    {
        _originAcX = originPoint.X;
        _originAcZ = originPoint.Z;

        _originGps = originPoint.GpsCoordinate;
        _hScale = hScale;
        _heightScale = heightScale;
        _heightOffset = heightOffset;
        _earthRadius = earthRadius;

        _lonRadius = _earthRadius * Math.Cos(_originGps.Latitude * Math.PI / 180.0);

        _cosRot = Math.Cos(rotRad);
        _sinRot = Math.Sin(rotRad);
    }

    /// <summary>
    /// Creates a converter from two reference pairs mapping AC (X,Y,Z) coordinates to GPS coordinates.
    /// Assumptions/contract:
    /// - AC points are in meters in an X/Z plane.
    /// - AC point contains Y coordinate which is height.
    /// - GPS points are close enough to use a local tangent-plane (ENU) approximation.
    /// - The transform is: ENU = scale * R(theta) * (ac - ac0), then added to gps0.
    /// - Height is calculated as: H = aY + b, where Y is AC height and a,b are calculated from the reference points.
    /// </summary>
    internal static GeoConverter FromTwoReferencePoints(
        ReferencePoint point0,
        ReferencePoint point1,
        double earthRadiusMeters = EarthRadiusMeters)
    {
        // AC delta vector
        double dax = point1.X - point0.X;
        double daz = point1.Z - point0.Z;
        var aLen = Math.Sqrt(dax * dax + daz * daz);
        const double minDelta = 1e-6;
        if (aLen < minDelta)
        {
            throw new ArgumentException("AC ref. points are identical/too close; can't derive scale/rotation.");
        }
        if (Math.Abs(point1.Y - point0.Y) < minDelta)
        {
            throw new ArgumentException("AC ref. points have identical/too close Y; can't derive height mapping.");
        }
        if (Math.Abs(point1.GpsCoordinate.Height - point0.GpsCoordinate.Height) < minDelta)
        {
            throw new ArgumentException("AC ref. points have identical/too close GPS height; can't derive height mapping.");
        }

        // GPS delta vector in local ENU meters around gps0
        var dLatRad = (point1.GpsCoordinate.Latitude - point0.GpsCoordinate.Latitude) * Math.PI / 180.0;
        var dLonRad = (point1.GpsCoordinate.Longitude - point0.GpsCoordinate.Longitude) * Math.PI / 180.0;

        var lonRadius = earthRadiusMeters * Math.Cos(point0.GpsCoordinate.Latitude * Math.PI / 180.0);
        var east = dLonRad * lonRadius;
        var north = dLatRad * earthRadiusMeters;

        var gLen = Math.Sqrt(east * east + north * north);
        if (gLen < minDelta)
        {
            throw new ArgumentException("GPS reference points are identical/too close; can't derive scale/rotation.");
        }

        var hScale = gLen / aLen;

        // Axis convention:
        // - ENU uses +East, +North.
        // - In many AC tracks, X behaves like East, but Z grows "south" (opposite of North).
        // So we map AC (X,Z) to an intermediate (E,S) plane with S = +Z.
        // Then convert to ENU by rotating (E,S) onto (East,North) and flipping the S/N sign.
        // Practically: treat the AC vector as (eastAc = dax, northAc = -daz).
        var eastAc = dax;
        var northAc = -daz;

        // Find rotation so that R(theta) * (eastAc,northAc) aligns with (east,north).
        var angleAc = Math.Atan2(northAc, eastAc);
        var angleGps = Math.Atan2(north, east);
        var rotRad = angleGps - angleAc;

        var height0 = point0.GpsCoordinate.Height;
        var height1 = point1.GpsCoordinate.Height;
        var y0 = point0.Y;
        var y1 = point1.Y;
        var heightScale = (height0 - height1) / (y0 - y1);
        var heightOffset = (height1 * y0 - height0 * y1) / (y0 - y1);

        return new GeoConverter(point0, hScale, heightScale, heightOffset, rotRad, earthRadiusMeters);
    }

    public GpsCoordinate FromGameCoordinates(float acX, float acY, float acZ)
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

        var east = _hScale * eastRot;
        var north = _hScale * northRot;

        // Convert meters to lat/lon degrees around GPS origin.
        var dLat = north / _earthRadius;
        var dLon = east / _lonRadius;

        var lat = _originGps.Latitude + dLat * 180.0 / Math.PI;
        var lon = _originGps.Longitude + dLon * 180.0 / Math.PI;
        var height = _heightScale * acY + _heightOffset;

        return new GpsCoordinate(lat, lon, height);
    }
}
