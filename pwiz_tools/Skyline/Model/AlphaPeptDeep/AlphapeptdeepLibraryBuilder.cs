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
using pwiz.Skyline.Model.Koina.Models;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.AlphaPeptDeep
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

    public class ModificationType
    {
        public ModificationType(string accession, string name, string comment)
        {
            Accession = accession;
            Name = name;
            Comment = comment;
        }
        public string Accession { get; private set; }
        public string Name { get; private set; }
        public string Comment { get; private set; }

        public string AlphaNameWithAminoAcid(string unmodifiedSequence, int index)
        {
            string modification = Name.Replace(@"(", "").Replace(@")", @"").Replace(@" ", @"@").Replace(@"Acetyl@N-term", @"Acetyl@Protein_N-term");
            char delimiter = '@';
            string[] name = modification.Split(delimiter);
            string alphaName = name[0] + @"@" + unmodifiedSequence[index];
            if (index == 0 && modification.EndsWith(@"term"))
            {
                alphaName = modification;
            }
            return alphaName;
        }
        public override string ToString() { return string.Format(ModelsResources.BuildPrecursorTable_ModificationType, Accession, Name, Comment); }
    }

    public class ModificationIndex
    {
        public ModificationIndex(int index, ModificationType modification)
        {
            Index = index;
            Modification = modification;
        }
        public ModificationType Modification { get; private set; }
        public int Index { get; private set; }

        public override string ToString()
        {
            return Index + @":" + Modification;
        }
    }


    public class AlphapeptdeepLibraryBuilder : IiRTCapableLibraryBuilder
    {
        private const string ALPHAPEPTDEEP = @"AlphaPeptDeep";
        private const string BLIB_BUILD = @"BlibBuild";
        private const string CMD_FLOW_COMMAND = @"cmd-flow";
        private const string EXPORT_SETTINGS_COMMAND = @"export-settings";
        private const string EXT_TSV = TextUtil.EXT_TSV;
        private const string INPUT = @"input";
        private const string LEFT_PARENTHESIS = TextUtil.LEFT_PARENTHESIS;
        private const string LEFT_SQUARE_BRACKET = TextUtil.LEFT_SQUARE_BRACKET;
        private const string LIBRARY_COMMAND = @"library";
        private const string MODIFIED_PEPTIDE = "ModifiedPeptide";
        private const string NORMALIZED_RT = "RT";
        private const string ION_MOBILITY = "IonMobility";
        private const string ION_MOBILITY_UNITS = "IonMobilityUnits";
        private const string CCS = "CCS";
        private const string COLLISIONAL_CROSS_SECTION = "CollisionalCrossSection";
        private const string PREC_CHARGE = "PrecursorCharge";
        private const string PREC_MZ = "PrecursorMz";
        private const string MODS = @"mods";
        private const string OUTPUT = @"output";
        private const string OUTPUT_MODELS = @"output_models";
        private const string OUTPUT_SPECTRAL_LIB_FILE_NAME = @"predict.speclib.tsv";
        private const string OUTPUT_SPECTRAL_LIBS = @"output_spectral_libs";
        private const string PEPTDEEP_EXECUTABLE = "peptdeep.exe";
        private const string RIGHT_PARENTHESIS = TextUtil.RIGHT_PARENTHESIS;
        private const string RIGHT_SQUARE_BRACKET = TextUtil.RIGHT_SQUARE_BRACKET;
        private const string SEMICOLON = TextUtil.SEMICOLON;
        private const string SETTINGS_FILE_NAME = @"settings.yaml";
        private const string SPACE = TextUtil.SPACE;
        private const string TAB = "\t";
        private const string TRANSFORMED_OUTPUT_SPECTRAL_LIB_FILE_NAME = @"predict_transformed.speclib.tsv";
        private const string UNDERSCORE = TextUtil.UNDERSCORE;
        private const double CCS_IM_COEFF = 1059.62245;  // https://github.com/MannLabs/alphabase/blob/main/alphabase/constants/const_files/common_constants.yaml
        private const double N2_MASS = 28.0;  // amu of N2 carrier gas

        public string AmbiguousMatchesMessage
        {
            get { return null; }
        }
        public IrtStandard IrtStandard { get; private set; }

        public string BuildCommandArgs
        {
            get { return null; }
        }
        public string BuildOutput
        {
            get { return null; }
        }

        public LibrarySpec LibrarySpec { get; private set; }
       

        private string BuilderLibraryPath
        {
            get => TransformedOutputSpectraLibFilepath; 
        }

        string ILibraryBuilder.BuilderLibraryPath
        {
            get => BuilderLibraryPath;
        }


        public LibraryHelper LibraryHelper { get; private set; }
        private string PythonVirtualEnvironmentScriptsDir { get; }
        private string PeptdeepExecutablePath => Path.Combine(PythonVirtualEnvironmentScriptsDir, PEPTDEEP_EXECUTABLE);

        private string _rootDir;
        private string RootDir
        {
            get => _rootDir;
            set => _rootDir = value;
        }

        private string SettingsFilePath => Path.Combine(RootDir, SETTINGS_FILE_NAME);
        private string InputFileName => INPUT + UNDERSCORE + EXT_TSV; 
        private string InputFilePath => Path.Combine(RootDir, InputFileName);
        private string OutputModelsDir => Path.Combine(RootDir, OUTPUT_MODELS);
        private string OutputSpectralLibsDir => Path.Combine(RootDir, OUTPUT_SPECTRAL_LIBS);
        private string OutputSpectraLibFilepath =>  Path.Combine(OutputSpectralLibsDir, OUTPUT_SPECTRAL_LIB_FILE_NAME);
        private string TransformedOutputSpectraLibFilepath => Path.Combine(OutputSpectralLibsDir, TRANSFORMED_OUTPUT_SPECTRAL_LIB_FILE_NAME);

        public string ToolName { get; }
        public SrmDocument Document { get; private set; }

        /// <summary>
        /// The peptdeep cmd-flow command is how we can pass arguments that will override the settings.yaml file.
        /// This is how the peptdeep CLI supports command line arguments.
        /// </summary>
        private IList<ArgumentAndValue> CmdFlowCommandArguments =>
            new[]
            {
                new ArgumentAndValue(@"task_workflow", @"library"),
                new ArgumentAndValue(@"settings_yaml", SettingsFilePath, true),
                new ArgumentAndValue(@"PEPTDEEP_HOME", RootDir, true),
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

        public AlphapeptdeepLibraryBuilder(string libName, string libOutPath, string pythonVirtualEnvironmentScriptsDir,
            SrmDocument document, IrtStandard irtStandard)

        {
            Document = document;
            IrtStandard = irtStandard;
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            PythonVirtualEnvironmentScriptsDir = pythonVirtualEnvironmentScriptsDir;

            ToolName = @"AlphaPeptDeep";

            if (RootDir == null)
            {
                RootDir = Path.GetDirectoryName(libOutPath);
            }

            if (RootDir != null)
            {
                RootDir = Path.Combine(RootDir, libName);
                //Directory.CreateDirectory(RootDir);
                InitializeLibraryHelper(RootDir);
            }
        }

        private void InitializeLibraryHelper(string rootDir)
        {
            if (LibraryHelper == null)
            {
                LibraryHelper = new LibraryHelper(rootDir, ALPHAPEPTDEEP);
                RootDir = LibraryHelper.GetRootDir(rootDir, ALPHAPEPTDEEP);
                LibraryHelper.InitializeLibraryHelper(InputFilePath);
            }
        }
        public bool BuildLibrary(IProgressMonitor progress)
        {
            IProgressStatus progressStatus = new ProgressStatus();

            try
            {
                InitializeLibraryHelper(RootDir);
                RunAlphapeptdeep(progress, ref progressStatus);
                progress.UpdateProgress(progressStatus = progressStatus.Complete());
                LibraryHelper = null;
                return true;
            }
            catch (Exception exception)
            {
                progress.UpdateProgress(progressStatus.ChangeErrorException(exception));
                LibraryHelper = null;
                return false;
            }
        }

        private void RunAlphapeptdeep(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            // DSHTEYN:  These should be better balanced as of May 2nd 2025
            var segmentEndPercentages = new[] { 5, 10, 15, 95 };
            progressStatus = progressStatus.ChangeSegments(0, ImmutableList<int>.ValueOf( segmentEndPercentages));
            LibraryHelper.PreparePrecursorInputFile(Document, progress, ref progressStatus, @"AlphaPeptDeep",
                IrtStandard);
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

            // generate template settings.yaml file
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
                pr.EnableImmediateLog = false;
                pr.EnableRunningTimeMessage = false;
                pr.ExpectedOutputLinesCount = 213;
                pr.Run(psi, string.Empty, progress, ref progressStatus, ProcessPriorityClass.BelowNormal, true);
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
            // compose peptdeep cmd-flow command arguments to build library
            var args = TextUtil.SpaceSeparate(CmdFlowCommandArguments.Select(arg => arg.ToString()));

            // execute command
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
                    @"           /_/                       /_/"
                };

                pr.EnableImmediateLog = true;
                pr.ExpectedOutputLinesCount = 119;
                timer.Start();
                pr.Run(psi, string.Empty, progress, ref progressStatus, new FilteredStringWriter(ImmutableList<string>.ValueOf(filterStrings), pr.EnableImmediateLog), ProcessPriorityClass.BelowNormal, true);
                timer.Stop();
                string message = string.Format(ModelResources.Alphapeptdeep_Process_Finished_in_time, timer.Elapsed.Minutes, timer.Elapsed.Seconds);
                Messages.WriteAsyncUserMessage(message);

            }
            catch (Exception ex)
            {
                throw new IOException(ModelResources.AlphapeptdeepLibraryBuilder_ExecutePeptdeep_Failed_to_build_library_by_executing_the_peptdeep_cmd_flow_command_, ex);
            }

        }

        private void TransformPeptdeepOutput(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.AlphapeptdeepLibraryBuilder_Importing_spectral_library));

            var result = new List<string>();
            var reader = new DsvFileReader(OutputSpectraLibFilepath, TextUtil.SEPARATOR_TSV);

            // transform table header
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
            var header = string.Join(TAB, newColNames);
            result.Add(header);

            // transform table body line by line
            while (null != reader.ReadLine())
            {
                var line = new List<string>();
                
                foreach (var colName in colNames)
                {
                    var cell = reader.GetFieldByName(colName);
                    if (colName == MODIFIED_PEPTIDE)
                    {
                        var transformedCell = cell.Replace(UNDERSCORE, String.Empty)
                            .Replace(LEFT_SQUARE_BRACKET, LEFT_PARENTHESIS)
                            .Replace(RIGHT_SQUARE_BRACKET, RIGHT_PARENTHESIS);
                        line.Add(transformedCell);
                    }
                    else if (colName == NORMALIZED_RT)
                    {
                        double transformedCell = double.Parse(cell.ToString(CultureInfo.InvariantCulture),
                            CultureInfo.InvariantCulture);// * 100;
                        line.Add(transformedCell.ToString(CultureInfo.InvariantCulture));
                    }
                    else if (colName != ION_MOBILITY)
                    {
                        line.Add(cell);
                    }
                }
                result.Add(string.Join(TAB, line));
            }

            // write to new file
            File.WriteAllLines(TransformedOutputSpectraLibFilepath, result);
        }

        private void ImportSpectralLibrary(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            string[] inputFile = { TransformedOutputSpectraLibFilepath };
            string incompleteBlibPath = BiblioSpecLiteSpec.GetRedundantName(LibrarySpec.FilePath);
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

            BlibFilter blibFilter = new BlibFilter();
            // Build the final filtered library
            completed = completed &&
                        blibFilter.Filter(incompleteBlibPath, LibrarySpec.FilePath, progress, ref progressStatus);

            if (completed)
            {
                Messages.WriteAsyncUserMessage(ModelResources.Alphapeptdeep_Blib_completed_ok);
            }
            else
            {
                Messages.WriteAsyncUserMessage(ModelResources.Alphapeptdeep_Blib_failed_to_complete);
            }
            File.Delete(incompleteBlibPath);
        }
    }
}
