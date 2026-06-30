using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Tests.ChromatogramProcessing;

[TestClass]
public class ChromatogramListFactoryTests
{
    [TestMethod]
    public void Wrap_IndexFilter_ParsesIntSetAndFilters()
    {
        var inner = ChromatogramListFilterTests.Build(count: 8);
        var wrapped = ChromatogramListFactory.Wrap(inner, "index [2,5]");
        Assert.AreEqual(4, wrapped.Count);
        Assert.AreEqual("chrom_2", wrapped.ChromatogramIdentity(0).Id);
        Assert.AreEqual("chrom_5", wrapped.ChromatogramIdentity(3).Id);
    }

    [TestMethod]
    public void Wrap_LockmassRefiner_NonWaters_LogsWarningAndPassesThrough()
    {
        // Non-Waters inner — the wrapper warns but still wraps; chromatograms pass through.
        var inner = ChromatogramListFilterTests.Build();
        using var err = new System.IO.StringWriter();
        var savedErr = Console.Error;
        Console.SetError(err);
        try
        {
            var wrapped = ChromatogramListFactory.Wrap(inner, "lockmassRefiner mz=556.2771 tol=0.5");
            Assert.AreEqual(inner.Count, wrapped.Count);
            StringAssert.Contains(err.ToString(), "non-Waters", StringComparison.OrdinalIgnoreCase);
        }
        finally { Console.SetError(savedErr); }
    }

    [TestMethod]
    [DataRow("lockmassRefiner mz=0 tol=1", DisplayName = "mz must be positive")]
    [DataRow("lockmassRefiner mz=556 tol=0", DisplayName = "tol must be positive")]
    [DataRow("lockmassRefiner mz=556 mzNegIons=0 tol=1", DisplayName = "mzNegIons must be positive")]
    public void Wrap_LockmassRefiner_RejectsBadArgs(string spec)
    {
        var inner = ChromatogramListFilterTests.Build();
        Assert.ThrowsException<ArgumentException>(() => ChromatogramListFactory.Wrap(inner, spec));
    }

    [TestMethod]
    public void Wrap_UnknownFilter_Throws()
    {
        var inner = ChromatogramListFilterTests.Build();
        Assert.ThrowsException<ArgumentException>(() =>
            ChromatogramListFactory.Wrap(inner, "nonExistent foo"));
    }

    [TestMethod]
    public void WrapMsd_ReplacesChromatogramList_AndPromotesDataProcessing()
    {
        var msd = new MSData { Id = "doc" };
        msd.Run.ChromatogramList = ChromatogramListFilterTests.Build(count: 4);
        ChromatogramListFactory.Wrap(msd, new List<string> { "index 1-2" });

        Assert.IsNotNull(msd.Run.ChromatogramList);
        Assert.AreEqual(2, msd.Run.ChromatogramList!.Count);
        // Wrapper seeded a "pwiz_Chromatogram_Processing" DataProcessing — should be promoted
        // onto the document.
        Assert.IsTrue(msd.DataProcessings.Any(d => d.Id == "pwiz_Chromatogram_Processing"),
            "expected wrapper DataProcessing on msd.DataProcessings");
    }

    [TestMethod]
    public void WrapMsd_NoChromatogramList_ReturnsNullSilently()
    {
        // cpp returns silently when chromatogramListPtr is empty (ChromatogramListFactory.cpp:213).
        var msd = new MSData { Id = "no_chroms" };
        var result = ChromatogramListFactory.Wrap(msd, new List<string> { "index 0-1" });
        Assert.IsNull(result);
    }
}
