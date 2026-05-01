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
using Pwiz.Data.MsData.Mgf;
using Pwiz.Data.MsData.Spectra;
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
/// Per-fixture scratch context for the vendor harness `[TestMethod]`s. A test method calls
/// <see cref="Run"/> once per config variant it wants to exercise on the fixture and ends with
/// <see cref="Check"/>; the context aggregates results across the calls and asserts that every
/// <see cref="Run"/> resulted in exactly one fixture being matched (so a typo in a fixture name
/// fails loudly instead of silently passing). Lives next to <see cref="TestResult"/> so all
/// three vendor test classes (Bruker, Thermo, Waters) can reuse it.
/// </summary>
public sealed class FixtureRunContext
{
    private readonly IReader _reader;
    private readonly string _root;
    private readonly TestPathPredicate _predicate;
    private readonly string _fixtureName;
    private readonly TestResult _result = new();
    private int _runs;

    /// <summary>Constructs the context. Returns from a per-fixture SetUp helper.</summary>
    public FixtureRunContext(IReader reader, string rootPath, TestPathPredicate predicate, string fixtureName)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(rootPath);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(fixtureName);
        _reader = reader;
        _root = rootPath;
        _predicate = predicate;
        _fixtureName = fixtureName;
    }

    /// <summary>Runs the harness for this fixture under <paramref name="config"/> and accumulates the result.</summary>
    public void Run(ReaderTestConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _runs++;
        _result.Add(VendorReaderTestHarness.TestReader(_reader, _root, _predicate, config));
    }

    /// <summary>
    /// Asserts that all accumulated runs passed and that <see cref="Run"/> matched exactly one
    /// fixture each time (predicate hit rate == call count). Throws on mismatch; the exception
    /// surfaces as a test failure in the calling MSTest method (TestHarness.csproj deliberately
    /// doesn't reference MSTest, so we can't call Assert.Fail directly here).
    /// </summary>
    public void Check()
    {
        if (_result.FailedTests > 0)
            throw new InvalidOperationException(
                $"{_fixtureName}: {_result.FailedTests} of {_result.TotalTests} runs failed:\n" +
                string.Join('\n', _result.FailureMessages));
        if (_result.TotalTests != _runs)
            throw new InvalidOperationException(
                $"{_fixtureName}: harness ran {_result.TotalTests} reads but {_runs} were expected (one per Run() call) — predicate may have failed to match");
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
        bool readSucceeded = false;
        try
        {
            ReadAndDiff(reader, rawPath, rootPath, config, out readSucceeded);
        }
        finally
        {
            // ReadAndDiff disposed msd already (the using-scope exits before this finally
            // runs). The check has to happen AFTER disposal so any vendor file handles are
            // released — that's what makes it a useful regression guard against missing
            // Dispose plumbing.
            if (readSucceeded)
                AssertFilesUnlocked(rawPath);
        }
    }

    private static void ReadAndDiff(IReader reader, string rawPath, string rootPath, ReaderTestConfig config,
        out bool readSucceeded)
    {
        readSucceeded = false;

        // 1. Read the raw file through the vendor reader. `using` cascades disposal to the
        // SpectrumList / ChromatogramList, releasing the underlying vendor file handle once
        // the diff is done.
        using var msd = new MSData();
        var readerConfig = new ReaderConfig
        {
            PreferOnlyMsLevel = config.PreferOnlyMsLevel,
            CombineIonMobilitySpectra = config.CombineIonMobilitySpectra,
            SimAsSpectra = config.SimAsSpectra,
            SortAndJitter = config.SortAndJitter,
            PeakPicking = config.PeakPicking,
            DdaProcessing = config.DdaProcessing,
            GlobalChromatogramsAreMs1Only = config.GlobalChromatogramsAreMs1Only,
            IgnoreCalibrationScans = config.IgnoreCalibrationScans,
            IsolationMzAndMobilityFilter = config.IsolationMzAndMobilityFilter,
        };
        try
        {
            reader.Read(rawPath, msd, readerConfig);
        }
        catch (VendorSupportNotEnabledException ex)
        {
            // Vendor SDK not compiled into this build (NO_VENDOR_SUPPORT). Verify Identify()
            // still recognizes the source and call it a pass — full read+diff parity is the
            // job of the with-vendor-DLLs build. NOTE: catch only this specific subclass so
            // legitimate NotSupportedException from a reader (e.g. "format X not yet ported")
            // bubbles up as a real test failure.
            string head = ReadHead(rawPath);
            CVID identified = reader.Identify(rawPath, head);
            if (identified == CVID.CVID_Unknown)
                throw new InvalidOperationException(
                    $"identify-only mode: Read() threw VendorSupportNotEnabledException AND Identify() returned CVID_Unknown for {rawPath}. " +
                    $"Underlying message: {ex.Message}");
            Console.WriteLine(
                $"[identify-only] {Path.GetFileName(rawPath.TrimEnd('/', '\\'))}: vendor SDK not built into this configuration; " +
                $"Identify() returned {identified}. Full read+diff is skipped.");
            return;
        }

        string sourceName = Path.GetFileName(rawPath.TrimEnd('/', '\\'));

        // 1a. Apply config-driven SpectrumList wrappers (peak picking, etc.) before mangling.
        config.Wrap(msd);

        // 1b. Apply IndexRange — pwiz C++ uses indexRange = (a, b) for a few reference
        // fixtures that only ship the first spectrum (e.g. -globalChromatogramsAreMs1Only).
        // We materialize the requested range into a SpectrumListSimple so the subsequent
        // diff sees only those spectra (and the SpectrumIdentity index numbers stay 0-based
        // contiguous, matching the cpp output).
        if (config.IndexRange is { } range && msd.Run.SpectrumList is { } sl)
        {
            int start = Math.Max(0, range.Start);
            int end = Math.Min(sl.Count - 1, range.End);
            var simple = new SpectrumListSimple { Dp = sl.DataProcessing };
            for (int i = start; i <= end; i++)
            {
                var spec = sl.GetSpectrum(i, getBinaryData: true);
                spec.Index = i - start;
                simple.Spectra.Add(spec);
            }
            msd.Run.SpectrumList = simple;
        }

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

        // 6. mzXML round-trip: write to mzXML, parse it back, verify the spectrum data made the
        // round trip. mzXML loses most metadata (instrument config, multi-scan combineIMS,
        // scan-list combination type, userParam units), so the check restricts to the data
        // path via DescribeSpectraDataOnly. Mirrors cpp VendorReaderTestHarness's mzXML diff
        // pattern — cpp uses a much-extended Diff with non-mzML tolerances we don't port.
        if (config.TestMzxmlRoundTrip && msd.Run.SpectrumList is not null)
        {
            using var mem = new MemoryStream();
            new Pwiz.Data.MsData.MzXml.MzxmlWriter().Write(msd, mem);
            mem.Position = 0;
            var roundtripped = new MSData();
            Pwiz.Data.MsData.MzXml.MzxmlReader.Read(mem, roundtripped);

            string mzxmlReport = MSDataDiff.DescribeSpectraDataOnly(
                msd, roundtripped, config.DiffPrecision ?? 1e-6);
            if (mzxmlReport.Length > 0)
                throw new InvalidOperationException("mzXML round-trip diff:\n" + mzxmlReport);
        }

        // 7. MGF round-trip: same idea, but MGF only carries MS2+ peak lists with precursors,
        // so we filter the original first and compare that filtered subset against the parsed
        // MGF. Mirrors cpp VendorReaderTestHarness's mzML↔MGF check.
        if (config.TestMgfRoundTrip && config.PreferOnlyMsLevel != 2 && msd.Run.SpectrumList is not null)
        {
            var filtered = BuildMgfFilteredCopy(msd);
            if (filtered.Run.SpectrumList?.Count > 0)
            {
                using var mem = new MemoryStream();
                using (var writer = new StreamWriter(mem, leaveOpen: true))
                    new MgfSerializer().Write(filtered, writer);
                mem.Position = 0;
                MSData roundtripped;
                using (var rdr = new StreamReader(mem))
                    roundtripped = new MgfSerializer().Read(rdr);

                string mgfReport = MSDataDiff.DescribeSpectraDataOnly(
                    filtered, roundtripped, config.DiffPrecision ?? 1e-6,
                    MSDataDiff.LossyMsLevelMode.MgfFlatten);
                if (mgfReport.Length > 0)
                    throw new InvalidOperationException("MGF round-trip diff:\n" + mgfReport);
            }
        }

        // We finished a real read+diff (vs. taking the identify-only short-circuit). Tell
        // TestOne to run the file-unlock probe once `using var msd` releases vendor handles.
        readSucceeded = true;
    }

    /// <summary>
    /// Mirrors pwiz cpp <c>VendorReaderTestHarness.cpp</c> lines 1004–1019: after the reader
    /// has been disposed, rename the vendor source to <c>&lt;path&gt;.renamed</c> and back.
    /// If a vendor handle is still open the OS will reject the rename with a sharing-violation
    /// IOException; that's the regression we want to catch (vendor SpectrumList missed a
    /// Dispose, MSData/Run didn't propagate, Converter didn't `using`, etc.).
    /// </summary>
    private static void AssertFilesUnlocked(string rawPath)
    {
        if (!File.Exists(rawPath) && !Directory.Exists(rawPath)) return;

        // Some vendor SDKs queue native handle release through a finalizer / thread-local
        // disposal path that's only synchronous after a GC pass. Force one before the probe
        // so the test asks "are handles released after Dispose + GC?" rather than "right
        // now?". Also retry briefly in case Windows hasn't fully reaped the handle yet.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        string renamed = rawPath.TrimEnd('/', '\\') + ".renamed";
        IOException? lastError = null;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Move(rawPath, renamed);
                lastError = null;
                break;
            }
            catch (IOException ex)
            {
                lastError = ex;
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException ex)
            {
                lastError = new IOException(ex.Message, ex);
                Thread.Sleep(50);
            }
        }
        if (lastError is not null)
            throw new InvalidOperationException(
                $"Cannot rename {rawPath} after dispose: there are unreleased file locks. " +
                $"Likely a missing Dispose somewhere in the SpectrumList → backing-data chain. " +
                $"Underlying error: {lastError.Message}", lastError);

        try
        {
            Directory.Move(renamed, rawPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Renamed {rawPath} -> {renamed} succeeded but couldn't move it back; " +
                $"underlying error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reads the first ~512 bytes of <paramref name="rawPath"/> as a Latin-1 string for
    /// header-sniffing identifiers. Returns an empty string for directories or when the file
    /// is unreadable; callers fall back to filename-based identification in that case.
    /// </summary>
    private static string ReadHead(string rawPath)
    {
        try
        {
            if (Directory.Exists(rawPath)) return string.Empty;
            using var fs = File.OpenRead(rawPath);
            Span<byte> buf = stackalloc byte[512];
            int n = fs.Read(buf);
            if (n <= 0) return string.Empty;
            return System.Text.Encoding.Latin1.GetString(buf[..n]);
        }
        catch { return string.Empty; }
    }

    /// <summary>
    /// Builds an <see cref="MSData"/> whose SpectrumList contains only the MS2+ precursor-bearing
    /// spectra of <paramref name="original"/> — i.e. the subset MGF would actually emit. Mirrors
    /// cpp <c>SpectrumList_MGF_Filter</c> in <c>VendorReaderTestHarness.cpp</c>.
    /// </summary>
    private static MSData BuildMgfFilteredCopy(MSData original)
    {
        var copy = new MSData { Id = original.Id };
        var sl = original.Run.SpectrumList;
        if (sl is null) return copy;

        var filtered = new SpectrumListSimple();
        for (int i = 0; i < sl.Count; i++)
        {
            var spec = sl.GetSpectrum(i, getBinaryData: true);
            if (!MgfSerializer.IsMgfWritable(spec)) continue;
            spec.Index = filtered.Spectra.Count;
            filtered.Spectra.Add(spec);
        }
        copy.Run.SpectrumList = filtered;
        return copy;
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
