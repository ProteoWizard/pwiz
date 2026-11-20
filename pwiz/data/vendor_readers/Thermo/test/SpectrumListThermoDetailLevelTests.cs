using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;
using Pwiz.TestHarness;

namespace Pwiz.Vendor.Thermo.Tests;

/// <summary>
/// The metadata levels below a full read return what a full read does, without its peaks.
/// Skyline asks each spectrum for instant metadata first and escalates only when a value is
/// missing; its precursor lookup settles on the first level that returns any precursors, so a
/// fast read's precursors must equal a full read's.
/// </summary>
[TestClass]
public class SpectrumListThermoDetailLevelTests
{
    private static readonly string[] FIXTURES =
    {
        // Triple-play: the selected ion comes from a zoom scan.
        "090701-LTQVelos-unittest-01.raw",
        "BSA-FT-ETD.raw",
        "BSA-FT-HCD.raw",
        "FT-HCD-MSX.raw",
        "IT-HCD-SPS.raw",
        "IsolationMzOffset-ReportedMassOffset.raw",
        "source_cid_test_3scans.raw",
    };

    [TestMethod]
    public void Thermo_MetadataLevels_MatchFullMetadata()
    {
        foreach (var fixture in FIXTURES)
        {
            string path = PwizSharpPaths.CppVendorTestData("Thermo", fixture);
            if (!File.Exists(path))
                Assert.Inconclusive($"{fixture} not present under {Path.GetDirectoryName(path)}");

            using var msd = new MSData();
            new Reader_Thermo().Read(path, msd);
            var spectra = msd.Run.SpectrumList!;
            Assert.IsTrue(spectra.Count > 0, fixture + ": no spectra");
            for (int i = 0; i < spectra.Count; i++)
            {
                var full = spectra.GetSpectrum(i, DetailLevel.FullMetadata);
                var fast = spectra.GetSpectrum(i, DetailLevel.FastMetadata);
                var instant = spectra.GetSpectrum(i, DetailLevel.InstantMetadata);
                string id = fixture + " " + full.Id;

                AssertNoPeaks(instant, id + " instant");
                AssertNoPeaks(fast, id + " fast");
                Assert.AreEqual(Find(full.Params, CVID.MS_ms_level), Find(instant.Params, CVID.MS_ms_level), id + ": ms level");
                Assert.AreEqual(Find(full.ScanList.Scans[0], CVID.MS_scan_start_time),
                    Find(instant.ScanList.Scans[0], CVID.MS_scan_start_time), id + ": start time");
                AssertSamePrecursors(full, fast, id);
            }
        }
    }

    private static void AssertNoPeaks(Spectrum spectrum, string context)
    {
        Assert.AreEqual(0, spectrum.DefaultArrayLength, context + ": peak count");
        Assert.AreEqual(0, spectrum.BinaryDataArrays.Count, context + ": arrays");
    }

    /// <summary>
    /// Everything but the precursor intensity, which reads the precursor scan's peaks and so
    /// waits for full metadata.
    /// </summary>
    private static void AssertSamePrecursors(Spectrum full, Spectrum fast, string id)
    {
        Assert.AreEqual(full.Precursors.Count, fast.Precursors.Count, id + ": precursor count");
        for (int p = 0; p < full.Precursors.Count; p++)
        {
            var fullPrecursor = full.Precursors[p];
            var fastPrecursor = fast.Precursors[p];
            Assert.AreEqual(fullPrecursor.SpectrumId, fastPrecursor.SpectrumId, id + ": precursor spectrum");
            AssertParamsEqual(fullPrecursor.IsolationWindow, fastPrecursor.IsolationWindow, id + ": isolation window");
            AssertParamsEqual(fullPrecursor.Activation, fastPrecursor.Activation, id + ": activation");
            Assert.AreEqual(fullPrecursor.SelectedIons.Count, fastPrecursor.SelectedIons.Count, id + ": selected ions");
            for (int s = 0; s < fullPrecursor.SelectedIons.Count; s++)
            {
                var fullIon = fullPrecursor.SelectedIons[s];
                var fastIon = fastPrecursor.SelectedIons[s];
                CollectionAssert.AreEqual(fullIon.CVParams.Where(c => c.Cvid != CVID.MS_peak_intensity).ToList(),
                    fastIon.CVParams, id + ": selected ion");
                CollectionAssert.AreEqual(fullIon.UserParams, fastIon.UserParams, id + ": selected ion user params");
            }
        }
    }

    private static CVParam? Find(ParamContainer container, CVID cvid) =>
        container.CVParams.FirstOrDefault(c => c.Cvid == cvid);

    private static void AssertParamsEqual(ParamContainer expected, ParamContainer actual, string context)
    {
        Assert.IsTrue(expected.Equals(actual),
            $"{context}: [{string.Join(", ", expected.CVParams)}] vs [{string.Join(", ", actual.CVParams)}]");
    }
}
