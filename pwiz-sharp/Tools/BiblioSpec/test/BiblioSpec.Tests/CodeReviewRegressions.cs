using System.Data.SQLite;
using System.Diagnostics;

namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// Regression tests for bugs found by the Phase 4 code review. Each pins down a specific
/// failure mode and proves the fix locks the right behavior in.
/// </summary>
[TestClass]
public class CodeReviewRegressions
{
    /// <summary>
    /// <c>BlibFilter foo.blib foo.blib</c> must refuse to run, not silently delete the input
    /// library. The cpp parent doesn't guard this; Init() honoring Overwrite=true would
    /// delete the output before opening the input for reading.
    /// </summary>
    [TestMethod]
    public void BlibFilter_RefusesIdenticalInputAndOutputPaths()
    {
        var fixture = GoldenFileFixture.Instance;
        if (fixture is null) { Assert.Inconclusive("Golden fixture not found."); return; }

        // Use any small .blib as a stand-in; we don't actually expect filtering to start.
        // A bare path is enough — the guard triggers before file existence is checked.
        string sharedPath = Path.Combine(fixture.OutputDir, "shared-for-inplace.blib");
        // Touch it so a real file exists (the cpp behavior was to delete it before checking
        // it could be read; we want to prove the file SURVIVES the call).
        File.WriteAllText(sharedPath, "stand-in");
        long sizeBefore = new FileInfo(sharedPath).Length;

        string? exePath = ExecuteBlib.TryResolveToolPath(BlibTool.BlibFilter);
        if (exePath is null) { Assert.Inconclusive("BlibFilter.exe not built."); return; }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = fixture.OutputDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(sharedPath);
        psi.ArgumentList.Add(sharedPath);

        using var proc = Process.Start(psi)!;
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        Assert.AreNotEqual(0, proc.ExitCode,
            "BlibFilter should refuse identical input/output paths.");
        Assert.IsTrue(stderr.Contains("must be different", StringComparison.OrdinalIgnoreCase),
            $"Expected stderr to explain the rejection. Got: {stderr}");
        Assert.IsTrue(File.Exists(sharedPath),
            "Source library was deleted despite the guard.");
        Assert.AreEqual(sizeBefore, new FileInfo(sharedPath).Length,
            "Source library was overwritten despite the guard.");

        File.Delete(sharedPath);
    }

    /// <summary>
    /// <c>-e &lt;expected&gt;</c> capture must respect cpp's <c>argfind &lt; argc - 2</c>
    /// bound: a trailing positional must remain AFTER -e's value. The Stage A port of
    /// BlibSearch/BlibToMs2 used <c>list.Count - 1</c>, mis-consuming the trailing arg
    /// when -e appeared second-to-last. Fix consolidated into <see cref="CliPreproc"/>.
    /// </summary>
    [TestMethod]
    public void CliPreproc_DashEStripRequiresTrailingPositional()
    {
        // -e at i=0 with 2 trailing positionals (typical: <input> <library>) → captured
        var (argv1, expected1) = CliPreproc.Strip(
            new[] { "-e", "the-error", "input.ssl", "out.blib" });
        Assert.AreEqual("the-error", expected1);
        CollectionAssert.AreEqual(new[] { "input.ssl", "out.blib" }, argv1);

        // -e at i=0 with NO trailing positional after its value → NOT captured.
        // cpp BlibBuild.cpp:108 loop bound `argfind < argc - 2` makes this an empty loop;
        // the buggy Stage A copy in BlibSearch / BlibToMs2 (`list.Count - 1`) would capture.
        var (argv2, expected2) = CliPreproc.Strip(new[] { "-e", "the-error" });
        Assert.AreEqual(string.Empty, expected2,
            "-e with no trailing positional after its value should NOT be captured "
            + "(cpp requires the loop to have a future positional to consume).");
        CollectionAssert.AreEqual(new[] { "-e", "the-error" }, argv2);
    }

    /// <summary>
    /// <c>--out=PATH</c> rewrite + <c>--unicode</c> strip must apply uniformly across the
    /// 4 tools. Verifies the rewrite is positional-correct.
    /// </summary>
    [TestMethod]
    public void CliPreproc_RewritesOutPathAndStripsUnicode()
    {
        var (argv, _) = CliPreproc.Strip(
            new[] { "--unicode", "-o", "--out=C:\\path\\to\\out.blib", "input.ssl" });
        // --unicode stripped, --out=PATH rewritten so PATH lands at the END as the lib name.
        CollectionAssert.AreEqual(
            new[] { "-o", "input.ssl", "C:\\path\\to\\out.blib" }, argv);
    }

    /// <summary>
    /// SSL files with quoted header tokens (e.g. <c>"file"\t"scan"\t...</c>) must populate
    /// the column index correctly. The pre-fix code stored literal keys including the quotes;
    /// the column-setter lookup then failed and required-column validation threw a misleading
    /// "missing required column" error on a valid SSL.
    /// </summary>
    [TestMethod]
    public void SslReader_AcceptsQuotedHeaderTokens()
    {
        // We build a tiny SSL with quoted headers and run BlibBuild against it. The data row
        // uses unquoted values pointing at demo.ms2 (not in MsData; we expect the spectrum
        // lookup to warn, but the COLUMN PARSING is what we're verifying — the run gets past
        // the header-validation step.
        var fixture = GoldenFileFixture.Instance;
        if (fixture is null) { Assert.Inconclusive("Golden fixture not found."); return; }

        string sslPath = Path.Combine(fixture.OutputDir, "quoted-header.ssl");
        File.WriteAllText(sslPath,
            "\"file\"\t\"scan\"\t\"charge\"\t\"sequence\"\n"
            + "demo.ms2\t137\t3\tNFLETVELQVGLK\n");

        string outPath = Path.Combine(fixture.OutputDir, "quoted-header.blib");
        if (File.Exists(outPath)) File.Delete(outPath);

        string? exePath = ExecuteBlib.TryResolveToolPath(BlibTool.BlibBuild);
        if (exePath is null) { Assert.Inconclusive("BlibBuild.exe not built."); return; }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = fixture.OutputDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(sslPath);
        psi.ArgumentList.Add(outPath);

        using var proc = Process.Start(psi)!;
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        // We don't require ExitCode=0 (demo.ms2 isn't bundled; the spec-reader path warns).
        // But the column-validation step must succeed — stderr should NOT mention a missing
        // required column.
        Assert.IsFalse(stderr.Contains("missing required column", StringComparison.OrdinalIgnoreCase),
            $"Quoted SSL header was rejected as if a required column were missing. stderr: {stderr}");
        Assert.IsFalse(stderr.Contains("\"file\"", StringComparison.OrdinalIgnoreCase),
            $"Quoted column name leaked into an error message — header was not unquoted. stderr: {stderr}");

        File.Delete(sslPath);
        if (File.Exists(outPath)) File.Delete(outPath);
    }

    /// <summary>
    /// Assigning a new <see cref="ISpecFileReader"/> via the <see cref="BuildParser.SpecReader"/>
    /// property must dispose the previously-held reader. Pre-fix the property was an auto-prop
    /// and the BuildParser ctor unconditionally allocated a default <see cref="PwizSharpSpecFileReader"/>,
    /// so any test or subclass that replaced it leaked the original's native handles.
    /// </summary>
    [TestMethod]
    public void BuildParser_SpecReaderSetterDisposesPreviousValue()
    {
        // We use SslReader (the minimal BuildParser subclass) bound to an empty BlibBuilder.
        // SslReader's ctor doesn't touch SpecReader — perfect for exercising the lazy/property.
        string sslPath = Path.Combine(Path.GetTempPath(),
            $"pwiz-sharp-bibliospec-disposetest-{Guid.NewGuid():N}.ssl");
        string libPath = Path.Combine(Path.GetTempPath(),
            $"pwiz-sharp-bibliospec-disposetest-{Guid.NewGuid():N}.blib");
        File.WriteAllText(sslPath, "file\tscan\tcharge\tsequence\ndemo.ms2\t137\t3\tNFLETVELQVGLK\n");
        try
        {
            using var builder = new BlibBuilder();
            builder.Overwrite = true;
            builder.SetLibName(libPath);
            builder.Init();
            using var reader = new SslReader(builder, sslPath, parentProgress: null);

            var first = new DisposeTrackingSpecReader();
            var second = new DisposeTrackingSpecReader();

            reader.SpecReader = first;
            Assert.IsFalse(first.IsDisposed, "First reader disposed too early.");

            reader.SpecReader = second;
            Assert.IsTrue(first.IsDisposed, "Setter must dispose the previously-held reader.");
            Assert.IsFalse(second.IsDisposed, "New reader should NOT be disposed by assignment.");

            // Assigning the same instance is a no-op.
            reader.SpecReader = second;
            Assert.IsFalse(second.IsDisposed, "Self-assignment must not dispose.");
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try { File.Delete(sslPath); } catch (IOException) { }
            try { if (File.Exists(libPath)) File.Delete(libPath); } catch (IOException) { }
        }
    }

    private sealed class DisposeTrackingSpecReader : SpecFileReaderBase
    {
        public bool IsDisposed { get; private set; }
        public override SpecIdType IdType { set { /* no-op */ } }
        public override void OpenFile(string fileName, bool mzSort = false) { /* no-op */ }
        public override bool GetSpectrum(int identifier, SpecData returnData, SpecIdType findBy, bool getPeaks = true) => false;
        public override bool GetSpectrum(string identifier, SpecData returnData, bool getPeaks = true) => false;
        public override bool GetNextSpectrum(SpecData returnData, bool getPeaks = true) => false;
        protected override void Dispose(bool disposing) { if (disposing) IsDisposed = true; base.Dispose(disposing); }
    }

    /// <summary>
    /// <see cref="Reportfile.WriteMatches"/> used to dereference <c>match.ExpSpec!</c> /
    /// <c>match.RefSpec!</c> with null-forgiving operators, so a programmer-error Match with
    /// either spectrum unset would throw <c>NullReferenceException</c> mid-write — leaving
    /// a partial .report and no actionable error. The fix replaces the bangs with an explicit
    /// guard that throws <see cref="BlibException"/> naming WHICH spectrum is missing.
    /// </summary>
    [TestMethod]
    public void Reportfile_WriteMatchesThrowsBlibExceptionOnMissingSpectrum()
    {
        string reportPath = Path.Combine(Path.GetTempPath(),
            $"pwiz-sharp-bibliospec-reporttest-{Guid.NewGuid():N}.report");
        try
        {
            using var report = new Reportfile(topMatches: 3, optionsString: "test");
            report.Open(reportPath);

            // A Match with no ExpSpec or RefSpec assigned — the bug we're guarding against.
            var bad = new Match { Rank = 1 };
            var ex = Assert.ThrowsException<BlibException>(
                () => report.WriteMatches(new[] { bad }));
            StringAssert.Contains(ex.Message, "rank 1",
                "Error should identify which match failed.");
            StringAssert.Contains(ex.Message, "query spectrum",
                "First-null check should name the query spectrum specifically.");
        }
        finally
        {
            try { if (File.Exists(reportPath)) File.Delete(reportPath); } catch (IOException) { }
        }
    }

    /// <summary>
    /// CompareLibraryContents's REAL → "0.0" formatting quirk (cpp parity with
    /// sqlite3_column_text) was gated on <c>raw is double</c>, missing <c>float</c> and
    /// <c>decimal</c>. A vendor-extension schema with a FLOAT column would silently drop
    /// the trailing ".0" for integral values and produce false test mismatches.
    /// </summary>
    [TestMethod]
    public void CompareLibraryContents_FormatsAllRealTypesWithTrailingZero()
    {
        // We build a tiny ephemeral .blib with a FLOAT column and confirm Compare's helper
        // produces "0.0" not "0" — exercising the widened switch.
        string path = Path.Combine(Path.GetTempPath(),
            $"pwiz-sharp-bibliospec-{Guid.NewGuid():N}.blib");
        try
        {
            using (var conn = SqliteRoutine.Open(path, readOnly: false))
            using (var cmd = conn.CreateCommand())
            {
                // FLOAT column affinity — SQLite still stores as REAL, but column type
                // declaration "FLOAT" can cause System.Data.SQLite to return float, not
                // double, depending on type-mapping settings.
                cmd.CommandText = @"
CREATE TABLE LibInfo (libLSID TEXT, numSpecs INTEGER, majorVersion INTEGER, minorVersion INTEGER);
INSERT INTO LibInfo VALUES ('test', 0, 1, 11);
CREATE TABLE Modifications (id INTEGER PRIMARY KEY, RefSpectraID INTEGER, position INTEGER, mass FLOAT);
INSERT INTO Modifications VALUES (1, 1, 5, 0);
INSERT INTO Modifications VALUES (2, 1, 7, 15.99);";
                cmd.ExecuteNonQuery();
            }

            // Dump and confirm the FLOAT 0 renders as "0.0" not "0".
            var lines = CompareLibraryContents.ExtractLines(path);
            // Find the Modifications dump row for mass=0 — should contain a trailing "0.0" field
            var zeroMassRow = lines.FirstOrDefault(l => l.StartsWith("1\t1\t5\t", StringComparison.Ordinal));
            Assert.IsNotNull(zeroMassRow, $"Modifications dump did not contain expected row. Got:\n{string.Join("\n", lines)}");
            Assert.IsTrue(zeroMassRow!.EndsWith("\t0.0", StringComparison.Ordinal),
                $"FLOAT 0 should format as '0.0' (cpp sqlite3_column_text parity). Got: '{zeroMassRow}'");
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
        }
    }
}
