using Pwiz.Util.Chemistry;

namespace Pwiz.Util.Tests.Chemistry;

[TestClass]
public class MZToleranceTests
{
    private const double Epsilon = 1e-9;

    // --- absolute (Da) arithmetic ---

    [TestMethod]
    public void Plus_Mz_AddsLiteralValue()
    {
        var tolerance = new MZTolerance(0.1);
        Assert.AreEqual(1000.1, 1000.0 + tolerance, Epsilon);
    }

    [TestMethod]
    public void Minus_Mz_SubtractsLiteralValue()
    {
        var tolerance = new MZTolerance(0.1);
        Assert.AreEqual(999.9, 1000.0 - tolerance, Epsilon);
    }

    // --- ppm arithmetic ---

    [TestMethod]
    public void Plus_Ppm_ScalesByAbsoluteValue()
    {
        var tolerance = new MZTolerance(5, MZToleranceUnits.Ppm);
        Assert.AreEqual(1000.005, 1000.0 + tolerance, Epsilon);
    }

    [TestMethod]
    public void Minus_Ppm_ScalesByAbsoluteValue()
    {
        var tolerance = new MZTolerance(5, MZToleranceUnits.Ppm);
        Assert.AreEqual(999.995, 1000.0 - tolerance, Epsilon);
    }

    [TestMethod]
    public void Ppm_NegativeValue_UsesAbsoluteValue()
    {
        // From C++ test: -1000 + tolerance == -999.995 (ppm uses |d|)
        var tolerance = new MZTolerance(5, MZToleranceUnits.Ppm);
        Assert.AreEqual(-999.995, -1000.0 + tolerance, Epsilon);
        Assert.AreEqual(-1000.005, -1000.0 - tolerance, Epsilon);
    }

    // --- IsWithinTolerance ---

    [TestMethod]
    public void IsWithinTolerance_Ppm_InsideWindow_True()
    {
        var fiveppm = new MZTolerance(5, MZToleranceUnits.Ppm);
        Assert.IsTrue(MZTolerance.IsWithinTolerance(1000.001, 1000, fiveppm));
        Assert.IsTrue(MZTolerance.IsWithinTolerance(999.997, 1000, fiveppm));
    }

    [TestMethod]
    public void IsWithinTolerance_Ppm_OutsideWindow_False()
    {
        var fiveppm = new MZTolerance(5, MZToleranceUnits.Ppm);
        Assert.IsFalse(MZTolerance.IsWithinTolerance(1000.01, 1000, fiveppm));
        Assert.IsFalse(MZTolerance.IsWithinTolerance(999.99, 1000, fiveppm));
    }

    [TestMethod]
    public void IsWithinTolerance_Mz_AbsoluteWindow()
    {
        var delta = new MZTolerance(0.01);
        Assert.IsTrue(MZTolerance.IsWithinTolerance(1000.001, 1000, delta));
        Assert.IsTrue(MZTolerance.IsWithinTolerance(999.999, 1000, delta));
        Assert.IsFalse(MZTolerance.IsWithinTolerance(1000.1, 1000, delta));
    }

    // --- Equality ---

    [TestMethod]
    public void RecordEquality_SameValueAndUnits_Equal()
    {
        Assert.AreEqual(new MZTolerance(5, MZToleranceUnits.Ppm), new MZTolerance(5, MZToleranceUnits.Ppm));
    }

    [TestMethod]
    public void RecordEquality_DifferentUnits_NotEqual()
    {
        Assert.AreNotEqual(new MZTolerance(5, MZToleranceUnits.Ppm), new MZTolerance(5, MZToleranceUnits.Mz));
    }

    // --- Parse / ToString round-trip ---

    [DataTestMethod]
    [DataRow("5ppm", 5.0, MZToleranceUnits.Ppm)]
    [DataRow("5 ppm", 5.0, MZToleranceUnits.Ppm)]
    [DataRow("5.0 PPM", 5.0, MZToleranceUnits.Ppm)]
    [DataRow("4.2mz", 4.2, MZToleranceUnits.Mz)]
    [DataRow("4.20 da", 4.20, MZToleranceUnits.Mz)]
    [DataRow("4.2 DALTONS", 4.2, MZToleranceUnits.Mz)]
    [DataRow("0.01 m/z", 0.01, MZToleranceUnits.Mz)]
    public void Parse_KnownFormats_Succeeds(string input, double expectedValue, MZToleranceUnits expectedUnits)
    {
        var parsed = MZTolerance.Parse(input);
        Assert.AreEqual(expectedValue, parsed.Value, Epsilon);
        Assert.AreEqual(expectedUnits, parsed.Units);
    }

    [TestMethod]
    public void Parse_ToStringRoundTrip_Preserves()
    {
        var original = new MZTolerance(5, MZToleranceUnits.Ppm);
        var roundTripped = MZTolerance.Parse(original.ToString());
        Assert.AreEqual(original, roundTripped);
    }

    [TestMethod]
    public void Parse_InvalidUnits_Throws()
    {
        Assert.ThrowsException<FormatException>(() => MZTolerance.Parse("5 nonsense"));
    }

    [TestMethod]
    public void Parse_Malformed_Throws()
    {
        Assert.ThrowsException<FormatException>(() => MZTolerance.Parse("nothing here"));
    }

    [TestMethod]
    public void TryParse_Invalid_ReturnsFalse()
    {
        Assert.IsFalse(MZTolerance.TryParse("xyz", out _));
    }

    [TestMethod]
    public void TryParse_Null_ReturnsFalse()
    {
        Assert.IsFalse(MZTolerance.TryParse(null, out _));
    }
}
