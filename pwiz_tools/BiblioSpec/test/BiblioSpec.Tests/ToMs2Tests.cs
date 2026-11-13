namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// Port of the cpp Jamfile.jam <c>blib-test-to-ms2</c> rows (Jamfile.jam:435).
/// Converts a <c>.blib</c> to a text <c>.lms2</c>, then text-compares against the
/// reference. Depends on a previous <see cref="BuildTests"/> run.
/// </summary>
[TestClass]
public class ToMs2Tests
{
    /// <summary>Jamfile.jam:435 — <c>lms2</c>. Depends on <c>sqt-ms2</c>.</summary>
    [TestMethod]
    public void Lms2()
    {
        new BuildTests().Sqt_Ms2();

        TestRunner.RunBlibTest(
            testName: nameof(Lms2),
            tool: BlibTool.BlibToMs2,
            args: Array.Empty<string>(),
            inputFilenames: new[] { "sqt-ms2.blib" },
            outputBlibName: "demo.lms2",
            referenceCheckName: "demo.lms2",
            skipLinesName: "lms2-skip-lines",
            inputsFromOutputDir: true);
    }
}
