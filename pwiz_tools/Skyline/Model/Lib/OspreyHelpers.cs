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
using System.Text.RegularExpressions;
using System.Xml.Linq;
using pwiz.BiblioSpec.IO;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.Database;
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
        public const int OSPREY_DOWNLOAD_SIZE_MB = 150;

        // Osprey's TeamCity build. Tests download from it as SkylineNightly downloads SkylineTester:
        // the latest successful build on a designated branch, found through the REST API, which the
        // Osprey project opens to guest access. Until Osprey has a release process, the pinned
        // package is the TeamCity build that produced OSPREY_VERSION, addressed by build id, which
        // stays valid only while that build is kept (pinned) on the server.
        public const string TEAMCITY_SERVER = @"https://teamcity.labkey.org";
        public const string TEAMCITY_BUILD_TYPE = @"ProteoWizard_OspreyWindowsNet";
        public const string TEAMCITY_BRANCH = @"master";
        public const string OSPREY_BUILD_ID = @"4234706";
        private const string OSPREY_ZIP_SUFFIX = @"-win-x64.zip";

        public static string OspreyDirectory => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), OSPREY_FILENAME);

        /// <summary>The pinned package's executable in the Tools folder, where a download puts it.</summary>
        public static string OspreyDefaultBinary => Path.Combine(OspreyDirectory, OSPREY_EXE);

        /// <summary>The Osprey Skyline runs: a registered search tool path when there is one, else the pinned package's.</summary>
        public static string OspreyBinary =>
            Settings.Default.SearchToolList.GetToolPathOrDefault(SearchToolType.Osprey, OspreyDefaultBinary);
        public static string OspreyArgs =>
            Settings.Default.SearchToolList.GetToolArgsOrDefault(SearchToolType.Osprey, string.Empty);

        /// <summary>
        /// Where the pinned package is downloaded from: the artifact of the TeamCity build that made
        /// it, until Osprey has a release site.
        /// </summary>
        public static readonly Uri OSPREY_DOWNLOAD_URL = GetArtifactUrl(OSPREY_BUILD_ID, OSPREY_FILENAME + @".zip");

        public static FileDownloadInfo OspreyDownloadInfo => CreateDownloadInfo(OSPREY_FILENAME, OSPREY_DOWNLOAD_URL);

        public static FileDownloadInfo[] FilesToDownload => new[] { OspreyDownloadInfo };

        /// <summary>
        /// The download of the latest successful Osprey build on <see cref="TEAMCITY_BRANCH"/>, which
        /// tests follow as SkylineNightly follows Skyline's head. The build is found through the REST
        /// API, its zip named from the build's artifact list (the version is in the name), and the
        /// download addressed by build id, so a build finishing between the two requests cannot
        /// change what is downloaded. Throws with the reason when TeamCity cannot be reached or the
        /// build has no package; these are developer messages, not user-facing text.
        /// </summary>
        public static FileDownloadInfo GetLatestBuildDownloadInfo()
        {
            using var client = new HttpClientWithProgress();
            string buildLocator = $@"buildType:{TEAMCITY_BUILD_TYPE},branch:{TEAMCITY_BRANCH},status:SUCCESS,count:1";
            // A locator in the path answers with the one build as the document root
            var build = XDocument.Parse(client.DownloadString($@"{TEAMCITY_SERVER}/guestAuth/app/rest/builds/{buildLocator}")).Root;
            string buildId = build?.Name.LocalName == @"build" ? build.Attribute(@"id")?.Value : null;
            if (string.IsNullOrEmpty(buildId))
            {
                throw new IOException(string.Format(@"No successful {0} build on branch {1} at {2}",
                    TEAMCITY_BUILD_TYPE, TEAMCITY_BRANCH, TEAMCITY_SERVER));
            }
            var artifacts = XDocument.Parse(client.DownloadString($@"{TEAMCITY_SERVER}/guestAuth/app/rest/builds/id:{buildId}/artifacts/children/"));
            string zipName = artifacts.Root?.Elements(@"file").Select(f => f.Attribute(@"name")?.Value)
                .FirstOrDefault(n => n != null && n.StartsWith(@"Osprey-") && n.EndsWith(OSPREY_ZIP_SUFFIX));
            if (zipName == null)
            {
                throw new IOException(string.Format(@"Build {0} of {1} has no Osprey-<version>{2} artifact",
                    build.Attribute(@"number")?.Value, TEAMCITY_BUILD_TYPE, OSPREY_ZIP_SUFFIX));
            }
            // The zip's top-level folder is its name without the extension
            string folderName = zipName.Substring(0, zipName.Length - 4);
            return CreateDownloadInfo(folderName, GetArtifactUrl(buildId, zipName));
        }

        private static Uri GetArtifactUrl(string buildId, string artifactName)
        {
            return new Uri($@"{TEAMCITY_SERVER}/guestAuth/app/rest/builds/id:{buildId}/artifacts/content/{artifactName}");
        }

        /// <summary>
        /// The download of one Osprey package: the zip holds a top-level <paramref name="folderName"/>
        /// folder, so it unpacks into the Tools folder, and the executable in that folder is both the
        /// check that it is installed and the path the download registers. The versioned path, not the
        /// registered one: a stale registration (a developer build that moved) must not pass for an
        /// install, and a version bump must install the new version.
        /// </summary>
        private static FileDownloadInfo CreateDownloadInfo(string folderName, Uri downloadUrl)
        {
            string toolPath = Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), folderName, OSPREY_EXE);
            return new FileDownloadInfo
            {
                Filename = folderName,
                InstallPath = ToolDescriptionHelpers.GetToolsDirectory(),
                CheckInstalledPath = toolPath,
                DownloadUrl = downloadUrl,
                OverwriteExisting = true,
                Unzip = true,
                ToolType = SearchToolType.Osprey,
                ToolPath = toolPath,
                ToolExtraArgs = OspreyArgs
            };
        }

        // Osprey's command-line vocabulary. The library-build options are CarafeSharp's, whose
        // declarations Skyline references directly (CarafeCommandLine.ARG_*); these are Osprey's own.
        public const string ARG_INPUT = @"-i";
        public const string ARG_FASTA = @"--fasta";
        public const string ARG_MODEL = @"--model";
        public const string ARG_TRAINING_INPUT = @"--training-input";
        public const string ARG_LIBRARY = @"-l";
        public const string ARG_DECOYS_IN_LIBRARY = @"--decoys-in-library";
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
        public const string ARG_CULTURE = @"--culture";
        public const string ARG_TIMESTAMP = @"--timestamp";
        // The precursor-table prediction Build Library runs: the PredictLibrary phase alone
        public const string ARG_PRECURSORS = @"--precursors";
        public const string ARG_TASK = @"--task";

        /// <summary>The suffix, after the output's stem, of the TSV a precursor table's prediction writes.</summary>
        public const string PREDICTED_TABLE_SUFFIX = @".predicted.speclib.tsv";

        // Osprey's --device: gpu and cpu are requirements (a run that cannot predict on the GPU
        // fails); auto takes the GPU when the CUDA runtime is installed, else the CPU.
        public const string DEVICE_AUTO = @"auto";
        public const string DEVICE_GPU = @"gpu";
        public const string DEVICE_CPU = @"cpu";
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
        /// The model a tuning search writes: Osprey saves it beside the library it predicts, in the
        /// working folder, as the output's stem with CarafeSharp's model extension (&lt;doc&gt;.model.zip).
        /// </summary>
        public static string GetTunedModelPath(string outputLibraryPath, string workDirectory)
        {
            return Path.Combine(workDirectory, Path.GetFileNameWithoutExtension(outputLibraryPath) + CarafeModelFile.EXTENSION);
        }

        /// <summary>
        /// The document's fine-tuned model: a copy of the model a tuning search wrote, beside the
        /// document and named after it (&lt;doc&gt;.model.zip), as the document library is. A support
        /// file of the document, like the library, rather than an entry in the model list; a user
        /// who wants it elsewhere adds it to the list by name through the list's Add... The copy
        /// lives outside the working folder, whose spectra caches a user may delete to reclaim space.
        /// </summary>
        public static string GetDocumentModelPath(string documentPath)
        {
            string dir = Path.GetDirectoryName(documentPath) ?? string.Empty;
            return Path.Combine(dir, Path.GetFileNameWithoutExtension(documentPath) + CarafeModelFile.EXTENSION);
        }

        /// <summary>
        /// True when the .blib at <paramref name="libraryPath"/> carries the DecoyPairs table Osprey
        /// and CarafeSharp write, which pairs each decoy with its target: the one case Osprey can
        /// search a library's own decoys (<c>--decoys-in-library</c>), as it does the library it
        /// predicts itself. Without the table Osprey generates decoys, and the flag would be refused.
        /// False for a library that cannot be opened, so the question is Osprey's to answer.
        /// </summary>
        public static bool LibraryHasDecoyPairs(string libraryPath)
        {
            if (string.IsNullOrEmpty(libraryPath) || !PathEx.HasExtension(libraryPath, BiblioSpecLiteSpec.EXT) || !File.Exists(libraryPath))
                return false;
            try
            {
                using (var connection = SqliteOperations.OpenConnection(libraryPath))
                {
                    return SqliteOperations.TableExists(connection, BlibSchema.TABLE_DECOY_PAIRS);
                }
            }
            catch (Exception)
            {
                return false;
            }
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
                // A Skyline N-terminal modification applies to every peptide, which is Carafe's
                // peptide N-term entry; a protein N-term entry would apply it to protein termini only,
                // so a modification Carafe has only in that form is unsupported rather than mismapped.
                case CarafeModificationType.peptide_n_term:
                    return mod.Terminus == ModTerminus.N && !onResidue;
                case CarafeModificationType.peptide_n_term_residue:
                    return mod.Terminus == ModTerminus.N && onResidue && carafeMod.Target.HasValue && Contains(mod.AAs, carafeMod.Target.Value);
                default:
                    return false;
            }
        }

        /// <summary>
        /// The command line for one search: the runs, the FASTA to predict the library from (or an
        /// existing library in its place) with the saved model to predict with and the runs to tune
        /// on, the output, the working folder, the libtorch runtimes folder shared with Skyline's own
        /// prediction, and the digest, modification, scoring and FDR settings. Paths are quoted;
        /// numbers are written in the invariant culture, which Osprey reads them in.
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
                if (config.DecoysInLibrary)
                    sb.Append(ARG_DECOYS_IN_LIBRARY).Append(' ');
            }
            else
            {
                sb.Append(ARG_FASTA).Append(' ').Append(config.FastaPath.Quote()).Append(' ');
                // A saved model is predicted with as it is, or, with tuning runs, is the model the
                // tuning starts from; with neither Osprey predicts with the pretrained models.
                if (!string.IsNullOrEmpty(config.ModelPath))
                    sb.Append(ARG_MODEL).Append(' ').Append(config.ModelPath.Quote()).Append(' ');
                if (config.TrainingInputs.Count > 0)
                {
                    sb.Append(ARG_TRAINING_INPUT);
                    foreach (var run in config.TrainingInputs)
                        sb.Append(' ').Append(run.Quote());
                    sb.Append(' ');
                }
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
            // A child process starts in the OS culture, not Skyline's UI language, so Osprey is told
            // which to write its messages in; the numbers above are invariant either way.
            if (!string.IsNullOrEmpty(config.CultureName))
                sb.Append(ARG_CULTURE).Append(' ').Append(config.CultureName).Append(' ');
            // Every line stamped with its time, for the log teed to the working folder; the wizard's
            // run page strips the stamp and shows the lines under its own Show timestamps box.
            sb.Append(ARG_TIMESTAMP);

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
        /// line is the status message, and <see cref="OspreyProgressMonitor"/> between the runner and
        /// the monitor turns Osprey's [TASK], file N/M and NN% lines into the bar's segments and
        /// position. The verdict is the exit code, which Osprey reconciles with its own Error: lines.
        /// Returns the output library path when Osprey wrote it, or null.
        /// </summary>
        public static string RunSearch(OspreyConfig config, IProgressMonitor progressMonitor, ref IProgressStatus status)
        {
            Directory.CreateDirectory(config.WorkDirectory);
            // The progress bar is driven by Osprey's [TASK] lines through OspreyProgressMonitor, so
            // ProcessRunner's own percent parsing is off: its bar restarted at every phase that
            // printed 100% and froze through the phases that print no percentage (the fine-tune).
            var taskMonitor = new OspreyProgressMonitor(progressMonitor, config);
            RunOsprey(BuildCommandLine(config), config.WorkDirectory, taskMonitor, ref status, false);
            return File.Exists(config.OutputPath) ? config.OutputPath : null;
        }

        /// <summary>
        /// The command line of the precursor-table prediction Build Library runs: Osprey's
        /// PredictLibrary phase alone over <paramref name="tablePath"/>, with the pretrained models or
        /// the saved <paramref name="modelPath"/>, on the GPU (a requirement, with the CUDA runtime in
        /// <paramref name="torchRuntimesDirectory"/>) or the CPU. Osprey writes the TSV beside
        /// <paramref name="outputBlib"/>, at <see cref="GetPredictedTablePath"/>, and writes its messages in
        /// <paramref name="cultureName"/>, Skyline's UI language, which a child process would not inherit.
        /// </summary>
        public static string BuildPredictLibraryCommandLine(string tablePath, string outputBlib, string modelPath,
            bool useGpu, string torchRuntimesDirectory, string cultureName)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(OspreyArgs))
                sb.Append(OspreyArgs).Append(' ');
            sb.Append(ARG_PRECURSORS).Append(' ').Append(tablePath.Quote()).Append(' ');
            sb.Append(ARG_TASK).Append(' ').Append(OspreyProgressMonitor.TASK_PREDICT_LIBRARY).Append(' ');
            sb.Append(ARG_OUTPUT).Append(' ').Append(outputBlib.Quote()).Append(' ');
            if (!string.IsNullOrEmpty(modelPath))
                sb.Append(ARG_MODEL).Append(' ').Append(modelPath.Quote()).Append(' ');
            sb.Append(ARG_DEVICE).Append(' ').Append(useGpu ? DEVICE_GPU : DEVICE_CPU);
            if (!string.IsNullOrEmpty(torchRuntimesDirectory))
                sb.Append(' ').Append(ARG_TORCH_RUNTIMES).Append(' ').Append(torchRuntimesDirectory.Quote());
            if (!string.IsNullOrEmpty(cultureName))
                sb.Append(' ').Append(ARG_CULTURE).Append(' ').Append(cultureName);
            return sb.ToString();
        }

        /// <summary>The TSV a precursor-table prediction with the output <paramref name="outputBlib"/> writes.</summary>
        public static string GetPredictedTablePath(string outputBlib)
        {
            return Path.Combine(Path.GetDirectoryName(outputBlib) ?? string.Empty,
                Path.GetFileNameWithoutExtension(outputBlib) + PREDICTED_TABLE_SUFFIX);
        }

        /// <summary>
        /// Runs Osprey with <paramref name="args"/>, every line of its output reported to
        /// <paramref name="progressMonitor"/> as the status message and teed to
        /// <see cref="STDOUT_LOG_NAME"/> in <paramref name="logDirectory"/>, an earlier log rolled aside.
        /// With <paramref name="updateProgressPercentage"/> the runner reads Osprey's NN% lines into the
        /// status, for a run of one task; the wizard passes false and an <see cref="OspreyProgressMonitor"/>
        /// instead. The verdict is the exit code, which Osprey reconciles with its own Error: lines;
        /// a failure throws with Osprey's last lines.
        /// </summary>
        public static void RunOsprey(string args, string logDirectory, IProgressMonitor progressMonitor, ref IProgressStatus status,
            bool updateProgressPercentage)
        {
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
            // can follow a long run from outside Skyline. Osprey's own --log-file is not used:
            // it redirects every line away from the console, which would leave the caller blind.
            using (var liveLog = TryOpenLiveLogWriter(logDirectory))
            {
                pr.Run(psi, null, progressMonitor, ref status, liveLog,
                    ProcessPriorityClass.BelowNormal, true, IsGoodOspreyOutput, updateProgressPercentage);
            }
        }

        private static bool IsGoodOspreyOutput(string stdErr, int exitCode)
        {
            return exitCode == 0;
        }

        private static TextWriter TryOpenLiveLogWriter(string workDirectory)
        {
            try
            {
                // An earlier search's log rolls aside under its own end time, so the fixed name the
                // run page's Open Log button opens is this search's alone and the earlier record is kept.
                string logPath = Path.Combine(workDirectory, STDOUT_LOG_NAME);
                FileTimeEx.RollWithTimeStamp(logPath);
                var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
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

        /// <summary>
        /// A saved fine-tuned model (.model.zip) to predict the library with, or to tune further when
        /// there are <see cref="TrainingInputs"/>; null for the pretrained models.
        /// </summary>
        public string ModelPath { get; set; }

        /// <summary>
        /// The runs the models are fine-tuned on before the library is predicted: a subset of
        /// <see cref="DataFiles"/>, other runs, or both. A run not among the data files is searched
        /// for the tuning only and is absent from the output. Empty predicts without tuning.
        /// </summary>
        public IList<string> TrainingInputs { get; set; } = new List<string>();

        /// <summary>
        /// Whether <see cref="LibraryPath"/> supplies its own paired decoys (its DecoyPairs table), so
        /// Osprey uses them in place of generating decoys, as it does for the library it predicts.
        /// </summary>
        public bool DecoysInLibrary { get; set; }

        public string OutputPath { get; set; }
        public string WorkDirectory { get; set; }

        /// <summary>The folder Osprey finds the CUDA runtime in: Skyline's Tools folder, where its own install lives, if any.</summary>
        public string TorchRuntimesDirectory { get; set; }

        /// <summary>
        /// gpu predicts on the GPU with the CUDA runtime in <see cref="TorchRuntimesDirectory"/> and
        /// fails without it; cpu predicts on the CPU; auto takes the GPU when the runtime is installed.
        /// </summary>
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

        /// <summary>The culture Osprey writes its messages in: Skyline's UI language, which the child would not inherit.</summary>
        public string CultureName { get; set; }
    }

    /// <summary>
    /// Drives the wizard's progress bar from Osprey's console lines. Each <c>[TASK] name:starting</c>
    /// line opens a segment of the bar, one per task of the run: four to search an existing library,
    /// five when the library is predicted first, eleven when a model is fine-tuned (the training
    /// library and its search, the tuning, then the library and its search). Within a per-file task
    /// the <c>file N/M:</c> lines advance the segment; within the others Osprey's own <c>NN%</c> lines
    /// do. A segment never moves backwards, since Osprey's percentages restart at every step of a
    /// task. Wraps the wizard's monitor as the one ProcessRunner reports to, with the runner's own
    /// percent parsing off, so every line arrives here as a message and the bar is decided here.
    /// </summary>
    public sealed class OspreyProgressMonitor : IProgressMonitor
    {
        // Osprey's task names as its [TASK] lines print them (OspreyTaskNames and LibraryBuildConfig in Osprey.Core)
        public const string TASK_PREDICT_TRAINING_LIBRARY = @"PredictTrainingLibrary";
        public const string TASK_PER_FILE_SCORING = @"PerFileScoring";
        public const string TASK_FIRST_PASS_FDR = @"FirstPassFDR";
        public const string TASK_PER_FILE_RESCORING = @"PerFileRescoring";
        public const string TASK_SECOND_PASS_FDR = @"SecondPassFDR";
        public const string TASK_TRAIN_MODEL = @"TrainModel";
        public const string TASK_PREDICT_LIBRARY = @"PredictLibrary";

        /// <summary>The four tasks of a search: PerFileScoring, FirstPassFDR, PerFileRescoring, SecondPassFDR.</summary>
        private const int SEARCH_TASK_COUNT = 4;

        private static readonly Regex TASK_LINE = new Regex(@"\[TASK\] (\S+?):(starting|done|skipping)\b", RegexOptions.Compiled);
        // "Scoring file 2/3: <path>" and its translations, which keep the N/M; no \b, which Japanese
        // text has none of around a digit. Indented lines are a task's sub-steps (fold 1/3, epoch 12/40).
        private static readonly Regex FILE_LINE = new Regex(@"^\S.*?(?<!\d)(\d+)/(\d+)(?!\d)", RegexOptions.Compiled);

        /// <summary>
        /// The tasks a run of <paramref name="config"/> prints: the search's four after a predicted
        /// library, and with tuning runs the training library's own search and the tuning before them.
        /// </summary>
        public static int ExpectedTaskCount(OspreyConfig config)
        {
            if (!string.IsNullOrEmpty(config.LibraryPath))
                return SEARCH_TASK_COUNT;
            if (config.TrainingInputs.Count > 0)
                return 1 + SEARCH_TASK_COUNT + 1 + 1 + SEARCH_TASK_COUNT;
            return 1 + SEARCH_TASK_COUNT;
        }

        private readonly IProgressMonitor _inner;
        private readonly int _expectedTaskCount;
        private int _tasksStarted;
        private string _taskName;
        private bool _perFileTask;
        private int _taskPercent;
        private int _fileIndex;
        private int _fileCount;

        public OspreyProgressMonitor(IProgressMonitor inner, OspreyConfig config)
            : this(inner, ExpectedTaskCount(config))
        {
        }

        public OspreyProgressMonitor(IProgressMonitor inner, int expectedTaskCount)
        {
            _inner = inner;
            _expectedTaskCount = expectedTaskCount;
        }

        public bool IsCanceled => _inner.IsCanceled;
        public bool HasUI => _inner.HasUI;

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            // A cancel or an error passes through as it is: ChangePercentComplete would put the
            // state back to running, which is how ProcessRunner reports the cancel
            if (status.IsCanceled || status.IsError)
                return _inner.UpdateProgress(status);

            // Osprey runs with --timestamp for the log on disk; the wizard shows the text alone
            string line = CommandStatusWriter.StripTimeStamp(status.Message);
            ReadLine(line);
            if (_tasksStarted > 0)
            {
                // Rebuilt on every line, because ProcessRunner keeps its own status and never sees
                // the segments set here. A run with more tasks than expected widens the bar.
                int segmentCount = Math.Max(_expectedTaskCount, _tasksStarted);
                status = status.ChangeSegments(_tasksStarted - 1, segmentCount)
                    .ChangePercentComplete(_taskPercent)
                    .ChangeSegmentName(GetBarText())
                    .ChangeMessage(line); // The log keeps every line as Osprey printed it, less the stamp
            }
            else if (!ReferenceEquals(line, status.Message))
            {
                status = status.ChangeMessage(line);
            }
            return _inner.UpdateProgress(status);
        }

        /// <summary>Reads one line of Osprey's output into the task state.</summary>
        private void ReadLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return;

            var taskMatch = TASK_LINE.Match(line);
            if (taskMatch.Success)
            {
                string taskName = taskMatch.Groups[1].Value;
                switch (taskMatch.Groups[2].Value)
                {
                    case @"starting":
                        StartTask(taskName);
                        break;
                    case @"skipping":
                        StartTask(taskName);
                        _taskPercent = 100;
                        break;
                    default: // done
                        _taskPercent = 100;
                        break;
                }
                return;
            }

            if (_tasksStarted == 0)
                return;

            if (_perFileTask)
            {
                // The task's file count is the first line's. A line with another count, or an
                // earlier index (the training exports written after the last file is re-scored),
                // neither moves the bar back nor changes the file it names.
                var fileMatch = FILE_LINE.Match(line);
                if (fileMatch.Success && int.TryParse(fileMatch.Groups[1].Value, out int fileIndex) &&
                    int.TryParse(fileMatch.Groups[2].Value, out int fileCount) &&
                    fileIndex >= 1 && fileIndex <= fileCount &&
                    (_fileCount == 0 || (fileCount == _fileCount && fileIndex >= _fileIndex)))
                {
                    _fileIndex = fileIndex;
                    _fileCount = fileCount;
                    AdvanceTask((fileIndex - 1) * 100 / fileCount);
                }
            }
            else
            {
                string trimmed = line.Trim();
                if (trimmed.EndsWith(@"%") &&
                    double.TryParse(trimmed.Substring(0, trimmed.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
                {
                    AdvanceTask((int)percent);
                }
            }
        }

        private void StartTask(string taskName)
        {
            _tasksStarted++;
            _taskName = taskName;
            _perFileTask = taskName == TASK_PER_FILE_SCORING || taskName == TASK_PER_FILE_RESCORING;
            _taskPercent = 0;
            _fileIndex = _fileCount = 0;
        }

        /// <summary>Moves the task forward to <paramref name="percent"/>, never back.</summary>
        private void AdvanceTask(int percent)
        {
            _taskPercent = Math.Max(_taskPercent, Math.Min(100, percent));
        }

        /// <summary>The text on the bar: the task's name, with the file it is on or its own percentage.</summary>
        private string GetBarText()
        {
            string name = GetTaskName(_taskName);
            if (_perFileTask)
                return _fileCount > 0 ? string.Format(@"{0} {1}/{2}", name, _fileIndex, _fileCount) : name;
            if (_taskPercent > 0 && _taskPercent < 100)
                return string.Format(@"{0} {1}%", name, _taskPercent);
            return name;
        }

        private static string GetTaskName(string taskName)
        {
            switch (taskName)
            {
                case TASK_PREDICT_TRAINING_LIBRARY:
                    return LibResources.OspreyProgressMonitor_GetTaskName_Predicting_the_training_library;
                case TASK_PER_FILE_SCORING:
                    return LibResources.OspreyProgressMonitor_GetTaskName_Scoring_files;
                case TASK_FIRST_PASS_FDR:
                    return LibResources.OspreyProgressMonitor_GetTaskName_First_pass_FDR;
                case TASK_PER_FILE_RESCORING:
                    return LibResources.OspreyProgressMonitor_GetTaskName_Re_scoring_files;
                case TASK_SECOND_PASS_FDR:
                    return LibResources.OspreyProgressMonitor_GetTaskName_Second_pass_FDR;
                case TASK_TRAIN_MODEL:
                    return LibResources.OspreyProgressMonitor_GetTaskName_Fine_tuning_the_model;
                case TASK_PREDICT_LIBRARY:
                    return LibResources.OspreyProgressMonitor_GetTaskName_Predicting_the_library;
                default:
                    return taskName;
            }
        }
    }
}
