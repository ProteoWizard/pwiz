using System.Globalization;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.Common.Tests.Params;

/// <summary>
/// Pins <see cref="PwizFloat"/> against boost::spirit::karma, which is what pwiz C++ uses to turn
/// doubles and floats into cvParam / XML attribute text. Every expected string in this file was
/// produced by running the value through a C++ harness built on the same karma headers and policies
/// pwiz configures (double12_policy, float5_policy, ChromatogramList_Agilent's nosci_policy), so a
/// failure here is a real divergence from the reference implementation, not a style opinion.
/// </summary>
[TestClass]
public class PwizFloatTests
{
    /// <summary>
    /// karma rounds in the binary domain — it scales only the fractional part by 10^precision and
    /// takes floor(x + 0.5). Rounding the whole value in the decimal domain instead disagrees
    /// whenever the scaled fraction lands near .5, which is what this table is mostly about.
    /// </summary>
    [TestMethod]
    public void ToPwizString_Double_MatchesKarmaDouble12Policy()
    {
        (double Value, string Expected)[] cases =
        {
            // --- the two regression pins -------------------------------------------------------
            // Fraction scales to 200614341470.497955, so floor(x + 0.5) keeps ...470. A decimal
            // round to 12 places sees a tie and goes to ...471; that was the old bug.
            (296.2006143414705, "296.20061434147"),
            (-296.2006143414705, "-296.20061434147"),
            // The double that motivated the old AwayFromZero pre-round: karma reproduces it too,
            // because 0.265625 * 1e5 is exactly 26562.5 and floor(26562.5 + 0.5) = 26563.
            // (as a double the value needs no rounding at all at precision 12; see the float test)
            (632.265625, "632.265625"),

            // --- rounding overflow carries into the integral part ------------------------------
            (1.9999999999999, "2.0"),
            (-1.9999999999999, "-2.0"),
            (0.9999999999999999, "1.0"),
            // Carry in scientific notation bumps the exponent as well.
            (999999.9999999999, "1.0e06"),
            (0.0009999999999999998, "1.0e-03"),

            // --- fixed / scientific selection: [1e-3, 1e5) is fixed, everything else is not -----
            (0.001, "0.001"),
            (-0.001, "-0.001"),
            (99999.999999999985, "99999.999999999985"),
            (100000.0, "1.0e05"),
            (-100000.0, "-1.0e05"),
            (0.0001, "1.0e-04"),
            (0.0001234, "1.234e-04"),
            (-1.234e-07, "-1.234e-07"),
            (5e-05, "5.0e-05"),
            (1e20, "1.0e20"),
            (1e22, "1.0e22"),
            (1e23, "1.0e23"),
            (1e-300, "1.0e-300"),
            (1.7976931348623157e308, "1.797693134862e308"),

            // --- zero, signed zero, and whole numbers keep exactly one fractional digit ---------
            (0.0, "0.0"),
            (-0.0, "0.0"),
            (1.0, "1.0"),
            (-1.0, "-1.0"),
            (123.0, "123.0"),
            (1059.0, "1059.0"),
            (309.0, "309.0"),

            // --- subnormals are clamped to the smallest normal before karma sees them ----------
            // (karma stack-overflows on them, so pwiz::util::toString clamps first)
            (2.2250738585072014e-308, "2.225073858507e-308"),
            (5e-324, "2.225073858507e-308"),
            (-5e-324, "-2.225073858507e-308"),

            // --- ordinary values, trailing zeros stripped, 12 fractional digits max -------------
            (0.5, "0.5"),
            (-0.5, "-0.5"),
            (0.1, "0.1"),
            (1234.5678, "1234.5678"),
            (12345.678901234567, "12345.678901234567"),
            (3.14159265358979, "3.14159265359"),
            (50.647, "50.647"),
            (228.996, "228.996"),
            (4.505483333, "4.505483333"),
            (0.00049999999999995, "5.0e-04"),
        };

        AssertAll(cases, v => PwizFloat.ToPwizString(v));
    }

    /// <summary>
    /// The float overload is not "the double algorithm on a widened value": karma instantiates the
    /// generator on <c>float</c>, so the scaling and rounding happen in single precision at
    /// precision 5. 632.265625f is the canonical case — 0.265625f * 1e5f is exactly 26562.5f.
    /// </summary>
    [TestMethod]
    public void ToPwizString_Float_MatchesKarmaFloat5Policy()
    {
        (float Value, string Expected)[] cases =
        {
            (632.265625f, "632.26563"),
            (1.0f, "1.0"),
            (0.0f, "0.0"),
            (-0.0f, "0.0"),
            (123.456f, "123.456"),
            (1234.5678f, "1234.56775"),
            (0.1f, "0.1"),
            (1.9999999f, "2.0"),          // carry
            (-1.9999999f, "-2.0"),
            (1e-4f, "1.0e-04"),
            (1e5f, "1.0e05"),
            (0.000123456f, "1.23456e-04"),
            (5.153265f, "5.15327"),       // single-precision rounding: the double path gives 5.15326
            (-1851525.0f, "-1.85152e06"),
            (3.4028235e38f, "3.40282e38"),
            (1.17549435e-38f, "1.17549e-38"),
            (1e-45f, "1.17549e-38"),      // subnormal, clamped to FLT_MIN
        };

        AssertAll(cases, v => PwizFloat.ToPwizString(v));
    }

    /// <summary>
    /// ChromatogramList_Agilent's nosci_policy: forced fixed notation, 9 fractional digits. Small
    /// magnitudes stay fixed (0.00005 does not become 5.0e-05) and anything below half of 1e-9
    /// flattens to "0.0".
    /// </summary>
    [TestMethod]
    public void ToKarmaNoSci_MatchesKarmaNosciPolicy()
    {
        (double Value, string Expected)[] cases =
        {
            (309.0, "309.0"),
            (228.996, "228.996"),
            (5e-05, "0.00005"),
            (4.505483333, "4.505483333"),
            (1059.0, "1059.0"),
            (0.0, "0.0"),
            (-2.5, "-2.5"),
            (1.9999999999, "2.0"),               // carry
            (123.456789012345, "123.456789012"), // truncated to 9 fractional digits
            (1e-10, "0.0"),                      // rounds away below the precision floor
            (4.999e-10, "0.0"),
            // Integral part past long.MaxValue: karma peels the digits off in floating point.
            (1e20, "100000000000000000000.0"),
        };

        AssertAll(cases, v => PwizFloat.ToKarmaNoSci(v));
    }

    /// <summary>
    /// karma signs a NaN off its <em>sign bit</em>, not off a comparison: real_policies::nan
    /// (real_policies.hpp:315) calls sign_inserter with traits::test_negative(n), and
    /// is_negative is specialized on core::signbit for float/double (numeric_utils.hpp:175-200).
    /// The quiet NaN both runtimes circulate — .NET's double.NaN / float.NaN, and MSVC's
    /// 0.0/0.0 — has that bit set, so pwiz C++ writes "-nan" into cvParams and so must we
    /// (e.g. every empty Agilent spectrum, whose base peak m/z and intensity are NaN).
    /// </summary>
    [TestMethod]
    public void NonFiniteValues_UseKarmaSpelling()
    {
        Assert.AreEqual("-nan", PwizFloat.ToPwizString(double.NaN));
        Assert.AreEqual("-nan", PwizFloat.ToPwizString(0.0 / 0.0));
        Assert.AreEqual("inf", PwizFloat.ToPwizString(double.PositiveInfinity));
        Assert.AreEqual("-inf", PwizFloat.ToPwizString(double.NegativeInfinity));
        Assert.AreEqual("-nan", PwizFloat.ToPwizString(float.NaN));
        Assert.AreEqual("inf", PwizFloat.ToPwizString(float.PositiveInfinity));
        Assert.AreEqual("-inf", PwizFloat.ToPwizString(float.NegativeInfinity));
        Assert.AreEqual("inf", PwizFloat.ToKarmaNoSci(double.PositiveInfinity));
        Assert.AreEqual("-nan", PwizFloat.ToKarmaNoSci(double.NaN));

        // A NaN whose sign bit is clear prints unsigned — the sign really is a bit test, not a
        // blanket "-nan" for everything non-numeric. .NET never mints one of these on its own,
        // so it has to be assembled from bits.
        double positiveNaN = BitConverter.Int64BitsToDouble(0x7FF8000000000000L);
        Assert.IsTrue(double.IsNaN(positiveNaN));
        Assert.AreEqual("nan", PwizFloat.ToPwizString(positiveNaN));
        float positiveNaNf = BitConverter.Int32BitsToSingle(0x7FC00000);
        Assert.IsTrue(float.IsNaN(positiveNaNf));
        Assert.AreEqual("nan", PwizFloat.ToPwizString(positiveNaNf));
    }

    /// <summary>The formatted text must parse back to a value that is still the closest double to
    /// the original at 12 fractional digits — i.e. the rounding never moves a digit it shouldn't.</summary>
    [TestMethod]
    public void ToPwizString_RoundTripsWithinPrecision()
    {
        double[] values = { 296.2006143414705, 1234.5678, 0.0001234, 99999.999999999985, 1e20, 0.1 };
        foreach (double v in values)
        {
            double parsed = double.Parse(PwizFloat.ToPwizString(v), NumberStyles.Float, CultureInfo.InvariantCulture);
            double tolerance = Math.Max(Math.Abs(v) * 1e-12, 1e-12);
            Assert.AreEqual(v, parsed, tolerance, $"round trip of {v:R}");
        }
    }

    private static void AssertAll<T>((T Value, string Expected)[] cases, Func<T, string> format)
    {
        var failures = new List<string>();
        foreach (var (value, expected) in cases)
        {
            string actual = format(value);
            if (actual != expected)
                failures.Add($"{value:R} -> expected '{expected}' but got '{actual}'");
        }
        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }
}
