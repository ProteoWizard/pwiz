/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    public static class DiannHelpers
    {
        // Default install: latest academic version. Non-academic users can opt for 1.9.1
        // (last fully-open-source release) via DiannDownloadDlg.
        public const string DIANN_VERSION = @"2.5.0";
        public static readonly string DIANN_FILENAME = $@"DIANN-{DIANN_VERSION}";
        public static string DiannDirectory => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), DIANN_FILENAME);
        public static string DiannBinary =>
            Settings.Default.SearchToolList.GetToolPathOrDefault(SearchToolType.DIANN, Path.Combine(DiannDirectory, @"diann.exe"));
        public static string DiannArgs =>
            Settings.Default.SearchToolList.GetToolArgsOrDefault(SearchToolType.DIANN, string.Empty);

        // DIA-NN 1.9.1 — last release under a non-restrictive license. Supports the same
        // command-line flags we emit (--cut, --use-quant, --reanalyse, --smart-profiling,
        // --rt-profiling, --gen-spec-lib, --predictor, etc.) so BuildSearchCommandLine /
        // BuildLibraryGenerationCommandLine are unchanged for either version.
        public const string DIANN_191_VERSION = @"1.9.1";
        public static readonly string DIANN_191_FILENAME = $@"DIANN-{DIANN_191_VERSION}";
        public static string Diann191Directory => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), DIANN_191_FILENAME);
        public static readonly Uri DIANN_191_ZIP_URL =
            new Uri($@"https://github.com/vdemichev/DiaNN/releases/download/{DIANN_191_VERSION}/DIA-NN.{DIANN_191_VERSION}.binaries.zip");

        public static readonly Uri DIANN_DOWNLOAD_URL = new Uri(@"https://github.com/vdemichev/DiaNN/releases");

        public static readonly Uri DIANN_MSI_URL =
            new Uri($@"https://github.com/vdemichev/DiaNN/releases/download/2.0/DIA-NN-{DIANN_VERSION}-Academia.msi");

        // GitHub blob view (renders markdown in browser) rather than the releases-download
        // .txt — the latter served a Content-Disposition that triggered a file download
        // instead of letting the user read the license.
        public static readonly Uri DIANN_LICENSE_URL =
            new Uri(@"https://github.com/vdemichev/DiaNN/blob/master/LICENSE.md");

        /// <summary>
        /// Pre-extracted DIA-NN 2.5.0 install zip on the Skyline tool testing S3 mirror.
        /// <see cref="DiannDownloadInfo"/> points <see cref="SimpleFileDownloader.DownloadRequiredFiles"/>
        /// directly at this URL for functional tests. End-user installs go through
        /// <see cref="DIANN_MSI_URL"/> via the download dialog, not this download info.
        /// </summary>
        private static readonly Uri DIANN_TEST_MIRROR_URL =
            new Uri($@"https://ci.skyline.ms/skyline_tool_testing_mirror/{DIANN_FILENAME}.zip");

        public static FileDownloadInfo DiannDownloadInfo => new FileDownloadInfo
        {
            Filename = DIANN_FILENAME,
            // The zip contains a top-level DIANN-<version>/ folder, so extract into the
            // Tools directory (its parent) rather than DiannDirectory.
            InstallPath = ToolDescriptionHelpers.GetToolsDirectory(),
            CheckInstalledPath = DiannBinary,
            DownloadUrl = DIANN_TEST_MIRROR_URL,
            OverwriteExisting = true,
            Unzip = true,
            ToolType = SearchToolType.DIANN,
            ToolPath = DiannBinary,
            ToolExtraArgs = DiannArgs
        };

        /// <summary>
        /// Same download/cache plumbing as <see cref="DiannDownloadInfo"/> but for the
        /// open-license 1.9.1 zip from GitHub. Used by tests that exercise both versions.
        /// The zip's top-level folder is <c>1.9.1/</c> and contains TWO executables:
        /// <c>DIA-NN.exe</c> (594 KB, the GUI loader) and <c>DiaNN.exe</c> (15 MB, the
        /// CLI binary we actually want to drive from Skyline).
        /// </summary>
        public static string Diann191Binary => Path.Combine(Diann191Directory, DIANN_191_VERSION, @"DiaNN.exe");
        public static FileDownloadInfo Diann191DownloadInfo => new FileDownloadInfo
        {
            Filename = DIANN_191_FILENAME,
            InstallPath = Diann191Directory,
            CheckInstalledPath = Diann191Binary,
            DownloadUrl = DIANN_191_ZIP_URL,
            OverwriteExisting = true,
            Unzip = true,
            ToolType = SearchToolType.DIANN,
            ToolPath = Diann191Binary,
            ToolExtraArgs = DiannArgs
        };

        public static FileDownloadInfo[] FilesToDownload => new[] { DiannDownloadInfo };

        /// <summary>
        /// Test-only override for <see cref="TryGetRegisteredDiannPath"/>. When set, the
        /// registry is not consulted and the override's return value is used instead.
        /// </summary>
        public static Func<string> RegisteredDiannPathOverride;

        /// <summary>
        /// Scan the Windows uninstall registry keys for a DIA-NN install and return the path
        /// to its diann.exe if found. Checks HKCU and HKLM (including WOW6432Node), per-user
        /// and per-machine. Returns null if no DIA-NN install is registered or its
        /// <c>InstallLocation</c> does not contain diann.exe.
        /// </summary>
        public static string TryGetRegisteredDiannPath()
        {
            if (RegisteredDiannPathOverride != null)
                return RegisteredDiannPathOverride();
            var roots = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                foreach (var root in roots)
                {
                    using var key = hive.OpenSubKey(root);
                    if (key == null)
                        continue;
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var displayName = subKey?.GetValue(@"DisplayName") as string;
                        if (string.IsNullOrEmpty(displayName) ||
                            !displayName.StartsWith(@"DIA-NN", StringComparison.OrdinalIgnoreCase))
                            continue;
                        var installLocation = subKey.GetValue(@"InstallLocation") as string;
                        if (string.IsNullOrEmpty(installLocation))
                            continue;
                        var candidate = Path.Combine(installLocation, @"diann.exe");
                        if (File.Exists(candidate))
                            return candidate;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Extract the DIA-NN MSI (admin install) into <paramref name="targetDir"/> without
        /// actually installing, then search the extracted tree for diann.exe.
        /// Returns the full path to the extracted diann.exe, or null if not found.
        /// </summary>
        public static string ExtractDiannMsi(string msiPath, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            var psi = new ProcessStartInfo(@"msiexec",
                string.Format(CultureInfo.InvariantCulture, @"/a ""{0}"" /qn TARGETDIR=""{1}""", msiPath, targetDir))
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = Process.Start(psi))
            {
                process?.WaitForExit();
                if (process == null || process.ExitCode != 0)
                    return null;
            }
            // /a leaves a full copy of the original .msi in the target; it's not needed after extraction.
            var msiCopy = Path.Combine(targetDir, Path.GetFileName(msiPath) ?? string.Empty);
            if (File.Exists(msiCopy))
                FileEx.SafeDelete(msiCopy, true);
            return Directory.EnumerateFiles(targetDir, @"diann.exe", SearchOption.AllDirectories).FirstOrDefault();
        }

        /// <summary>
        /// Extract a DIA-NN binaries .zip (used for the non-academic 1.9.1 distribution)
        /// into <paramref name="targetDir"/> and return the path to the extracted diann.exe.
        /// Returns null if no diann.exe is found in the archive.
        /// </summary>
        public static string ExtractDiannZip(string zipPath, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            using (var zip = new Ionic.Zip.ZipFile(zipPath))
            {
                zip.ExtractAll(targetDir, Ionic.Zip.ExtractExistingFileAction.OverwriteSilently);
            }
            // 2.x ships as diann.exe; 1.9.1 ships TWO binaries — DiaNN.exe (CLI, what we
            // want) and DIA-NN.exe (GUI loader, will pop a window — skip it). Match the
            // CLI names only. Note: 1.9.1 doesn't support UTF-8 paths properly, so users
            // who need non-ASCII paths should install DIA-NN 2.x.
            return Directory.EnumerateFiles(targetDir, @"*.exe", SearchOption.AllDirectories)
                .FirstOrDefault(p =>
                {
                    var name = Path.GetFileName(p);
                    return string.Equals(name, @"diann.exe", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, @"DiaNN.exe", StringComparison.OrdinalIgnoreCase);
                });
        }

        // Built-in preset names exposed in the SearchSettingsPresets dropdown.
        public const string PRESET_DEFAULT = @"DIA-NN default";
        public const string PRESET_ORBITRAP = @"DIA-NN Orbitrap (10/20 ppm)";
        public const string PRESET_LIBRARY_FREE_WIDE = @"DIA-NN library-free wide-window";

        /// <summary>
        /// Default DIA-NN presets surfaced by SearchSettingsPresetList alongside the
        /// Comet/MSFragger presets. Engine-specific values go into AdditionalSettings,
        /// the rest (tolerances, q-value, enzyme, etc.) are first-class preset fields.
        /// </summary>
        public static IEnumerable<SearchSettingsPreset> GetDefaultPresets()
        {
            var defaultAdditional = new Dictionary<string, string>
            {
                { @"MinPepLen", @"7" },
                { @"MaxPepLen", @"30" },
                { @"MinPrecursorCharge", @"1" },
                { @"MaxPrecursorCharge", @"4" },
            };
            yield return new SearchSettingsPreset(
                PRESET_DEFAULT,
                SearchEngine.DIANN,
                new MzTolerance(0, MzTolerance.Units.ppm),
                new MzTolerance(0, MzTolerance.Units.ppm),
                maxVariableMods: 2,
                fragmentIons: null,
                ms2Analyzer: null,
                cutoffScore: 0.01,
                additionalSettings: defaultAdditional,
                enzymeName: @"Trypsin",
                maxMissedCleavages: 1,
                workflowType: SearchWorkflowType.dia);

            var orbitrapAdditional = new Dictionary<string, string>(defaultAdditional);
            orbitrapAdditional[@"MinPrecursorCharge"] = @"2";
            orbitrapAdditional[@"MaxPrecursorCharge"] = @"3";
            yield return new SearchSettingsPreset(
                PRESET_ORBITRAP,
                SearchEngine.DIANN,
                new MzTolerance(10, MzTolerance.Units.ppm),
                new MzTolerance(20, MzTolerance.Units.ppm),
                maxVariableMods: 2,
                fragmentIons: null,
                ms2Analyzer: null,
                cutoffScore: 0.01,
                additionalSettings: orbitrapAdditional,
                enzymeName: @"Trypsin",
                maxMissedCleavages: 1,
                workflowType: SearchWorkflowType.dia);

            yield return new SearchSettingsPreset(
                PRESET_LIBRARY_FREE_WIDE,
                SearchEngine.DIANN,
                new MzTolerance(0, MzTolerance.Units.ppm), // auto MS1
                new MzTolerance(0, MzTolerance.Units.ppm), // auto MS2
                maxVariableMods: 2,
                fragmentIons: null,
                ms2Analyzer: null,
                cutoffScore: 0.01,
                additionalSettings: defaultAdditional,
                enzymeName: @"Trypsin",
                maxMissedCleavages: 2,
                workflowType: SearchWorkflowType.dia);
        }

        /// <summary>
        /// Maps Skyline enzyme names to DIA-NN --cut patterns.
        /// </summary>
        private static readonly Dictionary<string, string> ENZYME_MAP = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { @"Trypsin", @"K*,R*,!*P" },
            { @"Trypsin (semi)", @"K*,R*,!*P" },
            { @"Trypsin/P", @"K*,R*" },
            { @"Lys-C", @"K*" },
            { @"Lys-C/P", @"K*" },
            { @"Lys-N", @"*K" },
            { @"Arg-C", @"R*" },
            { @"Asp-N", @"*D" },
            { @"Chymotrypsin", @"F*,W*,Y*,L*" },
            { @"Glu-C", @"E*,D*" },
        };

        /// <summary>
        /// Convert a Skyline enzyme to a DIA-NN --cut argument value.
        /// Returns null if the enzyme has no known mapping (use no --cut arg = non-specific).
        /// </summary>
        public static string GetDiannCutPattern(Enzyme enzyme)
        {
            if (enzyme == null)
                return null;
            return ENZYME_MAP.TryGetValue(enzyme.Name, out var pattern) ? pattern : null;
        }

        /// <summary>
        /// Format a StaticMod as a DIA-NN modification argument value.
        /// Format: UniMod:{id},{mass},{amino_acids}
        /// For N-terminal mods, uses lowercase letters per DIA-NN convention.
        /// </summary>
        public static string FormatModification(StaticMod mod)
        {
            int? unimodId = mod.UnimodId;
            double mass = mod.MonoisotopicMass ?? 0;
            string massStr = mass.ToString(@"F6", CultureInfo.InvariantCulture);

            string aminoAcids;
            if (mod.Terminus == ModTerminus.N)
                aminoAcids = string.IsNullOrEmpty(mod.AAs) ? @"n" : mod.AAs.ToLowerInvariant();
            else if (mod.Terminus == ModTerminus.C)
                aminoAcids = string.IsNullOrEmpty(mod.AAs) ? @"c" : mod.AAs;
            else
                aminoAcids = mod.AAs ?? string.Empty;

            if (unimodId.HasValue)
                return string.Format(@"UniMod:{0},{1},{2}", unimodId.Value, massStr, aminoAcids);

            // Fallback: use mass-only format
            return string.Format(@"{0},{1}", massStr, aminoAcids);
        }

        /// <summary>
        /// Append common DIA-NN arguments (tolerances, enzyme, mods, etc.) to the command-line builder.
        /// </summary>
        private static void AppendCommonArgs(StringBuilder sb, DiannConfig config,
            IEnumerable<StaticMod> fixedMods, IEnumerable<StaticMod> variableMods, Enzyme enzyme)
        {
            // Extra args from search tool configuration
            if (!string.IsNullOrEmpty(DiannArgs))
                sb.Append(DiannArgs).Append(' ');

            // Mass tolerances (0 = auto-detect). DIA-NN 2.x uses --mass-acc-ms1 / --mass-acc;
            // the older 1.x --ms1-accuracy / --ms2-accuracy names are silently ignored.
            if (config.Ms1Accuracy > 0)
                sb.AppendFormat(CultureInfo.InvariantCulture, @"--mass-acc-ms1 {0} ", config.Ms1Accuracy);
            if (config.Ms2Accuracy > 0)
                sb.AppendFormat(CultureInfo.InvariantCulture, @"--mass-acc {0} ", config.Ms2Accuracy);

            // FDR
            sb.AppendFormat(CultureInfo.InvariantCulture, @"--qvalue {0} ", config.QValue);

            // Threading
            sb.AppendFormat(@"--threads {0} ", config.Threads);

            // Peptide length
            sb.AppendFormat(@"--min-pep-len {0} ", config.MinPepLen);
            sb.AppendFormat(@"--max-pep-len {0} ", config.MaxPepLen);

            // Precursor charge
            sb.AppendFormat(@"--min-pr-charge {0} ", config.MinPrCharge);
            sb.AppendFormat(@"--max-pr-charge {0} ", config.MaxPrCharge);

            // Enzyme
            var cutPattern = GetDiannCutPattern(enzyme);
            if (cutPattern != null)
                sb.AppendFormat(@"--cut {0} ", cutPattern);

            // Missed cleavages
            if (config.MaxMissedCleavages >= 0)
                sb.AppendFormat(@"--missed-cleavages {0} ", config.MaxMissedCleavages);

            // N-term methionine excision
            if (config.MetExcision)
                sb.Append(@"--met-excision ");

            // Fixed modifications
            if (fixedMods != null)
            {
                foreach (var mod in fixedMods)
                    sb.AppendFormat(@"--fixed-mod {0} ", FormatModification(mod));
            }

            // Variable modifications
            if (variableMods != null)
            {
                foreach (var mod in variableMods)
                    sb.AppendFormat(@"--var-mod {0} ", FormatModification(mod));
            }
            sb.AppendFormat(@"--var-mods {0} ", config.MaxVarMods);

            // DIA-NN recommends these for library-free / library-generation runs:
            //   --smart-profiling improves the empirical spectral library quality
            //   --rt-profiling improves retention-time modeling
            // (https://github.com/vdemichev/DiaNN README; matches GUI defaults.)
            sb.Append(@"--smart-profiling ");
            sb.Append(@"--rt-profiling ");

            // Skyline builds its own library from the speclib, so skip DIA-NN's protein inference.
            sb.Append(@"--no-prot-inf ");

            // Verbose output for progress tracking
            sb.Append(@"--verbose 1 ");
        }

        /// <summary>
        /// Build command-line for Step 1: Generate a predicted spectral library from FASTA using deep learning.
        /// No raw data files are provided in this step.
        /// </summary>
        public static string BuildLibraryGenerationCommandLine(
            string fastaFilepath,
            string predictedLibPath,
            DiannConfig config,
            IEnumerable<StaticMod> fixedMods = null,
            IEnumerable<StaticMod> variableMods = null,
            Enzyme enzyme = null)
        {
            var sb = new StringBuilder();

            sb.AppendFormat(@"--fasta {0} ", fastaFilepath.Quote());
            sb.Append(@"--fasta-search ");
            sb.AppendFormat(@"--out-lib {0} ", predictedLibPath.Quote());
            sb.Append(@"--predictor ");

            AppendCommonArgs(sb, config, fixedMods, variableMods, enzyme);

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Build command-line for Step 2: Search raw DIA files against the predicted spectral library.
        /// </summary>
        public static string BuildSearchCommandLine(
            IEnumerable<string> dataFiles,
            string fastaFilepath,
            string inputLibPath,
            string outputReportPath,
            string outputLibPath,
            DiannConfig config,
            IEnumerable<StaticMod> fixedMods = null,
            IEnumerable<StaticMod> variableMods = null,
            Enzyme enzyme = null)
        {
            var sb = new StringBuilder();

            // Input files
            int fileCount = 0;
            foreach (var file in dataFiles)
            {
                sb.AppendFormat(@"--f {0} ", file.Quote());
                fileCount++;
            }

            // FASTA (for protein inference) and predicted library
            sb.AppendFormat(@"--fasta {0} ", fastaFilepath.Quote());
            sb.AppendFormat(@"--lib {0} ", inputLibPath.Quote());

            // Output. With BiblioSpec's parquet reader (DiaNNSpecLibReader USE_PARQUET_READER),
            // the report-lib.parquet that --gen-spec-lib emits is consumed directly, so MBR
            // (--reanalyse) is no longer strictly required to extract the library.
            sb.AppendFormat(@"--out {0} ", outputReportPath.Quote());
            sb.AppendFormat(@"--out-lib {0} ", outputLibPath.Quote());
            sb.Append(@"--gen-spec-lib ");

            if (config.ReuseQuantFiles)
            {
                // --use-quant tells DIA-NN to load each `<raw>.quant` file alongside the
                // input and skip the per-file search step. (Note: DIA-NN 2.5.0 silently
                // ignores the similarly-named `--reuse-quant`.) Per DIA-NN docs:
                //   "the second step of MBR is done by running with --use-quant and
                //    --reanalyse, which is much quicker than the search with a predicted
                //    library."
                // i.e. --use-quant alone does NOT trigger MBR — it just skips the first
                // pass. MBR's second pass (empirical-library reanalysis) only runs when
                // --reanalyse is also supplied.
                sb.Append(@"--use-quant ");
            }
            if (fileCount >= 2)
            {
                // --reanalyse activates match-between-runs. Needs 2+ files. Compatible
                // with --use-quant: the cached .quant files satisfy the first pass and
                // --reanalyse does the empirical-library second pass on top.
                sb.Append(@"--reanalyse ");
            }

            AppendCommonArgs(sb, config, fixedMods, variableMods, enzyme);

            return sb.ToString().TrimEnd();
        }

        private static bool IsGoodDiannOutput(string stdOut, int exitCode)
        {
            return exitCode == 0 && !stdOut.Contains(@"Critical error");
        }

        private static void RunDiannProcess(string args, IProgressMonitor progressMonitor, ref IProgressStatus status)
        {
            var pr = new ProcessRunner();
            // DIA-NN's bundled RawWrapper.dll is a .NET assembly; its apphost reads
            // RawWrapper.runtimeconfig.json from diann.exe's directory and fails — calling
            // abort() (surfaces as STATUS_STACK_BUFFER_OVERRUN / FAST_FAIL_FATAL_APP_EXIT
            // before stage-2 .raw load) — if that directory contains non-ASCII characters.
            // TestRunner stages tools under `Tööls_<id>_<lang>`, which trips this. Hand DIA-NN
            // the 8.3 short-name form of its install path. Same fix below for %TMP%, which
            // TestRunner redirects to a path with `~& ^` characters that DIA-NN's intermediate
            // .quant writer can't parse.
            string diannExe = PathEx.GetNonUnicodePath(DiannBinary) ?? DiannBinary;
            var psi = new ProcessStartInfo(diannExe, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            pr.ChangeTmpDirEnvironmentVariableToNonUnicodePath(psi);

            status = status.ChangeMessage(string.Format(
                Resources.EncyclopeDiaHelpers_GenerateLibrary_Running_command___0___1_,
                psi.FileName, psi.Arguments));
            progressMonitor.UpdateProgress(status);

            // Tee DIA-NN's stdout to a flushed file beside the --out artifact so callers
            // (and humans following along) can `tail -f` while DIA-NN runs. DIA-NN's own
            // diann-output.log.txt is only finalised on clean exit, so it's useless for
            // live monitoring. Also wrap the progress monitor so DIA-NN stage markers
            // drive the percent-complete on the IProgressStatus.
            using (var liveLog = TryOpenLiveLogWriter(args))
            {
                var wrappedMonitor = new DiannProgressMonitor(progressMonitor);
                pr.Run(psi, null, wrappedMonitor, ref status, liveLog,
                    ProcessPriorityClass.BelowNormal, true, IsGoodDiannOutput, false);
            }
        }

        /// <summary>
        /// Create an append-mode, auto-flushed log file at `<--out dir>/diann-skyline-stdout.log`
        /// so each DIA-NN stdout line lands on disk immediately. Returns null when no
        /// `--out` argument is present (e.g. the predictor invocation uses `--out-lib`
        /// instead — that case falls back to writing next to that file).
        /// </summary>
        private static TextWriter TryOpenLiveLogWriter(string args)
        {
            try
            {
                // Pull the path argument right after `--out` or `--out-lib` (predictor stage).
                string outPath = ExtractOutputPath(args, @"--out") ?? ExtractOutputPath(args, @"--out-lib");
                if (string.IsNullOrEmpty(outPath))
                    return null;
                string dir = Path.GetDirectoryName(outPath);
                if (string.IsNullOrEmpty(dir))
                    return null;
                Directory.CreateDirectory(dir);
                var stream = new FileStream(Path.Combine(dir, @"diann-skyline-stdout.log"),
                    FileMode.Append, FileAccess.Write, FileShare.Read);
                return new StreamWriter(stream) { AutoFlush = true };
            }
            catch
            {
                return null; // logging is best-effort; never fail the search because of it
            }
        }

        private static string ExtractOutputPath(string args, string flag)
        {
            int idx = args.IndexOf(flag + @" ", StringComparison.Ordinal);
            if (idx < 0) return null;
            int start = idx + flag.Length + 1;
            // Path is quoted by ArgumentExtensions.Quote() in BuildSearchCommandLine.
            if (start >= args.Length || args[start] != '"') return null;
            int end = args.IndexOf('"', start + 1);
            if (end < 0) return null;
            return args.Substring(start + 1, end - start - 1);
        }

        /// <summary>
        /// Maps DIA-NN's free-text stage markers ("First pass", "File N/M",
        /// "Cross-run analysis", "Quantifying proteins", "report saved", ...) onto a
        /// monotonic 0..100 percent-complete. Wraps an inner <see cref="IProgressMonitor"/>
        /// so progress dialogs that bind to <see cref="IProgressStatus.PercentComplete"/>
        /// actually advance during a long DIA-NN run instead of sitting at 0.
        /// </summary>
        private sealed class DiannProgressMonitor : IProgressMonitor
        {
            private readonly IProgressMonitor _inner;
            private int _maxPctSeen;

            public DiannProgressMonitor(IProgressMonitor inner) { _inner = inner; }

            public bool IsCanceled => _inner.IsCanceled;
            public bool HasUI => _inner.HasUI;

            public UpdateProgressResponse UpdateProgress(IProgressStatus status)
            {
                int? parsed = ParseDiannPercent(status.Message);
                if (parsed.HasValue)
                {
                    int pct = Math.Max(_maxPctSeen, parsed.Value);
                    if (pct != _maxPctSeen)
                    {
                        _maxPctSeen = pct;
                        status = status.ChangePercentComplete(pct);
                    }
                }
                return _inner.UpdateProgress(status);
            }

            // Stages in chronological order — `null` means the message didn't match.
            private static int? ParseDiannPercent(string message)
            {
                if (string.IsNullOrEmpty(message)) return null;
                // Strip the "[mm:ss] " timestamp prefix DIA-NN writes on each line.
                int closeBracket = message.IndexOf(']');
                if (message.Length > 0 && message[0] == '[' && closeBracket > 0 && closeBracket < message.Length - 1)
                    message = message.Substring(closeBracket + 1).TrimStart();

                // File N/M during stage-2 first/second pass: linear 10..80%.
                var fileMatch = System.Text.RegularExpressions.Regex.Match(message, @"^File (\d+)/(\d+)\b");
                if (fileMatch.Success
                    && int.TryParse(fileMatch.Groups[1].Value, out var n)
                    && int.TryParse(fileMatch.Groups[2].Value, out var m) && m > 0)
                    return 10 + 70 * (n - 1) / m;

                if (message.StartsWith(@"Loading spectral library", StringComparison.Ordinal)) return 2;
                if (message.StartsWith(@"Initialising library", StringComparison.Ordinal))     return 5;
                if (message.StartsWith(@"First pass:", StringComparison.Ordinal))               return 10;
                if (message.StartsWith(@"Second pass:", StringComparison.Ordinal))              return 10; // resets within pass
                if (message.StartsWith(@"Cross-run analysis", StringComparison.Ordinal))        return 85;
                if (message.StartsWith(@"Quantifying peptides", StringComparison.Ordinal))      return 90;
                if (message.StartsWith(@"Quantifying proteins", StringComparison.Ordinal))      return 95;
                if (message.StartsWith(@"Generating spectral library", StringComparison.Ordinal)) return 97;
                if (message.IndexOf(@"report saved", StringComparison.OrdinalIgnoreCase) >= 0)  return 99;
                return null;
            }
        }

        /// <summary>
        /// Step 1: Generate a predicted spectral library from FASTA.
        /// DIA-NN uses deep learning to predict spectra and retention times.
        /// </summary>
        /// <returns>Path to the predicted spectral library</returns>
        public static string GeneratePredictedLibrary(
            string fastaFilepath,
            string outputDir,
            DiannConfig config,
            IProgressMonitor progressMonitor,
            ref IProgressStatus status,
            CancellationToken cancelToken,
            IEnumerable<StaticMod> fixedMods = null,
            IEnumerable<StaticMod> variableMods = null,
            Enzyme enzyme = null)
        {
            // DIA-NN 2.5 outputs .predicted.speclib in the output directory
            string predictedLibPath = Path.Combine(outputDir, @"diann-predicted.speclib");

            // If the caller has opted into library caching and a previous library is
            // already on disk, skip --predictor — DIA-NN's prediction is deterministic
            // given FASTA + mods + enzyme + length/charge bounds, so reusing is safe.
            if (config.ReuseCachedLibrary)
            {
                var cached = ResolvePredictedLibraryOutput(outputDir, predictedLibPath);
                if (cached != null)
                    return cached;
            }

            if (cancelToken.IsCancellationRequested)
                return null;

            string args = BuildLibraryGenerationCommandLine(fastaFilepath, predictedLibPath, config,
                fixedMods, variableMods, enzyme);
            RunDiannProcess(args, progressMonitor, ref status);

            return ResolvePredictedLibraryOutput(outputDir, predictedLibPath);
        }

        /// <summary>
        /// DIA-NN's --predictor produces the spectral library at one of three filename
        /// shapes depending on version / settings (`diann-predicted.speclib`,
        /// `diann-predicted.parquet`, or `diann-predicted.predicted.speclib`). Return
        /// whichever exists, or null if none do.
        /// </summary>
        private static string ResolvePredictedLibraryOutput(string outputDir, string predictedLibPath)
        {
            if (File.Exists(predictedLibPath))
                return predictedLibPath;
            string parquetPath = Path.ChangeExtension(predictedLibPath, @".parquet");
            if (File.Exists(parquetPath))
                return parquetPath;
            string altPath = Path.Combine(outputDir, @"diann-predicted.predicted.speclib");
            if (File.Exists(altPath))
                return altPath;
            return null;
        }

        /// <summary>
        /// Step 2: Search DIA raw files against the predicted spectral library.
        /// </summary>
        /// <returns>Path to the output spectral library, or null on failure</returns>
        public static string SearchFiles(
            IEnumerable<string> dataFiles,
            string fastaFilepath,
            string predictedLibPath,
            string outputDir,
            DiannConfig config,
            IProgressMonitor progressMonitor,
            ref IProgressStatus status,
            CancellationToken cancelToken,
            IEnumerable<StaticMod> fixedMods = null,
            IEnumerable<StaticMod> variableMods = null,
            Enzyme enzyme = null)
        {
            // Names paired so DiaNNSpecLibReader's "-lib.parquet" -> ".parquet"
            // report-derivation rule resolves the report file from the lib filename.
            string outputReportPath = Path.Combine(outputDir, @"diann-output.parquet");
            string outputLibPath = Path.Combine(outputDir, @"diann-output-lib.parquet");

            if (cancelToken.IsCancellationRequested)
                return null;

            string args = BuildSearchCommandLine(dataFiles, fastaFilepath, predictedLibPath,
                outputReportPath, outputLibPath, config, fixedMods, variableMods, enzyme);
            RunDiannProcess(args, progressMonitor, ref status);

            return File.Exists(outputLibPath) ? outputLibPath : null;
        }


        /// <summary>
        /// Run the full two-step DIA-NN workflow:
        /// 1. Generate predicted spectral library from FASTA
        /// 2. Search DIA files against the predicted library
        /// </summary>
        /// <returns>Path to the output spectral library, or null on failure</returns>
        public static string RunSearch(
            IEnumerable<string> dataFiles,
            string fastaFilepath,
            string outputDir,
            DiannConfig config,
            IProgressMonitor progressMonitor,
            ref IProgressStatus status,
            CancellationToken cancelToken,
            IEnumerable<StaticMod> fixedMods = null,
            IEnumerable<StaticMod> variableMods = null,
            Enzyme enzyme = null)
        {
            status = status.ChangeSegments(0, 2);

            var fixedModsList = fixedMods?.ToList();
            var variableModsList = variableMods?.ToList();

            // Step 1: Generate predicted library from FASTA
            status = status.ChangeMessage(LibResources.DiannHelpers_RunSearch_Generating_predicted_spectral_library_from_FASTA___);
            progressMonitor.UpdateProgress(status);

            string predictedLibPath = GeneratePredictedLibrary(fastaFilepath, outputDir, config,
                progressMonitor, ref status, cancelToken, fixedModsList, variableModsList, enzyme);

            if (predictedLibPath == null || cancelToken.IsCancellationRequested)
                return null;

            status = status.NextSegment();

            // Step 2: Search data files against predicted library
            status = status.ChangeMessage(LibResources.DiannHelpers_RunSearch_Searching_DIA_files_against_predicted_library___);
            progressMonitor.UpdateProgress(status);

            var dataFilesList = dataFiles.ToList();
            return SearchFiles(dataFilesList, fastaFilepath, predictedLibPath, outputDir, config,
                progressMonitor, ref status, cancelToken, fixedModsList, variableModsList, enzyme);
        }
    }

    /// <summary>
    /// Configuration parameters for a DIA-NN search.
    /// </summary>
    public class DiannConfig
    {
        public DiannConfig()
        {
            AdditionalSettings = new Dictionary<string, AbstractDdaSearchEngine.Setting>();
            foreach (var kvp in DefaultAdditionalSettings)
                AdditionalSettings[kvp.Key] = new AbstractDdaSearchEngine.Setting(kvp.Value);
        }

        public double Ms1Accuracy { get; set; }
        public double Ms2Accuracy { get; set; }
        public double QValue { get; set; } = 0.01;
        public int Threads { get; set; } = Environment.ProcessorCount;
        public int MinPepLen { get; set; } = 7;
        public int MaxPepLen { get; set; } = 30;
        public int MinPrCharge { get; set; } = 1;
        public int MaxPrCharge { get; set; } = 4;
        public int MaxMissedCleavages { get; set; } = 1;
        public bool MetExcision { get; set; } = true;
        // DIA-NN's CLI default for --var-mods is 1; the GUI default is 2. Match the GUI
        // so we're not silently more restrictive than what most DIA-NN users get.
        public int MaxVarMods { get; set; } = 2;

        /// <summary>
        /// When true, <see cref="GeneratePredictedLibrary"/> short-circuits and returns
        /// the cached library path if a previously-generated library already exists in
        /// the output directory. Used by perf tests to avoid the ~20-min library-prediction
        /// step on every iteration; off by default so production runs always regenerate.
        /// </summary>
        public bool ReuseCachedLibrary { get; set; } = false;

        /// <summary>
        /// When true, the search command line includes DIA-NN's `--reuse-quant` flag, so
        /// per-file `.quant` files alongside the inputs are reused instead of re-searched.
        /// Used by perf tests for incremental rebuilds; off by default because production
        /// runs should always pick up new search-parameter or library changes.
        /// </summary>
        public bool ReuseQuantFiles { get; set; } = false;

        public IDictionary<string, AbstractDdaSearchEngine.Setting> AdditionalSettings { get; }

        // ReSharper disable LocalizableElement
        public static readonly ImmutableDictionary<string, AbstractDdaSearchEngine.Setting> DefaultAdditionalSettings =
            new ImmutableDictionary<string, AbstractDdaSearchEngine.Setting>(new Dictionary<string, AbstractDdaSearchEngine.Setting>
            {
                { "MinPepLen", new AbstractDdaSearchEngine.Setting("MinPepLen", 7, 1, 100) },
                { "MaxPepLen", new AbstractDdaSearchEngine.Setting("MaxPepLen", 30, 1, 100) },
                { "MinPrecursorCharge", new AbstractDdaSearchEngine.Setting("MinPrecursorCharge", 1, 1, 10) },
                { "MaxPrecursorCharge", new AbstractDdaSearchEngine.Setting("MaxPrecursorCharge", 4, 1, 10) },
            });
        // ReSharper restore LocalizableElement

        public void ApplyAdditionalSettings()
        {
            MinPepLen = (int)AdditionalSettings[@"MinPepLen"].Value;
            MaxPepLen = (int)AdditionalSettings[@"MaxPepLen"].Value;
            MinPrCharge = (int)AdditionalSettings[@"MinPrecursorCharge"].Value;
            MaxPrCharge = (int)AdditionalSettings[@"MaxPrecursorCharge"].Value;
        }
    }
}
