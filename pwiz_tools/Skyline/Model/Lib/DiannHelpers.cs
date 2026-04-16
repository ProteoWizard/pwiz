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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    public static class DiannHelpers
    {
        public const string DIANN_VERSION = @"2.5.0";
        public static readonly string DIANN_FILENAME = $@"DIANN-{DIANN_VERSION}";
        public static string DiannDirectory => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), DIANN_FILENAME);
        public static string DiannBinary =>
            Settings.Default.SearchToolList.GetToolPathOrDefault(SearchToolType.DIANN, Path.Combine(DiannDirectory, @"diann.exe"));
        public static string DiannArgs =>
            Settings.Default.SearchToolList.GetToolArgsOrDefault(SearchToolType.DIANN, string.Empty);

        public static readonly Uri DIANN_DOWNLOAD_URL = new Uri(@"https://github.com/vdemichev/DiaNN/releases");

        /// <summary>
        /// Mirrored DIA-NN 2.5.0 install on the Skyline tool testing S3 bucket, used by
        /// functional tests. <see cref="SimpleFileDownloader.DownloadRequiredFiles"/> rewrites
        /// this URL to the mirror when running under a unit test.
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
        public static FileDownloadInfo[] FilesToDownload => new[] { DiannDownloadInfo };

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

            // Tolerances (0 = auto-detect)
            if (config.Ms1Accuracy > 0)
                sb.AppendFormat(CultureInfo.InvariantCulture, @"--ms1-accuracy {0} ", config.Ms1Accuracy);
            if (config.Ms2Accuracy > 0)
                sb.AppendFormat(CultureInfo.InvariantCulture, @"--ms2-accuracy {0} ", config.Ms2Accuracy);

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
            foreach (var file in dataFiles)
                sb.AppendFormat(@"--f {0} ", file.Quote());

            // FASTA (for protein inference) and predicted library
            sb.AppendFormat(@"--fasta {0} ", fastaFilepath.Quote());
            sb.AppendFormat(@"--lib {0} ", inputLibPath.Quote());

            // Output
            sb.AppendFormat(@"--out {0} ", outputReportPath.Quote());
            sb.AppendFormat(@"--out-lib {0} ", outputLibPath.Quote());
            sb.Append(@"--gen-spec-lib ");
            // MBR (--reanalyse) is required for DIA-NN to emit the .skyline.speclib file
            sb.Append(@"--reanalyse ");

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
            var psi = new ProcessStartInfo(DiannBinary, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            status = status.ChangeMessage(string.Format(
                Resources.EncyclopeDiaHelpers_GenerateLibrary_Running_command___0___1_,
                psi.FileName, psi.Arguments));
            progressMonitor.UpdateProgress(status);

            pr.Run(psi, null, progressMonitor, ref status, null,
                ProcessPriorityClass.BelowNormal, true, IsGoodDiannOutput, false);
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

            if (cancelToken.IsCancellationRequested)
                return null;

            string args = BuildLibraryGenerationCommandLine(fastaFilepath, predictedLibPath, config,
                fixedMods, variableMods, enzyme);
            RunDiannProcess(args, progressMonitor, ref status);

            // DIA-NN may output with a different extension (.parquet) — check both
            if (File.Exists(predictedLibPath))
                return predictedLibPath;

            string parquetPath = Path.ChangeExtension(predictedLibPath, @".parquet");
            if (File.Exists(parquetPath))
                return parquetPath;

            // Check for .predicted.speclib pattern that DIA-NN may generate
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
            string outputReportPath = Path.Combine(outputDir, @"diann-report.parquet");
            string outputLibPath = Path.Combine(outputDir, @"diann-output-lib.parquet");

            if (cancelToken.IsCancellationRequested)
                return null;

            string args = BuildSearchCommandLine(dataFiles, fastaFilepath, predictedLibPath,
                outputReportPath, outputLibPath, config, fixedMods, variableMods, enzyme);
            RunDiannProcess(args, progressMonitor, ref status);

            // DIA-NN 2.x produces a Skyline-compatible speclib alongside the parquet library
            // (requires --reanalyse and 2+ input files). Name: "<out-lib>.skyline.speclib".
            string skylineSpeclib = outputLibPath + @".skyline.speclib";
            if (File.Exists(skylineSpeclib))
                return skylineSpeclib;

            if (File.Exists(outputLibPath))
                return outputLibPath;

            return null;
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
            status = status.ChangeMessage(Resources.DiannHelpers_RunSearch_Generating_predicted_spectral_library_from_FASTA___);
            progressMonitor.UpdateProgress(status);

            string predictedLibPath = GeneratePredictedLibrary(fastaFilepath, outputDir, config,
                progressMonitor, ref status, cancelToken, fixedModsList, variableModsList, enzyme);

            if (predictedLibPath == null || cancelToken.IsCancellationRequested)
                return null;

            status = status.NextSegment();

            // Step 2: Search data files against predicted library
            status = status.ChangeMessage(Resources.DiannHelpers_RunSearch_Searching_DIA_files_against_predicted_library___);
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

        public static DiannConfig DEFAULT => new DiannConfig();
    }
}
