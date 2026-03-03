using ACRCBridge.Lib.Coordinates;
using ACRCBridge.Lib.Dto;
using Microsoft.Extensions.Configuration;

namespace ACRCBridge.Lib.Tests.Coordinates;

[TestClass]
public sealed class GeoConverterTests
{
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
        // Porsche Ring AC coordinates
        const float acZeroX = 0.0f;
        const float acZeroY = 0.0f;
        const float acZeroZ = 0.0f;
        const float acStartX = -299.5362f;
        const float acStartY = 0.0f;
        const float acStartZ = -132.2299f;
        const float acFurtherX = -135.2896f;
        const float acFurtherY = 0.0f;
        const float acFurtherZ = -388.3831f;

        // Expected GPS coordinates (approximate)
        const double expectedZeroLat = 58.401111; // Approximate expected latitude
        const double expectedZeroLon = 24.453306; // Approximate expected longitude
        const double expectedZeroHeight = 4; // Approximate expected height
        const double expectedStartLat = 58.402361; // Approximate expected latitude
        const double expectedStartLon = 24.448056; // Approximate expected longitude
        const double expectedStartHeight = 5; // Approximate expected height
        const double expectedFurtherLat = 58.404604; // Approximate expected latitude
        const double expectedFurtherLon = 24.450934; // Approximate expected longitude
        const double expectedFurtherHeight = 6; // Approximate expected height

        var porscheRingConverter = _converters?.GetConverter("auto24ring_v2");
        Assert.IsNotNull(porscheRingConverter, "Ensure auto24ring_v2 config is present in the test config");

        var gpsZeroCoord = porscheRingConverter.FromGameCoordinates(acZeroX, acZeroY, acZeroZ);
        var gpsStartCoord = porscheRingConverter.FromGameCoordinates(acStartX, acStartY, acStartZ);
        var gpsFurtherCoord = porscheRingConverter.FromGameCoordinates(acFurtherX, acFurtherY, acFurtherZ);

        Console.WriteLine($"Converted Zero AC to GPS Coordinates: Lat={gpsZeroCoord.Latitude}, Lon={gpsZeroCoord.Longitude}, Height={gpsZeroCoord.Height}");
        Console.WriteLine($"Converted Start AC to GPS Coordinates: Lat={gpsStartCoord.Latitude}, Lon={gpsStartCoord.Longitude}, Height={gpsStartCoord.Height}");
        Console.WriteLine($"Converted Further AC to GPS Coordinates: Lat={gpsFurtherCoord.Latitude}, Lon={gpsFurtherCoord.Longitude}, Height={gpsFurtherCoord.Height}");

        // Assert that the converted coordinates are within a small margin of error
        var delta = 0.001; // Acceptable error margin in degrees
        Assert.AreEqual(expectedZeroLat, gpsZeroCoord.Latitude, delta, "Latitude conversion is incorrect.");
        Assert.AreEqual(expectedZeroLon, gpsZeroCoord.Longitude, delta, "Longitude conversion is incorrect.");
        Assert.AreEqual(expectedZeroHeight, gpsZeroCoord.Height, delta, "Height conversion is incorrect.");
        Assert.AreEqual(expectedStartLat, gpsStartCoord.Latitude, delta, "Latitude conversion is incorrect.");
        Assert.AreEqual(expectedStartLon, gpsStartCoord.Longitude, delta, "Longitude conversion is incorrect.");
        Assert.AreEqual(expectedStartHeight, gpsStartCoord.Height, delta, "Height conversion is incorrect.");
        Assert.AreEqual(expectedFurtherLat, gpsFurtherCoord.Latitude, delta, "Latitude conversion is incorrect.");
        Assert.AreEqual(expectedFurtherLon, gpsFurtherCoord.Longitude, delta, "Longitude conversion is incorrect.");
        Assert.AreEqual(expectedFurtherHeight, gpsFurtherCoord.Height, delta, "Height conversion is incorrect.");
    }

    [TestMethod]
    public void FromTwoReferencePairs_MapsReferencePointsExactly()
    {
        Assert.IsNotNull(_converters, "Ensure test initialization has run.");
        // AC reference points
        var ac0 = (X: 0.0f, Y: 0.0f, Z: 0.0f);
        var ac1 = (X: -299.5362f, Y: 0.0f, Z: -132.2299f);

        // GPS reference points
        var gps0 = new GpsCoordinate(58.401111, 24.453306, 4);
        var gps1 = new GpsCoordinate(58.402361, 24.448056, 5);

        // Initialize a converter via the public API.
        var point0 = new ReferencePoint(ac0.X, ac0.Y, ac0.Z, gps0);
        var point1 = new ReferencePoint(ac1.X, ac1.Y, ac1.Z, gps1);
        _converters.AddConverter("test", GeoConverter.FromTwoReferencePoints(point0, point1));
        var converter = _converters.GetConverter("test");

        var out0 = converter.FromGameCoordinates(ac0.X, ac0.Y, ac0.Z);
        var out1 = converter.FromGameCoordinates(ac1.X, ac1.Y, ac1.Z);

        // These should match very closely since they are the calibration points.
        const double eps = 1e-9;
        Assert.AreEqual(gps0.Latitude, out0.Latitude, eps);
        Assert.AreEqual(gps0.Longitude, out0.Longitude, eps);
        Assert.AreEqual(gps0.Height, out0.Height, eps);
        Assert.AreEqual(gps1.Latitude, out1.Latitude, eps);
        Assert.AreEqual(gps1.Longitude, out1.Longitude, eps);
        Assert.AreEqual(gps1.Height, out1.Height, eps);

        // And a third point should be in the expected ballpark (sanity check).
        var outFurther = converter.FromGameCoordinates(-135.2896f, 0.0f, -388.3831f);
        Assert.IsTrue(outFurther.Latitude is > 58.39 and < 58.42);
        Assert.IsTrue(outFurther.Longitude is > 24.43 and < 24.47);
        Assert.IsTrue(outFurther.Height is > 90 and < 130);
    }

    [TestMethod]
    public void FromTwoReferencePairs_MapsHeightFromGameY()
    {
        Assert.IsNotNull(_converters, "Ensure test initialization has run.");

        var point0 = new ReferencePoint(
            X: 0.0f,
            Z: 0.0f,
            GpsCoordinate: new GpsCoordinate(58.401111, 24.453306, 4),
            Y: 100.0f);
        var point1 = new ReferencePoint(
            X: -299.5362f,
            Z: -132.2299f,
            GpsCoordinate: new GpsCoordinate(58.402361, 24.448056, 5),
            Y: 110.0f);

        _converters.AddConverter("height-test", GeoConverter.FromTwoReferencePoints(point0, point1));
        var converter = _converters.GetConverter("height-test");

        var out0 = converter.FromGameCoordinates(0.0f, 0.0f, 0.0f);
        var outMid = converter.FromGameCoordinates(0.0f, 0.0f, 0.0f);

        const double eps = 1e-9;
        Assert.AreEqual(10.0, out0.Height, eps);
        Assert.IsTrue(outMid.Height is > 10.0 and < 15.0);
    }
}