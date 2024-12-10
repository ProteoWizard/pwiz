using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Koina.Models;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Util.Extensions;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.AlphaPeptDeep
{
    public class ArgumentAndValue
    {
        public ArgumentAndValue(string name, string value)
        {
            Name = name;
            Value = value;
        }
        public string Name { get; private set; }
        public string Value { get; private set; }

        public override string ToString() { return @"--" + Name + TextUtil.SPACE + Value; }
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

        public override string ToString() { return string.Format(ModelsResources.AlphaPeptDeep_BuildPrecursorTable_ModificationType, Accession, Name, Comment); }
    }


    public class AlphapeptdeepLibraryBuilder : IiRTCapableLibraryBuilder
    {
        private const string ALPHAPEPTDEEP = @"alphapeptdeep";
        private const string BLIB_BUILD = "BlibBuild";
        private const string CHARGE = @"charge";
        private const string CMD_FLOW_COMMAND = @"cmd-flow";
        private const string EXPORT_SETTINGS_COMMAND = @"export-settings";
        private const string EXT_TSV = TextUtil.EXT_TSV;
        private const string INPUT = @"input";
        private const string LEFT_PARENTHESIS = TextUtil.LEFT_PARENTHESIS;
        private const string LEFT_SQUARE_BRACKET = TextUtil.LEFT_SQUARE_BRACKET;
        private const string LIBRARY_COMMAND = @"library";
        private const string MOD_SITES = @"mod_sites";
        private const string MODIFIED_PEPTIDE = "ModifiedPeptide";
        private const string MODS = @"mods";
        private const string OUTPUT = @"output";
        private const string OUTPUT_MODELS = @"output_models";
        private const string OUTPUT_SPECTRAL_LIB_FILE_NAME = @"predict.speclib.tsv";
        private const string OUTPUT_SPECTRAL_LIBS = @"output_spectral_libs";
        private const string PEPTDEEP_EXECUTABLE = "peptdeep.exe";
        private const string RIGHT_PARENTHESIS = TextUtil.RIGHT_PARENTHESIS;
        private const string RIGHT_SQUARE_BRACKET = TextUtil.RIGHT_SQUARE_BRACKET;
        private const string SEMICOLON = TextUtil.SEMICOLON;
        private const string SEQUENCE = @"sequence";
        private const string SETTINGS_FILE_NAME = @"settings.yaml";
        private const string SPACE = TextUtil.SPACE;
        private const string TAB = "\t";
        private const string TRANSFORMED_OUTPUT_SPECTRAL_LIB_FILE_NAME = @"predict_transformed.speclib.tsv";
        private const string UNDERSCORE = TextUtil.UNDERSCORE;

        /// <summary>
        /// List of UniMod Modifications available
        /// </summary>
        internal static readonly IList<ModificationType> AlphapeptdeepModificationName = populateUniModList();
        private static IList<ModificationType> populateUniModList()
        {
            IList<ModificationType> modList = new List<ModificationType>();
            for (int m = 0; m < UniModData.UNI_MOD_DATA.Length; m++)
            {
                if (!UniModData.UNI_MOD_DATA[m].ID.HasValue)
                    continue;
                var accession = UniModData.UNI_MOD_DATA[m].ID.Value + ":" + UniModData.UNI_MOD_DATA[m].AAs;
                var name = UniModData.UNI_MOD_DATA[m].Name;
                var formula = UniModData.UNI_MOD_DATA[m].Formula;
                modList.Append(new ModificationType(accession, name, formula));
            }
            return modList;
        }

        private static readonly IEnumerable<string> PrecursorTableColumnNames =
            new[] { SEQUENCE, MODS, MOD_SITES, CHARGE };
        private string PythonVirtualEnvironmentScriptsDir { get; }
        private string PeptdeepExecutablePath => Path.Combine(PythonVirtualEnvironmentScriptsDir, PEPTDEEP_EXECUTABLE);
        private string RootDir => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), ALPHAPEPTDEEP);
        private string SettingsFilePath => Path.Combine(RootDir, SETTINGS_FILE_NAME);
        private string InputFileName => INPUT + UNDERSCORE + Convert.ToBase64String(Encoding.ASCII.GetBytes(Document.DocumentHash)) + EXT_TSV;
        private string InputFilePath => Path.Combine(RootDir, InputFileName);
        private string OutputModelsDir => Path.Combine(RootDir, OUTPUT_MODELS);
        private string OutputSpectralLibsDir => Path.Combine(RootDir, OUTPUT_SPECTRAL_LIBS);
        private string OutputSpectraLibFilepath => Path.Combine(OutputSpectralLibsDir, OUTPUT_SPECTRAL_LIB_FILE_NAME);
        private string TransformedOutputSpectraLibFilepath => Path.Combine(OutputSpectralLibsDir, TRANSFORMED_OUTPUT_SPECTRAL_LIB_FILE_NAME);
        private SrmDocument Document { get; }
        /// <summary>
        /// The peptdeep cmd-flow command is how we can pass arguments that will override the settings.yaml file.
        /// This is how the peptdeep CLI supports command line arguments.
        /// </summary>
        private IList<ArgumentAndValue> CmdFlowCommandArguments =>
            new []
            {
                new ArgumentAndValue(@"task_workflow", @"library"),
                new ArgumentAndValue(@"settings_yaml", SettingsFilePath),
                new ArgumentAndValue(@"PEPTDEEP_HOME", RootDir),
                new ArgumentAndValue(@"transfer--model_output_folder", OutputModelsDir),
                new ArgumentAndValue(@"library--infile_type", @"precursor_table"),
                new ArgumentAndValue(@"library--infiles", InputFilePath),
                new ArgumentAndValue(@"library--output_folder", OutputSpectralLibsDir),
                new ArgumentAndValue(@"library--output_tsv--enabled", @"True"),
                new ArgumentAndValue(@"library--output_tsv--translate_mod_to_unimod_id", @"True"),
                new ArgumentAndValue(@"library--decoy", @"diann")
            };

        private Dictionary<string, string> OpenSwathAssayColName =>
            new Dictionary<string, string>()
            {
                { @"RT", @"NormalizedRetentionTime" },
                { @"ModifiedPeptide", @"ModifiedPeptideSequence" },
                { @"FragmentMz", @"ProductMz" },
                { @"RelativeIntensity", @"LibraryIntensity" },
                { @"FragmentNumber", @"FragmentSeriesNumber" }
            };

        public AlphapeptdeepLibraryBuilder(string libName, string libOutPath, string pythonVirtualEnvironmentScriptsDir, SrmDocument document)
        { 
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            PythonVirtualEnvironmentScriptsDir = pythonVirtualEnvironmentScriptsDir;
            Document = document;
            Directory.CreateDirectory(RootDir);
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

        private void RunAlphapeptdeep(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progressStatus = progressStatus.ChangeSegments(0, 5);

            PrepareInputFile(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();

            PrepareSettingsFile(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();

            ExecutePeptdeep(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();

            TransformPeptdeepOutput(progress, ref progressStatus);
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
                        var msg = string.Format(ModelsResources.AlphaPeptDeep_BuildPrecursorTable_UnsupportedModification, modifiedSequence, mod.Name);
                        Messages.WriteAsyncUserMessage(msg);
                        continue;
                    }

                    var unimodIdAA = mod.UnimodIdAA;
                    var modNames = AlphapeptdeepModificationName.Where(m => m.Accession == unimodIdAA);
                    if (modNames.Count() == 0)
                    {
                        var msg = string.Format(ModelsResources.AlphaPeptDeep_BuildPrecursorTable_Unimod_UnsupportedModification, modifiedSequence, mod.Name, unimodIdAA);
                        Messages.WriteAsyncUserMessage(msg);
                        continue;
                    }

                    var modName = modNames.Cast<ModificationType>().Single().Name;
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
                args.Append(arg).Append(SPACE);
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

        private void TransformPeptdeepOutput(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Importing spectral library")
                .ChangePercentComplete(0));

            var result = new List<string>();
            var reader = new DsvFileReader(OutputSpectraLibFilepath, TextUtil.SEPARATOR_TSV);

            // transform table header
            var colNames = reader.FieldNames;
            var newColNames = new List<string>();
            foreach(var colName in colNames)
            {
                string newColName;
                if (!OpenSwathAssayColName.TryGetValue(colName, out newColName))
                {
                    newColName = colName;
                }
                newColNames.Add(newColName);
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
                    else
                    {
                        line.Add(cell);
                    }
                }
                result.Add(string.Join(TAB, line));
            }

            // write to new file
            File.WriteAllLines(TransformedOutputSpectraLibFilepath, result);

            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
        }

        private void ImportSpectralLibrary(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Importing spectral library")
                .ChangePercentComplete(0));
            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo(BLIB_BUILD, $@"-o -i {LibrarySpec.Name} {TransformedOutputSpectraLibFilepath} {LibrarySpec.FilePath}")
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
                throw new Exception(@"Failed to import spectral library by executing BlibBuild", ex);
            }
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