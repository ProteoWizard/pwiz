using Pwiz.Util.Chemistry;

namespace Pwiz.Util.Tests.Chemistry;

[TestClass]
public class MZToleranceTests
{
    private const double Epsilon = 1e-9;

    [TestMethod]
    public void PlusMinus_AbsoluteAndPpm()
    {
        // Da (absolute) tolerance adds/subtracts the literal value.
        var da = new MZTolerance(0.1);
        Assert.AreEqual(1000.1, 1000.0 + da, Epsilon);
        Assert.AreEqual(999.9, 1000.0 - da, Epsilon);

        // ppm tolerance scales by |operand|, so negative operands flip the +/- sense.
        var ppm = new MZTolerance(5, MZToleranceUnits.Ppm);
        Assert.AreEqual(1000.005, 1000.0 + ppm, Epsilon);
        Assert.AreEqual(999.995, 1000.0 - ppm, Epsilon);
        Assert.AreEqual(-999.995, -1000.0 + ppm, Epsilon);
        Assert.AreEqual(-1000.005, -1000.0 - ppm, Epsilon);
    }

    [TestMethod]
    public void IsWithinTolerance_PpmAndDa()
    {
        // 5 ppm window centered at 1000 -> [999.995, 1000.005].
        var fiveppm = new MZTolerance(5, MZToleranceUnits.Ppm);
        Assert.IsTrue(MZTolerance.IsWithinTolerance(1000.001, 1000, fiveppm), "ppm: just inside upper");
        Assert.IsTrue(MZTolerance.IsWithinTolerance(999.997, 1000, fiveppm), "ppm: just inside lower");
        Assert.IsFalse(MZTolerance.IsWithinTolerance(1000.01, 1000, fiveppm), "ppm: outside upper");
        Assert.IsFalse(MZTolerance.IsWithinTolerance(999.99, 1000, fiveppm), "ppm: outside lower");

        // 0.01 Da absolute window centered at 1000 -> [999.99, 1000.01].
        var delta = new MZTolerance(0.01);
        Assert.IsTrue(MZTolerance.IsWithinTolerance(1000.001, 1000, delta), "Da: inside");
        Assert.IsTrue(MZTolerance.IsWithinTolerance(999.999, 1000, delta), "Da: inside (lower)");
        Assert.IsFalse(MZTolerance.IsWithinTolerance(1000.1, 1000, delta), "Da: outside");
    }

    [TestMethod]
    public void RecordEquality_ValueAndUnitsBothMatter()
    {
        Assert.AreEqual(new MZTolerance(5, MZToleranceUnits.Ppm), new MZTolerance(5, MZToleranceUnits.Ppm));
        Assert.AreNotEqual(new MZTolerance(5, MZToleranceUnits.Ppm), new MZTolerance(5, MZToleranceUnits.Mz));
    }

    [TestMethod]
    public void Parse_AllFormatsAndErrorPaths()
    {
        // Whitespace, case, and unit-name aliases all parse to the same canonical (value, units).
        var cases = new[]
        {
            ("5ppm", 5.0, MZToleranceUnits.Ppm),
            ("5 ppm", 5.0, MZToleranceUnits.Ppm),
            ("5.0 PPM", 5.0, MZToleranceUnits.Ppm),
            ("4.2mz", 4.2, MZToleranceUnits.Mz),
            ("4.20 da", 4.20, MZToleranceUnits.Mz),
            ("4.2 DALTONS", 4.2, MZToleranceUnits.Mz),
            ("0.01 m/z", 0.01, MZToleranceUnits.Mz),
        };
        foreach (var (input, expectedValue, expectedUnits) in cases)
        {
            var parsed = MZTolerance.Parse(input);
            Assert.AreEqual(expectedValue, parsed.Value, Epsilon, $"Parse(\"{input}\") value");
            Assert.AreEqual(expectedUnits, parsed.Units, $"Parse(\"{input}\") units");
        }

        // Round-trip: Parse(ToString(x)) == x.
        var original = new MZTolerance(5, MZToleranceUnits.Ppm);
        Assert.AreEqual(original, MZTolerance.Parse(original.ToString()));

        // Error paths: Parse throws on bad input; TryParse returns false.
        Assert.ThrowsException<FormatException>(() => MZTolerance.Parse("5 nonsense"), "unknown unit");
        Assert.ThrowsException<FormatException>(() => MZTolerance.Parse("nothing here"), "no value");
        Assert.IsFalse(MZTolerance.TryParse("xyz", out _), "TryParse: nonsense");
        Assert.IsFalse(MZTolerance.TryParse(null, out _), "TryParse: null");
    }
}
