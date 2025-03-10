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
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Koina.Models;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.AlphaPeptDeep
{

    public class LibraryHelper 
    {
        private const string SEQUENCE = @"sequence";
        private const string MODS = @"mods";
        private const string MOD_SITES = @"mod_sites";
        private const string CHARGE = @"charge";
        private const string TAB = "\t";
        private const string PRECURSOR = @"Precursor";
        private const string PEPTIDE = @"Peptide";
        private const string PRECURSOR_CHARGE = @"Precursor Charge";
        private const string ISOTOPE_LABEL_TYPE = @"Isotope Label Type";
        private const string PRECURSOR_MZ = @"Precursor Mz";
        private const string MODIFIED_SEQUENCE = @"Modified Sequence";
        private const string PRECURSOR_EXPLICIT_COLLISION_ENERGY = @"Precursor Explicit Collision Energy";
        private const string PRECURSOR_NOTE = @"Precursor Note";
        private const string LIBRARY_NAME = @"Library Name";
        private const string LIBRARY_TYPE = @"Library Type";
        private const string LIBRARY_PROBABILITY_SCORE = @"Library Probability Score";
        private const string PEPTIDE_MODIFIED_SEQUENCE_UNIMOD_IDS = @"Peptide Modified Sequence Unimod Ids";
        public string InputFilePath { get; private set; }
        private static readonly IEnumerable<string> PrecursorTableColumnNames = new[] { SEQUENCE, MODS, MOD_SITES, CHARGE };
        private static readonly IEnumerable<string> PrecursorTableColumnNamesCarafe = 
            new[] 
            { 
                PRECURSOR, PEPTIDE, PRECURSOR_CHARGE, ISOTOPE_LABEL_TYPE, PRECURSOR_MZ, MODIFIED_SEQUENCE, PRECURSOR_EXPLICIT_COLLISION_ENERGY,
                PRECURSOR_NOTE, LIBRARY_NAME, LIBRARY_TYPE, LIBRARY_PROBABILITY_SCORE, PEPTIDE_MODIFIED_SEQUENCE_UNIMOD_IDS
            };

        private static IList<ModificationIndex> carafeSupportedModificationIndices =>
            new[]
            {
                new ModificationIndex(4, new ModificationType(@"4", @"Carbamidomethyl", @"H(3) C(2) N O")),
                new ModificationIndex(21, new ModificationType(@"21", @"Phospho", @"H O(3) P")),
                new ModificationIndex(35, new ModificationType(@"35", @"Oxidation", @"O"))

            };

        internal static readonly IList<ModificationType> CarafeSupportedModificationNames = populateUniModList(carafeSupportedModificationIndices);

        /// <summary>
        /// List of UniMod Modifications available
        /// </summary>
        internal static readonly IList<ModificationType> AlphapeptdeepModificationNames = populateUniModList(null);
        private static IList<ModificationType> populateUniModList(IList<ModificationIndex> supportedList)
        {
            IList<ModificationType> modList = new List<ModificationType>();
            for (int m = 0; m < UniModData.UNI_MOD_DATA.Length; m++)
            {
                if (!UniModData.UNI_MOD_DATA[m].ID.HasValue || 
                    (supportedList != null && 
                     supportedList.Where(x => x.Index == UniModData.UNI_MOD_DATA[m].ID.Value).ToArray().SingleOrDefault() == null) )
                    continue;

                var accession = UniModData.UNI_MOD_DATA[m].ID.Value + @":" + UniModData.UNI_MOD_DATA[m].Name;
                var name = UniModData.UNI_MOD_DATA[m].Name;
                var formula = UniModData.UNI_MOD_DATA[m].Formula;
                modList.Add(new ModificationType(accession, name, formula));
            }
            return modList;
        }
        public LibraryHelper(string inputFilePath)
        {
            InputFilePath = inputFilePath;

        }
        public bool PrepareInputFile(SrmDocument Document, IProgressMonitor progress, ref IProgressStatus progressStatus, string toolName)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Preparing input file")
                .ChangePercentComplete(0));

            var warningMods = GetWarningMods(Document, toolName);

            bool write = true;
            if (warningMods.Count > 0)
            {
                string warningModString = string.Join(Environment.NewLine, warningMods);
                AlertDlg warnMessageDlg =
                    new AlertDlg(
                        string.Format(ModelResources.Alphapeptdeep_Warn_unknown_modification,
                            warningModString), MessageBoxButtons.OKCancel);
                var warnModChoice = warnMessageDlg.ShowDialog();
                                              
                if (warnModChoice == DialogResult.Cancel)
                {
                    write = false;

                }
                else if (warnModChoice == DialogResult.OK)
                {
                    // Attempt to build
                    write = true;
                }
            }

            if (write)
            {
                var precursorTable = GetPrecursorTable(Document, toolName, warningMods);
                File.WriteAllLines(InputFilePath, precursorTable);
            }
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
            
            return write;
        }

        public static IEnumerable<Tuple<ModifiedSequence, int>> GetPrecursors(SrmDocument document)
        {
            foreach (var peptideDocNode in document.Peptides)
            {
                var modifiedSequence =
                    ModifiedSequence.GetModifiedSequence(document.Settings, peptideDocNode, IsotopeLabelType.light);
                foreach (var charge in peptideDocNode.TransitionGroups
                             .Select(transitionGroup => transitionGroup.PrecursorCharge).Distinct())
                {
                    yield return Tuple.Create(modifiedSequence, charge);
                }
            }
        }

        public IEnumerable<string> GetPrecursorTable(SrmDocument Document, string toolName, List<string> warningMods = null)
        {
            var result = new List<string>();
            string header;
            bool alphapeptDeepFormat = toolName.Equals(@"alphapeptdeep");

            if (alphapeptDeepFormat)
                header = string.Join(TAB, PrecursorTableColumnNames);
            else if (toolName.Equals(@"carafe"))
                header = string.Join(TAB, PrecursorTableColumnNamesCarafe);
            else
                header = string.Join(TAB, PrecursorTableColumnNamesCarafe);


            result.Add(header);

            // Build precursor table row by row
            foreach (var peptide in Document.Peptides)
            {
                var unmodifiedSequence = peptide.Peptide.Sequence;
                var modifiedSequence = ModifiedSequence.GetModifiedSequence(Document.Settings, peptide, IsotopeLabelType.light);
                var modsBuilder = new StringBuilder();
                var modSitesBuilder = new StringBuilder();
                bool unsupportedModification = false;
           
                var ModificationNames =
                    alphapeptDeepFormat ? AlphapeptdeepModificationNames : CarafeSupportedModificationNames;
                
                for (var i = 0; i < modifiedSequence.ExplicitMods.Count; i++)
                {
                    var mod = modifiedSequence.ExplicitMods[i];
                    var modWarns = warningMods.Where(m => m == mod.Name).ToArray();
                    if (!mod.UnimodId.HasValue && modWarns.Length == 0)
                    {
                        var msg = string.Format(ModelsResources.BuildPrecursorTable_UnsupportedModification, modifiedSequence, mod.Name, toolName);
                        Messages.WriteAsyncUserMessage(msg);
                        unsupportedModification = true;
                        continue;
                    }

                    var unimodIdAA = mod.UnimodIdAA;
                    var modNames = ModificationNames.Where(m => m.Accession == unimodIdAA).ToArray();
                   
                    if (modNames.Length == 0 && modWarns.Length == 0 )
                    {
                        var msg = string.Format(ModelsResources.BuildPrecursorTable_Unimod_UnsupportedModification, modifiedSequence, mod.Name, unimodIdAA, toolName);
                        Messages.WriteAsyncUserMessage(msg);
                        unsupportedModification = true;

                        continue;
                    }
                    if (modNames.Length == 0) continue;

                    string modName = "";

                    if (alphapeptDeepFormat) 
                    {
                        modName = modNames.Single().AlphaNameWithAminoAcid(unmodifiedSequence, mod.IndexAA);        
                    }
                    else
                    {
                        modName = modNames.Single().Name;
                    }

                    modsBuilder.Append(modName);
                    modSitesBuilder.Append((mod.IndexAA + 1).ToString()); // + 1 because alphapeptdeep mod_site number starts from 1 as the first amino acid
                    if (i != modifiedSequence.ExplicitMods.Count - 1)
                    {
                        modsBuilder.Append(TextUtil.SEMICOLON);
                        modSitesBuilder.Append(TextUtil.SEMICOLON);
                    }
                }
                if (unsupportedModification) 
                    continue;
                
                foreach (var charge in peptide.TransitionGroups
                             .Select(transitionGroup => transitionGroup.PrecursorCharge).Distinct())
                {
                    if (alphapeptDeepFormat)
                    {
                        result.Add(string.Join(TAB, new[]
                            {
                                unmodifiedSequence, modsBuilder.ToString(), modSitesBuilder.ToString(), charge.ToString()
                            })
                        );
                    }
                    else
                    {
                        var docNode = peptide.TransitionGroups.Where(group => group.PrecursorCharge == charge).FirstOrDefault();
                        if (docNode == null)
                        {
                            continue;
                        }
                        var precursor = LabelPrecursor(docNode.TransitionGroup, docNode.PrecursorMz, string.Empty);
                        var collisionEnergy = docNode.ExplicitValues.CollisionEnergy != null ? docNode.ExplicitValues.CollisionEnergy.ToString() : @"#N/A";
                        var note = docNode.Annotations.Note != null ? docNode.Annotations.Note : @"#N/A";
                        var libraryName = docNode.LibInfo != null && docNode.LibInfo.LibraryName != null ? docNode.LibInfo.LibraryName : @"#N/A";
                        var libraryType = docNode.LibInfo != null && docNode.LibInfo.LibraryTypeName != null ? docNode.LibInfo.LibraryTypeName : @"#N/A";
                        var libraryScore = docNode.LibInfo != null && docNode.LibInfo.Score != null ? docNode.LibInfo.Score.ToString() : @"#N/A";
                        var unimodSequence = modifiedSequence.ToString();

                        result.Add(string.Join(TAB, new[]
                            {
                                precursor, unmodifiedSequence, charge.ToString(), docNode.LabelType.ToString(),
                                docNode.PrecursorMz.ToString(), unimodSequence, collisionEnergy,
                                note, libraryName, libraryType, libraryScore, modifiedSequence.UnimodIds
                            })
                        );
                    }
                }
            }

            return result;
        }
        public List<string> GetWarningMods(SrmDocument Document, string toolName)
        {
            var resultList = new List<string>(); 
           
            bool alphapeptDeepFormat = toolName.Equals(@"alphapeptdeep");
            
            // Build precursor table row by row
            foreach (var peptide in Document.Peptides)
            {
                var modifiedSequence = ModifiedSequence.GetModifiedSequence(Document.Settings, peptide, IsotopeLabelType.light);

                var ModificationNames =
                    alphapeptDeepFormat ? AlphapeptdeepModificationNames : CarafeSupportedModificationNames;

                for (var i = 0; i < modifiedSequence.ExplicitMods.Count; i++)
                {
                    var mod = modifiedSequence.ExplicitMods[i];
                    if (!mod.UnimodId.HasValue)
                    {
                        var haveMod = resultList.FirstOrDefault(m => m == mod.Name);
                        if (haveMod == null)
                        {
                            resultList.Add(mod.Name);
                        }
                    }

                    var unimodIdAA = mod.UnimodIdAA;
                    var modNames = ModificationNames.Where(m => m.Accession == unimodIdAA).ToArray();
                    if (modNames.Length == 0)
                    {
                        var haveMod = resultList.FirstOrDefault(m => m == mod.Name);
                        if (haveMod == null)
                        {
                            resultList.Add(mod.Name);
                        }
                    }
                }
            }
            return resultList;
        }
        public static string LabelPrecursor(TransitionGroup tranGroup, double precursorMz,
            string resultsText)
        {
            return string.Format(@"{0}{1}{2}{3}", LabelMz(tranGroup, precursorMz),
                Transition.GetChargeIndicator(tranGroup.PrecursorAdduct),
                tranGroup.LabelTypeText, resultsText);
        }

        private static string LabelMz(TransitionGroup tranGroup, double precursorMz)
        {
            int? massShift = tranGroup.DecoyMassShift;
            double shift = SequenceMassCalc.GetPeptideInterval(massShift);
            return string.Format(@"{0:F04}{1}", precursorMz - shift,
                Transition.GetDecoyText(massShift));
        }
    }
    public class ArgumentAndValue
    {
        public ArgumentAndValue(string name, string value, string dash = @"--")
        {
            Name = name;
            Value = value;
            Dash = dash;
        }
        public string Name { get; private set; }
        public string Value { get; private set; }
        public string Dash { get; set; }

        public override string ToString() { return Dash + Name + TextUtil.SPACE + Value; }
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
            string modification = Name.Replace(@"(", "").Replace(@")", @"").Replace(@" ", @"@").Replace(@"Acetyl@N-term",@"Acetyl@Protein_N-term");
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
            return Index.ToString() + @":" + Modification.ToString();
        }
    }


    public class AlphapeptdeepLibraryBuilder : IiRTCapableLibraryBuilder
    {
        private const string ALPHAPEPTDEEP = @"alphapeptdeep";
        private const string BLIB_BUILD = @"BlibBuild";
        private const string CMD_FLOW_COMMAND = @"cmd-flow";
        private const string EXPORT_SETTINGS_COMMAND = @"export-settings";
        private const string EXT_TSV = TextUtil.EXT_TSV;
        private const string INPUT = @"input";
        private const string LEFT_PARENTHESIS = TextUtil.LEFT_PARENTHESIS;
        private const string LEFT_SQUARE_BRACKET = TextUtil.LEFT_SQUARE_BRACKET;
        private const string LIBRARY_COMMAND = @"library";
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
        private const string SETTINGS_FILE_NAME = @"settings.yaml";
        private const string SPACE = TextUtil.SPACE;
        private const string TAB = "\t";
        private const string TRANSFORMED_OUTPUT_SPECTRAL_LIB_FILE_NAME = @"predict_transformed.speclib.tsv";
        private const string UNDERSCORE = TextUtil.UNDERSCORE;



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
        public string BuilderLibraryPath => OutputSpectraLibFilepath;

        private LibraryHelper LibraryHelper { get; set; }

        private static readonly DateTime _nowTime = DateTime.Now;

        public static readonly string TimeStamp = _nowTime.ToString(@"yyyy-MM-dd_HH-mm-ss");
        private string PythonVirtualEnvironmentScriptsDir { get; }
        private string PeptdeepExecutablePath => Path.Combine(PythonVirtualEnvironmentScriptsDir, PEPTDEEP_EXECUTABLE);

        private static readonly string RootDir = Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), ALPHAPEPTDEEP, TimeStamp);
        private string SettingsFilePath => Path.Combine(RootDir, SETTINGS_FILE_NAME);
        private string InputFileName => INPUT + UNDERSCORE + EXT_TSV; //Convert.ToBase64String(Encoding.ASCII.GetBytes(Document.DocumentHash)) + EXT_TSV;
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
                new ArgumentAndValue(@"library--decoy", @"diann"),
                new ArgumentAndValue(@"device", @"gpu"),
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
        {            Document = document;
            Directory.CreateDirectory(RootDir);
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            if (Document.DocumentHash != null) LibraryHelper = new LibraryHelper(InputFilePath);
            PythonVirtualEnvironmentScriptsDir = pythonVirtualEnvironmentScriptsDir;
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
            bool haveInputFile = false;
            progressStatus = progressStatus.ChangeSegments(0, 5);
            Directory.CreateDirectory(RootDir);
            if (Document.DocumentHash != null)
            {
                haveInputFile = LibraryHelper.PrepareInputFile(Document, progress, ref progressStatus, @"alphapeptdeep");
            }
            progressStatus = progressStatus.NextSegment();

            if (haveInputFile) 
                PrepareSettingsFile(progress, ref progressStatus);

            progressStatus = progressStatus.NextSegment();

            if (haveInputFile)
                ExecutePeptdeep(progress, ref progressStatus);

            progressStatus = progressStatus.NextSegment();

            if (haveInputFile)
                TransformPeptdeepOutput(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();
            
            if (haveInputFile)
                ImportSpectralLibrary(progress, ref progressStatus);
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
                pr.EnableImmediateLog = true;
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
                pr.EnableImmediateLog = true;
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
            string args = $@"-o -i {TextUtil.Quote(LibrarySpec.Name)} {TextUtil.Quote(TransformedOutputSpectraLibFilepath)} {TextUtil.Quote(LibrarySpec.FilePath)}";
            var psi = new ProcessStartInfo(BLIB_BUILD, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false
            };
            try
            {
                pr.EnableImmediateLog = true;
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
