using Pwiz.Analysis.Filters;
using Pwiz.Analysis.PeakFilters;
using Pwiz.Analysis.PeakPicking;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

/// <summary>
/// A wrapper asked for metadata below <see cref="DetailLevel.FullMetadata"/> passes that level to
/// the list it wraps and returns no peaks. Skyline and Osprey index a run through such requests,
/// and a wrapper that turned one into a full read made that pass a decode of every spectrum.
/// </summary>
[TestClass]
public class SpectrumListDetailLevelTests
{
    [TestMethod]
    public void Wrappers_MetadataLevels_ReachInnerListWithoutPeaks()
    {
        var wrappers = new (string Name, Func<ISpectrumList, ISpectrumList> Wrap)[]
        {
            ("filter", inner => new SpectrumListFilter(inner, new MsLevelPredicate(IntegerSet.Positive))),
            ("sorter", inner => new SpectrumListSorter(inner, (list, i) => i)),
            ("title maker", inner => new SpectrumListTitleMaker(new MSData(), inner, "<Id>")),
            ("peak filter", inner => new SpectrumListPeakFilter(inner, new ThresholdFilter())),
            ("zero samples filter", inner => new SpectrumListZeroSamplesFilter(inner)),
            ("m/z window", inner => new SpectrumListMzWindow(inner, 100, 200)),
            ("smoother", inner => new SpectrumList_Smoother(inner, new SavitzkyGolaySmoother(2, 5), IntegerSet.Positive)),
            ("m/z shift", inner => new SpectrumListMzShift(inner, new MZTolerance(1, MZToleranceUnits.Mz))),
            ("metadata fixer", inner => new SpectrumListMetadataFixer(inner)),
            ("ion mobility", inner => new SpectrumList_IonMobility(inner)),
            ("peak picker", inner => new SpectrumList_PeakPicker(inner, new LocalMaximumPeakDetector(3), false, IntegerSet.Positive)),
#if !NO_WATERS_SUPPORT
            ("lockmass refiner", inner => new SpectrumList_LockmassRefiner(inner, 0, 0, 0)),
#endif
        };

        foreach (var (name, wrap) in wrappers)
        {
            var inner = new LevelRecordingList();
            var wrapped = wrap(inner);
            foreach (var level in new[] { DetailLevel.InstantMetadata, DetailLevel.FastMetadata })
            {
                // Constructors may read the inner list, so only this request is recorded.
                inner.HighestLevelRequested = null;
                var spec = wrapped.GetSpectrum(0, level);
                string context = name + " at " + level;
                Assert.AreEqual(level, inner.HighestLevelRequested, context + ": level asked of the inner list");
                Assert.AreEqual(0, spec.DefaultArrayLength, context + ": peak count");
                Assert.AreEqual(0, spec.BinaryDataArrays.Sum(a => a.Data.Count), context + ": array points");
                Assert.AreEqual(1, spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0), context + ": ms level");
            }
        }

        // An in-memory list hands out its stored spectrum, and the picker changes the spectrum it
        // is given. A metadata read must not leave the stored spectrum labelled centroid with its
        // profile peaks unpicked, or every later read serves those peaks as picked.
        var simple = new SpectrumListSimple();
        simple.Spectra.Add(new LevelRecordingList().GetSpectrum(0, DetailLevel.FullData));
        var picker = new SpectrumList_PeakPicker(simple, new LocalMaximumPeakDetector(3), false, IntegerSet.Positive);
        picker.GetSpectrum(0, DetailLevel.FastMetadata);
        var picked = picker.GetSpectrum(0, DetailLevel.FullData);
        Assert.IsTrue(picked.GetMZArray()!.Data.Count < 10, "peaks picked from the stored spectrum");
        Assert.AreEqual(picked.GetMZArray()!.Data.Count, picked.DefaultArrayLength, "peak count of the stored spectrum");
    }

    /// <summary>
    /// One profile MS1 spectrum that carries its peak count from full metadata up and its
    /// arrays at full data, as the vendor readers do, and records the levels it is asked for.
    /// </summary>
    private sealed class LevelRecordingList : SpectrumListBase
    {
        private static readonly double[] MZ = { 100, 101, 102, 103, 104, 150, 151, 152, 153, 154 };
        private static readonly double[] INTENSITY = { 0, 5, 10, 5, 0, 0, 7, 14, 7, 0 };

        public DetailLevel? HighestLevelRequested { get; set; }

        public override int Count => 1;

        public override SpectrumIdentity SpectrumIdentity(int index) =>
            new() { Index = index, Id = "scan=1" };

        public override Spectrum GetSpectrum(int index, bool getBinaryData = false) =>
            GetSpectrum(index, getBinaryData ? DetailLevel.FullData : DetailLevel.FullMetadata);

        public override Spectrum GetSpectrum(int index, DetailLevel detailLevel)
        {
            if (HighestLevelRequested is null || detailLevel > HighestLevelRequested)
                HighestLevelRequested = detailLevel;

            var spec = new Spectrum { Index = index, Id = "scan=1" };
            spec.Params.Set(CVID.MS_ms_level, 1);
            spec.Params.Set(CVID.MS_MS1_spectrum);
            spec.Params.Set(CVID.MS_profile_spectrum);
            var scan = new Scan();
            scan.Set(CVID.MS_scan_start_time, 1.0, CVID.UO_minute);
            spec.ScanList.Scans.Add(scan);
            if (detailLevel >= DetailLevel.FullMetadata)
                spec.DefaultArrayLength = MZ.Length;
            if (detailLevel >= DetailLevel.FullData)
                spec.SetMZIntensityArrays(MZ, INTENSITY, CVID.MS_number_of_detector_counts);
            return spec;
        }
    }
}
