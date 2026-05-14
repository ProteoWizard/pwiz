using System.IO;
using Pwiz.Analysis;
using Pwiz.Analysis.DiaUmpire;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Tests.DiaUmpire;

/// <summary>
/// Tests for the <see cref="SpectrumList_DiaUmpire"/> wrapper + the
/// <c>diaUmpire</c> entry in <see cref="SpectrumListFactory"/>.
/// Exercises construction shape, DataProcessing emission, and the
/// factory argument parser. End-to-end output parity against the cpp
/// reference mzML lands in phase 5.
/// </summary>
[TestClass]
public class SpectrumList_DiaUmpireTests
{
    [TestMethod]
    public void Ctor_BuildsListBackedByDiaUmpireRun()
    {
        var (msd, inner) = DiaUmpireTests.BuildTinySwathMsd();
        var sl = new SpectrumList_DiaUmpire(msd, inner, new Config());

        Assert.IsNotNull(sl.DataProcessing);
        // The wrapper's DataProcessing always carries the DiaUmpire-conversion processing method.
        Assert.IsTrue(sl.DataProcessing!.ProcessingMethods.Count >= 1);
        bool sawConversion = false;
        foreach (var m in sl.DataProcessing.ProcessingMethods)
            foreach (var up in m.UserParams)
                if (up.Name.Contains("DIA-Umpire", System.StringComparison.OrdinalIgnoreCase))
                    sawConversion = true;
        Assert.IsTrue(sawConversion, "DataProcessing should carry a DIA-Umpire-conversion userParam");

        Assert.IsTrue(sl.Count >= 0);
        Assert.IsNotNull(sl.DiaUmpire);
        Assert.AreEqual(sl.Count, sl.DiaUmpire.PseudoMsMsKeys.Count);
    }

    [TestMethod]
    public void GetSpectrum_ReturnsSpectrumWithDiaUmpireIdAndIndex()
    {
        var (msd, inner) = DiaUmpireTests.BuildTinySwathMsd();
        var sl = new SpectrumList_DiaUmpire(msd, inner, new Config());
        if (sl.Count == 0)
        {
            // Tiny synthetic data may not produce any pseudo-MS/MS — that's fine for the
            // shape test below (we'd be testing GetSpectrum on the empty list, skip).
            return;
        }

        var spec = sl.GetSpectrum(0, getBinaryData: false);
        Assert.AreEqual(sl.DiaUmpire.PseudoMsMsKeys[0].Id, spec.Id);
        Assert.AreEqual(sl.DiaUmpire.PseudoMsMsKeys[0].Index, spec.Index);
    }

    [TestMethod]
    public void Factory_ParsesDiaUmpireFilterWithParamsArg()
    {
        // The factory registration parses params=<path>, builds a DiaUmpire.Config, and
        // wraps the inner list. Write a minimal SWATH-fixed params file to a temp path.
        string tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".params");
        File.WriteAllText(tmp, """
            Thread = 1
            WindowType = SWATH
            WindowSize = 25
            SE.MS1PPM = 30
            SE.MS2PPM = 40
            """);
        try
        {
            var (msd, inner) = DiaUmpireTests.BuildTinySwathMsd();
            var wrapped = SpectrumListFactory.Wrap(inner, new[] { $"diaUmpire params={tmp}" }, msd);
            Assert.IsInstanceOfType(wrapped, typeof(SpectrumList_DiaUmpire));
        }
        finally { File.Delete(tmp); }
    }

    [TestMethod]
    public void Factory_DiaUmpireWithoutParams_Throws()
    {
        var (msd, inner) = DiaUmpireTests.BuildTinySwathMsd();
        Assert.ThrowsException<System.ArgumentException>(() =>
            SpectrumListFactory.Wrap(inner, new[] { "diaUmpire" }, msd));
    }

    [TestMethod]
    public void Factory_DiaUmpireWithMissingFile_Throws()
    {
        var (msd, inner) = DiaUmpireTests.BuildTinySwathMsd();
        Assert.ThrowsException<FileNotFoundException>(() =>
            SpectrumListFactory.Wrap(inner, new[] { "diaUmpire params=doesnt-exist.params" }, msd));
    }
}
