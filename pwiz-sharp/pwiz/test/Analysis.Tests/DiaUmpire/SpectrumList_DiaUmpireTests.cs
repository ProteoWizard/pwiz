using System.IO;
using Pwiz.Analysis;
using Pwiz.Analysis.DiaUmpire;
using Pwiz.Data.MsData;

namespace Pwiz.Analysis.Tests.DiaUmpire;

/// <summary>
/// Tests for the <see cref="SpectrumList_DiaUmpire"/> wrapper and the
/// <c>diaUmpire</c> entry in <see cref="SpectrumListFactory"/>. Wrapper happy-path
/// behavior is implicitly covered by <see cref="DiaUmpireParityTests"/> (which
/// runs through this wrapper); this file pins the factory argument-parsing edge
/// cases plus a shape sanity check.
/// </summary>
[TestClass]
public class SpectrumList_DiaUmpireTests
{
    [TestMethod]
    public void Wrapper_AndFactory_Behavior()
    {
        AssertWrapperCarriesDiaUmpireProcessingMethod();
        AssertFactoryParsesParamsArg();
        AssertFactoryThrowsWithoutParams();
        AssertFactoryThrowsOnMissingFile();
    }

    private static void AssertWrapperCarriesDiaUmpireProcessingMethod()
    {
        var (msd, inner) = DiaUmpireTests.BuildTinySwathMsd();
        using var sl = new SpectrumList_DiaUmpire(msd, inner, new Config());

        Assert.IsNotNull(sl.DataProcessing);
        bool sawConversion = false;
        foreach (var m in sl.DataProcessing!.ProcessingMethods)
            foreach (var up in m.UserParams)
                if (up.Name.Contains("DIA-Umpire", System.StringComparison.OrdinalIgnoreCase))
                    sawConversion = true;
        Assert.IsTrue(sawConversion, "DataProcessing should carry a DIA-Umpire conversion userParam");
        Assert.AreEqual(sl.Count, sl.DiaUmpire.PseudoMsMsKeys.Count);
    }

    private static void AssertFactoryParsesParamsArg()
    {
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
            (wrapped as System.IDisposable)?.Dispose();
        }
        finally { File.Delete(tmp); }
    }

    private static void AssertFactoryThrowsWithoutParams()
    {
        var (msd, inner) = DiaUmpireTests.BuildTinySwathMsd();
        Assert.ThrowsException<System.ArgumentException>(() =>
            SpectrumListFactory.Wrap(inner, new[] { "diaUmpire" }, msd));
    }

    private static void AssertFactoryThrowsOnMissingFile()
    {
        var (msd, inner) = DiaUmpireTests.BuildTinySwathMsd();
        Assert.ThrowsException<FileNotFoundException>(() =>
            SpectrumListFactory.Wrap(inner, new[] { "diaUmpire params=doesnt-exist.params" }, msd));
    }
}
