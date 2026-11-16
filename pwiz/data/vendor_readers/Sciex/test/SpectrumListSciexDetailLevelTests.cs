using Pwiz.Analysis;
using Pwiz.Analysis.PeakPicking;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Spectra;
using Pwiz.TestHarness;

namespace Pwiz.Vendor.Sciex.Tests;

/// <summary>
/// The detail level a caller asks for reaches the WIFF readers, plain and through vendor peak
/// picking, and what instant metadata reports agrees with a full read. Skyline asks every spectrum for instant metadata (ms level, start time,
/// precursor) before it extracts anything, so a peak read there is paid once per spectrum per
/// import.
/// </summary>
[TestClass]
public class SpectrumListSciexDetailLevelTests
{
    // Spectra compared field by field against a full read; instant metadata is checked on all.
    private const int COMPARED_SPECTRA = 50;

    // Present at full metadata and above only: each needs the peaks or a whole-experiment
    // chromatogram, which is why instant metadata leaves them out.
    private static readonly CVID[] FULL_METADATA_ONLY =
    {
        CVID.MS_total_ion_current, CVID.MS_base_peak_m_z, CVID.MS_base_peak_intensity,
    };

    [TestMethod]
    public void Sciex_InstantMetadata_ReadsNoPeaks()
    {
        // The only in-tree legacy .wiff with profile MS2, so vendor centroiding has peaks to pick.
        using var wiff = AbstractWiffFile.Open(RequireFixture("201208-378803.wiff"));
        var plain = new SpectrumList_Sciex(wiff, ownsWiff: false, defaultInstrumentConfiguration: null,
            simAsSpectra: false, srmAsSpectra: false);
        var centroided = new SpectrumList_PeakPicker(plain, new VendorOnlyPeakDetector(),
            preferVendorPeakPicking: true, msLevelsToPeakPick: "1-");
        Assert.IsTrue(plain.Count > COMPARED_SPECTRA, "fixture should have more spectra than are compared");

        for (int i = 0; i < plain.Count; i++)
        {
            plain.GetSpectrum(i, DetailLevel.InstantMetadata);
            centroided.GetSpectrum(i, DetailLevel.InstantMetadata);
        }
        Assert.AreEqual(0, SpectrumDataReads(wiff), "instant metadata read peaks from the SDK");

        AssertInstantMatchesFull(plain, centroided, COMPARED_SPECTRA);
        // Without this the zero above could come from a counter that never counts.
        Assert.IsTrue(SpectrumDataReads(wiff) >= 2 * COMPARED_SPECTRA, "full data did not read peaks");

        // wiff2 takes an instant-metadata precursor from a plain SDK read and a full one from the
        // read with framing zeros and centroiding; the product ion scans here carry a collision
        // energy, so a disagreement between the two reads would show.
        using var wiff2 = AbstractWiffFile.Open(RequireFixture("ACO-CE-DKTGKN_2-332-Positive-2026-10-21-10-20-43.wiff2"));
        var plain2 = new SpectrumList_Sciex(wiff2, ownsWiff: false, defaultInstrumentConfiguration: null,
            simAsSpectra: false, srmAsSpectra: false);
        var centroided2 = new SpectrumList_PeakPicker(plain2, new VendorOnlyPeakDetector(),
            preferVendorPeakPicking: true, msLevelsToPeakPick: "1-");
        Assert.IsTrue(plain2.Count > 0, "wiff2 fixture should have spectra");
        AssertInstantMatchesFull(plain2, centroided2, Math.Min(plain2.Count, COMPARED_SPECTRA));
    }

    private static void AssertInstantMatchesFull(SpectrumList_Sciex plain, SpectrumList_PeakPicker centroided, int count)
    {
        for (int i = 0; i < count; i++)
        {
            AssertSameMetadata(plain.GetSpectrum(i, DetailLevel.InstantMetadata),
                plain.GetSpectrum(i, DetailLevel.FullData));
            AssertSameMetadata(centroided.GetSpectrum(i, DetailLevel.InstantMetadata),
                centroided.GetSpectrum(i, DetailLevel.FullData));
        }
    }

    private static int SpectrumDataReads(AbstractWiffFile wiff)
    {
        int reads = 0;
        for (int e = 0; e < wiff.ExperimentCount; e++)
            reads += ((WiffExperiment)wiff.GetExperiment(e)).SpectrumDataReads;
        return reads;
    }

    /// <summary>
    /// Everything instant metadata reports matches the full read, and the full read adds only
    /// <see cref="FULL_METADATA_ONLY"/> and the data.
    /// </summary>
    private static void AssertSameMetadata(Spectrum instant, Spectrum full)
    {
        string id = full.Id;
        Assert.AreEqual(id, instant.Id);
        Assert.AreEqual(0, instant.DefaultArrayLength, id + ": instant metadata has no peak count");
        Assert.AreEqual(0, instant.BinaryDataArrays.Count, id + ": instant metadata has no arrays");

        var fullParams = full.Params.CVParams.Where(p => !FULL_METADATA_ONLY.Contains(p.Cvid)).ToList();
        CollectionAssert.AreEquivalent(fullParams, instant.Params.CVParams, id + ": spectrum cvParams");

        AssertParamsEqual(full.ScanList, instant.ScanList, id + ": scan list");
        Assert.AreEqual(full.ScanList.Scans.Count, instant.ScanList.Scans.Count, id + ": scan count");
        for (int s = 0; s < full.ScanList.Scans.Count; s++)
        {
            var fullScan = full.ScanList.Scans[s];
            var instantScan = instant.ScanList.Scans[s];
            AssertParamsEqual(fullScan, instantScan, id + ": scan");
            Assert.AreEqual(fullScan.ScanWindows.Count, instantScan.ScanWindows.Count, id + ": scan windows");
            for (int w = 0; w < fullScan.ScanWindows.Count; w++)
                AssertParamsEqual(fullScan.ScanWindows[w], instantScan.ScanWindows[w], id + ": scan window");
        }

        Assert.AreEqual(full.Precursors.Count, instant.Precursors.Count, id + ": precursor count");
        for (int p = 0; p < full.Precursors.Count; p++)
        {
            var fullPrecursor = full.Precursors[p];
            var instantPrecursor = instant.Precursors[p];
            AssertParamsEqual(fullPrecursor.IsolationWindow, instantPrecursor.IsolationWindow, id + ": isolation window");
            AssertParamsEqual(fullPrecursor.Activation, instantPrecursor.Activation, id + ": activation");
            Assert.AreEqual(fullPrecursor.SelectedIons.Count, instantPrecursor.SelectedIons.Count, id + ": selected ions");
            for (int s = 0; s < fullPrecursor.SelectedIons.Count; s++)
                AssertParamsEqual(fullPrecursor.SelectedIons[s], instantPrecursor.SelectedIons[s], id + ": selected ion");
        }
        CollectionAssert.AreEqual(full.Products, instant.Products, id + ": products");
    }

    private static void AssertParamsEqual(ParamContainer expected, ParamContainer actual, string context)
    {
        Assert.IsTrue(expected.Equals(actual),
            $"{context}: [{string.Join(", ", expected.CVParams)}] vs [{string.Join(", ", actual.CVParams)}]");
    }

    private static string RequireFixture(string fileName)
    {
        string path = PwizSharpPaths.CppVendorTestData("Sciex", fileName);
        if (!File.Exists(path))
            Assert.Inconclusive($"{fileName} not present under {Path.GetDirectoryName(path)}");
        return path;
    }
}
