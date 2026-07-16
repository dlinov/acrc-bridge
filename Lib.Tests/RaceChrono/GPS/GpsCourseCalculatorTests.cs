using ACRCBridge.Lib.RaceChrono.GPS;

namespace ACRCBridge.Lib.Tests.RaceChrono.GPS;

[TestClass]
public sealed class GpsCourseCalculatorTests
{
    private const double Latitude = 58.402361;
    private const double Longitude = 24.448056;

    [TestMethod]
    public void Update_WhenMovementIsBelowThreshold_RetainsLastCourse()
    {
        var calculator = new GpsCourseCalculator();

        calculator.Update(Latitude, Longitude, speedKmh: 50);
        var eastboundCourse = calculator.Update(Latitude, Longitude + 0.0002, speedKmh: 50);
        var courseAfterJitter = calculator.Update(Latitude + 0.000001, Longitude + 0.0002, speedKmh: 50);

        Assert.AreEqual(90, eastboundCourse, 0.1);
        Assert.AreEqual(eastboundCourse, courseAfterJitter);
    }

    [TestMethod]
    public void Update_WhenStationary_RetainsCourseAndMovesAnchor()
    {
        var calculator = new GpsCourseCalculator();

        calculator.Update(Latitude, Longitude, speedKmh: 50);
        var eastboundCourse = calculator.Update(Latitude, Longitude + 0.0002, speedKmh: 50);
        var stationaryCourse = calculator.Update(Latitude + 0.001, Longitude + 0.001, speedKmh: 0);
        var northboundCourse = calculator.Update(Latitude + 0.0012, Longitude + 0.001, speedKmh: 50);

        Assert.AreEqual(eastboundCourse, stationaryCourse);
        Assert.AreEqual(0, northboundCourse, 0.1);
    }

    [TestMethod]
    public void Reset_ClearsPreviousCourseAndAnchor()
    {
        var calculator = new GpsCourseCalculator();

        calculator.Update(Latitude, Longitude, speedKmh: 50);
        calculator.Update(Latitude, Longitude + 0.0002, speedKmh: 50);
        calculator.Reset();

        var course = calculator.Update(Latitude + 0.001, Longitude + 0.001, speedKmh: 50);

        Assert.AreEqual(0, course);
    }
}