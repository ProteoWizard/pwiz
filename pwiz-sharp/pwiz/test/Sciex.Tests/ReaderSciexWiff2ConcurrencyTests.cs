using System.Collections.Concurrent;
using Pwiz.TestHarness;

namespace Pwiz.Vendor.Sciex.Tests;

/// <summary>
/// Several readers open on ONE <c>.wiff2</c> at the same time must not break each other.
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
/// <para>Measured 2026-09-15 on <c>swath.api.wiff2</c>: on current code, where each reader
/// owns its api, both tests pass trivially. On a shared api with NO ownership arbitration the
/// churn test fails 8 of 8 runs (<c>SQLiteException: bad parameter or other API misuse</c> or
/// <c>ObjectDisposedException</c> from the readers), while the dispose test still passes - the
/// SDK re-creates a purged storage location on the next request, so only a request in flight
/// at the moment of the purge can see it. On a shared api with per-path reference counting
/// (only the last reader on a path closes the file) both pass, 8 of 8. The churn test is
/// therefore the acceptance test for any retry of that fix.</para>
/// <para>Two details of the churn test keep its failure rate on the bare shared api near
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
    // Hang guard only: the SDK requests take no cancellation token, so a block inside the
    // SDK under contention would otherwise wedge the test process with no result.
    private const int TEST_TIMEOUT_MS = 60_000;
    // ~5 s on a shared api and ~8 s on current code, where every open creates its own api
    private const int CHURN_OPENS = 150;
    private const int READER_COUNT = 3;

    [TestMethod, Timeout(TEST_TIMEOUT_MS)]
    public void Reader_Sciex_wiff2_SecondReaderSurvivesFirstReaderDispose()
    {
        string wiff2 = RequireFixture(FIXTURE);

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

    [TestMethod, Timeout(TEST_TIMEOUT_MS)]
    public void Reader_Sciex_wiff2_ConcurrentReadersSurviveChurnOnSamePath()
    {
        string wiff2 = RequireFixture(FIXTURE);

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
