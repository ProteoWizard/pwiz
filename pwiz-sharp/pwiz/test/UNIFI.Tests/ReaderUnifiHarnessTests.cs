using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Diff;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Diff;
using Pwiz.Data.MsData.Encoding;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Readers;
using System.Text.RegularExpressions;

namespace Pwiz.Vendor.UNIFI.Tests;

/// <summary>
/// Walks <c>pwiz/data/vendor_readers/UNIFI/Reader_UNIFI_Test.data/urls.txt</c> and exercises
/// <see cref="Reader_UNIFI"/> against each active URL. Always validates Identify routing;
/// when <c>UNIFI_PASSWORD</c> / <c>WC_PASSWORD</c> env vars are present, splices them into
/// the URL (with the demo-account hardcoded usernames "msconvert" / "skyline" — same scheme
/// pwiz_tools/Skyline/TestUtil/{Unifi,WatersConnect}TestUtil.cs uses) and runs the live read
/// pipeline against the sibling reference mzML.
/// </summary>
/// <remarks>
/// <para>This is the only Identify validator for the UNIFI reader — the standalone test class
/// was dropped as redundant. Without credentials, the harness still does the routing check
/// on every fixture URL; with credentials it adds the read + diff cycle.</para>
/// <para><b>Reference layering.</b> The harness consults two locations and prefers the first:
/// <list type="number">
///   <item><c>pwiz-sharp/pwiz/test/UNIFI.Tests/Reference/&lt;run-id&gt;.mzML</c> — pwiz-sharp-only
///   override that the generate mode writes to. Edits here only re-trigger the pwiz-sharp
///   TC config, not every cpp vendor build.</item>
///   <item><c>pwiz/data/vendor_readers/UNIFI/Reader_UNIFI_Test.data/&lt;run-id&gt;.mzML</c> —
///   the canonical cpp test fixtures. These stay untouched until the pwiz-sharp output is
///   ready to land in the cpp tree as one coordinated commit.</item>
/// </list>
/// Eventually the override directory should empty out as we ratchet pwiz-sharp's output back
/// to bit-identical with cpp's reference.</para>
/// <para>To regenerate references: flip the local <c>IsRecordMode</c> bool inside the test
/// method, run, then revert before committing. Mirrors cpp <c>VendorReaderTestHarness</c>'s
/// <c>generateMzML</c> mode. Two safety nets prevent a "set true → forgot to revert" commit:
/// (1) the test asserts <c>IsRecordMode</c> is false at the end, so anyone who reruns the
/// harness will catch it; (2) <c>Util.Tests.CodeInspectionTests</c> greps the whole pwiz-sharp
/// tree at every CI run (rule <c>IsRecordMode_NeverCommittedTrue</c>), so even unrelated
/// commits get gated. Generated files always land in the pwiz-sharp override directory; cpp
/// test data is never written from here.</para>
/// </remarks>
[TestClass]
public class ReaderUnifiHarnessTests
{
    [TestMethod]
    public void Reader_UNIFI_HarnessAgainstReferenceUrls()
    {
        // Flip to true locally to (re)write the override-reference mzML files; flip back to
        // false before committing. Two layers catch a forgotten revert:
        //   1. the Assert.IsFalse(IsRecordMode, ...) at the end of this test — fires as soon
        //      as anyone reruns the harness, including the dev who flipped it.
        //   2. Util.Tests/CodeInspectionTests scans every .cs in the tree on every test run
        //      (via the IsRecordMode_NeverCommittedTrue rule) — catches it even if this
        //      specific test isn't part of the run.
        bool IsRecordMode = false;

        string? root = FindCppTestDataRoot();
        if (root is null)
        {
            // Sibling pwiz checkout missing — surface as a hard failure rather than a skip
            // so CI on a properly-checked-out tree never silently bypasses the harness.
            Assert.Fail("UNIFI test-data root not found (expected sibling pwiz checkout).");
            return;
        }
        string overrideRoot = FindOverrideReferenceRoot()
            ?? throw new InvalidOperationException(
                "pwiz-sharp UNIFI override-reference root not found "
                + "(expected pwiz-sharp/pwiz/test/UNIFI.Tests/Reference under the source tree).");

        string urlsFile = Path.Combine(root, "urls.txt");
        Assert.IsTrue(File.Exists(urlsFile), $"urls.txt missing under {root}");

        var urls = File.ReadAllLines(urlsFile)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToList();
        Assert.IsTrue(urls.Count > 0, $"no active URLs found in {urlsFile}");

        var reader = new Reader_UNIFI();
        bool haveUnifiCreds = HasEnv("UNIFI_PASSWORD");
        bool haveWcCreds = HasEnv("WC_PASSWORD");
        bool anyCreds = haveUnifiCreds || haveWcCreds;

        var failures = new List<string>();
        var generated = new List<string>();

        foreach (var url in urls)
        {
            // --- always-on: Identify routing ---
            var cvid = reader.Identify(url, head: null);
            if (cvid != CVID.MS_Waters_raw_format)
            {
                failures.Add($"Identify({url}): expected MS_Waters_raw_format, got {cvid}");
                continue;
            }
            bool isUnifi = Reader_UNIFI.IsUnifiUrl(url);
            bool isWc = Reader_UNIFI.IsWatersConnectUrl(url);
            if (isUnifi == isWc)
            {
                failures.Add($"URL family ambiguous: {url} (isUnifi={isUnifi}, isWatersConnect={isWc})");
                continue;
            }

            // --- live read: only when the matching credentials are present ---
            bool credsForThisUrl = isWc ? haveWcCreds : haveUnifiCreds;
            if (!credsForThisUrl)
                continue;

            string credUrl = SpliceCredentials(url, isWc);
            string? failure = IsRecordMode
                ? TryLiveReadAndGenerate(reader, credUrl, url, overrideRoot, generated)
                : TryLiveReadAndDiff(reader, credUrl, url, overrideRoot, root);
            if (failure is not null) failures.Add(failure);
        }

        if (IsRecordMode)
            Console.WriteLine($"[UNIFI harness] recorded {generated.Count} reference mzML(s):\n  "
                + string.Join("\n  ", generated));
        else if (!anyCreds)
            Console.WriteLine(
                $"[UNIFI harness] {urls.Count} URLs identify-validated; live read skipped "
                + "(set UNIFI_PASSWORD or WC_PASSWORD to enable; demo usernames hardcoded as "
                + "msconvert / skyline).");

        // Per-test safety net for IsRecordMode. Fires BEFORE the failures-Assert.Fail so a
        // forgotten "set true → forgot to revert" surfaces with this message even if the
        // recording itself failed (server flake, etc.). Util.Tests/IsRecordModeInspectionTests
        // adds a tree-wide grep as a second layer, so even a dev who skipped this specific
        // test in their local run still gets caught at CI time.
        Assert.IsFalse(IsRecordMode,
            "IsRecordMode is set true — flip it back to false before committing. The harness "
            + "should diff against references in CI, not record them. (When you set this true, "
            + "the per-URL recordings ran first; the test still fails as a reminder.)");

        if (failures.Count > 0)
            Assert.Fail($"{failures.Count} harness failures:\n" + string.Join('\n', failures));
    }

    /// <summary>Splices the demo-account credentials into a URL. Mirrors the URL the cpp
    /// test harness builds when running with <c>UNIFI_PASSWORD</c> / <c>WC_PASSWORD</c> set.
    /// Only used by tests — the reader/client itself never reads env vars.</summary>
    private static string SpliceCredentials(string url, bool isWatersConnect)
    {
        string user = isWatersConnect ? "skyline" : "msconvert";
        string passVar = isWatersConnect ? "WC_PASSWORD" : "UNIFI_PASSWORD";
        string pass = Environment.GetEnvironmentVariable(passVar) ?? string.Empty;
        string escapedPass = Uri.EscapeDataString(pass);
        return Regex.Replace(url, "^https://", $"https://{user}:{escapedPass}@");
    }

    private static string? TryLiveReadAndDiff(Reader_UNIFI reader, string credUrl, string displayUrl,
        string overrideRoot, string cppRoot)
    {
        try
        {
            using var msd = ReadAndSlice(reader, credUrl);

            string referenceFilename = SafeFilename(msd.Run.Id) + ".mzML";
            // Override-first: check pwiz-sharp's local reference dir before the cpp tree.
            // This lets us pin a pwiz-sharp-specific reference for fixtures whose output has
            // diverged from the cpp baseline without rewriting cpp test data.
            string overridePath = Path.Combine(overrideRoot, referenceFilename);
            string cppPath = Path.Combine(cppRoot, referenceFilename);
            string referencePath = File.Exists(overridePath) ? overridePath
                : File.Exists(cppPath) ? cppPath
                : string.Empty;
            if (string.IsNullOrEmpty(referencePath))
                return $"{displayUrl}: reference not found (looked at {overridePath} and {cppPath})";

            // MzmlReader.Read(string) parses the string as mzML CONTENT, not a path. Open
            // the file as a stream so the XmlReader sees the document body.
            using var refStream = File.OpenRead(referencePath);
            using var reference = new MzmlReader().Read(refStream);

            // Diff config: tolerant of metadata that drifts independently of the data
            // (source-file SHA-1 + start-timestamp), matching the conventions other vendor
            // harnesses use.
            var diffConfig = new DiffConfig
            {
                IgnoreSourceFileChecksum = true,
                IgnoreStartTimeStamp = true,
                // pwiz_Reader_UNIFI's software version is the pwiz build that wrote the
                // reference, not what we're running. cpp reference mzMLs have several
                // different vintages; ignore the version dimension so the diff focuses on
                // data structure.
                IgnoreVersions = true,
            };
            string diff = MSDataDiff.Describe(reference, msd, diffConfig);
            if (!string.IsNullOrEmpty(diff))
                return $"{displayUrl}: msdiff differences:\n{Truncate(diff, 4000)}";
            return null;
        }
        catch (Exception e)
        {
            return $"{displayUrl}: live read threw {DescribeException(e)}";
        }
    }

    private static string? TryLiveReadAndGenerate(Reader_UNIFI reader, string credUrl, string displayUrl,
        string overrideRoot, List<string> generated)
    {
        try
        {
            using var msd = ReadAndSlice(reader, credUrl);
            string referenceFilename = SafeFilename(msd.Run.Id) + ".mzML";
            // Always write to the pwiz-sharp override directory — never touch the cpp tree.
            // The override layer is only consumed by pwiz-sharp tests, so updates here don't
            // re-trigger the full TC vendor matrix.
            Directory.CreateDirectory(overrideRoot);
            string referencePath = Path.Combine(overrideRoot, referenceFilename);
            // Match cpp's default mzML output: 64-bit doubles, zlib compression, indexed.
            // The harness reads back via MzmlReader so any encoding pwiz-sharp can read is
            // fine, but matching cpp's defaults keeps a future cpp diff comparable.
            var encoderConfig = new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits64,
                Compression = BinaryCompression.Zlib,
            };
            using var output = File.Create(referencePath);
            new MzmlWriter(encoderConfig) { Indexed = true }.Write(msd, output);
            generated.Add(referencePath);
            return null;
        }
        catch (Exception e)
        {
            return $"{displayUrl}: live read+generate threw {DescribeException(e)}";
        }
    }

    private static MSData ReadAndSlice(Reader_UNIFI reader, string credUrl)
    {
        var msd = new MSData();
        var config = new ReaderConfig { AdjustUnknownTimeZonesToHostTimeZone = false };
        reader.Read(credUrl, msd, config);

        // cpp Reader_UNIFI_Test.cpp:99-125 generates the no-suffix reference mzMLs with
        // `config.indexRange = (0, 1)` — the last `IsUnifi()` pass overwrites earlier larger
        // ranges, so every fixture's plain `<run-id>.mzML` is written with just the first
        // two spectra. SpectrumListFactory.Wrap("index 0-1") replicates that slice in-memory.
        SpectrumListFactory.Wrap(msd, new[] { "index 0-1" });
        return msd;
    }

    private static string DescribeException(Exception e)
    {
        // Walk the full inner-exception chain so the test message names the real cause
        // (HttpClient + JsonDocument errors are typically wrapped 1-2 levels deep).
        var chain = new List<string>();
        for (var cur = e; cur is not null; cur = cur.InnerException)
            chain.Add($"{cur.GetType().Name}: {cur.Message}");
        return string.Join(" → ", chain);
    }

    private static string SafeFilename(string s)
    {
        // cpp Reader_UNIFI_Test references files like `Hi3_ClpB_MSe_01_1_D,1_1.mzML` — the
        // commas + colons stay; only forbidden filesystem chars get scrubbed.
        var bad = Path.GetInvalidFileNameChars();
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (Array.IndexOf(bad, chars[i]) >= 0) chars[i] = '_';
        return new string(chars);
    }

    private static bool HasEnv(string name) => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name));

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    private static string? FindCppTestDataRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "data", "vendor_readers", "UNIFI",
                "Reader_UNIFI_Test.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string? FindOverrideReferenceRoot()
    {
        // Walk parents of the build output until we hit the pwiz-sharp/pwiz/test/UNIFI.Tests
        // source directory, then return the Reference subdirectory underneath it. We don't
        // copy these to the build output (CopyToOutputDirectory) because generate mode needs
        // to write back to the source-tree path so the changes show up in git.
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz-sharp", "pwiz", "test", "UNIFI.Tests", "Reference");
            if (Directory.Exists(Path.GetDirectoryName(candidate)!))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
