using Pwiz.Analysis.PeakPicking;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

[TestClass]
public class VendorCentroidGateTests
{
    /// <summary>
    /// The peak picker must hand its MS-level set to the vendor reader and let the reader decide,
    /// rather than centroiding first and checking the level afterwards.
    /// </summary>
    /// <remarks>
    /// This runs with no vendor SDK and no data file, which is the point: the ordering bug it
    /// guards against was reader-independent, and a CI leg built without vendor archives can still
    /// catch a regression. Before the set was threaded through, the picker called the vendor
    /// centroid path for every spectrum and only then tested the MS level, so a spectrum the caller
    /// never selected had already been centroided by the time the check ran. Two live symptoms:
    /// Waters DAD traces (level 0) came back with defaultArrayLength=0 because MassLynx cannot
    /// centroid an absorbance-vs-wavelength trace, and Shimadzu spectra (also level 0) escaped
    /// carrying "centroid spectrum" and "profile spectrum" at once.
    /// </remarks>
    [TestMethod]
    public void PeakPicker_OnlyVendorCentroidsSelectedMsLevels()
    {
        // Index 0 is an ordinary MS1; index 1 is a non-MS spectrum (level 0, the DAD/UV case);
        // index 2 is MS2. Asking for level 1 only must leave 1 and 2 untouched.
        var inner = new RecordingVendorList(1, 0, 2);
        var picked = new SpectrumList_PeakPicker(
            inner, new LocalMaximumPeakDetector(3), preferVendorPeakPicking: true, new IntegerSet(1));

        // The reader receives the picker's own set - without this the reader cannot gate at all.
        _ = picked.GetSpectrum(0, true);
        Assert.IsNotNull(inner.LastMsLevelsReceived, "picker did not pass an msLevel set to the reader");
        Assert.IsTrue(inner.LastMsLevelsReceived.Contains(1), "requested level 1 missing from the set");
        Assert.IsFalse(inner.LastMsLevelsReceived.Contains(0), "level 0 must not be selected by \"1\"");
        Assert.IsFalse(inner.LastMsLevelsReceived.Contains(2), "level 2 must not be selected by \"1\"");

        AssertCentroided(picked.GetSpectrum(0, true), "MS1 was requested and must be centroided");
        AssertUntouched(picked, inner, 1, "a non-MS spectrum (level 0) must never be centroided");
        AssertUntouched(picked, inner, 2, "MS2 was not requested and must not be centroided");
    }

    /// <summary>
    /// The string overload must understand the same interval forms as cpp, because that is the
    /// overload Skyline uses: MsDataFileImpl asks for "1-" when both MS1 and MS2 centroiding are
    /// wanted, and "2-" for MS2 only. Parsing those with a plain int.TryParse per token yields an
    /// EMPTY set, and an empty set gates every spectrum off - so vendor centroiding was skipped
    /// entirely and Skyline silently imported profile data. Only the bare "1" case survived, which
    /// is why this escaped the unit tests and only showed up as drifted peak areas in the perf
    /// suite (BrukerDiaPasefImportTest, TestThermoSureQuantFAIMS).
    /// </summary>
    [TestMethod]
    public void PeakPicker_MsLevelSpecUnderstandsIntervalForms()
    {
        // (spec, levels that must be selected, levels that must not be)
        var cases = new[]
        {
            ("1-", new[] { 1, 2, 3 }, new[] { 0 }),
            ("2-", new[] { 2, 3 }, new[] { 0, 1 }),
            ("1", new[] { 1 }, new[] { 0, 2 }),
            ("1,2", new[] { 1, 2 }, new[] { 0, 3 }),
            ("1-2", new[] { 1, 2 }, new[] { 0, 3 }),
        };

        foreach (var (spec, selected, notSelected) in cases)
        {
            var picker = new SpectrumList_PeakPicker(
                new RecordingVendorList(1), new LocalMaximumPeakDetector(3),
                preferVendorPeakPicking: true, spec);

            foreach (int level in selected)
                Assert.IsTrue(picker.MsLevels.Contains(level),
                    $"\"{spec}\" must select MS level {level}");
            foreach (int level in notSelected)
                Assert.IsFalse(picker.MsLevels.Contains(level),
                    $"\"{spec}\" must not select MS level {level}");
        }
    }

    /// <summary>A picked spectrum carries the centroid term and NOT the profile term.</summary>
    private static void AssertCentroided(Spectrum spec, string because)
    {
        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_centroid_spectrum), because);
        // Readers set both terms on purpose (SpectrumList_Shimadzu, mirroring cpp :217+:244) so the
        // picker knows the source was profile. The picker owns removing the contradiction; a
        // spectrum declared both profile and centroid is not a valid output.
        Assert.IsFalse(spec.Params.HasCVParam(CVID.MS_profile_spectrum),
            "picked spectrum still declares both profile and centroid");
    }

    /// <summary>An unselected spectrum is returned exactly as the plain read produced it.</summary>
    private static void AssertUntouched(SpectrumList_PeakPicker picked, RecordingVendorList inner,
                                        int index, string because)
    {
        var throughPicker = picked.GetSpectrum(index, true);
        var plain = inner.GetSpectrum(index, true);

        Assert.IsFalse(throughPicker.Params.HasCVParam(CVID.MS_centroid_spectrum), because);
        Assert.AreEqual(plain.DefaultArrayLength, throughPicker.DefaultArrayLength,
            "unselected spectrum lost or gained points: " + because);

        var expected = plain.GetMZArray();
        var actual = throughPicker.GetMZArray();
        Assert.IsNotNull(actual, "unselected spectrum came back with no m/z array: " + because);
        Assert.AreEqual(expected!.Data.Count, actual!.Data.Count, "m/z array length changed: " + because);
        for (int i = 0; i < expected.Data.Count; i++)
            Assert.AreEqual(expected.Data[i], actual.Data[i], 1e-12, "m/z[" + i + "] changed: " + because);
    }

    /// <summary>
    /// Minimal vendor list that records the set it is handed and gates on it the way a real reader
    /// does. Profile input is shaped so the local-maximum detector would visibly reduce it.
    /// </summary>
    private sealed class RecordingVendorList : SpectrumListBase, IVendorCentroidingSpectrumList
    {
        private readonly int[] _msLevels;

        public RecordingVendorList(params int[] msLevelsPerSpectrum) => _msLevels = msLevelsPerSpectrum;

        public IntegerSet? LastMsLevelsReceived { get; private set; }

        public override int Count => _msLevels.Length;

        public override SpectrumIdentity SpectrumIdentity(int index) =>
            new() { Index = index, Id = "scan=" + (index + 1) };

        public override Spectrum GetSpectrum(int index, bool getBinaryData = false) =>
            Build(index, centroided: false);

        public string VendorCentroidName => "fake vendor peak picking";

        public Spectrum GetCentroidSpectrum(int index, bool getBinaryData, IntegerSet msLevelsToCentroid)
        {
            LastMsLevelsReceived = msLevelsToCentroid;
            return Build(index, msLevelsToCentroid.Contains(_msLevels[index]));
        }

        private Spectrum Build(int index, bool centroided)
        {
            var spec = new Spectrum { Index = index, Id = "scan=" + (index + 1) };
            if (_msLevels[index] > 0)
                spec.Params.Set(CVID.MS_ms_level, _msLevels[index]);

            spec.Params.Set(CVID.MS_profile_spectrum);
            if (centroided)
                spec.Params.Set(CVID.MS_centroid_spectrum);

            double[] mz = centroided ? new[] { 100.0 } : new[] { 99.0, 100.0, 101.0 };
            double[] intensity = centroided ? new[] { 5.0 } : new[] { 0.0, 5.0, 0.0 };
            spec.DefaultArrayLength = mz.Length;
            spec.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
            return spec;
        }
    }
}
