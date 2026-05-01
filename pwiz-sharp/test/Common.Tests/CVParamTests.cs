using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.Common.Tests.Params;

[TestClass]
public class CVParamTests
{
    [TestMethod]
    public void Construction_AndValueRoundTripForAllSupportedTypes()
    {
        // String values pass through as-is.
        Assert.AreEqual("proteowizard", new CVParam(CVID.MS_software, "proteowizard").Value);

        // Doubles serialize with InvariantCulture so "3.14" is stable across locales.
        var doubleParam = new CVParam(CVID.MS_scan_start_time, 3.14);
        Assert.IsTrue(doubleParam.Value.StartsWith("3.14", StringComparison.Ordinal),
            $"expected '3.14...' got '{doubleParam.Value}'");

        // Bools serialize as lowercase "true" / "false".
        Assert.AreEqual("true", new CVParam(CVID.CVID_Unknown, true).Value);
        Assert.AreEqual("false", new CVParam(CVID.CVID_Unknown, false).Value);

        // ValueAs<T> parses the string representation back to typed values.
        Assert.AreEqual(42.5, new CVParam(CVID.MS_scan_start_time, "42.5").ValueAs<double>());
        Assert.IsTrue(new CVParam(CVID.CVID_Unknown, "true").ValueAs<bool>());

        // Empty value (CVID-only constructor) returns the type's default — 0 / 0.0 / etc.
        var empty = new CVParam(CVID.MS_scan_start_time);
        Assert.AreEqual(0.0, empty.ValueAs<double>());
        Assert.AreEqual(0, empty.ValueAs<int>());

        // IsEmpty distinguishes default-constructed (no CVID) from any CVID-bearing param.
        Assert.IsTrue(new CVParam().IsEmpty, "default ctor");
        Assert.IsFalse(new CVParam(CVID.MS_software).IsEmpty, "with CVID");
    }

    [TestMethod]
    public void TimeInSeconds_AndFixedNotation()
    {
        // UO_minute scales by 60.
        Assert.AreEqual(150.0,
            new CVParam(CVID.MS_scan_start_time, 2.5, CVID.UO_minute).TimeInSeconds(), 1e-9);

        // Unknown units return 0 (signals "can't interpret as time").
        Assert.AreEqual(0.0,
            new CVParam(CVID.MS_scan_start_time, 1.0, CVID.CVID_Unknown).TimeInSeconds());

        // ValueFixedNotation strips exponent form (mzML disallows scientific notation in CV values).
        var exp = new CVParam(CVID.MS_scan_start_time, "1.5e-3");
        Assert.IsFalse(exp.ValueFixedNotation().Contains('e', StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Equality_AllFieldsMatter()
    {
        var a = new CVParam(CVID.MS_software, "pwiz");
        var same = new CVParam(CVID.MS_software, "pwiz");
        var differentValue = new CVParam(CVID.MS_software, "other");
        Assert.AreEqual(a, same);
        Assert.IsTrue(a == same, "operator==");
        Assert.AreNotEqual(a, differentValue);
    }
}
