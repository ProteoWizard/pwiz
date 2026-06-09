namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// Orchestrates a single golden-output test in the cpp <c>Jamfile.jam</c>
/// shape: run one of the four BiblioSpec CLI tools, then compare its output
/// against the checked-in reference using either the SQLite-content compare
/// (for <c>.blib</c> outputs) or the text compare (for everything else).
///
/// <para>Each future <c>[TestMethod]</c> is a one-liner that delegates here so
/// the test bodies stay short and uniform.</para>
/// </summary>
public static class TestRunner
{
    /// <summary>
    /// Run one golden-output test.
    /// </summary>
    /// <param name="testName">Logical name, used for the temp output subdir.</param>
    /// <param name="tool">Which BiblioSpec CLI tool to invoke.</param>
    /// <param name="args">Tool argv (excluding the executable itself, excluding
    /// the <c>--out=&lt;path&gt;</c> argument — that's added automatically using
    /// <paramref name="outputBlibName"/>). Input file references should already
    /// be absolute paths — typically built via
    /// <see cref="GoldenFileFixture.InputFile(string)"/>.</param>
    /// <param name="outputBlibName">Output file name (relative to the fixture's
    /// scratch output dir). Drives both the <c>--out=</c> argument and the
    /// comparison input. Extension chooses comparator: <c>.blib</c> uses
    /// <see cref="CompareLibraryContents"/>; anything else uses
    /// <see cref="CompareTextFiles"/>.</param>
    /// <param name="referenceCheckName">Reference filename (relative to the
    /// fixture's reference dir). Typically <c>&lt;testName&gt;.check</c> for
    /// .blib comparisons, or <c>&lt;testName&gt;.report</c> / <c>.lms2</c> for
    /// text comparisons.</param>
    /// <param name="skipLinesName">Optional skip-lines filename (relative to the
    /// fixture's reference dir). Pass null for strict comparison.</param>
    /// <summary>
    /// Cpp-Jamfile-style one-liner for a build/filter/toms2 test row. Mirrors:
    /// <code>
    /// blib-test-build &lt;name&gt; : &lt;args&gt; : &lt;output-name&gt; : &lt;reference-name&gt; : &lt;inputs&gt; ;
    /// </code>
    /// Resolves <paramref name="inputFilenames"/> against the cpp <c>tests/inputs/</c>
    /// directory (via <see cref="GoldenFileFixture"/>) so test methods just name the file.
    /// </summary>
    public static void RunBlibTest(
        string testName,
        BlibTool tool,
        string[] args,
        string[] inputFilenames,
        string outputBlibName,
        string referenceCheckName,
        string? skipLinesName = null)
    {
        ArgumentNullException.ThrowIfNull(inputFilenames);
        var fixture = GoldenFileFixture.Instance;
        if (fixture is null)
        {
            Assert.Inconclusive("BiblioSpec golden-file fixture not found.");
            return;
        }
        var resolvedArgs = new List<string>(args);
        foreach (var name in inputFilenames)
            resolvedArgs.Add(fixture.InputFile(name));
        RunBlibTest(testName, tool, resolvedArgs.ToArray(),
            outputBlibName, referenceCheckName, skipLinesName);
    }

    public static void RunBlibTest(
        string testName,
        BlibTool tool,
        string[] args,
        string outputBlibName,
        string referenceCheckName,
        string? skipLinesName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputBlibName);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceCheckName);

        var fixture = GoldenFileFixture.Instance;
        if (fixture is null)
        {
            Assert.Inconclusive(
                "BiblioSpec golden-file fixture not found. " +
                "Expected a sibling pwiz/ checkout exposing " +
                "pwiz_tools/BiblioSpec/tests/{inputs,reference}/.");
            return;
        }

        string outputPath = fixture.OutputFile(outputBlibName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        // Clean up any leftover from a previous run so we never compare stale output.
        if (File.Exists(outputPath))
            File.Delete(outputPath);

        // The cpp harness assembles the command line with --out=<path> appended;
        // do the same so individual tests don't have to.
        var fullArgs = new List<string>(args) { "--out=" + outputPath };

        int exitCode = ExecuteBlib.Execute(tool, fullArgs.ToArray(), fixture.OutputDir,
            out string stdout, out string stderr);
        if (exitCode != 0)
        {
            Assert.Fail(
                $"{tool} for test '{testName}' exited {exitCode}.\n" +
                $"stdout:\n{stdout}\n\nstderr:\n{stderr}");
        }

        string referencePath = fixture.ReferenceFile(referenceCheckName);
        var details = CompareDetails.FromFile(
            skipLinesName is null ? null : fixture.ReferenceFile(skipLinesName));

        if (outputPath.EndsWith(".blib", StringComparison.OrdinalIgnoreCase))
            CompareLibraryContents.AssertMatch(outputPath, referencePath, details);
        else
            CompareTextFiles.AssertMatch(outputPath, referencePath, details);
    }
}
