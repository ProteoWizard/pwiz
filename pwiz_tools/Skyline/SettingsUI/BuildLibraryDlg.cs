/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.BiblioSpec;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Koina;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    public partial class BuildLibraryDlg : FormEx, IMultipleViewProvider
    {
        public BuildLibraryGridView Grid { get; }
        public static string[] RESULTS_EXTS =>
            Program.ModeUI == SrmDocument.DOCUMENT_TYPE.small_molecules ? RESULTS_EXTS_SMALL_MOL : RESULTS_EXTS_PEPTIDES;

        public static readonly string[] RESULTS_EXTS_PEPTIDES =
        {
            BiblioSpecLiteBuilder.EXT_DAT,
            BiblioSpecLiteBuilder.EXT_PEP_XML,
            BiblioSpecLiteBuilder.EXT_PEP_XML_ONE_DOT,
            BiblioSpecLiteBuilder.EXT_MZID,
            BiblioSpecLiteBuilder.EXT_MZID_GZ,
            BiblioSpecLiteBuilder.EXT_XTAN_XML,
            BiblioSpecLiteBuilder.EXT_PROTEOME_DISC,
            BiblioSpecLiteBuilder.EXT_PROTEOME_DISC_FILTERED,
            BiblioSpecLiteBuilder.EXT_PILOT,
            BiblioSpecLiteBuilder.EXT_PILOT_XML,
            BiblioSpecLiteBuilder.EXT_PRIDE_XML,
            BiblioSpecLiteBuilder.EXT_IDP_XML,
            BiblioSpecLiteBuilder.EXT_SQT,
            BiblioSpecLiteBuilder.EXT_SSL,
            BiblioSpecLiteBuilder.EXT_PERCOLATOR,
            BiblioSpecLiteBuilder.EXT_PERCOLATOR_XML,
            BiblioSpecLiteBuilder.EXT_MAX_QUANT,
            BiblioSpecLiteBuilder.EXT_WATERS_MSE,
            BiblioSpecLiteBuilder.EXT_PROXL_XML,
            BiblioSpecLiteBuilder.EXT_TSV,
            BiblioSpecLiteBuilder.EXT_MZTAB,
            BiblioSpecLiteBuilder.EXT_MZTAB_TXT,
            BiblioSpecLiteBuilder.EXT_OPEN_SWATH,
            BiblioSpecLiteBuilder.EXT_SPECLIB,
        };

        public static readonly string[] RESULTS_EXTS_SMALL_MOL =
        {
            BiblioSpecLiteBuilder.EXT_SSL,
        };

        public enum Pages { properties, files, learning }

        public class PropertiesPage : IFormView { }
        public class FilesPage : IFormView { }
        public class LearningPage : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new PropertiesPage(), new FilesPage(), new LearningPage(),
        };

        private bool IsAlphaEnabled => false;   // TODO: Implement and enable
        private bool IsCarafeEnabled => false;   // TODO: Implement and enable

        public enum DataSourcePages { files, alpha, carafe, koina }
        public enum LearningOptions { none, libraries, document }

        private readonly MessageBoxHelper _helper;
        private readonly IDocumentUIContainer _documentUiContainer;
        private readonly SkylineWindow _skylineWindow;

        private readonly SettingsListComboDriver<IrtStandard> _driverStandards;
        private SettingsListBoxDriver<LibrarySpec> _driverLibrary;

        public BuildLibraryDlg(SkylineWindow skylineWindow)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _skylineWindow = skylineWindow;
            _documentUiContainer = skylineWindow;

            textName.Focus();
            textPath.Text = Settings.Default.LibraryDirectory;
            comboAction.SelectedItem = LibraryBuildAction.Create.GetLocalizedString();

            if (_documentUiContainer.Document.PeptideCount == 0)
                cbFilter.Hide();
            else
                cbFilter.Checked = Settings.Default.LibraryFilterDocumentPeptides;

            cbKeepRedundant.Checked = Settings.Default.LibraryKeepRedundant;

            ceCombo.Items.AddRange(
                Enumerable.Range(KoinaConstants.MIN_NCE, KoinaConstants.MAX_NCE - KoinaConstants.MIN_NCE + 1).Select(c => (object)c)
                    .ToArray());
            ceCombo.SelectedItem = Settings.Default.KoinaNCE;
            comboLearnFrom.SelectedIndex = 0;

            _helper = new MessageBoxHelper(this);

            _driverStandards = new SettingsListComboDriver<IrtStandard>(comboStandards, Settings.Default.IrtStandardList);
            _driverStandards.LoadList(IrtStandard.EMPTY.GetKey());

            Grid = gridInputFiles;
            Grid.FilesChanged += (sender, e) =>
            {
                btnNext.Enabled = tabControlMain.SelectedIndex == (int)Pages.files || Grid.IsReady;
            };

            // If we're not using dataSourceGroupBox (because we're in small molecule mode) shift other controls over where it was
            if (modeUIHandler.ComponentsDisabledForModeUI(dataSourceGroupBox))
            {
                tabControlDataSource.Left = dataSourceGroupBox.Left;
                Height -= tabControlDataSource.Bottom - dataSourceGroupBox.Bottom;
            }
            else
            {
                int heightDiffGroupBox = 0;
                if (!IsAlphaEnabled)
                {
                    int yShift = radioCarafeSource.Top - radioAlphaSource.Top;
                    radioCarafeSource.Top -= yShift;
                    radioKoinaSource.Top -= yShift;
                    koinaInfoSettingsBtn.Top -= yShift;
                    radioAlphaSource.Visible = false;
                    heightDiffGroupBox += yShift;
                }

                if (!IsCarafeEnabled)
                {
                    int yShift = radioKoinaSource.Top - radioCarafeSource.Top;
                    radioKoinaSource.Top -= yShift;
                    koinaInfoSettingsBtn.Top -= yShift;
                    radioCarafeSource.Visible = false;
                    heightDiffGroupBox += yShift;
                }

                dataSourceGroupBox.Height -= heightDiffGroupBox;
                iRTPeptidesLabel.Top -= heightDiffGroupBox;
                comboStandards.Top -= heightDiffGroupBox;
                Height -= heightDiffGroupBox;
            }
        }

        private void BuildLibraryDlg_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!Settings.Default.IrtStandardList.Contains(IrtStandard.AUTO))
            {
                Settings.Default.IrtStandardList.Insert(0, IrtStandard.AUTO);
            }
        }

        public ILibraryBuilder Builder { get; private set; }

        public IEnumerable<string> InputFileNames
        {
            get => Grid.FilePaths;
            set => Grid.FilePaths = value;
        }

        public string AddLibraryFile { get; private set; }

        private bool ValidateBuilder(bool validateInputFiles)
        {
            string name;
            if (!_helper.ValidateNameTextBox(textName, out name))
                return false;

            string outputPath = textPath.Text;
            if (string.IsNullOrEmpty(outputPath))
            {
                _helper.ShowTextBoxError(textPath, SettingsUIResources.BuildLibraryDlg_ValidateBuilder_You_must_specify_an_output_file_path, outputPath);
                return false;                
            }
            if (Directory.Exists(outputPath))
            {
                _helper.ShowTextBoxError(textPath, SettingsUIResources.BuildLibraryDlg_ValidateBuilder_The_output_path__0__is_a_directory_You_must_specify_a_file_path, outputPath);
                return false;                
            }
            string outputDir = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(outputDir))
            {
                _helper.ShowTextBoxError(textPath, SettingsUIResources.BuildLibraryDlg_ValidateBuilder_You_must_specify_an_output_file_path, outputPath);
                return false;
            }
            if (!Directory.Exists(outputDir))
            {
                _helper.ShowTextBoxError(textPath, SettingsUIResources.BuildLibraryDlg_ValidateBuilder_The_directory__0__does_not_exist, outputDir);
                return false;
            }
            if (!outputPath.EndsWith(BiblioSpecLiteSpec.EXT))
                outputPath += BiblioSpecLiteSpec.EXT;
            try
            {
                using (var sfLib = new FileSaver(outputPath))
                {
                    if (!sfLib.CanSave(this))
                    {
                        textPath.Focus();
                        textPath.SelectAll();
                        return false;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                _helper.ShowTextBoxError(textPath, TextUtil.LineSeparate(SettingsUIResources.BuildLibraryDlg_ValidateBuilder_Access_violation_attempting_to_write_to__0__,
                                                                         SettingsUIResources.BuildLibraryDlg_ValidateBuilder_Please_check_that_you_have_write_access_to_this_folder_), outputDir);
                return false;
            }
            catch (IOException)
            {
                _helper.ShowTextBoxError(textPath, TextUtil.LineSeparate(SettingsUIResources.BuildLibraryDlg_ValidateBuilder_Failure_attempting_to_create_a_file_in__0__,
                                                                         SettingsUIResources.BuildLibraryDlg_ValidateBuilder_Please_check_that_you_have_write_access_to_this_folder_), outputDir);
                return false;
            }

            var libraryBuildAction = LibraryBuildAction;

            if (validateInputFiles)
            {
                if (radioKoinaSource.Checked)
                {
                    if (!CreateKoinaBuilder(name, outputPath, NCE))
                        return false;
                }
                else if (radioAlphaSource.Checked)
                {
                    // TODO: Replace with working AlphaPeptDeep implementation
                    if (!CreateKoinaBuilder(name, outputPath))
                        return false;
                }
                else if (radioCarafeSource.Checked)
                {
                    string learningDocPath = string.Empty;
                    IList<LibrarySpec> learningLibraries = new List<LibrarySpec>();
                    if (comboLearnFrom.SelectedIndex == (int)LearningOptions.document)
                    {
                        learningDocPath = textLearningDoc.Text;
                        if (!PathEx.HasExtension(learningDocPath, SrmDocument.EXT) || !File.Exists(learningDocPath))
                        {
                            _helper.ShowTextBoxError(textPath, SettingsUIResources.BuildLibraryDlg_ValidateBuilder_You_must_specify_a_valid_path_to_a_Skyline_document_to_learn_from_, learningDocPath);
                            return false;
                        }
                        
                        // CONSIDER: Could also check for the ChromatogramCache.EXT file as a short-cut for full results checking

                        // TODO: Probably need to load the document int memory with progress UI and validate that it has results
                    }
                    else if (comboLearnFrom.SelectedIndex == (int)LearningOptions.libraries)
                    {
                        learningLibraries.AddRange(_driverLibrary.GetChosen(null));

                        // TODO: Probably need to validate that all the libraries can be loaded into memory with progress UI
                    }

                    // TODO: Create CarafeLibraryBuilder class with everything necessary to build a library
                    if (!CreateKoinaBuilder(name, outputPath))
                        return false;
                }
                else
                {
                    if (!Grid.Validate(this, null, true, out var thresholdsByFile))
                        return false;

                    List<Target> targetPeptidesChosen = null;
                    if (cbFilter.Checked)
                    {
                        targetPeptidesChosen = new List<Target>();
                        var doc = _documentUiContainer.Document;
                        foreach (PeptideDocNode nodePep in doc.Peptides)
                        {
                            // Add light modified sequences
                            targetPeptidesChosen.Add(nodePep.ModifiedTarget);
                            // Add heavy modified sequences
                            foreach (var nodeGroup in nodePep.TransitionGroups)
                            {
                                if (nodeGroup.TransitionGroup.LabelType.IsLight)
                                    continue;
                                targetPeptidesChosen.Add(doc.Settings.GetModifiedSequence(nodePep.Peptide.Target,
                                    nodeGroup.TransitionGroup.LabelType,
                                    nodePep.ExplicitMods,
                                    SequenceModFormatType.lib_precision));
                            }
                        }
                    }

                    Builder = new BiblioSpecLiteBuilder(name, outputPath, InputFileNames.ToArray(), targetPeptidesChosen)
                    {
                        Action = libraryBuildAction,
                        IncludeAmbiguousMatches = cbIncludeAmbiguousMatches.Checked,
                        KeepRedundant = LibraryKeepRedundant,
                        ScoreThresholdsByFile = thresholdsByFile,
                        Id = Helpers.MakeId(textName.Text),
                        IrtStandard = _driverStandards.SelectedItem,
                        PreferEmbeddedSpectra = PreferEmbeddedSpectra
                    };
                }
            }
            return true;
        }

        private bool CreateKoinaBuilder(string name, string outputPath, int nce = 27)
        {
            // TODO: Need to figure out a better way to do this, use KoinaPeptidePrecursorPair?
            var doc = _documentUiContainer.DocumentUI;
            var peptides = doc.Peptides.Where(pep=>!pep.IsDecoy).ToArray();
            var precursorCount = peptides.Sum(pep=>pep.TransitionGroupCount);
            var peptidesPerPrecursor = new PeptideDocNode[precursorCount];
            var precursors = new TransitionGroupDocNode[precursorCount];
            int index = 0;

            for (var i = 0; i < peptides.Length; ++i)
            {
                var groups = peptides[i].TransitionGroups.ToArray();
                Array.Copy(Enumerable.Repeat(peptides[i], groups.Length).ToArray(), 0, peptidesPerPrecursor, index,
                    groups.Length);
                Array.Copy(groups, 0, precursors, index, groups.Length);
                index += groups.Length;
            }

            if (index == 0)
            {
                MessageDlg.Show(this, Resources.BuildLibraryDlg_ValidateBuilder_Add_peptide_precursors_to_the_document_to_build_a_library_from_Koina_predictions_);
                return false;
            }

            try
            {
                KoinaUIHelpers.CheckKoinaSettings(this, _skylineWindow);
                // Still construct the library builder, otherwise a user might configure Koina
                // incorrectly, causing the build to silently fail
                Builder = new KoinaLibraryBuilder(doc, name, outputPath, () => true, IrtStandard,
                    peptidesPerPrecursor, precursors, nce);
            }
            catch (Exception ex)
            {
                MessageDlg.ShowWithException(this, ex.Message, ex);
                return false;
            }

            return true;
        }

        private void textName_TextChanged(object sender, EventArgs e)
        {
            string name = textName.Text;
            string outputPath = textPath.Text;
            if (outputPath.Length > 0 && !Directory.Exists(outputPath))
            {
                try
                {
                    // ReSharper disable once ConstantNullCoalescingCondition
                    outputPath = Path.GetDirectoryName(outputPath) ?? string.Empty;                
                }
                catch (Exception)
                {
                    outputPath = string.Empty;
                }
            }
            string id = (name.Length == 0 ? string.Empty : Helpers.MakeId(textName.Text));
            textPath.Text = id.Length == 0
                                ? outputPath
                                : Path.Combine(outputPath, id + BiblioSpecLiteSpec.EXT);
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            string fileName;
            try
            {
                fileName = Path.GetFileName(textPath.Text);
            }
            catch (Exception)
            {
                fileName = string.Empty;
            }

            using (var dlg = new SaveFileDialog())
            {
                dlg.InitialDirectory = Settings.Default.LibraryDirectory;
                dlg.FileName = fileName;
                dlg.OverwritePrompt = true;
                dlg.DefaultExt = BiblioSpecLiteSpec.EXT;
                dlg.Filter = TextUtil.FileDialogFiltersAll(BiblioSpecLiteSpec.FILTER_BLIB);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.LibraryDirectory = Path.GetDirectoryName(dlg.FileName);

                    textPath.Text = dlg.FileName;
                }
            }
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            OkWizardPage();
        }

        public void OkWizardPage()
        {
            if (tabControlMain.SelectedIndex != (int)Pages.properties || radioAlphaSource.Checked || radioKoinaSource.Checked)
            {
                if (ValidateBuilder(true))
                {
                    Settings.Default.LibraryFilterDocumentPeptides = LibraryFilterPeptides;
                    Settings.Default.LibraryKeepRedundant = LibraryKeepRedundant;
                    DialogResult = DialogResult.OK;
                }
            }
            else if (ValidateBuilder(false))
            {
                Settings.Default.LibraryDirectory = Path.GetDirectoryName(LibraryPath);

                tabControlMain.SelectedIndex = (int)(radioFilesSource.Checked
                    ? Pages.files
                    : Pages.learning);  // Carafe
                btnPrevious.Enabled = true;
                btnNext.Text = Resources.BuildLibraryDlg_OkWizardPage_Finish;
                AcceptButton = btnNext;
                if (radioFilesSource.Checked)
                    btnNext.Enabled = Grid.IsReady;
                else
                    btnNext.Enabled = true;
            }            
        }

        private void btnPrevious_Click(object sender, EventArgs e)
        {
            if (tabControlMain.SelectedIndex != (int)Pages.properties)
            {
                tabControlMain.SelectedIndex = (int)Pages.properties;
                btnNext.Text = Resources.BuildLibraryDlg_btnPrevious_Click__Next__;
                btnNext.Enabled = true;
                btnNext.DialogResult = DialogResult.None;
                AcceptButton = null;
                btnPrevious.Enabled = false;
            }
        }

        private void btnAddFile_Click(object sender, EventArgs e)
        {
            string[] addFiles = ShowAddFile(this, Settings.Default.LibraryResultsDirectory);
            if (addFiles != null)
            {
                AddInputFiles(addFiles);
            }
        }

        public static string[] ShowAddFile(Form parent, String initialDirectory)
        {
            var wildExts = new string[RESULTS_EXTS.Length];
            for (int i = 0; i < wildExts.Length; i++)
                wildExts[i] = @"*" + RESULTS_EXTS[i];

            // Adjust the button text for small molecule UI
            var buttonText = parent is FormEx formEx ?
                formEx.GetModeUIHelper().Translate(SettingsUIResources.BuildLibraryDlg_btnAddFile_Click_Matched_Peptides) :
                SettingsUIResources.BuildLibraryDlg_btnAddFile_Click_Matched_Peptides;
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = SettingsUIResources.BuildLibraryDlg_btnAddFile_Click_Add_Input_Files;
                dlg.InitialDirectory = initialDirectory;
                dlg.CheckPathExists = true;
                dlg.SupportMultiDottedExtensions = true;
                dlg.Multiselect = true;
                dlg.DefaultExt = BiblioSpecLibSpec.EXT;
                dlg.Filter = TextUtil.FileDialogFiltersAll(
                    buttonText + string.Join(@",", wildExts) + @")|" +
                    string.Join(@";", wildExts),
                    BiblioSpecLiteSpec.FILTER_BLIB);
                if (dlg.ShowDialog(parent) == DialogResult.OK)
                {
                    Settings.Default.LibraryResultsDirectory = Path.GetDirectoryName(dlg.FileName);

                    return dlg.FileNames;
                }
                return null;
            }
        }

        private void btnAddDirectory_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = SettingsUIResources.BuildLibraryDlg_btnAddDirectory_Click_Add_Input_Directory;
                dlg.ShowNewFolderButton = false;
                dlg.SelectedPath = Settings.Default.LibraryResultsDirectory;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.LibraryResultsDirectory = dlg.SelectedPath;

                    AddDirectory(dlg.SelectedPath);
                }
            }
        }

        public void AddDirectory(string dirPath)
        {
            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.Text = SettingsUIResources.BuildLibraryDlg_AddDirectory_Find_Input_Files;
                try
                {
                    var inputFiles = new List<string>();
                    longWaitDlg.PerformWork(this, 800, broker => FindInputFiles(dirPath, inputFiles, broker));
                    AddInputFiles(inputFiles);
                }
                catch (Exception x)
                {
                    var message = TextUtil.LineSeparate(string.Format(SettingsUIResources.BuildLibraryDlg_AddDirectory_An_error_occurred_reading_files_in_the_directory__0__,
                                                                      dirPath),
                                                        x.Message);
                    MessageDlg.ShowWithException(this, message, x);
                }
            }
        }

        private void btnAddPaths_Click(object sender, EventArgs e)
        {
            ShowAddPathsDlg();
        }

        public void ShowAddPathsDlg()
        {
            CheckDisposed();

            using (var dlg = new AddPathsDlg())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    AddInputFiles(dlg.FileNames);
                }
            }
        }

        private static void FindInputFiles(string dir, ICollection<string> inputFiles, ILongWaitBroker broker)
        {
            broker.ProgressValue = 0;
            FindInputFiles(dir, inputFiles, broker, 0, 100);
        }

        private static void FindInputFiles(string dir, ICollection<string> inputFiles,
            ILongWaitBroker broker, double start, double stop)
        {
            broker.Message = TextUtil.LineSeparate(SettingsUIResources.BuildLibraryDlg_FindInputFiles_Finding_library_input_files_in,
                                                   PathEx.ShortenPathForDisplay(dir));

            string[] fileNames = Directory.GetFiles(dir);
            Array.Sort(fileNames);
            string[] dirs = Directory.GetDirectories(dir);
            Array.Sort(dirs);

            double startSub = start;
            double increment = (stop - start) / (dirs.Length + 1);

            const string extPep = BiblioSpecLiteBuilder.EXT_PEP_XML_ONE_DOT;
            const string extIdp = BiblioSpecLiteBuilder.EXT_IDP_XML;            
            bool hasIdp = fileNames.Contains(f => PathEx.HasExtension(f, extIdp));

            foreach (string fileName in fileNames)
            {
                if (IsValidInputFile(fileName))
                {
                    // If the directory has any .idpXML files, then do not add the
                    // supporting .pepXML files.
                    if (!hasIdp || !PathEx.HasExtension(fileName, extPep))
                        inputFiles.Add(fileName);                    
                }
                if (broker.IsCanceled)
                    return;
            }

            startSub += increment;
            broker.ProgressValue = (int) startSub; 

            foreach (string dirSub in dirs)
            {
                FindInputFiles(dirSub, inputFiles, broker, startSub, startSub + increment);
                if (broker.IsCanceled)
                    return;
                startSub += increment;
                broker.ProgressValue = (int)startSub;
            }
        }

        public void AddInputFiles(IEnumerable<string> fileNames)
        {
            InputFileNames = AddInputFiles(this, InputFileNames, fileNames);
        }

        public static void CheckInputFiles(IEnumerable<string> inputFileNames, IEnumerable<string> fileNames, bool performDDASearch, out List<string> filesNew, out List<string> filesError)
        {
            filesNew = new List<string>(inputFileNames);
            filesError = new List<string>();
            foreach (var fileName in fileNames)
            {
                if (IsValidInputFile(fileName, performDDASearch))
                {
                    if (!filesNew.Contains(fileName))
                        filesNew.Add(fileName);
                }
                else
                {
                    if (!filesError.Contains(fileName))
                        filesError.Add(fileName);
                }
            }
        }

        private string[] AddInputFiles(Form parent, IEnumerable<string> inputFileNames, IEnumerable<string> fileNames)
        {
            CheckInputFiles(inputFileNames, fileNames, false, out var filesNew, out var filesError);

            if (filesError.Count > 0)
            {
                var filesLib = filesError.Where(IsLibraryFile).ToArray();
                if (filesError.Count == filesLib.Length)
                {
                    // All files are library files (e.g. msp, sptxt, etc)
                    if (filesLib.Length == 1)
                    {
                        using (var dlg = new MultiButtonMsgDlg(
                            string.Format(SettingsUIResources.BuildLibraryDlg_AddInputFiles_The_file__0__is_a_library_file_and_does_not_need_to_be_built__Would_you_like_to_add_this_library_to_the_document_,
                                filesLib[0]), MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false))
                        {
                            if (dlg.ShowDialog(parent) == DialogResult.Yes)
                            {
                                AddLibraryFile = filesLib[0];
                                DialogResult = DialogResult.OK;
                            }
                        }
                    }
                    else
                    {
                        MessageDlg.Show(parent, SettingsUIResources.BuildLibraryDlg_AddInputFiles_These_files_are_library_files_and_do_not_need_to_be_built__Edit_the_list_of_libraries_to_add_them_directly_);
                    }
                }
                else if (filesError.Count == 1)
                {
                    MessageDlg.Show(parent, string.Format(SettingsUIResources.BuildLibraryDlg_AddInputFiles_The_file__0__is_not_a_valid_library_input_file, filesError[0]));
                }
                else
                {
                    var message = TextUtil.SpaceSeparate(SettingsUIResources.BuildLibraryDlg_AddInputFiles_The_following_files_are_not_valid_library_input_files,
                                  string.Empty,
                                  // ReSharper disable LocalizableElement
                                  "\t" + string.Join("\n\t", filesError.ToArray()));
                                  // ReSharper restore LocalizableElement
                    MessageDlg.Show(parent, message);
                }
            }

            return filesNew.ToArray();
        }

        public static string[] AddInputFiles(Form parent, IEnumerable<string> inputFileNames, IEnumerable<string> fileNames, bool performDDASearch = false)
        {
            CheckInputFiles(inputFileNames, fileNames, performDDASearch, out var filesNew, out var filesError);

            if (filesError.Count == 1)
            {
                MessageDlg.Show(parent, string.Format(SettingsUIResources.BuildLibraryDlg_AddInputFiles_The_file__0__is_not_a_valid_library_input_file, filesError[0]));
            }
            else if (filesError.Count > 1)
            {
                var message = TextUtil.SpaceSeparate(SettingsUIResources.BuildLibraryDlg_AddInputFiles_The_following_files_are_not_valid_library_input_files,
                              string.Empty,
                              // ReSharper disable LocalizableElement
                              "\t" + string.Join("\n\t", filesError.ToArray()));
                              // ReSharper restore LocalizableElement
                MessageDlg.Show(parent, message);
            }

            return filesNew.ToArray();
        }

        private static bool IsValidInputFile(string fileName, bool performDDASearch = false)
        {
            if (performDDASearch)
                return true; // these are validated in OpenFileDialog
            else
            {
                foreach (string extResult in RESULTS_EXTS)
                {
                    if (PathEx.HasExtension(fileName, extResult))
                        return true;
                }
            }
            return fileName.EndsWith(BiblioSpecLiteSpec.EXT);
        }

        private static bool IsLibraryFile(string fileName)
        {
            return LibrarySpec.CreateFromPath(@"__internal__", fileName) != null;
        }

        private void textPath_TextChanged(object sender, EventArgs e)
        {
            bool existsRedundant = false;
            string path = textPath.Text;
            if (!string.IsNullOrEmpty(path) && path.EndsWith(BiblioSpecLiteSpec.EXT))
            {
                try
                {
                    string baseName = Path.GetFileNameWithoutExtension(textPath.Text);
                    string redundantName = baseName + BiblioSpecLiteSpec.EXT_REDUNDANT;
                    existsRedundant = File.Exists(Path.Combine(Path.GetDirectoryName(textPath.Text) ?? string.Empty, redundantName));
                }
                catch (IOException)
                {
                    // May happen if path is too long.
                }
            }

            if (existsRedundant)
            {
                if (!comboAction.Enabled)
                    comboAction.Enabled = true;
            }
            else
            {
                if (comboAction.Enabled)
                {
                    comboAction.SelectedItem = LibraryBuildAction.Create.GetLocalizedString();
                    comboAction.Enabled = false;
                }
            }
        }

        public string LibraryName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public string LibraryPath
        {
            get { return textPath.Text; }
            set { textPath.Text = value; }
        }

        public bool Koina
        {
            get { return radioKoinaSource.Checked; }
            set { radioKoinaSource.Checked = value; }
        }

        public int NCE
        {
            get { return (int)ceCombo.SelectedItem; }
            set { ceCombo.SelectedItem = value; }
        }

        public bool LibraryKeepRedundant
        {
            get { return cbKeepRedundant.Checked; }
            set { cbKeepRedundant.Checked = value; }
        }

        public bool IncludeAmbiguousMatches
        {
            get { return cbIncludeAmbiguousMatches.Checked; }
            set { cbIncludeAmbiguousMatches.Checked = value; }
        }

        public bool LibraryFilterPeptides
        {
            get { return cbFilter.Checked; }
            set { cbFilter.Checked = value; }
        }

        public LibraryBuildAction LibraryBuildAction
        {
            get
            {
                return (comboAction.SelectedIndex == 0
                            ? LibraryBuildAction.Create
                            : LibraryBuildAction.Append);
            }

            set
            {
                comboAction.SelectedIndex = (value == LibraryBuildAction.Create ? 0 : 1);
            }
        }

        public IrtStandard IrtStandard
        {
            get { return _driverStandards.SelectedItem; }
            set
            {
                var index = 0;
                if (value != null)
                {
                    for (var i = 0; i < comboStandards.Items.Count; i++)
                    {
                        if (comboStandards.Items[i].ToString().Equals(value.GetKey()))
                        {
                            index = i;
                            break;
                        }
                    }
                }
                comboStandards.SelectedIndex = index;
                _driverStandards.SelectedIndexChangedEvent(null, null);
            }
        }

        public bool? PreferEmbeddedSpectra { get; set; }

        private void comboStandards_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverStandards.SelectedIndexChangedEvent(sender, e);
        }

        private void dataSourceRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            // Only respond to the checking event, or this will happen
            // twice for every change.
            var radioSender = (RadioButton)sender;
            if (!radioSender.Checked)
                return;

            var selectedStandard = _driverStandards.SelectedItem;
            string nextText = Resources.BuildLibraryDlg_btnPrevious_Click__Next__;
            if (radioFilesSource.Checked)
            {
                tabControlDataSource.SelectedIndex = (int)DataSourcePages.files;
                if (!Settings.Default.IrtStandardList.Contains(IrtStandard.AUTO))
                {
                    Settings.Default.IrtStandardList.Insert(1, IrtStandard.AUTO);
                }
            }
            else
            {
                Settings.Default.IrtStandardList.Remove(IrtStandard.AUTO);

                if (radioCarafeSource.Checked)
                {
                    tabControlDataSource.SelectedIndex = (int)DataSourcePages.carafe;
                }
                else if (radioAlphaSource.Checked)
                {
                    tabControlDataSource.SelectedIndex = (int)DataSourcePages.alpha;
                    nextText = Resources.BuildLibraryDlg_OkWizardPage_Finish;
                }
                else // must be Koina
                {
                    tabControlDataSource.SelectedIndex = (int)DataSourcePages.koina;
                    KoinaUIHelpers.CheckKoinaSettings(this, _skylineWindow);
                    nextText = Resources.BuildLibraryDlg_OkWizardPage_Finish;
                }
            }
            _driverStandards.LoadList(selectedStandard.GetKey());

            if (!Equals(btnNext.Text, nextText))
                btnNext.Text = nextText;
        }

        private void koinaInfoSettingsBtn_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            _skylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Koina);
        }

        public IFormView ShowingFormView
        {
            get
            {
                return TAB_PAGES[tabControlMain.SelectedIndex];
            }
        }

        private void comboLearnFrom_SelectedIndexChanged(object sender, EventArgs e)
        {
            var learningOption = (LearningOptions)comboLearnFrom.SelectedIndex;
            tabControlLearning.SelectedIndex = (int)learningOption;
            switch (learningOption)
            {
                case LearningOptions.libraries:
                    PopulateLibraries();
                    break;
            }
        }

        private void PopulateLibraries()
        {
            if (_driverLibrary == null)
            {
                _driverLibrary = new SettingsListBoxDriver<LibrarySpec>(listLibraries, Settings.Default.SpectralLibraryList);
                _driverLibrary.LoadList(null, Array.Empty<LibrarySpec>());
            }
        }

        private void btnLearningDocBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = SkylineResources.SkylineWindow_importDocumentMenuItem_Click_Import_Skyline_Document;
                dlg.InitialDirectory = Settings.Default.ActiveDirectory;
                dlg.CheckPathExists = true;
                dlg.Multiselect = false;
                dlg.SupportMultiDottedExtensions = true;
                dlg.DefaultExt = SrmDocument.EXT;
                dlg.Filter = TextUtil.FileDialogFiltersAll(SrmDocument.FILTER_DOC);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    textLearningDoc.Text = dlg.FileName;
                }
            }

        }
    }
}
