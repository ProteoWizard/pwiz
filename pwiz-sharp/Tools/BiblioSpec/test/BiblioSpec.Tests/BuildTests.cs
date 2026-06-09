namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// Vertical-slice port of cpp <c>pwiz_tools/BiblioSpec/tests/Jamfile.jam</c>
/// <c>blib-test-build</c> rows. Each method is one Jamfile row; the rest will be
/// transcribed once the slice surfaces and fixes the integration issues.
/// </summary>
/// <remarks>
/// Cpp Jamfile shape (line 110-120 of Jamfile.jam):
/// <code>
/// blib-test-build &lt;name&gt; : &lt;args&gt; : output/&lt;output&gt; : &lt;reference&gt;.check : inputs/&lt;input&gt; ;
/// </code>
/// → <c>BlibBuild &lt;args&gt; --out=&lt;output_path&gt; &lt;input_path&gt;</c>, then
/// <c>CompareLibraryContents &lt;output&gt; reference/&lt;reference&gt;.check</c>.
/// </remarks>
[TestClass]
public class BuildTests
{
    /// <summary>
    /// cpp Jamfile.jam:209 — SSL with extra non-required columns.
    /// <c>blib-test-build ssl-ex : -o : output/ssl-ex.blib : ssl-ex.check : extra-cols.ssl ;</c>
    /// (The Jamfile.jam:208 `ssl` test uses `--unicode` which triggers cpp-test-harness-side
    /// file renaming to Chinese-character paths + a copy of demo.ms2 to demo-copy.cms2; that
    /// pre-run side effect isn't replicated by our C# harness yet, so the basic SSL coverage
    /// uses ssl-ex instead — same SslReader code path, no harness side effect.)
    /// </summary>
    [TestMethod]
    public void Ssl_ExtraColumns()
    {
        TestRunner.RunBlibTest(
            testName: nameof(Ssl_ExtraColumns),
            tool: BlibTool.BlibBuild,
            args: new[] { "-o" },
            inputFilenames: new[] { "extra-cols.ssl" },
            outputBlibName: "ssl-ex.blib",
            referenceCheckName: "ssl-ex.check");
    }

    /// <summary>
    /// cpp Jamfile.jam:211 — SSL with retention-time column.
    /// </summary>
    [TestMethod]
    public void Ssl_WithRetentionTime()
    {
        TestRunner.RunBlibTest(
            testName: nameof(Ssl_WithRetentionTime),
            tool: BlibTool.BlibBuild,
            args: new[] { "-o" },
            inputFilenames: new[] { "ssl-with-rt.ssl" },
            outputBlibName: "ssl-rt.blib",
            referenceCheckName: "ssl-rt.check");
    }

    /// <summary>
    /// cpp Jamfile.jam:199 — basic Sequest SQT input.
    /// <c>blib-test-build sqt-ms2 : -o : output/sqt-ms2.blib : sqt-ms2.check : demo.sqt ;</c>
    /// </summary>
    [TestMethod]
    public void Sqt_DemoMs2()
    {
        TestRunner.RunBlibTest(
            testName: nameof(Sqt_DemoMs2),
            tool: BlibTool.BlibBuild,
            args: new[] { "-o" },
            inputFilenames: new[] { "demo.sqt" },
            outputBlibName: "sqt-ms2.blib",
            referenceCheckName: "sqt-ms2.check");
    }

    /// <summary>
    /// cpp Jamfile.jam:236 — PeptideProphet pep.xml with CAexample.mzXML for peak lookup.
    /// </summary>
    [TestMethod]
    public void PepXml_PeptideProphet()
    {
        TestRunner.RunBlibTest(
            testName: nameof(PepXml_PeptideProphet),
            tool: BlibTool.BlibBuild,
            args: new[] { "-o" },
            inputFilenames: new[] { "CAexample.pep.xml" },
            outputBlibName: "pep-proph.blib",
            referenceCheckName: "pep-proph.check");
    }
}
