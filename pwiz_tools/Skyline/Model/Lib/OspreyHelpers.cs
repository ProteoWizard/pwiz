/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using CarafeEnzyme = pwiz.CarafeSharp.Proteome.Enzyme;

namespace pwiz.Skyline.Model.Lib
{
    /// <summary>
    /// Runs Osprey as a DIA search engine from Skyline: where the pinned Osprey build is installed,
    /// how its command line is built from the search wizard's settings, and how the process is run
    /// under a progress monitor. Osprey predicts its spectral library from the FASTA with CarafeSharp
    /// inside the one command and writes exactly the .blib Skyline reads, so unlike the DIA-NN path
    /// there is no separate prediction step and no BlibBuild conversion of the output.
    /// </summary>
    public static class OspreyHelpers
    {
        // The pinned Osprey build. The release zip unpacks to a folder of this name in the Tools
        // folder, as the DIA-NN zip does; a developer or a test points Skyline at a local build
        // through the registered search tool path instead (Settings > Search Tools).
        public const string OSPREY_VERSION = @"26.1.1.296";
        public static readonly string OSPREY_FILENAME = $@"Osprey-{OSPREY_VERSION}-win-x64";
        public const string OSPREY_EXE = @"Osprey.exe";

        /// <summary>The compressed size of the Osprey package, stated before the download starts.</summary>
        public const int OSPREY_DOWNLOAD_SIZE_MB = 105;

        public static string OspreyDirectory => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), OSPREY_FILENAME);
        public static string OspreyBinary =>
            Settings.Default.SearchToolList.GetToolPathOrDefault(SearchToolType.Osprey, Path.Combine(OspreyDirectory, OSPREY_EXE));
        public static string OspreyArgs =>
            Settings.Default.SearchToolList.GetToolArgsOrDefault(SearchToolType.Osprey, string.Empty);

        /// <summary>
        /// Where the pinned package is downloaded from. Osprey has no release site yet, so the zip is
        /// served from the Skyline tool testing mirror, which is also where
        /// <see cref="SimpleFileDownloader.DownloadRequiredFiles"/> looks under tests.
        /// </summary>
        public static readonly Uri OSPREY_DOWNLOAD_URL =
            new Uri($@"https://ci.skyline.ms/skyline_tool_testing_mirror/{OSPREY_FILENAME}.zip");

        public static FileDownloadInfo OspreyDownloadInfo => new FileDownloadInfo
        {
            Filename = OSPREY_FILENAME,
            // The zip holds a top-level Osprey-<version>-win-x64 folder, so it unpacks into the
            // Tools folder, OspreyDirectory's parent.
            InstallPath = ToolDescriptionHelpers.GetToolsDirectory(),
            CheckInstalledPath = OspreyBinary,
            DownloadUrl = OSPREY_DOWNLOAD_URL,
            OverwriteExisting = true,
            Unzip = true,
            ToolType = SearchToolType.Osprey,
            ToolPath = OspreyBinary,
            ToolExtraArgs = OspreyArgs
        };

        public static FileDownloadInfo[] FilesToDownload => new[] { OspreyDownloadInfo };

        // Osprey's command-line vocabulary. The library-build options are CarafeSharp's, whose
        // declarations Skyline references directly (CarafeCommandLine.ARG_*); these are Osprey's own.
        public const string ARG_INPUT = @"-i";
        public const string ARG_FASTA = @"--fasta";
        public const string ARG_LIBRARY = @"-l";
        public const string ARG_OUTPUT = @"-o";
        public const string ARG_WORK_DIR = @"--work-dir";
        public const string ARG_TORCH_RUNTIMES = @"--torch-runtimes";
        public const string ARG_DEVICE = @"--device";
        public const string ARG_RESOLUTION = @"--resolution";
        public const string ARG_FRAGMENT_TOLERANCE = @"--fragment-tolerance";
        public const string ARG_FRAGMENT_UNIT = @"--fragment-unit";
        public const string ARG_RUN_FDR = @"--run-fdr";
        public const string ARG_EXPERIMENT_FDR = @"--experiment-fdr";
        public const string ARG_PROTEIN_FDR = @"--protein-fdr";
        public const string ARG_THREADS = @"--threads";

        public const string DEVICE_AUTO = @"auto";
        public const string RESOLUTION_HRAM = @"hram";
        public const string RESOLUTION_UNIT = @"unit";
        public const string FRAGMENT_UNIT_PPM = @"ppm";
        public const string FRAGMENT_UNIT_MZ = @"mz";

        /// <summary>The folder beside the document that holds Osprey's working files for the document's search.</summary>
        public const string WORK_DIR_SUFFIX = @".osprey";

        /// <summary>Osprey's console output, teed to disk as it runs, in the working folder.</summary>
        public const string STDOUT_LOG_NAME = @"osprey-skyline-stdout.log";

        /// <summary>
        /// Osprey's output for a document is the document library itself, the .blib Skyline names
        /// after the document, so the search result becomes the document library with no conversion.
        /// </summary>
        public static string GetOutputLibraryPath(string documentPath)
        {
            return BiblioSpecLiteSpec.GetLibraryFileName(documentPath);
        }

        /// <summary>
        /// Osprey's working files (the spectra caches, the per-run scores, the predicted library and
        /// its working folder) go in a folder named after the document, beside it, rather than
        /// beside the raw data files, which is Osprey's own default.
        /// </summary>
        public static string GetWorkDirectory(string documentPath)
        {
            string dir = Path.GetDirectoryName(documentPath) ?? string.Empty;
            return Path.Combine(dir, Path.GetFileNameWithoutExtension(documentPath) + WORK_DIR_SUFFIX);
        }

        /// <summary>
        /// The CarafeSharp enzyme that digests exactly as <paramref name="enzyme"/> does, as the index
        /// CarafeSharp's <c>--enzyme</c> takes, or null when none does (a semi-specific enzyme, or a
        /// cleavage rule Carafe's table lacks). Matched on behavior - the two tables spell the same
        /// enzyme differently (Skyline's "Trypsin/P" is Carafe's "Trypsin (no P rule)") - by comparing
        /// the cleavage decision over every pair of the twenty amino acids.
        /// </summary>
        public static int? GetCarafeEnzymeIndex(DocSettings.Enzyme enzyme)
        {
            if (enzyme == null || enzyme.IsSemiCleaving)
                return null;
            var candidates = EnzymeTable.All;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (DigestsAlike(enzyme, candidates[i]))
                    return i;
            }
            return null;
        }

        private const string AMINO_ACIDS = @"ACDEFGHIKLMNPQRSTVWY";

        private static bool DigestsAlike(DocSettings.Enzyme enzyme, CarafeEnzyme carafeEnzyme)
        {
            foreach (char before in AMINO_ACIDS)
            {
                foreach (char after in AMINO_ACIDS)
                {
                    if (IsCleavageSite(enzyme, before, after) != carafeEnzyme.IsCleavageSite(before, after))
                        return false;
                }
            }
            return true;
        }

        private static bool IsCleavageSite(DocSettings.Enzyme enzyme, char before, char after)
        {
            if (Contains(enzyme.CleavageC, before) && !Contains(enzyme.RestrictC, after))
                return true;
            return Contains(enzyme.CleavageN, after) && !Contains(enzyme.RestrictN, before);
        }

        private static bool Contains(string residues, char aa)
        {
            return residues != null && residues.IndexOf(aa) >= 0;
        }

        /// <summary>
        /// The CarafeSharp modification ids for <paramref name="mods"/>, as <c>--fix-mod</c> and
        /// <c>--var-mod</c> take them, with the modifications none of Carafe's table describes in
        /// <paramref name="unsupported"/>. A Skyline modification is matched by its UniMod accession
        /// and site: Carafe lists one entry per residue, so a modification on several residues
        /// (Phospho (ST)) maps to several ids.
        /// </summary>
        public static IList<int> GetCarafeModificationIds(IEnumerable<StaticMod> mods, out IList<StaticMod> unsupported)
        {
            var ids = new List<int>();
            unsupported = new List<StaticMod>();
            foreach (var mod in mods)
            {
                var matched = CarafeModification.TopModifications.Where(m => Matches(mod, m)).Select(m => m.Id).ToList();
                if (matched.Count == 0)
                    unsupported.Add(mod);
                else
                    ids.AddRange(matched.Where(id => !ids.Contains(id)));
            }
            return ids;
        }

        private static bool Matches(StaticMod mod, CarafeModification carafeMod)
        {
            if (!mod.UnimodId.HasValue || mod.UnimodId.Value != carafeMod.UnimodAccession)
                return false;
            bool onResidue = !string.IsNullOrEmpty(mod.AAs);
            switch (carafeMod.Type)
            {
                case CarafeModificationType.residue:
                    return mod.Terminus == null && onResidue && carafeMod.Target.HasValue && Contains(mod.AAs, carafeMod.Target.Value);
                case CarafeModificationType.peptide_n_term:
                case CarafeModificationType.protein_n_term:
                    return mod.Terminus == ModTerminus.N && !onResidue;
                case CarafeModificationType.peptide_n_term_residue:
                    return mod.Terminus == ModTerminus.N && onResidue && carafeMod.Target.HasValue && Contains(mod.AAs, carafeMod.Target.Value);
                default:
                    return false;
            }
        }

        /// <summary>
        /// The command line for one search: the runs, the FASTA to predict the library from (or an
        /// existing library in its place), the output, the working folder, the libtorch runtimes
        /// folder shared with Skyline's own prediction, and the digest, modification, scoring and FDR
        /// settings. Paths are quoted; numbers are written in the invariant culture, which Osprey
        /// reads them in.
        /// </summary>
        public static string BuildCommandLine(OspreyConfig config)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(OspreyArgs))
                sb.Append(OspreyArgs).Append(' ');

            sb.Append(ARG_INPUT);
            foreach (var file in config.DataFiles)
                sb.Append(' ').Append(file.Quote());
            sb.Append(' ');

            if (!string.IsNullOrEmpty(config.LibraryPath))
            {
                sb.Append(ARG_LIBRARY).Append(' ').Append(config.LibraryPath.Quote()).Append(' ');
            }
            else
            {
                sb.Append(ARG_FASTA).Append(' ').Append(config.FastaPath.Quote()).Append(' ');
                AppendLibraryBuildArgs(sb, config);
            }

            sb.Append(ARG_OUTPUT).Append(' ').Append(config.OutputPath.Quote()).Append(' ');
            sb.Append(ARG_WORK_DIR).Append(' ').Append(config.WorkDirectory.Quote()).Append(' ');

            sb.Append(ARG_RESOLUTION).Append(' ').Append(config.Resolution).Append(' ');
            AppendNumber(sb, ARG_FRAGMENT_TOLERANCE, config.FragmentTolerance);
            sb.Append(ARG_FRAGMENT_UNIT).Append(' ').Append(config.FragmentUnit).Append(' ');
            AppendNumber(sb, ARG_RUN_FDR, config.RunFdr);
            AppendNumber(sb, ARG_EXPERIMENT_FDR, config.ExperimentFdr);
            AppendNumber(sb, ARG_PROTEIN_FDR, config.ProteinFdr);
            AppendNumber(sb, ARG_THREADS, config.Threads);

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// The library-build options: the device and the runtimes folder Osprey finds the CUDA
        /// runtime in when Skyline has installed one, and CarafeSharp's digest options in Osprey's
        /// hyphenated spelling.
        /// </summary>
        private static void AppendLibraryBuildArgs(StringBuilder sb, OspreyConfig config)
        {
            sb.Append(ARG_DEVICE).Append(' ').Append(config.Device).Append(' ');
            if (!string.IsNullOrEmpty(config.TorchRuntimesDirectory))
                sb.Append(ARG_TORCH_RUNTIMES).Append(' ').Append(config.TorchRuntimesDirectory.Quote()).Append(' ');
            if (config.EnzymeIndex.HasValue)
                AppendNumber(sb, CarafeCommandLine.ARG_ENZYME.ArgumentText, config.EnzymeIndex.Value);
            AppendNumber(sb, CarafeCommandLine.ARG_MISS_C.ArgumentText, config.MaxMissedCleavages);
            AppendNumber(sb, CarafeCommandLine.ARG_MIN_LENGTH.ArgumentText, config.MinPeptideLength);
            AppendNumber(sb, CarafeCommandLine.ARG_MAX_LENGTH.ArgumentText, config.MaxPeptideLength);
            AppendNumber(sb, CarafeCommandLine.ARG_MIN_PEP_CHARGE.ArgumentText, config.MinCharge);
            AppendNumber(sb, CarafeCommandLine.ARG_MAX_PEP_CHARGE.ArgumentText, config.MaxCharge);
            // "0" is Carafe's spelling of no modifications; left out, Carafe would apply its own defaults.
            sb.Append(CarafeCommandLine.ARG_FIX_MOD.ArgumentText).Append(' ').Append(FormatModificationIds(config.FixedModificationIds)).Append(' ');
            sb.Append(CarafeCommandLine.ARG_VAR_MOD.ArgumentText).Append(' ').Append(FormatModificationIds(config.VariableModificationIds)).Append(' ');
            AppendNumber(sb, CarafeCommandLine.ARG_MAX_VAR.ArgumentText, config.MaxVariableMods);
        }

        private static string FormatModificationIds(IEnumerable<int> ids)
        {
            var list = ids?.ToList() ?? new List<int>();
            return list.Count == 0 ? @"0" : string.Join(@",", list.Select(id => id.ToString(CultureInfo.InvariantCulture)));
        }

        private static void AppendNumber(StringBuilder sb, string arg, double value)
        {
            sb.Append(arg).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).Append(' ');
        }

        private static void AppendNumber(StringBuilder sb, string arg, int value)
        {
            sb.Append(arg).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).Append(' ');
        }

        /// <summary>
        /// Run the search, reporting Osprey's output through <paramref name="progressMonitor"/>: each
        /// line is the status message and each "NN%" line Osprey's progress reporter writes is the
        /// percent complete, with no wrapper, since Osprey writes through the same status writer
        /// Skyline's command line uses. The verdict is the exit code, which Osprey reconciles with
        /// its own Error: lines. Returns the output library path when Osprey wrote it, or null.
        /// </summary>
        public static string RunSearch(OspreyConfig config, IProgressMonitor progressMonitor, ref IProgressStatus status)
        {
            Directory.CreateDirectory(config.WorkDirectory);
            string args = BuildCommandLine(config);

            var pr = new ProcessRunner();
            // The 8.3 short form of the install path and a non-Unicode TMP, the precautions the
            // DIA-NN path takes under TestRunner's Unicode tool folder; harmless everywhere else.
            string ospreyExe = PathEx.GetNonUnicodePath(OspreyBinary) ?? OspreyBinary;
            var psi = new ProcessStartInfo(ospreyExe, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            pr.ChangeTmpDirEnvironmentVariableToNonUnicodePath(psi);

            status = status.ChangeMessage(string.Format(
                Resources.EncyclopeDiaHelpers_GenerateLibrary_Running_command___0___1_,
                psi.FileName, psi.Arguments));
            progressMonitor.UpdateProgress(status);

            // Tee Osprey's output to a flushed file in the working folder so a user (or a developer)
            // can follow a long search from outside Skyline. Osprey's own --log-file is not used:
            // it redirects every line away from the console, which would leave the wizard blind.
            using (var liveLog = TryOpenLiveLogWriter(config.WorkDirectory))
            {
                pr.Run(psi, null, progressMonitor, ref status, liveLog,
                    ProcessPriorityClass.BelowNormal, true, IsGoodOspreyOutput, true);
            }

            return File.Exists(config.OutputPath) ? config.OutputPath : null;
        }

        private static bool IsGoodOspreyOutput(string stdErr, int exitCode)
        {
            return exitCode == 0;
        }

        private static TextWriter TryOpenLiveLogWriter(string workDirectory)
        {
            try
            {
                var stream = new FileStream(Path.Combine(workDirectory, STDOUT_LOG_NAME),
                    FileMode.Append, FileAccess.Write, FileShare.Read);
                return new StreamWriter(stream) { AutoFlush = true };
            }
            catch
            {
                return null; // Logging is best-effort; never fail the search because of it
            }
        }
    }

    /// <summary>
    /// One Osprey search as the wizard configures it: the inputs, the output, and the settings
    /// <see cref="OspreyHelpers.BuildCommandLine"/> turns into the command line.
    /// </summary>
    public class OspreyConfig
    {
        public IList<string> DataFiles { get; set; } = new List<string>();

        /// <summary>The FASTA the library is predicted from, and the proteins the results are matched to.</summary>
        public string FastaPath { get; set; }

        /// <summary>An existing library to search in place of predicting one; null predicts from <see cref="FastaPath"/>.</summary>
        public string LibraryPath { get; set; }

        public string OutputPath { get; set; }
        public string WorkDirectory { get; set; }

        /// <summary>Skyline's Tools folder, where its own CUDA runtime install lives, if any.</summary>
        public string TorchRuntimesDirectory { get; set; }

        /// <summary>auto lets Osprey use the GPU when a CUDA runtime is installed in <see cref="TorchRuntimesDirectory"/>.</summary>
        public string Device { get; set; } = OspreyHelpers.DEVICE_AUTO;

        public string Resolution { get; set; } = OspreyHelpers.RESOLUTION_HRAM;
        public double FragmentTolerance { get; set; } = 10;
        public string FragmentUnit { get; set; } = OspreyHelpers.FRAGMENT_UNIT_PPM;
        public double RunFdr { get; set; } = 0.01;
        public double ExperimentFdr { get; set; } = 0.01;
        public double ProteinFdr { get; set; } = 0.01;
        public int Threads { get; set; } = Environment.ProcessorCount;

        /// <summary>The index into CarafeSharp's enzyme table; null leaves Carafe's default (trypsin).</summary>
        public int? EnzymeIndex { get; set; }
        public int MaxMissedCleavages { get; set; } = 1;
        public int MinPeptideLength { get; set; } = 7;
        public int MaxPeptideLength { get; set; } = 35;
        public int MinCharge { get; set; } = 2;
        public int MaxCharge { get; set; } = 3;
        public int MaxVariableMods { get; set; } = 1;
        public IList<int> FixedModificationIds { get; set; } = new List<int>();
        public IList<int> VariableModificationIds { get; set; } = new List<int>();
    }
}
