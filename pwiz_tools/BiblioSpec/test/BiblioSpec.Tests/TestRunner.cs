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
    /// Pass <paramref name="inputsFromOutputDir"/>=<c>true</c> when the inputs are .blibs
    /// produced by an earlier build test (filter, search, merge, to-ms2 cases).
    /// </summary>
    public static void RunBlibTest(
        string testName,
        BlibTool tool,
        string[] args,
        string[] inputFilenames,
        string outputBlibName,
        string referenceCheckName,
        string? skipLinesName = null,
        bool inputsFromOutputDir = false)
    {
        ArgumentNullException.ThrowIfNull(inputFilenames);
        var fixture = GoldenFileFixture.Instance;
        if (fixture is null)
        {
            Assert.Inconclusive("BiblioSpec golden-file fixture not found.");
            return;
        }
        // cpp parity: ExecuteBlib.cpp:145 — when --unicode is in args, the cpp harness copies
        // each non-flag, non-.blib, non-.check input to a sibling renamed with a "试验_" prefix,
        // and feeds the renamed path to the tool. The .check goldens encode the renamed path so
        // we must mirror this side effect to compare.
        bool unicodeTest = Array.IndexOf(args, "--unicode") >= 0;

        // cpp parity: ExecuteBlib.cpp:164 sorts .blib args alphabetically before invoking the
        // tool. The Merge test relies on this — its inputs are passed (sqt-cms2, sqt-ms2,
        // pep-proph) but cpp processes them as (pep-proph, sqt-cms2, sqt-ms2), which controls
        // the order spectra get RefSpectraIDs in the merged library.
        var orderedInputs = new List<string>();
        foreach (var name in inputFilenames)
        {
            string resolved = inputsFromOutputDir
                ? fixture.OutputFile(name)
                : fixture.InputFile(name);

            if (unicodeTest && !resolved.EndsWith(".blib", StringComparison.OrdinalIgnoreCase))
            {
                var dir = Path.GetDirectoryName(resolved)!;
                var renamed = Path.Combine(dir, "试验_" + Path.GetFileName(resolved));
                if (!File.Exists(renamed))
                    File.Copy(resolved, renamed);
                resolved = renamed;
            }
            orderedInputs.Add(resolved);
        }
        // Only sort when ALL inputs are .blib (the cpp sort is on the "libNames" bucket,
        // which only collects .blib paths). For non-.blib inputs (.sqt, .pep.xml, .mzid, etc.)
        // there is at most one and ordering doesn't matter.
        if (orderedInputs.Count > 1 && orderedInputs.TrueForAll(p => p.EndsWith(".blib", StringComparison.OrdinalIgnoreCase)))
            orderedInputs.Sort(StringComparer.Ordinal);

        var resolvedArgs = new List<string>(args);
        resolvedArgs.AddRange(orderedInputs);
        RunBlibTest(testName, tool, resolvedArgs.ToArray(),
            outputBlibName, referenceCheckName, skipLinesName);
    }

    /// <summary>
    /// Negative-test variant: run the tool with <c>-e &lt;expected&gt;</c> and assert it exits 0
    /// (which means the tool emitted the expected error string). No <c>.check</c> comparison
    /// is done. The cpp Jamfile's negative tests don't have a meaningful reference file because
    /// no output is produced when the parser bails on bad input.
    /// </summary>
    public static void RunNegativeBlibTest(
        string testName,
        BlibTool tool,
        string[] args,
        string[] inputFilenames,
        string outputBlibName)
    {
        ArgumentNullException.ThrowIfNull(inputFilenames);
        var fixture = GoldenFileFixture.Instance;
        if (fixture is null)
        {
            Assert.Inconclusive("BiblioSpec golden-file fixture not found.");
            return;
        }
        var fullArgs = new List<string>(args);
        foreach (var name in inputFilenames)
            fullArgs.Add(fixture.InputFile(name));

        string outputPath = fixture.OutputFile(outputBlibName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        if (File.Exists(outputPath))
            File.Delete(outputPath);
        fullArgs.Add("--out=" + outputPath);

        int exitCode = ExecuteBlib.Execute(tool, fullArgs.ToArray(), fixture.OutputDir,
            out string stdout, out string stderr);

        // The tool's `-e` capture (handled by CliPreproc + each Program.cs) maps a matched
        // expected-error to exit 0 and an unmatched expectation to exit 1. So a passing
        // negative test is exactly exit 0.
        if (exitCode != 0)
        {
            Assert.Fail(
                $"{tool} for negative test '{testName}' exited {exitCode} — expected 0 "
                + "(tool's -e capture should have caught the expected error).\n"
                + $"stdout:\n{stdout}\n\nstderr:\n{stderr}");
        }
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

        // Negative tests (those that passed `-e <expected-error>` to the tool) exit 0 when
        // the expected error was matched — but they DON'T produce an output file, so a .check
        // comparison would always fail. cpp's Jamfile handles this by giving the reference an
        // empty file; we detect the pattern (no output produced) and skip the comparison.
        if (!File.Exists(outputPath))
            return;

        string referencePath = fixture.ReferenceFile(referenceCheckName);
        var details = CompareDetails.FromFile(
            skipLinesName is null ? null : fixture.ReferenceFile(skipLinesName));

        if (outputPath.EndsWith(".blib", StringComparison.OrdinalIgnoreCase))
            CompareLibraryContents.AssertMatch(outputPath, referencePath, details);
        else
            CompareTextFiles.AssertMatch(outputPath, referencePath, details);
    }
}
