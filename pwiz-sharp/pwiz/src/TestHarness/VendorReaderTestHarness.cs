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
                // Preserve the full exception chain (type + message + stack) so flaky vendor-SDK
                // failures surface a useful call site in TC instead of just the bare message.
                // RuntimeBinderException's Message is "Cannot perform runtime binding on a null
                // reference" which is useless without the stack frame that hit the dynamic call.
                result.FailureMessages.Add(
                    $"{Path.GetFileName(entry.TrimEnd('/', '\\'))}: {e.GetType().Name}: {e.Message}\n"
                    + Indent(e.StackTrace ?? "(no stack trace)", 2)
                    + (e.InnerException is null
                        ? string.Empty
                        : "\n  ---> " + Indent(e.InnerException.ToString(), 6).TrimStart()));
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
                AssertFilesUnlocked(rawPath, IsKnownLeakySdkPath(rawPath, config));
        }
    }

    /// <summary>True iff <paramref name="rawPath"/>+<paramref name="config"/> hits a known
    /// vendor-SDK case where managed-side <c>Dispose</c> doesn't release the native file
    /// handle on .NET 8. Soft-fails the post-test rename probe rather than failing the test
    /// outright. cpp's .NET-Framework builds don't have this issue; the cases are listed
    /// per-file-format below.</summary>
    private static bool IsKnownLeakySdkPath(string rawPath, ReaderTestConfig config)
    {
        // 1. wiff2: SCIEX.Apis.Data.v1 + the bundled Clearcore2 hold native readers
        //    in the wiff2 ALC; full release waits for ALC unload (next process).
        if (rawPath.EndsWith(".wiff2", StringComparison.OrdinalIgnoreCase))
            return true;
        // 2. legacy .wiff with simAsSpectra / srmAsSpectra: Clearcore2 builds extra
        //    transition tables when MRM/SIM experiments are pulled into the spectrum
        //    list; those allocate native readers that survive Dispose / GC pairs.
        //    Confirmed by tracing: every Dispose in the cascade succeeds and returns,
        //    but the file remains locked. Same SDK-lifetime story as wiff2.
        if (rawPath.EndsWith(".wiff", StringComparison.OrdinalIgnoreCase)
            && (config.SrmAsSpectra || config.SimAsSpectra))
            return true;
        return false;
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
            SrmAsSpectra = config.SrmAsSpectra,
            // cpp VendorReaderTestHarness.cpp:343 sets adjustUnknownTimeZonesToHostTimeZone=false
            // so reference mzMLs don't pick up the build agent's local TZ. Mirror that here.
            AdjustUnknownTimeZonesToHostTimeZone = false,
            SortAndJitter = config.SortAndJitter,
            PeakPicking = config.PeakPicking,
            DdaProcessing = config.DdaProcessing,
            GlobalChromatogramsAreMs1Only = config.GlobalChromatogramsAreMs1Only,
            IgnoreCalibrationScans = config.IgnoreCalibrationScans,
            IsolationMzAndMobilityFilter = config.IsolationMzAndMobilityFilter,
            IgnoreZeroIntensityPoints = config.IgnoreZeroIntensityPoints,
            // Multi-sample WIFF / WIFF2 inputs: cpp's testReader passes runIndex through
            // to Reader::read, and Sciex's reader uses it to pick which sample to open.
            // ReaderTestConfig.RunIndex is nullable (most fixtures don't care); fall
            // through to 0 = first sample.
            RunIndex = config.RunIndex ?? 0,
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
        // Most vendor readers (Mobilion, Sciex, Agilent, Shimadzu, ...) build their
        // SpectrumList and ChromatogramList around shared backing data, so disposing
        // the SpectrumList tears down state the parallel ChromatogramList still needs
        // for the diff. We orphan the original SL here (replaced with `simple`) but
        // hold a reference until the diff is done, then dispose explicitly to release
        // SDK handles deterministically — without that, MSData.Dispose only cascades
        // into `simple` (a managed-only list with no SDK handles) and the original SL
        // leaks until GC catches up. cpp's harness doesn't have this problem because
        // its IndexRange path operates on the shared SpectrumList directly.
        // The orphaned-list dispose has to wait until after the round-trips below run,
        // because the parallel ChromatogramList (which mzMLb's MzmlWriter walks) shares
        // backing data with the orphaned SpectrumList for vendors like Mobilion. Earlier
        // versions disposed right after the reference diff and crashed in the mzMLb
        // round-trip with ObjectDisposedException on the vendor data.
        ISpectrumList? orphanedSpectrumList = null;
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
            orphanedSpectrumList = sl;
            msd.Run.SpectrumList = simple;
        }

        // 2. Mangle paths + checksums + pwiz software to match how the reference mzML was written.
        CalculateSourceFileChecksums(msd.FileDescription.SourceFiles);
        MangleSourceFileLocations(sourceName, msd.FileDescription.SourceFiles);
        ManglePwizSoftware(msd);

        // 3. Locate the reference mzML and read it. Prefer a pwiz-sharp-specific
        // override at <test-assembly-dir>/Reference/<filename> if present, else fall
        // back to the cpp tree at <rootPath>/<filename>. The override path lets us
        // ship pwiz-sharp-only references (e.g. fixtures cpp doesn't carry, or
        // intermediate references during alignment work) without retriggering every
        // cpp vendor TC config — pwiz-sharp/test/<Vendor>.Tests/Reference/ is opt-in
        // per test project (csproj copies Reference/*.mzML into bin). See
        // pwiz-sharp/test/UNIFI.Tests/Reference/README.md for the rationale.
        string referenceFilename = config.ResultFilename(msd.Run.Id + ".mzML");
        string overridePath = Path.Combine(AppContext.BaseDirectory, "Reference", referenceFilename);
        string cppPath = Path.Combine(rootPath, referenceFilename);
        string referencePath = File.Exists(overridePath) ? overridePath : cppPath;
        if (!File.Exists(referencePath))
            throw new FileNotFoundException(
                $"reference mzML not found at {cppPath} or override {overridePath}");
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

        // 8. mzMLb round-trip: write to HDF5-backed mzML, read it back, verify the spectrum
        // data made the trip. One round-trip exercises every mzMLb writer/reader path
        // in one go:
        //   - m/z array: numpress_linear (opaque-bytes AppendBytes + DecodeDoublesFromRawBytes)
        //   - intensity array: 32-bit float (float-narrow + _float-suffixed dataset)
        //   - other arrays (time / wavelength / etc.): default 64-bit double (_double path)
        // Mirrors cpp msconvert's "--filter numpress" + default intensity precision.
        //
        // Diff tolerance has to accommodate the lossy narrowing. The dominant
        // source of error is float32 precision on intensity arrays: single-precision
        // float has ~7 sig digits, so an intensity of 1e7 has ULP ~1.0. 1.0 absolute
        // is a safe envelope across all the vendor fixtures we exercise (m/z is
        // numpress_linear which round-trips much tighter than that).
        // mzMLb writes are path-bound (HDF5 needs random file I/O), so materialize
        // to %TEMP%.
        // The two HDF5-backed round-trips below both re-parse the round-tripped file's
        // embedded mzML XML. Under a CLR coverage profiler (dotCover, etc.) the
        // per-method instrumentation overhead on the XML reader's tight inner loop
        // amplifies a 1–2 s parse into 10+ minutes — confirmed with dotnet-stack:
        // single CPU-bound thread parked in MzmlReader.ReadMzmlBody. To keep TC
        // coverage runs reasonable, only the per-vendor rep fixture (smallest one,
        // flagged via <see cref="ReaderTestConfig.RunRoundTripUnderProfiler"/>) runs
        // the round-trip under profiler; everyone else short-circuits. Off-profiler
        // dev runs always run the full round-trip suite.
        bool skipRoundTripForProfiler =
            IsCoverageProfilerActive() && !config.RunRoundTripUnderProfiler;

        if (config.TestMzmlbRoundTrip && !skipRoundTripForProfiler && msd.Run.SpectrumList is not null)
        {
            var encoderConfig = new Pwiz.Data.MsData.Encoding.BinaryEncoderConfig();
            encoderConfig.PrecisionOverrides[CVID.MS_intensity_array] =
                Pwiz.Data.MsData.Encoding.BinaryPrecision.Bits32;
            encoderConfig.NumpressOverrides[CVID.MS_m_z_array] =
                Pwiz.Data.MsData.Encoding.BinaryNumpress.Linear;
            RunMzmlbRoundTrip(msd, config, encoderConfig, diffPrecision: 1.0);
        }

        // mz5 round-trip removed — there's no in-process mz5 writer (the C# port is
        // read-only), and the previous workaround (write mzML, shell out to cpp
        // msconvert.exe --mz5, read the mz5 back) made pwiz-sharp's CI results depend
        // on whichever cpp build happened to live in the agent's build-nt-x86 tree.
        // That coupling produced reproducible "we didn't change anything but the
        // vendor mz5 tests started failing" surprises (see TC build 3993953 vs 3991833:
        // cpp version stamp drifted between runs, breaking these on the agent without
        // any pwiz-sharp change). The mzMLb round-trip above already covers the same
        // HDF5-backed binary-format round-trip path with no out-of-process dependency.
        // The Mz5ReaderAdapter itself is tested independently against committed cpp-
        // written mz5 fixtures (see test/MsData.Tests/Mz5CppFixtureTests).

        // Round-trips are done; safe now to release the orphaned SpectrumList's SDK
        // handles (and the shared vendor data that the ChromatogramList rode on top of).
        // If a round-trip threw, the orphan leaks until MSData.Dispose — same as
        // pre-IndexRange behavior.
        orphanedSpectrumList?.Dispose();

        // We finished a real read+diff (vs. taking the identify-only short-circuit). Tell
        // TestOne to run the file-unlock probe once `using var msd` releases vendor handles.
        readSucceeded = true;
    }

    /// <summary>
    /// One mzMLb write+read cycle. Writes <paramref name="msd"/> via
    /// <see cref="Pwiz.Data.MsData.MzMlb.MzMlbWriter"/> (configured with the supplied
    /// <paramref name="encoderConfig"/>) to a temp file, reads it back with
    /// <see cref="Pwiz.Data.MsData.Readers.MzMlbReaderAdapter"/>, and diffs the spectrum
    /// data at the requested precision.
    /// </summary>
    private static void RunMzmlbRoundTrip(
        MSData msd,
        ReaderTestConfig config,
        Pwiz.Data.MsData.Encoding.BinaryEncoderConfig? encoderConfig,
        double diffPrecision)
    {
        string tmp = Path.Combine(
            Path.GetTempPath(),
            $"harness-mzmlb-{Guid.NewGuid():N}.mzMLb");
        try
        {
            new Pwiz.Data.MsData.MzMlb.MzMlbWriter(encoderConfig).Write(msd, tmp);
            var roundtripped = new MSData();
            new Pwiz.Data.MsData.Readers.MzMlbReaderAdapter().Read(tmp, roundtripped);
            string report = MSDataDiff.DescribeSpectraDataOnly(msd, roundtripped, diffPrecision);
            if (report.Length > 0)
                throw new InvalidOperationException("mzMLb round-trip diff:\n" + report);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    /// <summary>
    /// True when the current process is being instrumented by a .NET coverage / profiler
    /// tool (dotCover, OpenCover, coverlet collector, etc.). Detected via the standard
    /// CLR profiling env vars (<c>CORECLR_ENABLE_PROFILING</c>=<c>1</c> or
    /// <c>COR_ENABLE_PROFILING</c>=<c>1</c>) — every CLR profiler sets these before the
    /// runtime spins up. We use this to short-circuit the HDF5-backed round-trips,
    /// whose embedded-mzML re-parse is multiplicative-slow under per-method
    /// instrumentation (see comment at the round-trip call site).
    /// </summary>
    private static bool IsCoverageProfilerActive()
    {
        return string.Equals(Environment.GetEnvironmentVariable("CORECLR_ENABLE_PROFILING"), "1", StringComparison.Ordinal)
            || string.Equals(Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING"), "1", StringComparison.Ordinal);
    }

    /// <summary>
    /// Mirrors pwiz cpp <c>VendorReaderTestHarness.cpp</c> lines 1004–1019: after the reader
    /// has been disposed, rename the vendor source to <c>&lt;path&gt;.renamed</c> and back.
    /// If a vendor handle is still open the OS will reject the rename with a sharing-violation
    /// IOException; that's the regression we want to catch (vendor SpectrumList missed a
    /// Dispose, MSData/Run didn't propagate, Converter didn't `using`, etc.).
    /// </summary>
    private static void AssertFilesUnlocked(string rawPath, bool knownLeakySdk = false)
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
        // 5×50ms wasn't enough for cleanup that happens to fall on a finalizer
        // schedule (the SDK queues handle release rather than synchronizing).
        // 5×100ms is plenty for the well-behaved cases; known-leaky cases short-
        // circuit to soft-fail below.
        int maxAttempts = 5;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
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
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException ex)
            {
                lastError = new IOException(ex.Message, ex);
                Thread.Sleep(100);
            }
        }
        if (lastError is not null)
        {
            // cpp VendorReaderTestHarness.cpp:1014-1016 has the same HACK for Bruker YEP/FID
            // CompassXtract leaks. We tag the cases where Clearcore2 / wiff2 deliberately
            // retain handles on .NET 8 (see IsKnownLeakySdkPath in TestOne) and soft-fail
            // those — the read+diff already passed, so the rename probe's only role here
            // would be to flag a regression in our own Dispose plumbing, not in SDK lifetime.
            if (knownLeakySdk)
            {
                Console.Error.WriteLine(
                    $"warning: cannot rename {rawPath} after dispose (vendor SDK retains handles " +
                    $"on .NET 8 — see VendorReaderTestHarness.IsKnownLeakySdkPath): {lastError.Message}");
                return;
            }
            throw new InvalidOperationException(
                $"Cannot rename {rawPath} after dispose: there are unreleased file locks. " +
                $"Likely a missing Dispose somewhere in the SpectrumList → backing-data chain. " +
                $"Underlying error: {lastError.Message}", lastError);
        }

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

    /// <summary>Prefixes every non-empty line of <paramref name="text"/> with
    /// <paramref name="spaces"/> spaces. Used to nest stack traces under the failure header.</summary>
    private static string Indent(string text, int spaces)
    {
        ArgumentNullException.ThrowIfNull(text);
        string pad = new(' ', spaces);
        return string.Join('\n', text.Split('\n').Select(l => l.Length == 0 ? l : pad + l));
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
