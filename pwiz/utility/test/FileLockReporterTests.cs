using Pwiz.TestHarness;

namespace Pwiz.Util.Tests;

/// <summary>
/// Controls for <see cref="FileLockReporter"/>: the diagnostic that names whoever holds a vendor
/// fixture open, and the <see cref="FileLockReporter.SelfHoldsPath"/> predicate the vendor probe
/// asserts on.
/// </summary>
/// <remarks>
/// Both only run on a failure that has never reproduced locally, so nothing else exercises them
/// before they are relied on. One that silently finds nobody is indistinguishable from one that
/// works, and would let a real leak pass, so these assert both directions: a known holder is
/// found and correctly attributed, and an unheld path reports none.
/// </remarks>
[TestClass]
public class FileLockReporterTests
{
    [TestMethod]
    public void SelfHoldsPath_DetectsAndClearsAHandleThisProcessOpens()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lockprobe-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(path, "held");
        try
        {
            var held = FileLockReporter.HandleCheck.Unsupported;
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                held = FileLockReporter.SelfHoldsPath(path);
            var released = FileLockReporter.SelfHoldsPath(path);

            if (held == FileLockReporter.HandleCheck.Unsupported)
            {
                Assert.AreEqual(FileLockReporter.HandleCheck.Unsupported, released,
                    "an unsupported platform must report Unsupported consistently, never a pass");
                return;
            }
            // FileShare.Read is deliberate: a sharing mode that permits other readers still
            // has to register as a handle WE hold, or a leaked read handle goes unnoticed.
            Assert.AreEqual(FileLockReporter.HandleCheck.SelfHolds, held);
            Assert.AreEqual(FileLockReporter.HandleCheck.SelfDoesNotHold, released);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void SelfHoldsPath_DetectsAHandleOnAFileInsideADirectory()
    {
        // The Bruker ".d" shape: the probe is given the directory, the handle is on a child.
        string dir = Path.Combine(Path.GetTempPath(), $"lockprobe-{Guid.NewGuid():N}.d");
        Directory.CreateDirectory(dir);
        string inner = Path.Combine(dir, "analysis.baf");
        File.WriteAllText(inner, "held");
        try
        {
            using (File.Open(inner, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var held = FileLockReporter.SelfHoldsPath(dir);
                if (held == FileLockReporter.HandleCheck.Unsupported) return;
                Assert.AreEqual(FileLockReporter.HandleCheck.SelfHolds, held);
            }
            if (FileLockReporter.SelfHoldsPath(dir) != FileLockReporter.HandleCheck.Unsupported)
                Assert.AreEqual(FileLockReporter.HandleCheck.SelfDoesNotHold,
                    FileLockReporter.SelfHoldsPath(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // A handle held only by ANOTHER process - the case that motivated replacing the rename
    // probe - is not unit-tested here: making a child process hold a file for a bounded time
    // is not portable enough to be worth it. It is covered where it matters instead, by
    // breaking a vendor reader's dispose chain and confirming the probe fires (and by CI build
    // #500, where msconvert held the fixture and the rename probe wrongly failed).

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
