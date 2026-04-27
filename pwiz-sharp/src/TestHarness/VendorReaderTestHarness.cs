using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Diff;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Diff;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Sources;
using Pwiz.Util;

namespace Pwiz.TestHarness;

/// <summary>
/// Predicate that decides whether a given <c>.d</c> / <c>.raw</c> / etc. path should be tested
/// by <see cref="VendorReaderTestHarness.TestReader"/>. Port of
/// <c>pwiz::util::TestPathPredicate</c>.
/// </summary>
public abstract class TestPathPredicate
{
    /// <summary>True when <paramref name="rawPath"/> is a real vendor source this predicate matches.</summary>
    public abstract bool Matches(string rawPath);
}

/// <summary>Predicate that matches by filename.</summary>
public sealed class IsNamedRawFile : TestPathPredicate
{
    private readonly HashSet<string> _names;

    /// <summary>Matches any of the given filenames (case-insensitive, filename only).</summary>
    public IsNamedRawFile(params string[] names)
    {
        _names = new HashSet<string>(names ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public override bool Matches(string rawPath) =>
        _names.Contains(Path.GetFileName(rawPath.TrimEnd('/', '\\')));
}

/// <summary>Test orchestration result — accumulates pass/fail counts across harness runs.</summary>
public sealed class TestResult
{
    /// <summary>Total number of vendor sources tested.</summary>
    public int TotalTests { get; set; }

    /// <summary>Number of sources where the diff was non-empty or an exception was thrown.</summary>
    public int FailedTests { get; set; }

    /// <summary>Per-source failure messages collected during the run.</summary>
    public List<string> FailureMessages { get; } = new();

    /// <summary>Merges <paramref name="other"/> into this result.</summary>
    public TestResult Add(TestResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        TotalTests += other.TotalTests;
        FailedTests += other.FailedTests;
        FailureMessages.AddRange(other.FailureMessages);
        return this;
    }

    /// <summary>Throws if any failures were recorded (or if no tests ran at all).</summary>
    public void Check()
    {
        if (TotalTests == 0)
            throw new InvalidOperationException("no vendor test data found — supply a valid rootPath");
        if (FailedTests > 0)
            throw new InvalidOperationException(
                $"failed {FailedTests} of {TotalTests} tests:\n" + string.Join('\n', FailureMessages));
    }
}

/// <summary>
/// Test harness that mirrors pwiz C++ <c>VendorReaderTestHarness</c>. For each vendor source
/// directory under a supplied root that matches a <see cref="TestPathPredicate"/>, the reader
/// is invoked, the in-memory <see cref="MSData"/> is compared against a sibling reference mzML
/// (filename derived from the run id and <see cref="ReaderTestConfig"/>).
/// </summary>
/// <remarks>
/// Scope vs. pwiz C++: we run the core <b>read + diff against reference mzML</b> cycle. We do
/// not (yet) exercise round-trip through mzXML/MGF/mz5/mzMLb serializers, Unicode rename, or
/// thread-safety variants — those can be layered on later.
/// </remarks>
public static class VendorReaderTestHarness
{
    /// <summary>
    /// Iterates immediate children of <paramref name="rootPath"/> and, for any matching
    /// <paramref name="predicate"/>, reads through <paramref name="reader"/> and compares to
    /// the reference mzML at <c>rootPath/&lt;ResultFilename&gt;</c>. Failures are accumulated
    /// into the returned <see cref="TestResult"/>; call <see cref="TestResult.Check"/> to throw.
    /// </summary>
    public static TestResult TestReader(
        IReader reader,
        string rootPath,
        TestPathPredicate predicate,
        ReaderTestConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(rootPath);
        ArgumentNullException.ThrowIfNull(predicate);
        config ??= new ReaderTestConfig();

        var result = new TestResult();
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Test data root not found: {rootPath}");

        foreach (var entry in Directory.EnumerateFileSystemEntries(rootPath))
        {
            if (!predicate.Matches(entry)) continue;
            result.TotalTests++;
            try
            {
                TestOne(reader, entry, rootPath, config);
            }
            catch (Exception e)
            {
                result.FailedTests++;
                result.FailureMessages.Add(
                    $"{Path.GetFileName(entry.TrimEnd('/', '\\'))}: {e.Message}");
            }
        }

        return result;
    }

    private static void TestOne(IReader reader, string rawPath, string rootPath, ReaderTestConfig config)
    {
        // 1. Read the raw file through the vendor reader.
        var msd = new MSData();
        var readerConfig = new ReaderConfig
        {
            PreferOnlyMsLevel = config.PreferOnlyMsLevel,
            CombineIonMobilitySpectra = config.CombineIonMobilitySpectra,
            SimAsSpectra = config.SimAsSpectra,
            SortAndJitter = config.SortAndJitter,
        };
        reader.Read(rawPath, msd, readerConfig);

        string sourceName = Path.GetFileName(rawPath.TrimEnd('/', '\\'));

        // 1a. Apply config-driven SpectrumList wrappers (peak picking, etc.) before mangling.
        config.Wrap(msd);

        // 2. Mangle paths + checksums + pwiz software to match how the reference mzML was written.
        CalculateSourceFileChecksums(msd.FileDescription.SourceFiles);
        MangleSourceFileLocations(sourceName, msd.FileDescription.SourceFiles);
        ManglePwizSoftware(msd);

        // 3. Locate the reference mzML and read it.
        string referenceFilename = config.ResultFilename(msd.Run.Id + ".mzML");
        string referencePath = Path.Combine(rootPath, referenceFilename);
        if (!File.Exists(referencePath))
            throw new FileNotFoundException($"reference mzML not found: {referencePath}");
        MSData referenceMsd;
        using (var fs = File.OpenRead(referencePath))
            referenceMsd = new MzmlReader().Read(fs);

        // 4. Apply the same "hack in memory" treatment to the reference MSData (strip trailing
        // sourceFile pwiz writes on load; normalize paths/pwiz software).
        HackInMemoryMSData(sourceName, referenceMsd);

        // 5. Diff.
        var diffConfig = config.BuildDiffConfig();
        diffConfig.IgnoreVersions = true; // tolerate minor pwiz version drift
        string diff = MSDataDiff.Describe(msd, referenceMsd, diffConfig);
        if (diff.Length > 0)
            throw new InvalidOperationException(diff);
    }

    // ---------- helpers ported from VendorReaderTestHarness.cpp ----------

    /// <summary>
    /// Rewrites absolute <c>file://...</c> locations in <paramref name="sourceFiles"/> to be
    /// path-agnostic (start at the source name). Port of <c>mangleSourceFileLocations</c>.
    /// </summary>
    public static void MangleSourceFileLocations(string sourceName, IEnumerable<SourceFile> sourceFiles)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);
        foreach (var sf in sourceFiles)
        {
            if (string.IsNullOrEmpty(sf.Location)) continue;
            if (sf.Location.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;

            int idx = sf.Location.IndexOf(sourceName, StringComparison.Ordinal);
            if (idx >= 0)
                sf.Location = "file:///" + sf.Location[idx..];
            else
                sf.Location = "file:///";
        }
    }

    /// <summary>
    /// Normalizes the pwiz software entry in <paramref name="msd"/> to a stable id so a
    /// pwiz version change doesn't require regenerating every reference mzML. Port of
    /// <c>manglePwizSoftware</c> — we pick the first pwiz entry, rename to <c>current pwiz</c>,
    /// remove the rest, and re-point ProcessingMethod refs. Version text is kept as-is.
    /// </summary>
    public static void ManglePwizSoftware(MSData msd)
    {
        ArgumentNullException.ThrowIfNull(msd);

        var pwizIndices = new List<int>();
        for (int i = 0; i < msd.Software.Count; i++)
            if (msd.Software[i].HasCVParam(CVID.MS_pwiz))
                pwizIndices.Add(i);

        if (pwizIndices.Count == 0) return;

        var pwizSoftware = msd.Software[pwizIndices[0]];
        pwizSoftware.Id = "current pwiz";
        pwizSoftware.Version = MSData.PwizVersion; // normalize so different-versioned refs match

        // Collapse to a single DataProcessing (keep the first one only).
        if (msd.DataProcessings.Count > 1)
            msd.DataProcessings.RemoveRange(1, msd.DataProcessings.Count - 1);

        foreach (var dp in msd.DataProcessings)
            foreach (var pm in dp.ProcessingMethods)
                pm.Software = pwizSoftware;

        // Remove other pwiz software entries (reverse order so indices stay valid).
        for (int k = pwizIndices.Count - 1; k >= 1; k--)
            msd.Software.RemoveAt(pwizIndices[k]);
    }

    /// <summary>
    /// Computes a SHA-1 for each <see cref="SourceFile"/> referring to a local file and sets
    /// the <see cref="CVID.MS_SHA_1"/> param. Port of <c>calculateSourceFileChecksums</c>.
    /// </summary>
    public static void CalculateSourceFileChecksums(IEnumerable<SourceFile> sourceFiles)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);
        const string uriPrefix = "file://";
        foreach (var sf in sourceFiles)
        {
            if (!sf.Location.StartsWith(uriPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            string location = sf.Location[uriPrefix.Length..].TrimStart('/');
            string localPath = Path.Combine(location, sf.Name);
            if (!File.Exists(localPath)) continue;
            sf.Set(CVID.MS_SHA_1, Sha1Calculator.HashFile(localPath));
        }
    }

    /// <summary>
    /// Post-read normalization applied to the <b>reference</b> MSData loaded from the mzML
    /// fixture so it's directly comparable to a freshly-read vendor MSData. Port of
    /// <c>hackInMemoryMSData</c>.
    /// </summary>
    public static void HackInMemoryMSData(string sourceName, MSData msd)
    {
        ArgumentNullException.ThrowIfNull(msd);
        // NOTE: pwiz C++ strips the last sourceFile here because its mzML loader appends the
        // mzML path as a self-reference. Our MzmlReader doesn't, so we don't strip.
        MangleSourceFileLocations(sourceName, msd.FileDescription.SourceFiles);
        ManglePwizSoftware(msd);
    }
}
