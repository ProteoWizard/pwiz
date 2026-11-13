using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mgf;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Port of cpp's <c>SpectrumList_MGF_Test.cpp</c>, over cpp's own three-spectrum fixture
/// (<c>TestData/mgf/testMGF.txt</c>).
/// </summary>
/// <remarks>
/// MgfRoundTripTests writes MGF with our serializer and reads it back with our reader, so it holds
/// as long as the two agree. cpp instead reads a fixed fixture and asserts absolute values - the
/// title, the peak statistics it derives, and the two lookups MGF supports.
/// </remarks>
[TestClass]
public class SpectrumListMgfCppParityTests
{
    [TestMethod]
    public void CppFixture_ReadsCppsValues()
    {
        var list = ReadFixture().Run.SpectrumList!;

        Assert.AreEqual(3, list.Count, "spectrum count");
        for (int i = 0; i < 3; i++)
            Assert.AreEqual($"index={i}", list.SpectrumIdentity(i).Id, $"id[{i}]");

        // cpp: spectrum 0's title, level, and the statistics derived from its three peaks.
        var s = list.GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual("index=0", s.Id, "spectrum[0].id");
        Assert.AreEqual("small.pwiz.0003.0003.2",
            s.Params.CvParam(CVID.MS_spectrum_title).Value, "title");
        Assert.AreEqual(2, s.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0), "ms level");
        Assert.AreEqual(64.992226, s.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>(),
            1e-5, "TIC");
        Assert.AreEqual(231.38884, s.Params.CvParam(CVID.MS_base_peak_m_z).ValueAs<double>(),
            1e-5, "base peak m/z");
        Assert.AreEqual(26.545113, s.Params.CvParam(CVID.MS_base_peak_intensity).ValueAs<double>(),
            1e-5, "base peak intensity");

        Assert.AreEqual(1, s.Precursors.Count, "precursor count");
        Assert.AreEqual(1, s.Precursors[0].SelectedIons.Count, "selected ion count");
    }

    /// <summary>
    /// cpp calls this a hack and tests it deliberately: MGF has no scan numbers, but looking a
    /// spectrum up as scan=N resolves to the Nth spectrum so that callers holding a scan-based id
    /// still find it.
    /// </summary>
    [TestMethod]
    public void LookupByIndexAndByScanNumber_BothResolve()
    {
        var list = ReadFixture().Run.SpectrumList!;

        for (int i = 0; i < 3; i++)
            Assert.AreEqual(i, list.Find($"index={i}"), $"find index={i}");

        for (int i = 0; i < 3; i++)
            Assert.AreEqual(i, list.Find($"scan={i + 1}"), $"find scan={i + 1}");
    }

    private static MSData ReadFixture()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestData", "mgf", "testMGF.txt");
        return new MgfSerializer().Read(File.ReadAllText(path));
    }
}
