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

        string verdict = isSelf
            ? "THIS TEST PROCESS - a handle we did not release"
            : IsTestHost(name)
                ? "a SIBLING test host - shared-fixture isolation problem, not a lifetime bug"
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
