/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.SystemUtil;

namespace TestRunnerLib
{
    /// <summary>
    /// Assembles the net8 Skyline and test-project build outputs into one directory for tests to
    /// run from.
    /// <para>The old Jam build dropped everything into a single bin. The net8 SDK build gives each
    /// project its own bin\&lt;Config&gt;\net8.0-windows, so nothing sees the others, and the test
    /// runner needs Skyline plus every test assembly side by side.</para>
    /// <para>This is the one implementation of staging. It used to live in a PowerShell script that
    /// callers shelled out to, which put an external process on the critical path of every test
    /// run: its colored warnings arrived as escape codes, its robocopy retried a locked file a
    /// million times at thirty second intervals - indistinguishable from a hang - and reading its
    /// output streams could deadlock. Here, a locked file fails immediately and says which process
    /// holds it.</para>
    /// </summary>
    public class TestStager
    {
        public const string TFM = "net8.0-windows";
        public const string STAGING_ROOT = "staging-net8";

        /// <summary>
        /// TestTutorial and TestPerf are in SkylineTester's test DLL list, so leaving them out
        /// leaves the Tutorials tab and the perf tests empty even after a successful staging run.
        /// </summary>
        public static readonly string[] DEFAULT_PROJECTS =
        {
            "Skyline", "CommonTest", "Test", "TestData", "TestFunctional", "TestConnected",
            "TestRunner", "TestTutorial", "TestPerf"
        };

        private const int COPY_ATTEMPTS = 3;
        private const int COPY_RETRY_MILLIS = 500;
        private const int STAGING_LOCK_SECONDS = 120;

        private readonly Action<string> _log;

        public TestStager(string skylineDir, string configuration, Action<string> log = null)
        {
            SkylineDir = skylineDir;
            Configuration = configuration;
            _log = log ?? (s => { });
            Projects = DEFAULT_PROJECTS;
            StagingDir = Path.Combine(skylineDir, "bin", STAGING_ROOT, configuration);
            DotnetSource = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
            RuntimeMajorMinor = "8.0";
            StageRuntime = true;
        }

        public string SkylineDir { get; }
        public string Configuration { get; }
        public string StagingDir { get; set; }
        public IList<string> Projects { get; set; }
        public bool StageRuntime { get; set; }
        public string DotnetSource { get; set; }
        public string RuntimeMajorMinor { get; set; }

        /// <summary>
        /// Copies every project's output into the staging directory, then the portable runtime.
        /// Throws if a file cannot be written, naming the process holding it when one does.
        /// </summary>
        public void Stage()
        {
            Directory.CreateDirectory(StagingDir);

            // Two stagings into one directory lock each other's files, and each then waits on the
            // other. Serialize instead: whoever is second waits, and says why if it gives up.
            using (var stagingLock = new StagingLock(StagingDir))
            {
                if (!stagingLock.Acquired)
                {
                    throw new IOException(
                        $"Another staging of {StagingDir} did not finish within {STAGING_LOCK_SECONDS} seconds. " +
                        @"Wait for it, or stop whatever started it, and try again.");
                }

                foreach (var project in OrderedProjects())
                {
                    _log($"Staging {project.Name}  ({project.OutputDir})");
                    MergeDirectory(project.OutputDir, StagingDir);
                }

                if (StageRuntime)
                    StagePortableRuntime();
            }

            _log($"Staged net8 tests to: {StagingDir}");
        }

        /// <summary>
        /// The projects to stage, oldest build FIRST so the most recently built one wins.
        /// <para>Order decides correctness, not just speed: the projects merge into one directory,
        /// so the last copy of a SHARED file wins, and every project carries its own copy of
        /// TestRunnerLib, ClrMD and the rest. Oldest build first, most recent build last, so the
        /// winner is the copy that was built most recently.</para>
        /// <para>The key is when the project was BUILT, not whether it is stale against its own
        /// sources - those answer different questions, and this used to ask the wrong one. The
        /// standard build excludes TestPerf and TestTutorial, whose sources rarely change, so both
        /// looked current, sorted last, and overwrote a freshly built TestRunnerLib with the copy
        /// from whenever they were last compiled. TestRunner then failed with MissingMethodException
        /// on a method its own source declares.</para>
        /// </summary>
        private IEnumerable<StagedProject> OrderedProjects()
        {
            var staged = new List<StagedProject>();
            foreach (var name in Projects)
            {
                var outputDir = GetProjectOutput(name);
                if (!Directory.Exists(outputDir))
                {
                    _log($"Skipping {name} - no output at {outputDir} (build it first).");
                    continue;
                }
                staged.Add(new StagedProject(name, outputDir, NewestOutput(outputDir)));
                WarnIfOutputStale(name, outputDir);
            }
            // OrderBy is stable, so projects built in the same second keep their declared order.
            return staged.OrderBy(p => p.Built);
        }

        /// <summary>
        /// When a project was last built, taken from the newest assembly it produced.
        /// <see cref="DateTime.MinValue"/> when it produced none, which sorts it first - a
        /// directory with no assemblies cannot be the authority on a shared one.
        /// </summary>
        private static DateTime NewestOutput(string outputDir)
        {
            return Directory.EnumerateFiles(outputDir, "*.dll", SearchOption.TopDirectoryOnly)
                .Select(File.GetLastWriteTime)
                .DefaultIfEmpty(DateTime.MinValue).Max();
        }

        /// <summary>
        /// Skyline.csproj sits at the Skyline root, so its output is directly under it. The test
        /// projects are each in their own subdirectory.
        /// <para>The platform is part of that path and cannot be assumed. Visual Studio and
        /// Build-Skyline.ps1 build x64, which lands in bin\x64\&lt;Config&gt;\&lt;TFM&gt;; a plain
        /// "dotnet build" with no platform writes bin\&lt;Config&gt;\&lt;TFM&gt;. Assuming either one
        /// stages from whichever build happens to have left output there, so a switch between the
        /// two silently stages binaries hours older than the source.</para>
        /// </summary>
        private string GetProjectOutput(string project)
        {
            var projectDir = Equals(project, "Skyline")
                ? SkylineDir
                : Path.Combine(SkylineDir, project);
            var binDir = Path.Combine(projectDir, "bin");

            var candidates = new[]
            {
                Path.Combine(binDir, "x64", Configuration, TFM),
                Path.Combine(binDir, Configuration, TFM)
            };

            // Newest wins, so a stale layout left over from the other kind of build cannot shadow
            // the one that was just produced.
            var existing = candidates.Where(Directory.Exists)
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .FirstOrDefault();

            // Nothing built yet - hand back the preferred location so the caller's own
            // "was not found" reporting names where it should have been.
            return existing ?? candidates[0];
        }

        /// <summary>
        /// Warns when a project is about to be staged from output older than its own sources.
        /// Staging copies whatever is on disk, so a project that was never rebuilt is staged
        /// silently, and the standard build deliberately excludes TestPerf and TestTutorial while
        /// this stages them by default - which makes that easy to hit.
        /// <para>A warning, not an ordering rule. Which project wins a shared file is decided by
        /// when each was BUILT - see <see cref="OrderedProjects"/> - and a project can be current
        /// against its own sources while carrying shared assemblies months out of date.</para>
        /// </summary>
        private void WarnIfOutputStale(string project, string outputDir)
        {
            var sourceDir = Equals(project, "Skyline") ? SkylineDir : Path.Combine(SkylineDir, project);
            if (!Directory.Exists(sourceDir))
                return;

            // The Skyline directory physically contains the test projects, whose sources are not its
            var siblings = Equals(project, "Skyline")
                ? Projects.Where(p => !Equals(p, "Skyline")).Select(p => Path.Combine(SkylineDir, p)).ToArray()
                : new string[0];

            var newestSource = NewestSourceFile(sourceDir, siblings);
            if (newestSource == null)
                return;

            var newestOutput = Directory.EnumerateFiles(outputDir, "*.dll", SearchOption.TopDirectoryOnly)
                .Select(f => (DateTime?) File.GetLastWriteTime(f))
                .DefaultIfEmpty(null).Max();
            if (newestOutput == null || newestSource.Item2 <= newestOutput.Value)
                return;

            // Seconds, because a build finishes within the minute it started: reported to the
            // minute this said a file changed at 15:23 and the assembly was built at 15:23, and
            // then claimed the second was older than the first.
            _log($"WARNING: {project} output looks stale: {newestSource.Item1} changed " +
                 $"{newestSource.Item2:MM-dd HH:mm:ss} but the newest built assembly is " +
                 $"{newestOutput.Value:MM-dd HH:mm:ss}. Staging it first so freshly built projects " +
                 $"win any shared file - rebuild {project} if that is not intended.");
            return;
        }

        private static Tuple<string, DateTime> NewestSourceFile(string sourceDir, string[] siblings)
        {
            Tuple<string, DateTime> newest = null;
            foreach (var file in Directory.EnumerateFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                if (!IsSourceFile(file) || IsBuildOutput(file))
                    continue;
                if (siblings.Any(s => file.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
                    continue;
                var written = File.GetLastWriteTime(file);
                if (newest != null && written <= newest.Item2)
                    continue;
                // Checked only for a file that would become the newest, because it reads the file.
                if (IsGeneratedSource(file))
                    continue;
                newest = Tuple.Create(Path.GetFileName(file), written);
            }
            return newest;
        }

        /// <summary>
        /// True for a source file the BUILD itself writes into the source tree.
        /// <para>Protobuf regenerates Test\ProtocolBuffers\GeneratedCode on every build, so the
        /// newest .cs under Test is routinely one the build had just written - which made this
        /// report the output stale the moment the build succeeded, and tell the developer to
        /// rebuild what they had only just rebuilt. A warning that is always wrong is how the
        /// true ones stop being read.</para>
        /// </summary>
        private static bool IsGeneratedSource(string path)
        {
            try
            {
                using (var reader = new StreamReader(path))
                {
                    // The marker is a first-line convention, so this reads one line, not the file.
                    var firstLine = reader.ReadLine();
                    return firstLine != null && firstLine.Contains("<auto-generated");
                }
            }
            catch (IOException)
            {
                // Unreadable is not evidence either way; leave it a candidate.
                return false;
            }
        }

        private static bool IsSourceFile(string path)
        {
            return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBuildOutput(string path)
        {
            return path.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Merges one directory tree into another, replacing any file that DIFFERS from its source
        /// and skipping the rest.
        /// <para>Deliberately not "copy only if newer". Staging once did that, and a NuGet assembly
        /// carrying its package's original timestamp lost to whatever was already staged and
        /// survived every re-stage. The staging directory has to end up matching the build output,
        /// so a file that differs is replaced whatever the timestamps say.</para>
        /// </summary>
        private void MergeDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var sourceFile in Directory.EnumerateFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(sourceFile));
                if (!NeedsCopy(sourceFile, destFile))
                    continue;
                CopyFile(sourceFile, destFile);
            }

            // Recurse for satellite resource directories and the like
            foreach (var sourceSubDir in Directory.EnumerateDirectories(sourceDir))
                MergeDirectory(sourceSubDir, Path.Combine(destDir, Path.GetFileName(sourceSubDir)));
        }

        private static bool NeedsCopy(string sourceFile, string destFile)
        {
            if (!File.Exists(destFile))
                return true;
            var source = new FileInfo(sourceFile);
            var dest = new FileInfo(destFile);
            return source.Length != dest.Length || source.LastWriteTimeUtc != dest.LastWriteTimeUtc;
        }

        /// <summary>
        /// Copies one file, retrying briefly, and naming the process holding it when it cannot.
        /// A locked file is a condition someone has to act on, so it fails in seconds with the
        /// culprit rather than retrying quietly for a very long time.
        /// </summary>
        private static void CopyFile(string sourceFile, string destFile)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.Copy(sourceFile, destFile, true);
                    File.SetLastWriteTimeUtc(destFile, File.GetLastWriteTimeUtc(sourceFile));
                    return;
                }
                catch (Exception x) when (x is IOException || x is UnauthorizedAccessException)
                {
                    if (attempt >= COPY_ATTEMPTS)
                        throw new IOException(DescribeLockedCopy(destFile, x), x);
                    Thread.Sleep(COPY_RETRY_MILLIS);
                }
            }
        }

        private static string DescribeLockedCopy(string destFile, Exception x)
        {
            var message = $"Could not stage {destFile}. {x.Message}";
            try
            {
                var holders = FileLockingProcessFinder.GetProcessesUsingFile(destFile)
                    .Select(p => $"{p.ProcessName} (PID: {p.Id})").ToArray();
                if (holders.Length > 0)
                    message += $" Held by: {string.Join(@", ", holders)}.";
            }
            catch (Exception)
            {
                // Naming the holder is a courtesy; never let it replace the failure it explains
            }
            return message;
        }

        /// <summary>
        /// Stages a portable .NET Desktop runtime under the staging directory so container workers,
        /// which have no .NET installed, can run the net8 apphost via DOTNET_ROOT. The minimal set
        /// is host\fxr plus the two shared frameworks; dotnet.exe comes along so running the DLL
        /// directly also works.
        /// </summary>
        private void StagePortableRuntime()
        {
            var netCore = HighestVersionDir(Path.Combine(DotnetSource, @"shared\Microsoft.NETCore.App"));
            var winDesktop = HighestVersionDir(Path.Combine(DotnetSource, @"shared\Microsoft.WindowsDesktop.App"));
            var fxr = HighestVersionDir(Path.Combine(DotnetSource, @"host\fxr"));
            if (netCore == null || winDesktop == null || fxr == null)
            {
                throw new IOException(
                    $"Could not find a {RuntimeMajorMinor}.x runtime under {DotnetSource} " +
                    @"(NETCore.App/WindowsDesktop.App/host\fxr).");
            }

            _log($"Staging .NET runtime  (NETCore.App {Path.GetFileName(netCore)}, " +
                 $"WindowsDesktop.App {Path.GetFileName(winDesktop)}, fxr {Path.GetFileName(fxr)})");

            var runtimeDest = Path.Combine(StagingDir, "dotnet");
            Directory.CreateDirectory(runtimeDest);

            var dotnetExe = Path.Combine(DotnetSource, "dotnet.exe");
            if (File.Exists(dotnetExe))
                CopyFile(dotnetExe, Path.Combine(runtimeDest, "dotnet.exe"));

            MergeDirectory(fxr, Path.Combine(runtimeDest, @"host\fxr", Path.GetFileName(fxr)));
            MergeDirectory(netCore,
                Path.Combine(runtimeDest, @"shared\Microsoft.NETCore.App", Path.GetFileName(netCore)));
            MergeDirectory(winDesktop,
                Path.Combine(runtimeDest, @"shared\Microsoft.WindowsDesktop.App", Path.GetFileName(winDesktop)));
        }

        private string HighestVersionDir(string parent)
        {
            if (!Directory.Exists(parent))
                return null;
            return Directory.EnumerateDirectories(parent)
                .Where(d => Path.GetFileName(d).StartsWith(RuntimeMajorMinor, StringComparison.Ordinal))
                .OrderBy(d => ParseVersion(Path.GetFileName(d)))
                .LastOrDefault();
        }

        private static Version ParseVersion(string name)
        {
            return Version.TryParse(name, out var version) ? version : new Version(0, 0, 0);
        }

        private class StagedProject
        {
            public StagedProject(string name, string outputDir, DateTime built)
            {
                Name = name;
                OutputDir = outputDir;
                Built = built;
            }

            public string Name { get; }
            public string OutputDir { get; }
            public DateTime Built { get; }
        }

        /// <summary>
        /// A machine-wide lock on one staging directory, so two stagings cannot interleave their
        /// copies into it and block each other on the files they are both writing.
        /// </summary>
        private class StagingLock : IDisposable
        {
            private readonly Mutex _mutex;

            public StagingLock(string stagingDir)
            {
                // A mutex name cannot contain a path separator
                var name = @"Global\SkylineStaging_" + stagingDir.ToLowerInvariant()
                    .Replace('\\', '_').Replace(':', '_').Replace('/', '_');
                _mutex = new Mutex(false, name);
                try
                {
                    Acquired = _mutex.WaitOne(TimeSpan.FromSeconds(STAGING_LOCK_SECONDS));
                }
                catch (AbandonedMutexException)
                {
                    Acquired = true;    // The previous holder died; the directory is ours to fix up
                }
            }

            public bool Acquired { get; }

            public void Dispose()
            {
                if (Acquired)
                    _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }
    }
}
