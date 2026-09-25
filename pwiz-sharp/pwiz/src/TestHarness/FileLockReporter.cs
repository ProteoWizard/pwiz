using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Pwiz.TestHarness;

/// <summary>
/// Names the processes holding a file or directory open, via the Windows Restart Manager.
/// </summary>
/// <remarks>
/// <para>The rename probe in <c>VendorReaderTestHarness.AssertFilesUnlocked</c> fails with
/// "The process cannot access the file because it is being used by another process", which
/// never says <em>which</em> process. That failure has only ever been seen on CI agents and
/// has not reproduced locally in ~200 isolated runs or 26 pinned whole-suite runs, so the
/// holder has to be identified where it happens rather than by local repro.</para>
/// <para>The answer partitions the problem three ways, which is why the report distinguishes
/// them explicitly:</para>
/// <list type="bullet">
///   <item>the probing process itself — a handle we failed to release (the regression the
///   probe exists to catch);</item>
///   <item>a sibling test host — another suite touching the same shared fixture under
///   <c>pwiz/data/vendor_readers/</c>, which is a test-isolation bug, not a lifetime bug;</item>
///   <item>anything else — a virus scanner, the search indexer or a backup agent, in which
///   case the probe is reporting the environment rather than a defect in our code.</item>
/// </list>
/// <para>Diagnostics must never change a test's outcome, so every call is best-effort: this
/// returns a note instead of throwing, and returns an empty-handed report rather than
/// propagating a Restart Manager failure. On non-Windows it is a no-op.</para>
/// <para>Known blind spot: the Restart Manager enumerates open file handles only. A process
/// whose current directory is the probed directory denies the rename without holding any
/// handle, and is reported as "no process holding this path" — verified against a cwd-only
/// holder. Reading another process's current directory means reading its PEB, which is too
/// fragile to belong in a diagnostic, so the empty result names that possibility instead of
/// implying the environment is at fault.</para>
/// </remarks>
public static class FileLockReporter
{
    private const int RmRebootReasonNone = 0;
    private const int CCH_RM_SESSION_KEY = 32;
    private const int CCH_RM_MAX_APP_NAME = 255;
    private const int CCH_RM_MAX_SVC_NAME = 63;
    private const int ERROR_SUCCESS = 0;
    private const int ERROR_MORE_DATA = 234;

    /// <summary>Upper bound on files registered for a directory probe. Vendor ".d" / ".raw"
    /// directories can hold thousands of files; the Restart Manager only needs enough of them
    /// to find a holder, and registering everything makes the failure path slow.</summary>
    private const int MaxRegisteredFiles = 64;

    [StructLayout(LayoutKind.Sequential)]
    private struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    private enum RM_APP_TYPE
    {
        RmUnknownApp = 0,
        RmMainWindow = 1,
        RmOtherWindow = 2,
        RmService = 3,
        RmExplorer = 4,
        RmConsole = 5,
        RmCritical = 1000,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
        public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
        public string strServiceShortName;
        public RM_APP_TYPE ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags,
        char[] strSessionKey);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(uint pSessionHandle, uint nFiles,
        string[] rgsFilenames, uint nApplications, RM_UNIQUE_PROCESS[]? rgApplications,
        uint nServices, string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded,
        ref uint pnProcInfo, [In, Out] RM_PROCESS_INFO[]? rgAffectedApps,
        ref uint lpdwRebootReasons);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint pSessionHandle);

    /// <summary>
    /// Returns an indented, multi-line description of every process holding
    /// <paramref name="path"/> (or, for a directory, anything beneath it) open. Returns a
    /// single explanatory line when the platform has no Restart Manager, when the query
    /// fails, or when nothing holds the path.
    /// </summary>
    public static string Describe(string path)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return "  lock holders: not available on this platform (Windows Restart Manager only).";
            return DescribeWindows(path);
        }
        catch (Exception ex)
        {
            // A diagnostic that throws would replace the real failure with its own.
            return $"  lock holders: query failed ({ex.GetType().Name}: {ex.Message}).";
        }
    }

    /// <summary>Outcome of <see cref="SelfHoldsPath"/>.</summary>
    public enum HandleCheck
    {
        /// <summary>This process holds a handle on the path — a leak in our own dispose chain.</summary>
        SelfHolds,
        /// <summary>This process holds no handle on the path. Other processes may.</summary>
        SelfDoesNotHold,
        /// <summary>No implementation for this platform; the caller must not read this as a pass.</summary>
        Unsupported,
    }

    /// <summary>
    /// Answers whether THIS process holds a handle on <paramref name="path"/> (or, for a
    /// directory, anything beneath it).
    /// </summary>
    /// <remarks>
    /// <para>This is the question the vendor rename probe was really asking. Renaming was a
    /// proxy for it, and a poor one: the OS denies a rename for a handle held by ANY process,
    /// so a sibling test suite reading the same shared fixture failed the probe with no defect
    /// in our code (observed on CI: msconvert, spawned by Installer.Tests, holding
    /// FT-HCD-MSX.raw). Asking only about our own handles cannot be perturbed that way.</para>
    /// <para>The two platform implementations are asymmetric but converge on the same
    /// predicate. Windows asks the Restart Manager who holds the path and keeps only our own
    /// pid. Linux reads <c>/proc/self/fd</c>, which is self-scoped by construction. Linux also
    /// gains real coverage here for the first time: POSIX allows renaming an open file, and a
    /// directory containing one, so the rename probe could never fail there.</para>
    /// <para>Accepted limitation on Windows: the Restart Manager sees open file handles only,
    /// so a self-held CURRENT DIRECTORY on the path is not detected. That is a false negative,
    /// which is the safer direction than the false positives the rename produced.</para>
    /// </remarks>
    public static HandleCheck SelfHoldsPath(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return TryGetWindowsHolderPids(path, out var pids)
                    ? (pids.Contains(Environment.ProcessId) ? HandleCheck.SelfHolds : HandleCheck.SelfDoesNotHold)
                    : HandleCheck.Unsupported;
            if (OperatingSystem.IsLinux())
                return LinuxSelfHoldsPath(path) ? HandleCheck.SelfHolds : HandleCheck.SelfDoesNotHold;
            return HandleCheck.Unsupported;
        }
        catch (Exception)
        {
            // Never let the check itself decide a test's fate.
            return HandleCheck.Unsupported;
        }
    }

    /// <summary>
    /// Enumerates <c>/proc/self/fd</c>, whose entries are symlinks to the paths this process
    /// has open. Four shapes have to be handled, all of them observed: non-file descriptors
    /// (<c>pipe:[…]</c>, <c>socket:[…]</c>) which are not absolute paths; a deleted target,
    /// rendered <c>/path/to/f (deleted)</c>, which still represents a held handle; entries that
    /// disappear mid-walk as descriptors close; and a directory probe, where a handle on any
    /// child counts.
    /// <para>Gap: a memory mapping does not require the descriptor to stay open, so a leak that
    /// only holds an <c>mmap</c> is invisible here. <c>/proc/self/maps</c> would cover it; no
    /// vendor reader is known to need that yet.</para>
    /// </summary>
    private static bool LinuxSelfHoldsPath(string path)
    {
        const string deletedSuffix = " (deleted)";
        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        bool probeIsDirectory = Directory.Exists(path);

        foreach (string entry in Directory.EnumerateFileSystemEntries("/proc/self/fd"))
        {
            string? link;
            try
            {
                link = new FileInfo(entry).LinkTarget ?? new DirectoryInfo(entry).LinkTarget;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue; // descriptor closed while we walked, or is not inspectable
            }
            if (string.IsNullOrEmpty(link)) continue;

            if (link.EndsWith(deletedSuffix, StringComparison.Ordinal))
                link = link.Substring(0, link.Length - deletedSuffix.Length);
            if (!link.StartsWith('/')) continue; // pipe:[…], socket:[…], anon_inode:…

            link = Path.TrimEndingDirectorySeparator(link);
            if (string.Equals(link, target, StringComparison.Ordinal)) return true;
            if (probeIsDirectory && link.StartsWith(target + '/', StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>Runs the Restart Manager query and returns the holding pids.</summary>
    private static bool TryGetWindowsHolderPids(string path, out List<int> pids)
    {
        pids = new List<int>();
        string[] resources = CollectResources(path);
        if (resources.Length == 0) return true; // nothing to hold

        var key = new char[CCH_RM_SESSION_KEY + 1];
        if (RmStartSession(out uint session, 0, key) != ERROR_SUCCESS) return false;
        try
        {
            if (RmRegisterResources(session, (uint)resources.Length, resources, 0, null, 0, null) != ERROR_SUCCESS)
                return false;

            uint procInfo = 0;
            uint rebootReasons = RmRebootReasonNone;
            int rc = RmGetList(session, out uint needed, ref procInfo, null, ref rebootReasons);
            if (rc == ERROR_SUCCESS && needed == 0) return true; // no holders
            if (rc != ERROR_MORE_DATA) return false;

            var infos = new RM_PROCESS_INFO[needed];
            procInfo = needed;
            if (RmGetList(session, out needed, ref procInfo, infos, ref rebootReasons) != ERROR_SUCCESS)
                return false;
            for (int i = 0; i < procInfo; i++)
                pids.Add(infos[i].Process.dwProcessId);
            return true;
        }
        finally
        {
            int endRc = RmEndSession(session);
            if (endRc != ERROR_SUCCESS)
                Debug.WriteLine($"FileLockReporter: RmEndSession failed (rc={endRc}).");
        }
    }

    private static string DescribeWindows(string path)
    {
        string[] resources = CollectResources(path);
        if (resources.Length == 0)
            return "  lock holders: nothing to query (path no longer exists).";

        var key = new char[CCH_RM_SESSION_KEY + 1];
        int rc = RmStartSession(out uint session, 0, key);
        if (rc != ERROR_SUCCESS)
            return $"  lock holders: RmStartSession failed (rc={rc}).";

        try
        {
            rc = RmRegisterResources(session, (uint)resources.Length, resources, 0, null, 0, null);
            if (rc != ERROR_SUCCESS)
                return $"  lock holders: RmRegisterResources failed (rc={rc}).";

            uint procInfo = 0;
            uint rebootReasons = RmRebootReasonNone;
            rc = RmGetList(session, out uint needed, ref procInfo, null, ref rebootReasons);
            if (rc == ERROR_SUCCESS && needed == 0)
                return "  lock holders: Restart Manager reports NO process holding this path. It " +
                       "only sees open file handles, so the blocker is one of the things it cannot " +
                       "see: a process whose CURRENT DIRECTORY is this directory, or a kernel-mode " +
                       "filter such as an on-access virus scanner, search indexer or backup agent.";
            if (rc != ERROR_MORE_DATA)
                return $"  lock holders: RmGetList sizing call failed (rc={rc}, needed={needed}).";

            var infos = new RM_PROCESS_INFO[needed];
            procInfo = needed;
            rc = RmGetList(session, out needed, ref procInfo, infos, ref rebootReasons);
            if (rc != ERROR_SUCCESS)
                return $"  lock holders: RmGetList failed (rc={rc}).";

            var sb = new StringBuilder();
            sb.Append("  lock holders (Restart Manager), ")
              .Append(procInfo)
              .Append(procInfo == 1 ? " process" : " processes")
              .Append("; this process is pid ")
              .Append(Environment.ProcessId)
              .AppendLine(":");
            for (int i = 0; i < procInfo; i++)
                sb.AppendLine(DescribeHolder(infos[i]));
            sb.Append("  registered ").Append(resources.Length)
              .Append(resources.Length == 1 ? " resource: " : " resources, first: ")
              .Append(resources[0]);
            return sb.ToString();
        }
        finally
        {
            int endRc = RmEndSession(session);
            if (endRc != ERROR_SUCCESS)
                Debug.WriteLine($"FileLockReporter: RmEndSession failed (rc={endRc}).");
        }
    }

    private static string DescribeHolder(RM_PROCESS_INFO info)
    {
        int pid = info.Process.dwProcessId;
        bool isSelf = pid == Environment.ProcessId;

        string name = info.strAppName;
        string extra = string.Empty;
        string start = string.Empty;
        try
        {
            using var p = Process.GetProcessById(pid);
            name = p.ProcessName;
            start = $", started {p.StartTime:HH:mm:ss}";
            if (!isSelf) extra = DescribeForeignProcess(p);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                       or System.ComponentModel.Win32Exception)
        {
            // Process exited between the RM query and here, or is not inspectable.
        }

        // The first CI failure this reporter explained was msconvert, spawned by
        // Installer.Tests converting the same shared fixture. Calling that "foreign,
        // environmental" told the reader to dismiss the actual cause, so our own tools get
        // their own verdict.
        string verdict = isSelf
            ? "THIS TEST PROCESS - a handle we did not release"
            : IsTestHost(name)
                ? "a SIBLING test host - another suite sharing this fixture"
                : IsOurTool(name)
                    ? "OUR OWN TOOL, spawned by a sibling suite sharing this fixture"
                    : "a FOREIGN process - environmental, not a defect in our code";

        return $"    pid {pid} {name}{start} [{info.ApplicationType}] -> {verdict}{extra}";
    }

    /// <summary>
    /// Best-effort identification of which suite a sibling test host is running, by looking
    /// for a loaded <c>*.Tests.dll</c>. Reading another process's module list needs rights we
    /// usually have for same-user processes but not always, so failure is silent.
    /// </summary>
    private static string DescribeForeignProcess(Process p)
    {
        try
        {
            foreach (ProcessModule module in p.Modules)
            {
                string moduleName = module.ModuleName ?? string.Empty;
                if (moduleName.EndsWith(".Tests.dll", StringComparison.OrdinalIgnoreCase))
                    return $" (running {moduleName})";
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                       or System.ComponentModel.Win32Exception
                                       or NotSupportedException)
        {
        }
        return string.Empty;
    }

    /// <summary>Tools this repository builds and tests spawn against vendor fixtures.</summary>
    private static readonly string[] OurTools =
        { "msconvert", "blibbuild", "blibfilter", "blibtoms2", "msbenchmark", "bullseyesharp", "msdiff" };

    private static bool IsOurTool(string processName) =>
        Array.Exists(OurTools, t => processName.StartsWith(t, StringComparison.OrdinalIgnoreCase));

    private static bool IsTestHost(string processName) =>
        processName.StartsWith("testhost", StringComparison.OrdinalIgnoreCase)
        || processName.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
        || processName.StartsWith("vstest", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The Restart Manager registers files, not directories. A vendor source can be either, so
    /// for a directory we register the files beneath it — that is what a stale handle would
    /// actually be on, and a handle on any child is enough to deny the parent's rename.
    /// </summary>
    private static string[] CollectResources(string path)
    {
        if (File.Exists(path)) return new[] { path };
        if (!Directory.Exists(path)) return Array.Empty<string>();

        var files = new List<string>(MaxRegisteredFiles);
        try
        {
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                files.Add(file);
                if (files.Count >= MaxRegisteredFiles) break;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        // An empty directory can still be held open (a process with it as its current
        // directory), and RM accepts the path even though it indexes files.
        return files.Count > 0 ? files.ToArray() : new[] { path };
    }
}
