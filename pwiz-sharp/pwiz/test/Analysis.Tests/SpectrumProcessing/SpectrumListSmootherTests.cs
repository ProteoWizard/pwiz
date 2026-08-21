using Pwiz.Analysis.PeakPicking;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

[TestClass]
public class SpectrumListSmootherTests
{
    [TestMethod]
    public void Smooths_ProfileSpectrumOnly_WithinAllowedMsLevels()
    {
        var sl = new MemorySpectrumList();
        sl.Add(MakeSpectrum("scan=1", msLevel: 1, profile: true,
            mz: new[] { 100.0, 100.1, 100.2, 100.3, 100.4 },
            intensity: new[] { 0.0, 50.0, 200.0, 50.0, 0.0 }));
        sl.Add(MakeSpectrum("scan=2", msLevel: 2, profile: false, // already centroided
            mz: new[] { 200.0 },
            intensity: new[] { 1234.0 }));
        sl.Add(MakeSpectrum("scan=3", msLevel: 2, profile: true, // profile but excluded by msLevels
            mz: new[] { 300.0, 300.1, 300.2 },
            intensity: new[] { 1.0, 5.0, 1.0 }));

        var smoother = new SpectrumList_Smoother(sl,
            new SavitzkyGolaySmoother(polynomialOrder: 2, windowSize: 5),
            new IntegerSet(1, 1)); // smooth MS1 only

        // Profile MS1 → smoothed: same number of samples or more (ZeroSampleFiller may pad);
        // intensities have changed.
        var s1 = smoother.GetSpectrum(0, getBinaryData: true);
        var s1Int = s1.GetIntensityArray()!.Data;
        Assert.IsTrue(s1Int.Count >= 5);
        Assert.AreNotEqual(200.0, s1Int.Max(), 1e-9, "Profile MS1 should be smoothed (peak attenuated)");

        // Centroid MS2 → unchanged pass-through (one peak, exact intensity preserved).
        var s2 = smoother.GetSpectrum(1, getBinaryData: true);
        Assert.AreEqual(1, s2.GetIntensityArray()!.Data.Count);
        Assert.AreEqual(1234.0, s2.GetIntensityArray()!.Data[0], 1e-9);

        // Profile MS2 outside our msLevels → also pass-through.
        var s3 = smoother.GetSpectrum(2, getBinaryData: true);
        var s3Int = s3.GetIntensityArray()!.Data;
        CollectionAssert.AreEqual(new[] { 1.0, 5.0, 1.0 }, s3Int.ToArray(),
            comparer: System.Collections.Generic.Comparer<double>.Default);
    }

    [TestMethod]
    public void DataProcessing_AppendsSmoothingMethod()
    {
        var sl = new MemorySpectrumList();
        sl.Add(MakeSpectrum("scan=1", msLevel: 1, profile: true,
            mz: new[] { 100.0, 100.1 }, intensity: new[] { 0.0, 1.0 }));

        var smoother = new SpectrumList_Smoother(sl,
            new SavitzkyGolaySmoother(2, 5), new IntegerSet(1, 2));
        var dp = smoother.DataProcessing;
        Assert.IsNotNull(dp);
        Assert.IsTrue(dp.ProcessingMethods.Any(pm =>
            pm.Params.HasCVParam(CVID.MS_smoothing) &&
            pm.UserParams.Any(u => u.Name.Contains("Savitzky-Golay"))));
    }

    private static Spectrum MakeSpectrum(string id, int msLevel, bool profile,
        double[] mz, double[] intensity)
    {
        var s = new Spectrum { Id = id };
        s.Params.Set(CVID.MS_ms_level, msLevel);
        s.Params.Set(profile ? CVID.MS_profile_spectrum : CVID.MS_centroid_spectrum);
        s.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
        s.DefaultArrayLength = mz.Length;
        return s;
    }

    /// <summary>Minimal in-memory ISpectrumList for unit tests — holds spectra by index.</summary>
    private sealed class MemorySpectrumList : Pwiz.Data.MsData.Spectra.SpectrumListBase
    {
        private readonly List<Spectrum> _spectra = new();

        public void Add(Spectrum s)
        {
            s.Index = _spectra.Count;
            _spectra.Add(s);
        }

        public override int Count => _spectra.Count;
        public override SpectrumIdentity SpectrumIdentity(int index) =>
            new() { Index = index, Id = _spectra[index].Id };
        public override Spectrum GetSpectrum(int index, bool getBinaryData = false) => _spectra[index];
        public override Pwiz.Data.MsData.Processing.DataProcessing? DataProcessing => null;
    }
}
