namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// Port of the cpp Jamfile.jam <c>blib-test-search</c> rows
/// (Jamfile.jam:439-443). Search tests run BlibSearch against a query MS2/mzML
/// plus a <c>.blib</c> library. The library inputs typically come from a previous
/// <see cref="BuildTests"/> build run, so each method calls the dependency build
/// first to make the tests self-contained.
/// </summary>
/// <remarks>
/// Jamfile.jam:168 shows the search rule appends <c>--out=@&lt;path&gt;</c> (note the
/// <c>@</c> separator vs <c>=</c> used by build/filter). The C# harness uses
/// <c>--out=</c> uniformly via <see cref="TestRunner"/>; if the BlibSearch CLI port
/// requires the <c>@</c> form, adjust the runner rather than each test.
/// </remarks>
[TestClass]
public class SearchTests
{
    /// <summary>Jamfile.jam:439 — <c>search-demo</c>. Depends on <c>sqt-ms2</c>.</summary>
    [TestMethod]
    public void Search_Demo()
    {
        var fixture = GoldenFileFixture.Instance;
        if (fixture is null)
        {
            Assert.Inconclusive("BiblioSpec golden-file fixture not found.");
            return;
        }
        new BuildTests().Sqt_Ms2();

        // The search query .ms2 lives in inputs/; the .blib was just built into the OutputDir.
        // Mix the two by resolving both paths into args directly.
        TestRunner.RunBlibTest(
            testName: nameof(Search_Demo),
            tool: BlibTool.BlibSearch,
            args: new[]
            {
                "--unicode", "--preserve-order",
                fixture.InputFile("demo.ms2"),
                fixture.OutputFile("sqt-ms2.blib"),
            },
            outputBlibName: "search-demo.report",
            referenceCheckName: "demo.report",
            skipLinesName: "demo.skip-lines");
    }

    /// <summary>Jamfile.jam:440 — <c>search-demo-negative</c>. No build-test dependency; both inputs are checked-in.</summary>
    [TestMethod]
    public void Search_DemoNegative()
    {
        var fixture = GoldenFileFixture.Instance;
        if (fixture is null)
        {
            Assert.Inconclusive("BiblioSpec golden-file fixture not found.");
            return;
        }
        TestRunner.RunBlibTest(
            testName: nameof(Search_DemoNegative),
            tool: BlibTool.BlibSearch,
            args: new[]
            {
                "--unicode", "--preserve-order",
                fixture.InputFile("demo-negative.mzML"),
                fixture.InputFile("sqt-ms2-negative.blib"),
            },
            outputBlibName: "search-demo-negative.report",
            referenceCheckName: "demo-negative.report",
            skipLinesName: "demo.skip-lines");
    }

    /// <summary>Jamfile.jam:441 — <c>search-decoy</c>. Depends on <c>sqt-ms2</c> and
    /// <c>search-demo</c>. Custom test body because the cpp harness rewrites the
    /// <c>--out=foo.decoy.report</c> argument back to <c>--out=foo.report</c> before invoking
    /// BlibSearch (ExecuteBlib.cpp:186), then compares the <c>foo.decoy.report</c> that
    /// BlibSearch writes alongside it against the reference. <see cref="TestRunner.RunBlibTest"/>
    /// has a single output path that drives both <c>--out=</c> and the comparison, so we
    /// inline the rewrite here.</summary>
    [TestMethod]
    public void Search_Decoy()
    {
        var fixture = GoldenFileFixture.Instance;
        if (fixture is null)
        {
            Assert.Inconclusive("BiblioSpec golden-file fixture not found.");
            return;
        }
        new BuildTests().Sqt_Ms2();

        // BlibSearch writes the target report to whatever path --out= names, and the decoy
        // report to ReplaceExtension(targetReport, "decoy.report"). Match cpp's harness: feed
        // the target name to --out=, and compare the resulting decoy file to the reference.
        var targetReportPath = fixture.OutputFile("search-demo.decoy-target.report");
        var expectedDecoyPath = BlibUtils.ReplaceExtension(targetReportPath, "decoy.report");
        Directory.CreateDirectory(Path.GetDirectoryName(targetReportPath)!);
        if (File.Exists(targetReportPath)) File.Delete(targetReportPath);
        if (File.Exists(expectedDecoyPath)) File.Delete(expectedDecoyPath);

        // cpp ExecuteBlib.cpp:148 — under --unicode, the non-flag non-.blib input gets
        // renamed with a "试验_" prefix and the renamed path is fed to the tool.
        var ms2Path = fixture.InputFile("demo.ms2");
        var unicodeMs2 = Path.Combine(Path.GetDirectoryName(ms2Path)!, "试验_" + Path.GetFileName(ms2Path));
        if (!File.Exists(unicodeMs2)) File.Copy(ms2Path, unicodeMs2);

        var args = new[]
        {
            "--unicode", "--preserve-order", "--decoys-per-target=1",
            unicodeMs2,
            fixture.OutputFile("sqt-ms2.blib"),
            "--out=" + targetReportPath,
        };
        int exitCode = ExecuteBlib.Execute(BlibTool.BlibSearch, args, fixture.OutputDir,
            out string stdout, out string stderr);
        if (exitCode != 0)
        {
            Assert.Fail(
                $"BlibSearch for test 'Search_Decoy' exited {exitCode}.\n" +
                $"stdout:\n{stdout}\n\nstderr:\n{stderr}");
        }

        var details = CompareDetails.FromFile(fixture.ReferenceFile("demo.skip-lines"));
        CompareTextFiles.AssertMatch(expectedDecoyPath, fixture.ReferenceFile("demo.decoy.report"), details);
    }

    /// <summary>Jamfile.jam:442 — <c>search-mzsorted</c>. Depends on <c>sqt-ms2</c>.</summary>
    [TestMethod]
    public void Search_MzSorted()
    {
        var fixture = GoldenFileFixture.Instance;
        if (fixture is null)
        {
            Assert.Inconclusive("BiblioSpec golden-file fixture not found.");
            return;
        }
        new BuildTests().Sqt_Ms2();

        TestRunner.RunBlibTest(
            testName: nameof(Search_MzSorted),
            tool: BlibTool.BlibSearch,
            args: new[]
            {
                "--unicode",
                fixture.InputFile("mzsorted.ms2"),
                fixture.OutputFile("sqt-ms2.blib"),
            },
            outputBlibName: "search-mzsorted.report",
            referenceCheckName: "mzsorted.report",
            skipLinesName: "mzsorted.skip-lines");
    }

    /// <summary>Jamfile.jam:443 — <c>search-binning</c>. Depends on <c>sqt-ms2</c>.</summary>
    [TestMethod]
    public void Search_Binning()
    {
        var fixture = GoldenFileFixture.Instance;
        if (fixture is null)
        {
            Assert.Inconclusive("BiblioSpec golden-file fixture not found.");
            return;
        }
        new BuildTests().Sqt_Ms2();
        TestRunner.RunBlibTest(
            testName: nameof(Search_Binning),
            tool: BlibTool.BlibSearch,
            args: new[]
            {
                "--unicode", "--bin-size=1.1", "--bin-offset=0.2",
                fixture.InputFile("binning.ms2"),
                fixture.OutputFile("sqt-ms2.blib"),
            },
            outputBlibName: "search-binning.report",
            referenceCheckName: "binning.report",
            skipLinesName: "demo.skip-lines");
    }
}
