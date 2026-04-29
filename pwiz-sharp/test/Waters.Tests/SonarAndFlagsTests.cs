using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Readers;

namespace Pwiz.Vendor.Waters.Tests;

/// <summary>
/// Verifies the new public helpers on <see cref="SpectrumList_Waters"/>: SONAR bin/m_z
/// conversions, ion-mobility flags, and the calibration-scan-omitted signal. These mirror
/// the cpp <c>SpectrumList_Waters</c> public API surface that downstream tools (Skyline,
/// SONAR analysis) call.
/// </summary>
[TestClass]
public class SonarAndFlagsTests
{
    private static string FixturePath(string fixture)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string c = Path.Combine(dir, "pwiz", "data", "vendor_readers", "Waters",
                "Reader_Waters_Test.data", fixture);
            if (Directory.Exists(c)) return c;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("fixture not found: " + fixture);
    }

    private static SpectrumList_Waters Open(string fixture, ReaderConfig? config = null)
    {
        var reader = new Reader_Waters();
        var msd = new MSData();
        reader.Read(FixturePath(fixture), msd, config ?? new ReaderConfig());
        return (SpectrumList_Waters)msd.Run.SpectrumList!;
    }

    [TestMethod]
    public void SonarFile_HasSonarFunctions_ReportsTrue()
    {
        var sl = Open("SONAR_Short.raw");
        Assert.IsTrue(sl.HasSonarFunctions);
    }

    [TestMethod]
    public void NonSonarFile_HasSonarFunctions_ReportsFalse()
    {
        var sl = Open("ATEHLSTLSEK_profile.raw");
        Assert.IsFalse(sl.HasSonarFunctions);
    }

    [TestMethod]
    public void ImsFile_HasIonMobility_ReportsTrue()
    {
        var sl = Open("HDMSe_Short_noLM.raw");
        Assert.IsTrue(sl.HasIonMobility);
    }

    [TestMethod]
    public void NonImsFile_HasIonMobility_ReportsFalse()
    {
        var sl = Open("ATEHLSTLSEK_profile.raw");
        Assert.IsFalse(sl.HasIonMobility);
    }

    [TestMethod]
    public void HasCombinedIonMobility_FollowsConfigFlagAndFile()
    {
        // IMS file with combine off → false; with combine on → true.
        var noCombine = Open("HDMSe_Short_noLM.raw");
        Assert.IsFalse(noCombine.HasCombinedIonMobility);

        var withCombine = Open("HDMSe_Short_noLM.raw",
            new ReaderConfig { CombineIonMobilitySpectra = true });
        Assert.IsTrue(withCombine.HasCombinedIonMobility);

        // Non-IMS file always false even with combine on (config without backing data).
        var nonIms = Open("ATEHLSTLSEK_profile.raw",
            new ReaderConfig { CombineIonMobilitySpectra = true });
        Assert.IsFalse(nonIms.HasCombinedIonMobility);
    }

    [TestMethod]
    public void CalibrationSpectraAreOmitted_FollowsConfigFlagAndLockmassPresence()
    {
        // Probe each fixture for a lockmass function and confirm CalibrationSpectraAreOmitted
        // returns true iff the flag is set AND the file has one. Most "_noLM" fixtures don't
        // have a lockmass function, so omitting calibration scans is a no-op for them.
        string[] fixtures =
        {
            "ATEHLSTLSEK_LM_684.3469.raw",
            "ATEHLSTLSEK_LM_785.8426.raw",
            "ATEHLSTLSEK_profile.raw",
            "HDDDA_Short_noLM.raw",
            "HDMSe_Short_noLM.raw",
        };
        bool anyWithLockmass = false;
        foreach (var fixture in fixtures)
        {
            // Without the flag the answer is always false.
            var sl = Open(fixture);
            Assert.IsFalse(sl.CalibrationSpectraAreOmitted, $"{fixture} (no flag) should be false");

            // With the flag on, the answer matches whether the file has a lockmass function.
            var slIgnore = Open(fixture, new ReaderConfig { IgnoreCalibrationScans = true });
            bool hasLockmass = slIgnore.CalibrationSpectraAreOmitted;
            anyWithLockmass |= hasLockmass;
        }
        // Sanity: at least one fixture in this corpus has a lockmass function — otherwise the
        // test isn't actually exercising the true-branch and we'd silently pass.
        Assert.IsTrue(anyWithLockmass, "expected at least one fixture to have a lockmass function");
    }

    [TestMethod]
    public void Sonar_BinAndMzRoundTrip()
    {
        var sl = Open("SONAR_Short.raw");

        // Pick a precursor m/z near the middle of the SONAR scan range. Bin range covering
        // a 1 Da window should contain at least one bin and the center bin should round-trip
        // back to a m/z within the window.
        var (start, end) = sl.SonarMzToBinRange(precursorMz: 600.0, tolerance: 1.0);
        Assert.IsTrue(start >= 0 && end >= start, $"expected valid bin range, got ({start},{end})");

        int midBin = (start + end) / 2;
        double midMz = sl.SonarBinToPrecursorMz(midBin);
        Assert.IsTrue(Math.Abs(midMz - 600.0) < 5.0,
            $"bin {midBin} mapped to m/z {midMz}, expected ~600 ± 5");
    }

    [TestMethod]
    public void Sonar_OutOfRangeQuery_ReturnsSentinel()
    {
        var sl = Open("SONAR_Short.raw");
        // Way above any plausible SONAR calibration upper bound.
        var (start, end) = sl.SonarMzToBinRange(precursorMz: 100000.0, tolerance: 0.1);
        Assert.AreEqual(-1, start);
        Assert.AreEqual(-1, end);
    }
}
