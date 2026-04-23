using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.Common.Tests.Params;

[TestClass]
public class CVParamTests
{
    [TestMethod]
    public void StringValue_RoundTrips()
    {
        var p = new CVParam(CVID.MS_software, "proteowizard");
        Assert.AreEqual("proteowizard", p.Value);
    }

    [TestMethod]
    public void DoubleValue_UsesInvariantCulture()
    {
        var p = new CVParam(CVID.MS_scan_start_time, 3.14);
        // "R" format of 3.14 is "3.14" in invariant culture regardless of machine locale.
        Assert.IsTrue(p.Value.StartsWith("3.14", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BoolValue_TrueFalseStrings()
    {
        var t = new CVParam(CVID.CVID_Unknown, true);
        var f = new CVParam(CVID.CVID_Unknown, false);
        Assert.AreEqual("true", t.Value);
        Assert.AreEqual("false", f.Value);
    }

    [TestMethod]
    public void ValueAs_Double_Parses()
    {
        var p = new CVParam(CVID.MS_scan_start_time, "42.5");
        Assert.AreEqual(42.5, p.ValueAs<double>());
    }

    [TestMethod]
    public void ValueAs_Bool_Parses()
    {
        var p = new CVParam(CVID.CVID_Unknown, "true");
        Assert.IsTrue(p.ValueAs<bool>());
    }

    [TestMethod]
    public void ValueAs_EmptyString_ReturnsZero()
    {
        var p = new CVParam(CVID.MS_scan_start_time);
        Assert.AreEqual(0.0, p.ValueAs<double>());
        Assert.AreEqual(0, p.ValueAs<int>());
    }

    [TestMethod]
    public void IsEmpty_DefaultConstructor_True()
    {
        Assert.IsTrue(new CVParam().IsEmpty);
    }

    [TestMethod]
    public void IsEmpty_WithCvid_False()
    {
        Assert.IsFalse(new CVParam(CVID.MS_software).IsEmpty);
    }

    [TestMethod]
    public void TimeInSeconds_MinuteUnits_ScalesBy60()
    {
        var p = new CVParam(CVID.MS_scan_start_time, 2.5, CVID.UO_minute);
        Assert.AreEqual(150.0, p.TimeInSeconds(), 1e-9);
    }

    [TestMethod]
    public void TimeInSeconds_UnknownUnits_ReturnsZero()
    {
        var p = new CVParam(CVID.MS_scan_start_time, 1.0, CVID.CVID_Unknown);
        Assert.AreEqual(0.0, p.TimeInSeconds());
    }

    [TestMethod]
    public void ValueFixedNotation_WithExponent_ExpandsDigits()
    {
        var p = new CVParam(CVID.MS_scan_start_time, "1.5e-3");
        Assert.IsFalse(p.ValueFixedNotation().Contains('e', StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Equality_SameFields_Equal()
    {
        var a = new CVParam(CVID.MS_software, "pwiz");
        var b = new CVParam(CVID.MS_software, "pwiz");
        Assert.AreEqual(a, b);
        Assert.IsTrue(a == b);
    }

    [TestMethod]
    public void Equality_DifferentValue_NotEqual()
    {
        var a = new CVParam(CVID.MS_software, "pwiz");
        var b = new CVParam(CVID.MS_software, "other");
        Assert.AreNotEqual(a, b);
    }
}
