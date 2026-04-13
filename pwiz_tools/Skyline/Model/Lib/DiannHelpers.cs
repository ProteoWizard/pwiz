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
using System.Text;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    public static class DiannHelpers
    {
        public static string DiannBinary =>
            Settings.Default.SearchToolList.GetToolPathOrDefault(SearchToolType.DIANN, @"diann.exe");
        public static string DiannArgs =>
            Settings.Default.SearchToolList.GetToolArgsOrDefault(SearchToolType.DIANN, string.Empty);

        public static readonly Uri DIANN_DOWNLOAD_URL = new Uri(@"https://github.com/vdemichev/DiaNN/releases");

        /// <summary>
        /// Maps Skyline enzyme names to DIA-NN --cut patterns.
        /// DIA-NN cut syntax: letters followed by * mean "cut after this AA";
        /// !*X means "don't cut if followed by X".
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

            // Fallback: use mass-only format with empty UniMod placeholder
            return string.Format(@"{0},{1}", massStr, aminoAcids);
        }

        /// <summary>
        /// Build the DIA-NN command-line arguments string.
        /// </summary>
        public static string BuildCommandLine(
            IEnumerable<string> dataFiles,
            string fastaFilepath,
            string outputReportPath,
            string outputLibPath,
            DiannConfig config,
            IEnumerable<StaticMod> fixedMods = null,
            IEnumerable<StaticMod> variableMods = null,
            Enzyme enzyme = null,
            string inputLibPath = null)
        {
            var sb = new StringBuilder();

            // Extra args from search tool configuration
            if (!string.IsNullOrEmpty(DiannArgs))
                sb.Append(DiannArgs).Append(' ');

            // Input files
            foreach (var file in dataFiles)
                sb.AppendFormat(@"--f {0} ", file.Quote());

            // FASTA and library
            sb.AppendFormat(@"--fasta {0} ", fastaFilepath.Quote());
            if (!string.IsNullOrEmpty(inputLibPath))
                sb.AppendFormat(@"--lib {0} ", inputLibPath.Quote());
            else
                sb.Append(@"--fasta-search ");

            // Output
            sb.AppendFormat(@"--out {0} ", outputReportPath.Quote());
            sb.AppendFormat(@"--out-lib {0} ", outputLibPath.Quote());
            sb.Append(@"--gen-spec-lib ");

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

            // Verbose output for progress tracking
            sb.Append(@"--verbose 1 ");

            return sb.ToString().TrimEnd();
        }

        private static bool IsGoodDiannOutput(string stdOut, int exitCode)
        {
            return exitCode == 0 && !stdOut.Contains(@"Critical error");
        }

        /// <summary>
        /// Run a DIA-NN search with the given configuration.
        /// </summary>
        /// <param name="dataFiles">Paths to DIA raw data files</param>
        /// <param name="fastaFilepath">Path to FASTA database</param>
        /// <param name="outputDir">Directory for output files</param>
        /// <param name="config">DIA-NN configuration parameters</param>
        /// <param name="progressMonitor">Progress monitor for UI updates</param>
        /// <param name="status">Progress status</param>
        /// <param name="cancelToken">Cancellation token</param>
        /// <param name="fixedMods">Fixed modifications to apply</param>
        /// <param name="variableMods">Variable modifications to apply</param>
        /// <param name="enzyme">Enzyme for digestion</param>
        /// <param name="inputLibPath">Optional input spectral library</param>
        /// <returns>Path to the output spectral library (.speclib), or null on failure</returns>
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
            Enzyme enzyme = null,
            string inputLibPath = null)
        {
            string outputReportPath = Path.Combine(outputDir, @"diann-report.tsv");
            string outputLibPath = Path.Combine(outputDir, @"diann-output.speclib");

            string args = BuildCommandLine(dataFiles, fastaFilepath, outputReportPath, outputLibPath,
                config, fixedMods, variableMods, enzyme, inputLibPath);

            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo(DiannBinary, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            if (cancelToken.IsCancellationRequested)
                return null;

            status = status.ChangeMessage(string.Format(
                Resources.EncyclopeDiaHelpers_GenerateLibrary_Running_command___0___1_,
                psi.FileName, psi.Arguments));
            progressMonitor.UpdateProgress(status);

            pr.Run(psi, null, progressMonitor, ref status, null,
                ProcessPriorityClass.BelowNormal, true, IsGoodDiannOutput, false);

            if (File.Exists(outputLibPath))
                return outputLibPath;

            return null;
        }
    }

    /// <summary>
    /// Configuration parameters for a DIA-NN search.
    /// </summary>
    public class DiannConfig
    {
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

        public static DiannConfig DEFAULT => new DiannConfig();
    }
}
