namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// Port of the cpp Jamfile.jam <c>blib-test-filter</c> rows
/// (Jamfile.jam:383-395). Each filter test consumes a <c>.blib</c> produced by
/// a previous <see cref="BuildTests"/> build test, so each method calls the
/// dependency build first to make the tests self-contained and parallel-safe.
/// </summary>
[TestClass]
public class FilterTests
{
    /// <summary>Jamfile.jam:383 — <c>filter</c>. Depends on <c>merge</c>.</summary>
    [TestMethod]
    public void Filter()
    {
        new BuildTests().Merge();

        TestRunner.RunBlibTest(
            testName: nameof(Filter),
            tool: BlibTool.BlibFilter,
            args: new[] { "--unicode" },
            inputFilenames: new[] { "xmerged-redundant.blib" },
            outputBlibName: "zmerged-filtered.blib",
            referenceCheckName: "zmerged-filtered.check",
            inputsFromOutputDir: true);
    }

    /// <summary>Jamfile.jam:386 — <c>filter-mobility</c>. Depends on <c>mse-mobility</c>.</summary>
    [TestMethod]
    public void Filter_Mobility()
    {
        new BuildTests().Mse_Mobility();

        TestRunner.RunBlibTest(
            testName: nameof(Filter_Mobility),
            tool: BlibTool.BlibFilter,
            args: new[] { "--unicode" },
            inputFilenames: new[] { "mse-mobility.blib" },
            outputBlibName: "mse-mobility-filtered.blib",
            referenceCheckName: "mse-mobility-filtered.check",
            inputsFromOutputDir: true);
    }

    /// <summary>Jamfile.jam:389 — <c>filter-ssl-small-mol</c>. Depends on <c>ssl-small-mol</c>.</summary>
    [TestMethod]
    public void Filter_Ssl_SmallMol()
    {
        new BuildTests().Ssl_SmallMol();

        TestRunner.RunBlibTest(
            testName: nameof(Filter_Ssl_SmallMol),
            tool: BlibTool.BlibFilter,
            args: new[] { "--unicode" },
            inputFilenames: new[] { "ssl-small-mol.blib" },
            outputBlibName: "ssl-small-mol-filtered.blib",
            referenceCheckName: "ssl-small-mol-filtered.check",
            inputsFromOutputDir: true);
    }

    /// <summary>Jamfile.jam:392 — <c>filter-best-scoring-one</c>. Depends on <c>maxquant3</c>.</summary>
    [TestMethod]
    public void Filter_BestScoring_One()
    {
        new BuildTests().MaxQuant3();

        TestRunner.RunBlibTest(
            testName: nameof(Filter_BestScoring_One),
            tool: BlibTool.BlibFilter,
            args: new[] { "-b", "1", "--unicode" },
            inputFilenames: new[] { "maxquant3.blib" },
            outputBlibName: "filter-best-scoring-one.blib",
            referenceCheckName: "filter-best-scoring-one.check",
            inputsFromOutputDir: true);
    }

    /// <summary>Jamfile.jam:395 — <c>filter-best-scoring-multi</c>. Depends on <c>merge</c>.</summary>
    [TestMethod]
    public void Filter_BestScoring_Multi()
    {
        new BuildTests().Merge();

        TestRunner.RunBlibTest(
            testName: nameof(Filter_BestScoring_Multi),
            tool: BlibTool.BlibFilter,
            args: new[] { "-b", "1", "--unicode" },
            inputFilenames: new[] { "xmerged-redundant.blib" },
            outputBlibName: "filter-best-scoring-multi.blib",
            referenceCheckName: "filter-best-scoring-multi.check",
            inputsFromOutputDir: true);
    }
}
