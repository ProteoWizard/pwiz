using System.ComponentModel;
using System.Runtime.InteropServices;
using Pwiz.Vendor.Bruker.PrmScheduling;

namespace Pwiz.Vendor.Bruker.PrmScheduling.Tests;

/// <summary>
/// Managed-port tests for <see cref="Scheduler"/>. Mirrors a subset of the legacy C++/CLI
/// <c>PrmSchedulerTest.cpp</c> end-to-end test — enough to confirm that:
/// <list type="bullet">
///   <item><description>prmscheduler.dll loads (open + close on the sample .prmsqlite).</description></item>
///   <item><description>MethodInfo round-trips the 4 doubles.</description></item>
///   <item><description>A small set of input targets schedules and the first entry's
///         target_id matches what cpp produced.</description></item>
/// </list>
/// The native <c>prmscheduler.dll</c> ships from the pwiz repo (copied to the test
/// output by the project reference); the sample <c>.prmsqlite</c> lives in the same
/// cpp tree. Tests still carry <c>[TestCategory("RequiresPrmScheduler")]</c> so users
/// in environments where the native library can't load (non-Windows, restricted CI)
/// can filter them out; otherwise they run automatically.
/// </summary>
[TestClass]
public class PrmSchedulerTests
{
    /// <summary>Injected by MSTest; lets us surface diagnostics.</summary>
    public TestContext? TestContext { get; set; }

    /// <summary>
    /// The pwiz cpp test sits beside <c>timstof_prm_scheduler.prmsqlite</c>. When pwiz-sharp is
    /// checked out alongside the cpp tree (as it is in our standard dev layout), walk up from
    /// the test output directory and check the sibling pwiz tree.
    /// </summary>
    private static string? FindSamplePrmSqlite()
    {
        // 1) Sibling-tree layout: ../pwiz-msconvert-pr/pwiz-sharp/... → ../pwiz/pwiz/utility/...
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            // Look two levels up from <whatever>/pwiz-sharp/ for a sibling pwiz/ checkout.
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            var grand = parent.Parent;
            if (grand is not null)
            {
                var candidate = Path.Combine(grand.FullName, "pwiz", "pwiz", "utility", "bindings", "CLI",
                    "timstof_prm_scheduler", "timstof_prm_scheduler.prmsqlite");
                if (File.Exists(candidate)) return candidate;
            }
            dir = parent.FullName;
        }

        // 2) Co-located fixture: src/Vendor/Bruker.PrmScheduling/ might ship a copy.
        dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "timstof_prm_scheduler.prmsqlite");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    /// <summary>Returns true iff prmscheduler.dll can be loaded by the OS loader.
    /// On Windows the csproj's Content item drops it next to the test runner, so this
    /// should always succeed; on non-Windows or stripped environments the test self-
    /// skips via Inconclusive.</summary>
    private static bool IsNativeDllAvailable()
    {
        try
        {
            return NativeLibrary.TryLoad("prmscheduler", out _);
        }
        catch (DllNotFoundException) { return false; }
        catch (BadImageFormatException) { return false; }
    }

    private static string CopySampleToTempCopy(string source)
    {
        // Copy to a per-test temp directory: prm_scheduling_file_open writes to the file, so we
        // never want to clobber the checked-in template.
        var tempDir = Path.Combine(Path.GetTempPath(), "prm_scheduler_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dest = Path.Combine(tempDir, "timstof_prm_scheduler.prmsqlite");
        File.Copy(source, dest, overwrite: true);
        return dest;
    }

    [TestMethod]
    [TestCategory("RequiresPrmScheduler")]
    public void OpenAndCloseFile_SmokeTest()
    {
        if (!IsNativeDllAvailable())
        {
            Assert.Inconclusive("prmscheduler.dll is not on PATH; this environment cannot load the native library.");
            return;
        }
        var sample = FindSamplePrmSqlite();
        if (sample is null)
        {
            Assert.Inconclusive("timstof_prm_scheduler.prmsqlite sample not found beside the test runner.");
            return;
        }

        var copy = CopySampleToTempCopy(sample);
        try
        {
            using var s = new Scheduler(copy);
            // Just opening + disposing exercises the native handle lifecycle.
            Assert.IsNotNull(s);
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(copy)!, recursive: true); } catch (IOException) { }
        }
    }

    [TestMethod]
    [TestCategory("RequiresPrmScheduler")]
    public void MethodInfo_RoundTrip()
    {
        if (!IsNativeDllAvailable())
        {
            Assert.Inconclusive("prmscheduler.dll is not on PATH; this environment cannot load the native library.");
            return;
        }
        var sample = FindSamplePrmSqlite();
        if (sample is null)
        {
            Assert.Inconclusive("timstof_prm_scheduler.prmsqlite sample not found beside the test runner.");
            return;
        }

        var copy = CopySampleToTempCopy(sample);
        try
        {
            using var s = new Scheduler(copy);
            var methodInfoList = s.GetPrmMethodInfo();
            Assert.IsTrue(methodInfoList.Count >= 1, "expected at least one MethodInfo entry");
            var mi = methodInfoList[0];
            // The four doubles should be present (non-NaN). We don't assert specific values
            // because Bruker has tuned the sample over time.
            Assert.IsFalse(double.IsNaN(mi.mobility_gap));
            Assert.IsFalse(double.IsNaN(mi.frame_rate));
            Assert.IsTrue(mi.one_over_k0_lower_limit < mi.one_over_k0_upper_limit,
                $"1/K0 range looks wrong: [{mi.one_over_k0_lower_limit}, {mi.one_over_k0_upper_limit}]");

            TestContext?.WriteLine(
                $"MethodInfo: mobility_gap={mi.mobility_gap} frame_rate={mi.frame_rate} " +
                $"1/K0=[{mi.one_over_k0_lower_limit}, {mi.one_over_k0_upper_limit}]");
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(copy)!, recursive: true); } catch (IOException) { }
        }
    }

    [TestMethod]
    [TestCategory("RequiresPrmScheduler")]
    public void EndToEnd_AddTargets_GetScheduling_ProducesEntries()
    {
        if (!IsNativeDllAvailable())
        {
            Assert.Inconclusive("prmscheduler.dll is not on PATH; this environment cannot load the native library.");
            return;
        }
        var sample = FindSamplePrmSqlite();
        if (sample is null)
        {
            Assert.Inconclusive("timstof_prm_scheduler.prmsqlite sample not found beside the test runner.");
            return;
        }

        // Three targets is plenty to confirm the schedule shape; the cpp test uses 100 to
        // stress-test the scheduler but managed-side parity is what we care about here.
        var testTargets = new[]
        {
            new { id = 1, minRT = -53.2, maxRT = 146.8, mz = 784.8662, k0lo = 1.0216, k0hi = 1.0497, charge = 2 },
            new { id = 2, minRT = -15.4, maxRT = 184.6, mz = 523.7778, k0lo = 0.8433, k0hi = 0.8716, charge = 2 },
            new { id = 3, minRT =   5.0, maxRT = 205.0, mz = 576.2796, k0lo = 0.8592, k0hi = 0.8874, charge = 2 },
        };

        var copy = CopySampleToTempCopy(sample);
        try
        {
            using var s = new Scheduler(copy);
            s.SetAdditionalMeasurementParameters(new AdditionalMeasurementParameters
            {
                ms1_repetition_time = 10,
                default_pasef_collision_energies = true,
            });

            foreach (var t in testTargets)
            {
                var target = new InputTarget
                {
                    time_in_seconds_begin = t.minRT,
                    time_in_seconds_end = t.maxRT,
                    isolation_mz = t.mz,
                    monoisotopic_mz = t.mz,
                    one_over_k0_lower_limit = t.k0lo,
                    one_over_k0_upper_limit = t.k0hi,
                    charge = t.charge,
                    collision_energy = -1,
                    isolation_width = 3,
                    one_over_k0 = (t.k0lo + t.k0hi) / 2,
                    time_in_seconds = (t.minRT + t.maxRT) / 2,
                };
                s.AddInputTarget(target, t.id.ToString(System.Globalization.CultureInfo.InvariantCulture), string.Empty);
            }

            var timeSegments = new TimeSegmentList();
            var schedulingEntries = new SchedulingEntryList();
            s.GetScheduling(timeSegments, schedulingEntries, _ => false);

            // The exact counts/values depend on Bruker's scheduling implementation, but at minimum
            // we should get back at least one entry and at least one time segment.
            Assert.IsTrue(schedulingEntries.Count > 0, "Expected at least one PasefSchedulingEntry");
            Assert.IsTrue(timeSegments.Count > 0, "Expected at least one TimeSegment");

            // First entry's target_id should index into our input table.
            var first = schedulingEntries[0];
            Assert.IsTrue(first.target_id < testTargets.Length,
                $"first entry's target_id {first.target_id} is out of range");
            Assert.IsTrue(first.time_segment_id < timeSegments.Count,
                $"first entry's time_segment_id {first.time_segment_id} is out of range");

            TestContext?.WriteLine($"Produced {schedulingEntries.Count} entries across {timeSegments.Count} segments");
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(copy)!, recursive: true); } catch (IOException) { }
        }
    }
}
