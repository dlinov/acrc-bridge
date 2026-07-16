namespace ACRCBridge.Lib.RaceChrono.GPS;

internal sealed class GpsCourseCalculator
{
    private const double EarthRadiusMeters = 6_378_137;
    private const double MinimumMovementMeters = 1;
    private const float MinimumSpeedKmh = 1;

    private readonly object _sync = new();
    private (double Latitude, double Longitude)? _anchor;
    private float _lastCourseDegrees;

    public void Reset()
    {
        lock (_sync)
        {
            _anchor = null;
            _lastCourseDegrees = 0;
        }
    }

    public float Update(double latitude, double longitude, float speedKmh)
    {
        lock (_sync)
        {
            if (!IsValidCoordinate(latitude, longitude))
            {
                return _lastCourseDegrees;
            }

            if (_anchor is null || speedKmh < MinimumSpeedKmh)
            {
                _anchor = (latitude, longitude);
                return _lastCourseDegrees;
            }

            var anchor = _anchor.Value;
            if (DistanceMeters(anchor.Latitude, anchor.Longitude, latitude, longitude) < MinimumMovementMeters)
            {
                return _lastCourseDegrees;
            }

            _lastCourseDegrees = InitialBearingDegrees(
                anchor.Latitude,
                anchor.Longitude,
                latitude,
                longitude);
            _anchor = (latitude, longitude);
            return _lastCourseDegrees;
        }
    }

    private static bool IsValidCoordinate(double latitude, double longitude)
    {
        return double.IsFinite(latitude) &&
               double.IsFinite(longitude) &&
               latitude is >= -90 and <= 90 &&
               longitude is >= -180 and <= 180;
    }

    private static double DistanceMeters(
        double latitude0,
        double longitude0,
        double latitude1,
        double longitude1)
    {
        var latitude0Rad = DegreesToRadians(latitude0);
        var latitude1Rad = DegreesToRadians(latitude1);
        var deltaLatitude = DegreesToRadians(latitude1 - latitude0);
        var deltaLongitude = DegreesToRadians(longitude1 - longitude0);
        var haversine = Math.Pow(Math.Sin(deltaLatitude / 2), 2) +
                        Math.Cos(latitude0Rad) * Math.Cos(latitude1Rad) *
                        Math.Pow(Math.Sin(deltaLongitude / 2), 2);

        return 2 * EarthRadiusMeters * Math.Asin(Math.Sqrt(Math.Clamp(haversine, 0, 1)));
    }

    private static float InitialBearingDegrees(
        double latitude0,
        double longitude0,
        double latitude1,
        double longitude1)
    {
        var latitude0Rad = DegreesToRadians(latitude0);
        var latitude1Rad = DegreesToRadians(latitude1);
        var deltaLongitude = DegreesToRadians(longitude1 - longitude0);
        var y = Math.Sin(deltaLongitude) * Math.Cos(latitude1Rad);
        var x = Math.Cos(latitude0Rad) * Math.Sin(latitude1Rad) -
                Math.Sin(latitude0Rad) * Math.Cos(latitude1Rad) * Math.Cos(deltaLongitude);
        var bearing = Math.Atan2(y, x) * 180 / Math.PI;

        return (float)((bearing + 360) % 360);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}