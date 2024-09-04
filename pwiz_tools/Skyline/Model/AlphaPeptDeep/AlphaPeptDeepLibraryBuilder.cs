using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.AlphaPeptDeep
{
    public class AlphapeptdeepLibraryBuilder : IiRTCapableLibraryBuilder
    {
        private const string PEPTDEEP_EXECUTABLE = "peptdeep.exe";
        private const string SEMICOLON = TextUtil.SEMICOLON;
        private const string SPACE = TextUtil.SPACE;
        private const string ALPHAPEPTDEEP = @"alphapeptdeep";
        private const string INPUT = @"input";
        private const string OUTPUT = @"output";
        private const string EXT_TSV = TextUtil.EXT_TSV;
        private const string UNDERSCORE = TextUtil.UNDERSCORE;
        private const string TAB = "\t";
        private const string SEQUENCE = @"sequence";
        private const string MODS = @"mods";
        private const string MOD_SITES = @"mod_sites";
        private const string CHARGE = @"charge";
        private const string SETTINGS_FILE_NAME = @"settings.yaml";
        private const string OUTPUT_MODELS = @"output_models";
        private const string OUTPUT_SPECTRAL_LIBS = @"output_spectral_libs";
        private const string OUTPUT_SPECTRAL_LIB_FILE_NAME = @"predict.speclib.tsv";
        private const string EXPORT_SETTINGS_COMMAND = @"export-settings";
        private const string LIBRARY_COMMAND = @"library";
        private const string CMD_FLOW_COMMAND = @"cmd-flow";
        private const string BLIB_BUILD = "BlibBuild";

        /// <summary>
        /// key: unimod ID, value: modification name supported by Alphapeptdeep
        /// </summary>
        private static readonly Dictionary<int, string> AlphapeptdeepModificationName = new Dictionary<int, string>()
        {
            {4, @"Carbamidomethyl@C"},
            {21, @"Phospho@S"},
            {35, @"Oxidation@M"},
        };
        private static readonly IEnumerable<string> PrecursorTableColumnNames = new[] { SEQUENCE, MODS, MOD_SITES, CHARGE };

        private string PythonVirtualEnvironmentScriptsDir { get; }
        private string PeptdeepExecutablePath => Path.Combine(PythonVirtualEnvironmentScriptsDir, PEPTDEEP_EXECUTABLE);
        private string RootDir => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), ALPHAPEPTDEEP);
        private string SettingsFilePath => Path.Combine(RootDir, SETTINGS_FILE_NAME);
        private string InputFileName => INPUT + UNDERSCORE +Document.DocumentHash +EXT_TSV;
        private string InputFilePath => Path.Combine(RootDir, InputFileName);
        private string OutputModelsDir => Path.Combine(RootDir, OUTPUT_MODELS);
        private string OutputSpectralLibsDir => Path.Combine(RootDir, OUTPUT_SPECTRAL_LIBS);
        private string OutputSpectraLibFilepath => Path.Combine(OutputSpectralLibsDir, OUTPUT_SPECTRAL_LIB_FILE_NAME);
        private SrmDocument Document { get; }
        /// <summary>
        /// The peptdeep cmd-flow command is how we can pass arguments that will override the settings.yaml file.
        /// This is how the peptdeep CLI supports command line arguments.
        /// </summary>
        private Dictionary<string, string> CmdFlowCommandArguments =>
            new Dictionary<string, string>()
            {
                {@"--task_workflow", @"library"},
                {@"--settings_yaml", SettingsFilePath},
                {@"--PEPTDEEP_HOME", RootDir},
                {@"--transfer--model_output_folder", OutputModelsDir},
                {@"--library--infile_type", @"precursor_table"},
                {@"--library--infiles", InputFilePath},
                {@"--library--output_folder", OutputSpectralLibsDir},
                {@"--library--output_tsv--enabled", @"True"},
                {@"--library--output_tsv--translate_mod_to_unimod_id", @"True"},
                {@"--library--decoy", @"diann"}
            };

        public AlphapeptdeepLibraryBuilder(string libName, string libOutPath, string pythonVirtualEnvironmentScriptsDir, SrmDocument document)
        { 
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            PythonVirtualEnvironmentScriptsDir = pythonVirtualEnvironmentScriptsDir;
            Document = document;
            CreateDirIfNotExist(RootDir);
        }

        public string AmbiguousMatchesMessage
        {
            //TODO(xgwang): implement
            get { return null; }
        }

        public IrtStandard IrtStandard
        {
            //TODO(xgwang): implement
            get { return null; }
        }

        public string BuildCommandArgs
        {
            //TODO(xgwang): implement
            get { return null; }
        }

        public string BuildOutput
        {
            //TODO(xgwang): implement
            get { return null; }
        }

        public LibrarySpec LibrarySpec { get; private set; }

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

        private static string CreateDirIfNotExist(string dir)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        private void RunAlphapeptdeep(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progressStatus = progressStatus.ChangeSegments(0, 4);

            PrepareInputFile(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();

            PrepareSettingsFile(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();

            ExecutePeptdeep(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();

            ImportSpectralLibrary(progress, ref progressStatus);
        }

        private void PrepareInputFile(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Preparing input file")
                .ChangePercentComplete(0));
            
            var precursorTable = GetPrecursorTable();
            File.WriteAllLines(InputFilePath, precursorTable);

            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
        }

        private IEnumerable<string> GetPrecursorTable()
        {
            var result = new List<string>();
            var header = string.Join(TAB, PrecursorTableColumnNames);
            result.Add(header);

            // Build precursor table row by row
            foreach (var peptide in Document.Peptides)
            {
                var unmodifiedSequence = peptide.Peptide.Sequence;
                var modifiedSequence = ModifiedSequence
                    .GetModifiedSequence(Document.Settings, peptide, IsotopeLabelType.light);
                var modsBuilder = new StringBuilder();
                var modSitesBuilder = new StringBuilder();

                for (var i = 0; i < modifiedSequence.ExplicitMods.Count; i++)
                {
                    var mod = modifiedSequence.ExplicitMods[i];
                    if (!mod.UnimodId.HasValue)
                    {
                        // TODO(xgwang): update this exception to an Alphapeptdeep specific one
                        throw new Exception(
                            @$"Modification {mod} is missing unimod ID, which is required by AlphapeptdeepLibraryBuilder");
                    }

                    var unimodId = mod.UnimodId.Value;
                    if (!AlphapeptdeepModificationName.TryGetValue(unimodId, out var modName))
                    {
                        // TODO(xgwang): update this exception to an Alphapeptdeep specific one
                        throw new Exception(
                            @$"Modification with unimod ID of {unimodId} is not yet supported by Alphapeptdeep. Please remove such modifications and try again.");

                    }
                    modsBuilder.Append(modName);
                    modSitesBuilder.Append((mod.IndexAA + 1).ToString()); // + 1 because alphapeptdeep mod_site number starts from 1 as the first amino acid
                    if (i != modifiedSequence.ExplicitMods.Count - 1)
                    {
                        modsBuilder.Append(SEMICOLON);
                        modSitesBuilder.Append(SEMICOLON);
                    }
                }

                foreach (var charge in peptide.TransitionGroups
                             .Select(transitionGroup => transitionGroup.PrecursorCharge).Distinct())
                {
                    result.Add(string.Join(TAB, new[]
                        {
                            unmodifiedSequence, modsBuilder.ToString(), modSitesBuilder.ToString(), charge.ToString()
                        })
                    );
                }
            }

            return result;
        }

        private void PrepareSettingsFile(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Preparing settings file")
                .ChangePercentComplete(0));

            // generate template settings.yaml file
            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo(PeptdeepExecutablePath, $@"{EXPORT_SETTINGS_COMMAND} {SettingsFilePath}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false
            };
            try
            {
                pr.Run(psi, string.Empty, progress, ref progressStatus, ProcessPriorityClass.BelowNormal, true);
            }
            catch (Exception ex)
            {
                // TODO(xgwang): update this exception to an Alphapeptdeep specific one
                throw new Exception(@"Failed to generate settings.yaml file by executing the peptdeep export-settings command.", ex);
            }

            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
        }

        private void ExecutePeptdeep(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Executing peptdeep")
                .ChangePercentComplete(0));

            // compose peptdeep cmd-flow command arguments to build library
            var args = new StringBuilder();
            foreach (var arg in CmdFlowCommandArguments)
            {
                args.Append(arg.Key);
                args.Append(SPACE);
                args.Append(arg.Value);
                args.Append(SPACE);
            }

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
                pr.Run(psi, string.Empty, progress, ref progressStatus, ProcessPriorityClass.BelowNormal, true);
            }
            catch (Exception ex)
            {
                // TODO(xgwang): update this exception to an Alphapeptdeep specific one
                throw new Exception(@"Failed to build library by executing the peptdeep cmd-flow command.", ex);
            }

            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
        }

        private void ImportSpectralLibrary(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Importing spectral library")
                .ChangePercentComplete(0));
            //TODO(xgwang): implement
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
        }

        //TODO(xgwang): implement
        public bool IsCanceled => false;

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            //TODO(xgwang): implement
            return UpdateProgressResponse.normal;
        }

        public bool HasUI => false;
    }
}