/*
 * Author: David Shteynberg <dshteyn .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.BiblioSpec;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib.AlphaPeptDeep
{
    public class ArgumentAndValue
    {
        private const string DEFAULT_DASH = @"--";

        public ArgumentAndValue(string name, string value, bool quoteValue)
            : this(name, value, DEFAULT_DASH, quoteValue)
        {}

        public ArgumentAndValue(string name, string value, string dash = DEFAULT_DASH, bool quoteValue = false)
        {
            Name = name;
            Value = value;
            if (quoteValue)
                Value = '"' + Value + '"';
            Dash = dash;
        }
        public string Name { get; private set; }
        public string Value { get; private set; }
        public string Dash { get; set; }

        public override string ToString() { return TextUtil.SpaceSeparate(Dash + Name, Value); }
    }

    public class AlphapeptdeepLibraryBuilder : AbstractDeepLibraryBuilder, IiRTCapableLibraryBuilder
    {
        public const string ALPHAPEPTDEEP = @"AlphaPeptDeep";

        // AlphaPeptDeep commands
        private const string PEPTDEEP_EXECUTABLE = @"peptdeep.exe";
        private const string CMD_FLOW_COMMAND = @"cmd-flow";
        private const string EXPORT_SETTINGS_COMMAND = @"export-settings";

        // Processing folders
        private const string PREFIX_WORKDIR = "APD";
        private const string OUTPUT_MODELS = @"output_models";
        private const string OUTPUT_SPECTRAL_LIBS = @"output_libs";

        // Processing intermediate file names
        private const string INPUT_FILE_NAME = @"input.tsv";
        private const string SETTINGS_FILE_NAME = @"settings.yaml";
        private const string OUTPUT_SPECTRAL_LIB_FILE_NAME = @"predict.speclib.tsv";
        private const string TRANSFORMED_OUTPUT_SPECTRAL_LIB_FILE_NAME = @"predict_sky.speclib.tsv";

        // Column names for AlphaPeptDeep
        private const string SEQUENCE = @"sequence";
        private const string MODS = @"mods";
        private const string MOD_SITES = @"mod_sites";
        private const string CHARGE = @"charge";

        private static readonly IEnumerable<string> PrecursorTableColumnNames = new[] { SEQUENCE, MODS, MOD_SITES, CHARGE };

        // Column names for BlibBuild
        private const string MODIFIED_PEPTIDE = "ModifiedPeptide";
        private const string NORMALIZED_RT = "RT";
        private const string ION_MOBILITY = "IonMobility";
        private const string CCS = "CCS";
        private const string COLLISIONAL_CROSS_SECTION = "CollisionalCrossSection";

        public static string PythonVersion => Settings.Default.PythonEmbeddableVersion;

        public static string ScriptsDir => PythonInstallerUtil.GetPythonVirtualEnvironmentScriptsDir(PythonVersion, ALPHAPEPTDEEP);

        public static PythonInstaller CreatePythonInstaller(TextWriter writer)
        {
            var packages = new[]
            {
                new PythonPackage { Name = @"peptdeep", Version = null },

                // We manually set numpy to the latest version before 2.0 because of a backward incompatibility issue
                // See details for tracking issue in AlphaPeptDeep repo: https://github.com/MannLabs/alphapeptdeep/issues/190
                // TODO: delete the following line after the issue above is resolved
                new PythonPackage { Name = @"numpy", Version = @"1.26.4" }
            };

            return new PythonInstaller(packages, writer, AlphapeptdeepLibraryBuilder.ALPHAPEPTDEEP);
        }

        protected override string ToolName => ALPHAPEPTDEEP;

        protected override LibraryBuilderModificationSupport LibraryBuilderModificationSupport { get; }

        internal static List<ModificationType> MODEL_SUPPORTED_UNIMODS = new List<ModificationType>
        {
            GetUniModType(4, PredictionSupport.all), // Carbamidomethyl (C)
            GetUniModType(21, PredictionSupport.fragmentation), // Phospho
            GetUniModType(35, PredictionSupport.all), // Oxidation
            GetUniModType(121, PredictionSupport.fragmentation) // GlyGly (a.k.a. GG)
        };

        public LibrarySpec LibrarySpec { get; private set; }

        protected override IEnumerable<string> GetHeaderColumnNames(bool training)
        {
            return PrecursorTableColumnNames;
        }

        protected override string GetTableRow(PeptideDocNode peptide, ModifiedSequence modifiedSequence,
            int charge, bool training, string modsBuilder, string modSitesBuilder)
        {
            return new[] { modifiedSequence.GetUnmodifiedSequence(), modsBuilder, modSitesBuilder, charge.ToString() }
                .ToDsvLine(TextUtil.SEPARATOR_TSV);
        }

        private string PeptdeepExecutablePath => Path.Combine(ScriptsDir, PEPTDEEP_EXECUTABLE);

        public override string InputFilePath => Path.Combine(WorkDir, INPUT_FILE_NAME);
        public override string TrainingFilePath => null;
        
        private string SettingsFilePath => Path.Combine(WorkDir, SETTINGS_FILE_NAME);
        private string OutputModelsDir => Path.Combine(WorkDir, OUTPUT_MODELS);
        private string OutputSpectralLibsDir => Path.Combine(WorkDir, OUTPUT_SPECTRAL_LIBS);
        
        public string OutputSpectraLibFilepath => Path.Combine(OutputSpectralLibsDir, OUTPUT_SPECTRAL_LIB_FILE_NAME);
        public string TransformedOutputSpectraLibFilepath => Path.Combine(OutputSpectralLibsDir, TRANSFORMED_OUTPUT_SPECTRAL_LIB_FILE_NAME);

        /// <summary>
        /// The peptdeep cmd-flow command is how we can pass arguments that will override the settings.yaml file.
        /// This is how the peptdeep CLI supports command line arguments.
        /// </summary>
        private IList<ArgumentAndValue> CmdFlowCommandArguments =>
            new[]
            {
                new ArgumentAndValue(@"task_workflow", @"library"),
                new ArgumentAndValue(@"settings_yaml", SettingsFilePath, true),
                new ArgumentAndValue(@"PEPTDEEP_HOME", WorkDir, true),
                new ArgumentAndValue(@"transfer--model_output_folder", OutputModelsDir, true),
                new ArgumentAndValue(@"library--infile_type", @"precursor_table"),
                new ArgumentAndValue(@"library--infiles", InputFilePath, true),
                new ArgumentAndValue(@"library--output_folder", OutputSpectralLibsDir, true),
                new ArgumentAndValue(@"library--output_tsv--enabled", @"True"),
                new ArgumentAndValue(@"library--output_tsv--translate_mod_to_unimod_id", @"True"),
                new ArgumentAndValue(@"library--rt_to_irt", @"True"),
                new ArgumentAndValue(@"library--decoy", @"diann"),
                new ArgumentAndValue(@"device", 
                    PythonInstaller.SimulatedInstallationState != PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD ? @"gpu" : @"cpu")
            };

        private Dictionary<string, string> OpenSwathAssayLikeColName =>
            new Dictionary<string, string>()
            {
                { @"RT", @"NormalizedRetentionTimeAPD" },
                { @"ModifiedPeptide", @"ModifiedPeptideSequence" },
                { @"FragmentMz", @"ProductMz" },
                { @"RelativeIntensity", @"LibraryIntensity" },
                { @"FragmentNumber", @"FragmentSeriesNumber" },
                { CCS, COLLISIONAL_CROSS_SECTION},
                { ION_MOBILITY, null}
            };

        /// <summary>
        /// Constructor for AlphaPeptDeep Library Builder.
        /// </summary>
        /// <param name="libName">Name of the library to build.</param>
        /// <param name="libOutPath">Path to the blib final product.</param>
        /// <param name="document">Input document for building the library.</param>
        /// <param name="irtStandard">iRT peptide standard to include in the library.</param>
        public AlphapeptdeepLibraryBuilder(string libName, string libOutPath,
            SrmDocument document, IrtStandard irtStandard) : base(document, irtStandard)
        {
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            LibraryBuilderModificationSupport = new LibraryBuilderModificationSupport(MODEL_SUPPORTED_UNIMODS);
            string rootProcessingDir = Path.GetDirectoryName(libOutPath);
            if (string.IsNullOrEmpty(rootProcessingDir))
                throw new ArgumentException($@"AlphapeptdeepLibraryBuilder libOutputPath {libOutPath} must be a full path.");

            rootProcessingDir = Path.Combine(rootProcessingDir, Path.GetFileNameWithoutExtension(libOutPath));
            EnsureWorkDir(rootProcessingDir, PREFIX_WORKDIR);
        }

        public bool BuildLibrary(IProgressMonitor progress)
        {
            IProgressStatus progressStatus = new ProgressStatus();

            try
            {
                RunAlphapeptdeep(progress, ref progressStatus);
                progress.UpdateProgress(progressStatus = progressStatus.Complete());
                return true;
            }
            catch (Exception exception)
            {
                progress.UpdateProgress(progressStatus.ChangeErrorException(exception));
                return false;
            }
        }

        private void RunAlphapeptdeep(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            // Note: Segments are distributed to balance the expected work of each task
            var segmentEndPercentages = new[] { 5, 10, 15, 95 };
            progressStatus = progressStatus.ChangeSegments(0, ImmutableList<int>.ValueOf(segmentEndPercentages));
            PreparePrecursorInputFile(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();
            PrepareSettingsFile(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();
            ExecutePeptdeep(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();
            TransformPeptdeepOutput(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();
            ImportSpectralLibrary(progress, ref progressStatus);
        }

        private void PrepareSettingsFile(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.AlphapeptdeepLibraryBuilder_PrepareSettingsFile_Preparing_settings_file));

            // Generate template settings.yaml file
            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo(PeptdeepExecutablePath, $@"{EXPORT_SETTINGS_COMMAND} ""{SettingsFilePath}""")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false
            };
            try
            {
                //REMOVE: This runs so quickly that counting lines here is not necessary for only 5% of the total progress bar
                //pr.ExpectedOutputLinesCount = 213;
                pr.Run(psi, string.Empty, progress, ref progressStatus, ProcessPriorityClass.BelowNormal, true);
                //TotalExpectedLinesOfOutput += pr.ExpectedOutputLinesCount;
                //TotalGeneratedLinesOfOutput += pr.OutputLinesGenerated;
            }
            catch (Exception ex)
            {
                throw new IOException(ModelResources.AlphapeptdeepLibraryBuilder_PrepareSettingsFile_Failed_to_generate_settings_yaml_file_by_executing_the_peptdeep_export_settings_command_, ex);
            }
        }

        private void ExecutePeptdeep(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            Stopwatch timer = new Stopwatch();
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.AlphapeptdeepLibraryBuilder_Running_AlphaPeptDeep));

            progressStatus.ChangePercentComplete(0);
            // Compose peptdeep cmd-flow command arguments to build library
            var args = TextUtil.SpaceSeparate(CmdFlowCommandArguments.Select(arg => arg.ToString()));

            // Execute command
            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo(PeptdeepExecutablePath, $@"{CMD_FLOW_COMMAND} {args}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false
            };
            try
            {
                var filterStrings = new[]
                {
                    @"     ____             __  ____",
                    @"    / __ \___  ____  / /_/ __ \___  ___  ____",
                    @"   / /_/ / _ \/ __ \/ __/ / / / _ \/ _ \/ __ \",
                    @"  / ____/  __/ /_/ / /_/ /_/ /  __/  __/ /_/ /",
                    @" /_/    \___/ .___/\__/_____/\___/\___/ .___/",
                    @"           /_/                       /_/",
                    @"s/DiaNN\/Spectronaut/Skyline/"    // Replace DiaNN/Spectronaut with Skyline
                };

                pr.SilenceStatusMessageUpdates = true;  // Use FilteredUserMessageWriter to write process output instead of ProgressStatus.ChangeMessage()
                pr.ExpectedOutputLinesCount = 119;
                timer.Start();
                pr.Run(psi, string.Empty, progress, ref progressStatus, new FilteredUserMessageWriter(filterStrings), ProcessPriorityClass.BelowNormal, true);
                timer.Stop();
                string message = string.Format(ModelResources.AlphapeptdeepLibraryBuilder_ExecutePeptdeep_AlphaPeptDeep_finished_in__0__minutes__1__seconds_, timer.Elapsed.Minutes, timer.Elapsed.Seconds);
                Messages.WriteAsyncUserMessage(message);
                TotalExpectedLinesOfOutput += pr.ExpectedOutputLinesCount;
                TotalGeneratedLinesOfOutput += pr.OutputLinesGenerated;
            }
            catch (Exception ex)
            {
                throw new IOException(ModelResources.AlphapeptdeepLibraryBuilder_ExecutePeptdeep_Failed_to_build_library_by_executing_the_peptdeep_cmd_flow_command_, ex);
            }

        }

        public void TransformPeptdeepOutput(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.AlphapeptdeepLibraryBuilder_Importing_spectral_library));

            var result = new List<string>();

            using var reader = new DsvFileReader(OutputSpectraLibFilepath, TextUtil.SEPARATOR_TSV);

            // Transform table header
            var colNames = reader.FieldNames;
            var newColNames = new List<string>();
            foreach (var colName in colNames)
            {
                string newColName;
                if (!OpenSwathAssayLikeColName.TryGetValue(colName, out newColName))
                {
                    newColName = colName;
                }
                if (newColName != null)
                {
                    newColNames.Add(newColName);
                }
            }
            var header = string.Join(TextUtil.SEPARATOR_TSV_STR, newColNames);
            result.Add(header);

            // Transform table body line by line
            while (null != reader.ReadLine())
            {
                var line = new List<string>();
                string peptideWithMods = string.Empty;
                bool modifiedPeptide = false;
                foreach (var colName in colNames)
                {
                    var cell = reader.GetFieldByName(colName);
                    if (colName == MODIFIED_PEPTIDE)
                    {
                        var transformedCell = cell.Replace(TextUtil.UNDERSCORE, string.Empty)
                            .Replace(TextUtil.LEFT_SQUARE_BRACKET, TextUtil.LEFT_PARENTHESIS)
                            .Replace(TextUtil.RIGHT_SQUARE_BRACKET, TextUtil.RIGHT_PARENTHESIS);
                        line.Add(transformedCell);
                        if (transformedCell.Contains('('))
                        {
                            modifiedPeptide = true;
                        }
                        peptideWithMods = cell;
                    }
                    else if (colName == CCS)
                    {
                        AddDoubleCell(line, cell,
                            !modifiedPeptide || LibraryBuilderModificationSupport.PeptideHasOnlyCcsSupportedMod(peptideWithMods));
                    }
                    else if (colName == NORMALIZED_RT)
                    {
                        AddDoubleCell(line, cell,
                            !modifiedPeptide || LibraryBuilderModificationSupport.PeptideHasOnlyRtSupportedMod(peptideWithMods));
                    }
                    else if (colName != ION_MOBILITY)
                    {
                        line.Add(cell);
                    }
                }
                // Only add a row, if there are no modifications or all modifications at least support spectrum prediction
                if (!modifiedPeptide || LibraryBuilderModificationSupport.PeptideHasOnlyMs2SupportedMod(peptideWithMods))
                    result.Add(string.Join(TextUtil.SEPARATOR_TSV_STR, line));
            }

            // Write to new file
            File.WriteAllLines(TransformedOutputSpectraLibFilepath, result);
        }

        private void AddDoubleCell(List<string> line, string cell, bool allowedValue)
        {
            double valueToAdd;
            if (!allowedValue || !double.TryParse(cell.ToString(CultureInfo.InvariantCulture), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out valueToAdd))
            {
                valueToAdd = 0; // CONSIDER: Zero is a valid value for a normalized-RT
            }
            line.Add(valueToAdd.ToString(CultureInfo.InvariantCulture));
        }

        public void ImportSpectralLibrary(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            string[] inputFile = { TransformedOutputSpectraLibFilepath.ToLongPath() };
            string output = LibrarySpec.FilePath.ToLongPath();
            string incompleteBlibPath = BiblioSpecLiteSpec.GetRedundantName(output).ToLongPath();
            var build = new BlibBuild(incompleteBlibPath, inputFile);

            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.AlphapeptdeepLibraryBuilder_Importing_spectral_library));

            string[] ambiguous;
            bool completed = build.BuildLibrary(LibraryBuildAction.Create, progress, ref progressStatus, out ambiguous);

            if (ambiguous.Length > 0)
            {
                foreach (string msg in ambiguous)
                {
                    Messages.WriteAsyncUserMessage(msg);
                }
            }

            var blibFilter = new BlibFilter();
            // Build the final filtered library
            completed = completed && blibFilter.Filter(incompleteBlibPath, output, progress, ref progressStatus);

            if (completed)
            {
                Messages.WriteAsyncUserMessage(ModelResources.AlphapeptdeepLibraryBuilder_ImportSpectralLibrary_BlibBuild_completed_successfully_);
            }
            else
            {
                Messages.WriteAsyncUserMessage(ModelResources.AlphapeptdeepLibraryBuilder_ImportSpectralLibrary_BlibBuild_failed_to_complete_);
            }
            File.Delete(incompleteBlibPath);
        }
    }
}
