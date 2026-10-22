using Pwiz.Analysis.PeakFilters;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

[TestClass]
public class IsolationWindowFilterTests
{
    private static readonly double[] PrecursorMz = { 100, 399.6, 400.4, 400.6, 401, 598.5, 601.9, 602.1, 800 };

    [TestMethod]
    public void Apply_GivenWindow_KeepsPeaksInsideIt()
    {
        var spectrum = MakeSpectrum(msLevel: 1, PrecursorMz);
        new IsolationWindowFilter(2, Window(600, lowerOffset: 1.5, upperOffset: null)).Apply(spectrum);

        // 598.5 to 600 + the default width of 2 on the side without an offset.
        CollectionAssert.AreEqual(new[] { 598.5, 601.9 }, spectrum.GetMZArray()!.Data.ToArray());
        CollectionAssert.AreEqual(new[] { 6.0, 7.0 }, spectrum.GetIntensityArray()!.Data.ToArray());
    }

    [TestMethod]
    public void Apply_ProductSpectra_KeepsPeaksInsideTheirWindowsOnce()
    {
        var list = new SpectrumListSimple();
        Add(list, MakeSpectrum(msLevel: 1, PrecursorMz));
        Add(list, MakeProduct(Window(400, lowerOffset: 0.5, upperOffset: 0.5)));
        Add(list, MakeProduct(Window(400.3, lowerOffset: 0.5, upperOffset: 0.5))); // overlaps the one above
        Add(list, MakeProduct(Window(600, lowerOffset: null, upperOffset: null))); // default width either side
        Add(list, MakeSpectrum(msLevel: 1, new[] { 1.0 }));                     // next cycle: stop here
        Add(list, MakeProduct(Window(800, lowerOffset: 1, upperOffset: 1)));

        var precursor = list.Spectra[0];
        new IsolationWindowFilter(2, list).Apply(precursor);

        CollectionAssert.AreEqual(new[] { 399.6, 400.4, 400.6, 598.5, 601.9 }, precursor.GetMZArray()!.Data.ToArray());
    }

    private static IsolationWindow Window(double target, double? lowerOffset, double? upperOffset)
    {
        var window = new IsolationWindow();
        window.Set(CVID.MS_isolation_window_target_m_z, target);
        if (lowerOffset.HasValue)
            window.Set(CVID.MS_isolation_window_lower_offset, lowerOffset.Value);
        if (upperOffset.HasValue)
            window.Set(CVID.MS_isolation_window_upper_offset, upperOffset.Value);
        return window;
    }

    private static Spectrum MakeSpectrum(int msLevel, double[] mz)
    {
        var s = new Spectrum { Id = "scan=" + mz.Length };
        s.Params.Set(CVID.MS_ms_level, msLevel);
        // intensity i + 1 at m/z index i, so the kept intensities identify the kept peaks
        s.SetMZIntensityArrays(mz, mz.Select((_, i) => (double) (i + 1)).ToArray(), CVID.MS_number_of_detector_counts);
        s.DefaultArrayLength = mz.Length;
        return s;
    }

    private static Spectrum MakeProduct(IsolationWindow window)
    {
        var s = MakeSpectrum(msLevel: 2, new[] { 200.0 });
        s.Precursors.Add(new Precursor { IsolationWindow = window });
        return s;
    }

    private static void Add(SpectrumListSimple list, Spectrum s)
    {
        s.Index = list.Spectra.Count;
        list.Spectra.Add(s);
    }
}
