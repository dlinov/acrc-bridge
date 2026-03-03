using ACRCBridge.Lib.Coordinates;
using ACRCBridge.Lib.Dto;
using Microsoft.Extensions.Configuration;

namespace ACRCBridge.Lib.Tests.Coordinates;

[TestClass]
public sealed class GeoConverterTests
{
    // Porsche Ring AC coordinates
    private const float ACZeroX = 0.0f;
    private const float ACZeroY = -0.50f;
    private const float ACZeroZ = 0.0f;
    private const float ACStartX = -299.5362f;
    private const float ACStartY = 0.45f;
    private const float ACStartZ = -132.2299f;
    private const float ACFurtherX = -135.2896f;
    private const float ACFurtherY = 1.63f;
    private const float ACFurtherZ = -388.3831f;

    // Porsche Ring GPS coordinates (approximate)
    private const double ZeroLat = 58.401111;
    private const double ZeroLon = 24.453306;
    private const double ZeroHeight = 4;
    private const double StartLat = 58.402361;
    private const double StartLon = 24.448056;
    private const double StartHeight = 5;
    private const double FurtherLat = 58.404604;
    private const double FurtherLon = 24.450934;
    private const double FurtherHeight = 6;

    private GeoConvertersCollection? _converters;

    [TestInitialize]
    public void Init()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Test.json", optional: false)
            .Build();
        var tracks = config.GetSection("Tracks").Get<IDictionary<string, TrackReferencePoints>>();
        Assert.IsNotNull(tracks);
        _converters = new GeoConvertersCollection(tracks);
    }

    [TestMethod]
    public void ConvertPorscheRingCoordinates_ToGpsCoordinates_WorksCorrectly()
    {
        var porscheRingConverter = _converters?.GetConverter("auto24ring_v2");
        Assert.IsNotNull(porscheRingConverter, "Ensure auto24ring_v2 config is present in the test config");

        var gpsZeroCoord = porscheRingConverter.FromGameCoordinates(ACZeroX, ACZeroY, ACZeroZ);
        var gpsStartCoord = porscheRingConverter.FromGameCoordinates(ACStartX, ACStartY, ACStartZ);
        var gpsFurtherCoord = porscheRingConverter.FromGameCoordinates(ACFurtherX, ACFurtherY, ACFurtherZ);

        Console.WriteLine($"Converted Zero AC to GPS Coordinates: Lat={gpsZeroCoord.Latitude}, Lon={gpsZeroCoord.Longitude}, Height={gpsZeroCoord.Height}");
        Console.WriteLine($"Converted Start AC to GPS Coordinates: Lat={gpsStartCoord.Latitude}, Lon={gpsStartCoord.Longitude}, Height={gpsStartCoord.Height}");
        Console.WriteLine($"Converted Further AC to GPS Coordinates: Lat={gpsFurtherCoord.Latitude}, Lon={gpsFurtherCoord.Longitude}, Height={gpsFurtherCoord.Height}");

        // Assert that the converted coordinates are within a small margin of error
        const double delta = 0.001; // Acceptable error margin in degrees
        const double deltaH = 0.2; // Height is less precise due to less precise data source
        Assert.AreEqual(ZeroLat, gpsZeroCoord.Latitude, delta, "Latitude conversion is incorrect.");
        Assert.AreEqual(ZeroLon, gpsZeroCoord.Longitude, delta, "Longitude conversion is incorrect.");
        Assert.AreEqual(StartLat, gpsStartCoord.Latitude, delta, "Latitude conversion is incorrect.");
        Assert.AreEqual(StartLon, gpsStartCoord.Longitude, delta, "Longitude conversion is incorrect.");
        Assert.AreEqual(FurtherLat, gpsFurtherCoord.Latitude, delta, "Latitude conversion is incorrect.");
        Assert.AreEqual(FurtherLon, gpsFurtherCoord.Longitude, delta, "Longitude conversion is incorrect.");
        Assert.AreEqual(ZeroHeight, gpsZeroCoord.Height, deltaH, "Height conversion is incorrect.");
        Assert.AreEqual(StartHeight, gpsStartCoord.Height, deltaH, "Height conversion is incorrect.");
        Assert.AreEqual(FurtherHeight, gpsFurtherCoord.Height, deltaH, "Height conversion is incorrect.");
    }

    [TestMethod]
    public void FromTwoReferencePairs_MapsReferencePointsExactly()
    {
        Assert.IsNotNull(_converters, "Ensure test initialization has run.");
        // AC reference points
        var ac0 = (X: ACZeroX, Y: ACZeroY, Z: ACZeroZ);
        var ac1 = (X: ACStartX, Y: ACStartY, Z: ACStartZ);

        // GPS reference points
        var gps0 = new GpsCoordinate(ZeroLat, ZeroLon, ZeroHeight);
        var gps1 = new GpsCoordinate(StartLat, StartLon, StartHeight);

        // Initialize a converter via the public API.
        var point0 = new ReferencePoint(ac0.X, ac0.Y, ac0.Z, gps0);
        var point1 = new ReferencePoint(ac1.X, ac1.Y, ac1.Z, gps1);
        _converters.AddConverter("test", GeoConverter.FromTwoReferencePoints(point0, point1));
        var converter = _converters.GetConverter("test");

        var out0 = converter.FromGameCoordinates(ac0.X, ac0.Y, ac0.Z);
        var out1 = converter.FromGameCoordinates(ac1.X, ac1.Y, ac1.Z);

        // These should match very closely since they are the calibration points.
        const double eps = 1e-9;
        const double epsH = 0.2; // Height is less precise due to less precise data source
        Assert.AreEqual(gps0.Latitude, out0.Latitude, eps);
        Assert.AreEqual(gps0.Longitude, out0.Longitude, eps);
        Assert.AreEqual(gps1.Latitude, out1.Latitude, eps);
        Assert.AreEqual(gps1.Longitude, out1.Longitude, eps);
        Assert.AreEqual(gps0.Height, out0.Height, epsH);
        Assert.AreEqual(gps1.Height, out1.Height, epsH);

        // And a third point should be in the expected ballpark (sanity check).
        var outFurther = converter.FromGameCoordinates(ACFurtherX, ACFurtherY, ACFurtherZ);
        Assert.IsInRange(58.39, 58.42, outFurther.Latitude);
        Assert.IsInRange(24.43, 24.47, outFurther.Longitude);
        Assert.IsInRange(5.5, 6.5, outFurther.Height);
    }
}