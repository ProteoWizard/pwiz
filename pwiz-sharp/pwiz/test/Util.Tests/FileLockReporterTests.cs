using Pwiz.TestHarness;

namespace Pwiz.Util.Tests;

/// <summary>
/// Controls for <see cref="FileLockReporter"/>, the diagnostic the vendor rename probe uses to
/// name whoever holds a fixture open.
/// </summary>
/// <remarks>
/// The reporter only ever runs on a failure that has never reproduced locally, so nothing else
/// exercises it before it is relied on to explain a CI failure. A reporter that silently names
/// nobody is indistinguishable from one that works, and would send the investigation off in the
/// wrong direction, so these assert both directions: a known holder is found and correctly
/// attributed, and an unheld path reports no holder.
/// </remarks>
[TestClass]
public class FileLockReporterTests
{
    [TestMethod]
    public void Describe_NamesThisProcess_WhenThisProcessHoldsTheFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lockprobe-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(path, "held");
        try
        {
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                string report = FileLockReporter.Describe(path);
                if (!OperatingSystem.IsWindows())
                {
                    StringAssert.Contains(report, "not available on this platform");
                    return;
                }
                StringAssert.Contains(report, $"pid {Environment.ProcessId}", report);
                StringAssert.Contains(report, "THIS TEST PROCESS", report);
            }
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Describe_FindsHolderOfAFileInside_WhenProbingADirectory()
    {
        // The Bruker path: the probe renames a ".d" directory, and a handle on any child is
        // enough to deny that rename. The Restart Manager registers files, not directories,
        // so this is the case where an unwired reporter silently finds nothing.
        string dir = Path.Combine(Path.GetTempPath(), $"lockprobe-{Guid.NewGuid():N}.d");
        Directory.CreateDirectory(dir);
        string inner = Path.Combine(dir, "analysis.baf");
        File.WriteAllText(inner, "held");
        try
        {
            using (File.Open(inner, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                string report = FileLockReporter.Describe(dir);
                if (!OperatingSystem.IsWindows()) return;
                StringAssert.Contains(report, $"pid {Environment.ProcessId}", report);
                StringAssert.Contains(report, "THIS TEST PROCESS", report);
            }
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [TestMethod]
    public void Describe_ReportsNoHolder_WhenNothingHoldsTheFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lockprobe-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(path, "not held");
        try
        {
            string report = FileLockReporter.Describe(path);
            if (!OperatingSystem.IsWindows()) return;
            StringAssert.Contains(report, "NO process holding this path", report);
            // The negative case must not implicate anyone: that is the reading that would send
            // the investigation after a lifetime bug that does not exist.
            Assert.IsFalse(report.Contains("THIS TEST PROCESS", StringComparison.Ordinal), report);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Describe_ReturnsANote_AndDoesNotThrow_ForAMissingPath()
    {
        // A diagnostic that throws replaces the failure it was meant to explain.
        string report = FileLockReporter.Describe(
            Path.Combine(Path.GetTempPath(), $"lockprobe-absent-{Guid.NewGuid():N}"));
        Assert.IsFalse(string.IsNullOrWhiteSpace(report));
    }
}
