using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Vendor.Waters.Tests;

/// <summary>
/// Unit tests for CCS ↔ drift time conversion exposed via
/// <see cref="IIonMobilityCcsConversion"/>. The conversions go through the MassLynx SDK's
/// <c>getCollisionalCrossSection</c> / <c>getDriftTime_CCS</c>, which require a CCS
/// calibration (<c>mob_cal.csv</c>) to be present in the .raw directory.
/// </summary>
[TestClass]
public class CcsConversionTests
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

    [TestMethod]
    public void HdmseShort_HasCcsCalibration_AndConvertsRoundTrip()
    {
        // HDMSe_Short_noLM ships with mob_cal.csv (Waters CCS reference compounds + curve
        // parameters in the file's header). The fixture's reference compound is at
        // m/z=556.2771, charge=1, and the calibration declares EPCMeasuredDT=4.36543 ms.
        // Round-tripping that DT through DriftTimeToCcs → CcsToDriftTime should land back on
        // the same DT to within ~0.1 ms (the calibration's RMS % CCS is 0.54%, so DT round-
        // trip drift is bounded by the calibration's fit error).
        var reader = new Reader_Waters();
        var msd = new MSData();
        reader.Read(FixturePath("HDMSe_Short_noLM.raw"), msd, new ReaderConfig());
        var sl = (IIonMobilityCcsConversion)msd.Run.SpectrumList!;

        Assert.IsTrue(sl.CanConvertIonMobilityAndCcs, "HDMSe_Short_noLM has mob_cal.csv");

        // Use one of the calibration's reference points (drift time 4.36543, m/z 556.2771,
        // charge 1) as a sanity probe. The expected CCS is in the calibration's tc/OmegaC
        // table — we can't predict the exact value the SDK returns without running it, so
        // assert it's in the right order of magnitude and round-trips.
        double driftTime = 4.36543;
        double mz = 556.2771;
        int charge = 1;
        double ccs = sl.IonMobilityToCcs(driftTime, mz, charge);
        Assert.IsTrue(ccs > 100 && ccs < 1000,
            $"CCS for the reference compound should be a typical small-molecule value (got {ccs} Å²)");

        double dtBack = sl.CcsToIonMobility(ccs, mz, charge);
        Assert.AreEqual(driftTime, dtBack, 0.1,
            "DT → CCS → DT should round-trip within calibration precision (~0.1 ms)");
    }

    [TestMethod]
    public void NfdmRaw_HasNoCcsCalibration()
    {
        // 091204_NFDM_008 is a non-IMS acquisition; no mob_cal.csv. CanConvertIonMobilityAndCcs
        // should be false (the conversion calls would fail if invoked).
        var reader = new Reader_Waters();
        var msd = new MSData();
        reader.Read(FixturePath("091204_NFDM_008.raw"), msd, new ReaderConfig());
        var sl = (IIonMobilityCcsConversion)msd.Run.SpectrumList!;
        Assert.IsFalse(sl.CanConvertIonMobilityAndCcs);
    }

    [TestMethod]
    public void SonarShort_HasNoCcsCalibration_EvenIfMobCalPresent()
    {
        // Per pwiz C++: SONAR uses IMS hardware but the m/z dimension replaces the mobility
        // dimension, so even if mob_cal.csv exists the file shouldn't claim CCS support.
        // SONAR_Short doesn't ship mob_cal.csv either, so this also tests the simpler
        // "no calibration file" case for an IMS fixture.
        var reader = new Reader_Waters();
        var msd = new MSData();
        reader.Read(FixturePath("SONAR_Short.raw"), msd, new ReaderConfig());
        var sl = (IIonMobilityCcsConversion)msd.Run.SpectrumList!;
        Assert.IsFalse(sl.CanConvertIonMobilityAndCcs);
    }
}
