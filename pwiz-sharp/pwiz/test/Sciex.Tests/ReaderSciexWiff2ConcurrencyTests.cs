using System.Collections.Concurrent;
using Pwiz.TestHarness;

namespace Pwiz.Vendor.Sciex.Tests;

/// <summary>
/// Several <c>.wiff2</c> readers open at the same time, on one file or on two, must not break
/// each other; and a reader that failed to open must not break the process.
/// </summary>
/// <remarks>
/// <para>This pins the invariant any change that shares a single <c>ISampleDataApi</c> across
/// readers has to preserve. Sharing the api is the only known fix for the SDK leak (every
/// <c>CreateSampleDataApi</c> leaves a <c>SampleDataProviderServer</c> rooted by its own timer,
/// and the SDK has no shutdown), but the SDK keeps one pooled storage location per path and
/// <c>CloseFile</c> from one reader purges it while another reader on the same path may be
/// mid-request. The reader's catch blocks turn that into empty spectra, not an error. Skyline
/// creates exactly that shape when it imports a multi-sample <c>.wiff2</c> as one replicate
/// per sample through <c>MultiFileLoader</c>, several loads at once, and
/// <c>Reader_Sciex.EnumerateSampleNames</c> races the same way.</para>
/// <para>Measured 2026-09-15 on <c>swath.api.wiff2</c>, against three shapes of the reader.
/// On the api-per-reader code that preceded <c>Wiff2Sdk</c>, the dispose and same-path churn
/// scenarios pass trivially. On a shared api with NO ownership arbitration the churn scenario
/// fails 8 of 8 runs (<c>SQLiteException: bad parameter or other API misuse</c> or
/// <c>ObjectDisposedException</c> from the readers), while the dispose scenario still passes -
/// the SDK re-creates a purged storage location on the next request, so only a request in
/// flight at the moment of the purge can see it. On the shared api with per-path reference
/// counting that <c>Wiff2Sdk</c> now carries (only the last reader on a path closes the file)
/// every scenario passes, 8 of 8. The churn scenario is therefore the acceptance test for that
/// fix, and the cross-path scenario covers the one axis the fix leaves unserialized.</para>
/// <para>Two details of the churn scenario keep its failure rate on the bare shared api near
/// certain, and both were measured: spectra are read with <c>addZeros: false</c>, because the
/// framing-zeros path retries the first SDK failure through a process-wide latch and hid two
/// of three failing readers; and the churn thread touches only the cycle count before it
/// disposes, because a spectrum read of its own (whose SDK enumerator is never disposed)
/// halved the rate. Fewer than ~150 churn opens also halves it.</para>
/// <para>Not covered: the fixtures available are single-sample, so a reader-per-FILE design
/// that switches samples in place (the C++ <c>WiffFile2</c> shape) would not be caught here
/// if it handed one reader another sample's data.</para>
/// </remarks>
[TestClass]
public class ReaderSciexWiff2ConcurrencyTests
{
    private const string FIXTURE = "swath.api.wiff2";
    // A second, unrelated path - the cross-path test needs two files, not two samples
    private const string FIXTURE_EAD = "7600ZenoTOFMSMS_EAD_TestData.wiff2";
    // Hang guard only: the SDK requests take no cancellation token, so a block inside the
    // SDK under contention would otherwise wedge the test process with no result. Covers all
    // three scenarios, which together take ~19 s.
    private const int TEST_TIMEOUT_MS = 120_000;
    // ~5 s per churn on the shared api; ~8 s on the api-per-open code it replaced
    private const int CHURN_OPENS = 150;
    private const int READER_COUNT = 3;

    /// <summary>
    /// One method rather than three: each scenario is a few seconds of SDK work on the same
    /// two fixtures, none needs its own process or scheduling, and the fixture lookup and
    /// SDK warm-up are paid once. Ordered from the simplest shape to the widest.
    /// </summary>
    [TestMethod, Timeout(TEST_TIMEOUT_MS)]
    public void Reader_Sciex_wiff2_ConcurrentReaders()
    {
        string wiff2 = RequireFixture(FIXTURE);
        string otherWiff2 = RequireFixture(FIXTURE_EAD);

        SecondReaderSurvivesFirstReaderDispose(wiff2);
        ConcurrentReadersSurviveChurnOnSamePath(wiff2);
        CloseOnOnePathDoesNotDisturbAnother(churnPath: wiff2, readPath: otherWiff2);
    }

    /// <summary>
    /// A constructor that throws before it claims the path still leaves an object for the
    /// finalizer, which the counted-reader design added so that a reader dropped without
    /// <c>Dispose</c> cannot strand the count. That finalizer has to see a never-claimed reader
    /// as nothing to release: an exception escaping it ends the process, and the original open
    /// error with it.
    /// </summary>
    [TestMethod, Timeout(TEST_TIMEOUT_MS)]
    public void Reader_Sciex_wiff2_FailedOpenFinalizesCleanly()
    {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".wiff2");
        var failure = OpenExpectingFailure(missing);
        Assert.IsInstanceOfType<FileNotFoundException>(failure.GetBaseException(),
            "a missing file should fail to open: " + Describe(failure));
        // Run the failed reader's finalizer now, inside this test, rather than at whatever
        // later point the GC would reach it: a throw from it ends the test host either way,
        // and here the crash is attributable
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    /// <summary>
    /// Opens in its own frame so that no reference to the half-constructed reader survives
    /// into the caller's collection.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Exception OpenExpectingFailure(string path)
    {
        try
        {
            using var reader = AbstractWiffFile.Open(path);
        }
        catch (Exception x)
        {
            return x;
        }
        Assert.Fail("opening " + path + " should have thrown");
        return null!;    // Unreachable: Assert.Fail throws
    }

    private static void SecondReaderSurvivesFirstReaderDispose(string wiff2)
    {
        using var first = AbstractWiffFile.Open(wiff2);
        // First is alone at this point, so it is the baseline
        var baseline = ReadBaseline(first);

        using var second = AbstractWiffFile.Open(wiff2);
        // Touch the SDK through the second reader before the first goes away. This fills the
        // second reader's per-instance caches (cycle count, TIC), so of the reads after the
        // dispose it is the spectrum, which re-enters the SDK on every call, that matters.
        Assert.IsNull(DescribeReaderFailure(second.GetExperiment(0), baseline),
            "second reader disagreed with a lone reader before the first was disposed");

        first.Dispose();

        Assert.IsNull(DescribeReaderFailure(second.GetExperiment(0), baseline),
            "second reader broke when the first reader was disposed");
    }

    private static void ConcurrentReadersSurviveChurnOnSamePath(string wiff2)
    {
        Baseline baseline;
        using (var solo = AbstractWiffFile.Open(wiff2))
            baseline = ReadBaseline(solo);

        var failures = new ConcurrentQueue<string>();
        // The readers' own opens race the churn as well: a reader's Open is the longest SDK
        // sequence there is, and with the churn held back until every reader was open the
        // failure rate on a bare shared api halved.
        using var churnDone = new ManualResetEventSlim();
        int churned = 0, reads = 0;

        // Churn: open, touch and dispose readers on the path, so CloseFile lands at arbitrary
        // points inside the long-lived readers' SDK calls. See the class remarks for why this
        // reads only the cycle count.
        void Churn()
        {
            try
            {
                for (int i = 0; i < CHURN_OPENS; i++)
                {
                    using var r = AbstractWiffFile.Open(wiff2);
                    if (r.GetExperiment(0).CycleCount != baseline.CycleCount)
                    {
                        failures.Enqueue("churn: cycle count " + r.GetExperiment(0).CycleCount);
                        return;
                    }
                    Interlocked.Increment(ref churned);
                }
            }
            catch (Exception x)
            {
                failures.Enqueue("churn: " + Describe(x));
            }
            finally
            {
                churnDone.Set();
            }
        }

        // Readers: hold an open reader and keep reading through it until the churn is done.
        void Read()
        {
            try
            {
                using var r = AbstractWiffFile.Open(wiff2);
                var exp = r.GetExperiment(0);
                while (!churnDone.IsSet)
                {
                    var failure = DescribeReaderFailure(exp, baseline);
                    if (failure != null)
                    {
                        failures.Enqueue("reader: " + failure);
                        return;
                    }
                    Interlocked.Increment(ref reads);
                }
            }
            catch (Exception x)
            {
                failures.Enqueue("reader: " + Describe(x));
            }
        }

        var tasks = new List<Task>();
        for (int i = 0; i < READER_COUNT; i++)
            tasks.Add(Task.Factory.StartNew(Read, TaskCreationOptions.LongRunning));
        tasks.Add(Task.Factory.StartNew(Churn, TaskCreationOptions.LongRunning));
        Task.WaitAll(tasks.ToArray());

        Assert.AreEqual(0, failures.Count,
            "concurrent readers on one .wiff2 failed: " + string.Join(" | ", failures));
        Assert.AreEqual(CHURN_OPENS, churned, "the churn did not complete its opens");
        Assert.IsTrue(reads > 0, "no reader completed a read during the churn");
    }

    /// <summary>
    /// Closing a file on one path must not disturb a reader on a DIFFERENT path.
    /// </summary>
    /// <remarks>
    /// The reader counts and closes per path, and deliberately does not serialize readers of
    /// different files against each other - they share one <c>ISampleDataApi</c> concurrently,
    /// as cpp's readers have for years. That leaves one hazard the same-path scenarios above do
    /// not reach: a real <c>CloseFile</c> on path A landing inside a read on path B. The churn
    /// here is the ONLY reader on its path, so unlike the same-path churn every one of its
    /// disposes does reach CloseFile - which is what makes this a test of the cross-path axis
    /// rather than of two readers that merely coexist.
    /// </remarks>
    private static void CloseOnOnePathDoesNotDisturbAnother(string churnPath, string readPath)
    {
        Baseline baseline;
        using (var solo = AbstractWiffFile.Open(readPath))
            baseline = ReadBaseline(solo);

        var failures = new ConcurrentQueue<string>();
        using var readerReady = new ManualResetEventSlim();
        using var churnDone = new ManualResetEventSlim();
        int churned = 0, reads = 0;

        // Hold a reader open on one path and keep reading through it until the churn is done
        void Read()
        {
            try
            {
                using var r = AbstractWiffFile.Open(readPath);
                var exp = r.GetExperiment(0);
                // Fill this reader's caches before releasing the churn, so what the churn races
                // is the spectrum, which re-enters the SDK on every call
                if (DescribeReaderFailure(exp, baseline) != null)
                {
                    failures.Enqueue("reader disagreed with a lone reader before the churn started");
                    return;
                }
                readerReady.Set();
                while (!churnDone.IsSet)
                {
                    var failure = DescribeReaderFailure(exp, baseline);
                    if (failure != null)
                    {
                        failures.Enqueue("reader: " + failure);
                        return;
                    }
                    Interlocked.Increment(ref reads);
                }
            }
            catch (Exception x)
            {
                failures.Enqueue("reader: " + Describe(x));
            }
            finally
            {
                // Never leave the churn waiting on a reader that failed during its open
                readerReady.Set();
            }
        }

        // Open and close readers on the OTHER path, so CloseFile lands at arbitrary points
        // inside the long-lived reader's SDK calls
        void Churn()
        {
            try
            {
                readerReady.Wait();
                for (int i = 0; i < CHURN_OPENS; i++)
                {
                    using (var r = AbstractWiffFile.Open(churnPath))
                    {
                        if (r.GetExperiment(0).CycleCount <= 0)
                        {
                            failures.Enqueue("churn: read no cycles");
                            return;
                        }
                    }
                    Interlocked.Increment(ref churned);
                }
            }
            catch (Exception x)
            {
                failures.Enqueue("churn: " + Describe(x));
            }
            finally
            {
                churnDone.Set();
            }
        }

        Task.WaitAll(
            Task.Factory.StartNew(Read, TaskCreationOptions.LongRunning),
            Task.Factory.StartNew(Churn, TaskCreationOptions.LongRunning));

        Assert.AreEqual(0, failures.Count,
            "closing one .wiff2 disturbed a reader on another: " + string.Join(" | ", failures));
        Assert.AreEqual(CHURN_OPENS, churned, "the churn did not complete its opens");
        Assert.IsTrue(reads > 0, "the reader completed no reads while the other path churned");
    }

    /// <summary>
    /// What a lone reader yields on the fixture. The checks compare against this rather than
    /// against "not empty", which a reader that swallowed an SDK error into empty data would
    /// also satisfy if the fixture happened to be empty.
    /// </summary>
    private sealed record Baseline(int CycleCount, double[] Tic, double[] Cycle1Mz);

    private static Baseline ReadBaseline(AbstractWiffFile reader)
    {
        var exp = reader.GetExperiment(0);
        var baseline = new Baseline(exp.CycleCount, exp.GetTic().Intensities,
            exp.GetSpectrum(1, addZeros: false, centroid: false)?.XValues ?? Array.Empty<double>());
        Assert.IsTrue(baseline.CycleCount > 0, "fixture should have cycles");
        Assert.IsTrue(baseline.Tic.Length > 0, "fixture should have a TIC");
        Assert.IsTrue(baseline.Cycle1Mz.Length > 0, "fixture should have a first spectrum");
        return baseline;
    }

    /// <summary>
    /// Null when the experiment still reads exactly what a lone reader read; otherwise what
    /// differs. Only the spectrum re-enters the SDK on every call - the reader memoizes the
    /// cycle count and the TIC after their first read - so it is the spectrum that observes
    /// a purge, and the other two guard against a reader that started out wrong.
    /// </summary>
    private static string? DescribeReaderFailure(AbstractWiffExperiment exp, Baseline baseline)
    {
        if (exp.CycleCount != baseline.CycleCount)
            return $"cycle count {exp.CycleCount} != {baseline.CycleCount}";
        var tic = exp.GetTic().Intensities;
        if (!tic.SequenceEqual(baseline.Tic))
            return $"TIC differs ({tic.Length} points vs {baseline.Tic.Length})";
        var spectrum = exp.GetSpectrum(1, addZeros: false, centroid: false);
        if (spectrum is null)
            return "null spectrum";
        if (!spectrum.XValues.SequenceEqual(baseline.Cycle1Mz))
            return $"spectrum differs ({spectrum.XValues.Length} points vs {baseline.Cycle1Mz.Length})";
        return null;
    }

    private static string Describe(Exception x)
    {
        // AbstractWiffFile.Open constructs the reader through Activator.CreateInstance, which
        // wraps a constructor failure in TargetInvocationException with a fixed message
        var inner = x.GetBaseException();
        return inner.GetType().Name + ": " + inner.Message;
    }

    private static string RequireFixture(string fileName)
    {
        // Same lookup as ReaderSciexTests.SetUp: the pwiz-sharp Reference/ override first,
        // then the cpp tree's vendor test data
        string overridePath = Path.Combine(AppContext.BaseDirectory, "Reference", fileName);
        if (File.Exists(overridePath))
            return overridePath;
        string cppPath = PwizSharpPaths.CppVendorTestData("ABI", fileName);
        if (File.Exists(cppPath))
            return cppPath;
        Assert.Inconclusive($"{fileName} not present under Reference/ or {Path.GetDirectoryName(cppPath)}");
        return cppPath;    // Unreachable: Assert.Inconclusive throws
    }
}
