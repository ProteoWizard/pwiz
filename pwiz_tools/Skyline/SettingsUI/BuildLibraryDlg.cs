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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.BiblioSpec;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Prosit;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    public partial class BuildLibraryDlg : FormEx
    {
        public static readonly string[] RESULTS_EXTS =
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
    
        private string[] _inputFileNames = new string[0];
        private string _dirInputRoot = string.Empty;

        private readonly MessageBoxHelper _helper;
        private readonly IDocumentUIContainer _documentUiContainer;
        private readonly SkylineWindow _skylineWindow;

        private readonly SettingsListComboDriver<IrtStandard> _driverStandards;

        private readonly Point _actionLabelPos;
        private readonly Point _actionComboPos;
        private readonly Point _iRTLabelPos;
        private readonly Point _iRTComboPos;

        public BuildLibraryDlg(SkylineWindow skylineWindow)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _skylineWindow = skylineWindow;
            _documentUiContainer = skylineWindow;

            // Store locations of those controls since we move the irt label/combo around
            _actionLabelPos = actionLabel.Location;
            _actionComboPos = comboAction.Location;
            _iRTLabelPos = iRTPeptidesLabel.Location;
            _iRTComboPos = comboStandards.Location;

            panelFiles.Visible = false;
            textName.Focus();
            textPath.Text = Settings.Default.LibraryDirectory;
            comboAction.SelectedItem = LibraryBuildAction.Create.GetLocalizedString();
            textCutoff.Text = Settings.Default.LibraryResultCutOff.ToString(LocalizationHelper.CurrentCulture);

            if (_documentUiContainer.Document.PeptideCount == 0)
                cbFilter.Hide();
            else
                cbFilter.Checked = Settings.Default.LibraryFilterDocumentPeptides;

            cbKeepRedundant.Checked = Settings.Default.LibraryKeepRedundant;

            ceCombo.Items.AddRange(
                Enumerable.Range(PrositConstants.MIN_NCE, PrositConstants.MAX_NCE - PrositConstants.MIN_NCE + 1).Select(c => (object)c)
                    .ToArray());
            ceCombo.SelectedItem = Settings.Default.PrositNCE;

            _helper = new MessageBoxHelper(this);

            _driverStandards = new SettingsListComboDriver<IrtStandard>(comboStandards, Settings.Default.IrtStandardList);
            _driverStandards.LoadList(IrtStandard.EMPTY.GetKey());
        }

        private void BuildLibraryDlg_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!Settings.Default.IrtStandardList.Contains(IrtStandard.AUTO))
            {
                Settings.Default.IrtStandardList.Insert(0, IrtStandard.AUTO);
            }
        }

        public ILibraryBuilder Builder { get; private set; }

        public string[] InputFileNames
        {
            get { return _inputFileNames; }

            set
            {
                // Store checked state for existing files
                var checkStates = new Dictionary<string, bool>();
                for (int i = 0; i < _inputFileNames.Length; i++)
                    checkStates.Add(_inputFileNames[i], listInputFiles.GetItemChecked(i));

                // Set new value
                _inputFileNames = value;

                // Always show sorted list of files
                Array.Sort(_inputFileNames);

                // Calculate the common root directory
                _dirInputRoot = PathEx.GetCommonRoot(_inputFileNames);

                // Populate the input files list
                listInputFiles.Items.Clear();
                foreach (string fileName in _inputFileNames)
                {
                    bool checkFile;
                    if (!checkStates.TryGetValue(fileName, out checkFile))
                        checkFile = true;   // New files start out checked
                    listInputFiles.Items.Add(PathEx.RemovePrefix(fileName, _dirInputRoot), checkFile);
                }
                int count = listInputFiles.CheckedItems.Count;
                btnNext.Enabled = (panelProperties.Visible || count > 0);
                cbSelect.Enabled = (count > 0);
            }
        }

        private bool ValidateBuilder(bool validateInputFiles)
        {
            string name;
            if (!_helper.ValidateNameTextBox(textName, out name))
                return false;

            string outputPath = textPath.Text;
            if (string.IsNullOrEmpty(outputPath))
            {
                _helper.ShowTextBoxError(textPath, Resources.BuildLibraryDlg_ValidateBuilder_You_must_specify_an_output_file_path, outputPath);
                return false;                
            }
            if (Directory.Exists(outputPath))
            {
                _helper.ShowTextBoxError(textPath, Resources.BuildLibraryDlg_ValidateBuilder_The_output_path__0__is_a_directory_You_must_specify_a_file_path, outputPath);
                return false;                
            }
            string outputDir = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
            {
                _helper.ShowTextBoxError(textPath, Resources.BuildLibraryDlg_ValidateBuilder_The_directory__0__does_not_exist, outputDir);
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
                _helper.ShowTextBoxError(textPath, TextUtil.LineSeparate(Resources.BuildLibraryDlg_ValidateBuilder_Access_violation_attempting_to_write_to__0__,
                                                                         Resources.BuildLibraryDlg_ValidateBuilder_Please_check_that_you_have_write_access_to_this_folder_), outputDir);
                return false;
            }
            catch (IOException)
            {
                _helper.ShowTextBoxError(textPath, TextUtil.LineSeparate(Resources.BuildLibraryDlg_ValidateBuilder_Failure_attempting_to_create_a_file_in__0__,
                                                                         Resources.BuildLibraryDlg_ValidateBuilder_Please_check_that_you_have_write_access_to_this_folder_), outputDir);
                return false;
            }

            double cutOffScore;
            if (!_helper.ValidateDecimalTextBox(textCutoff, 0, 1.0, out cutOffScore))
                return false;
            Settings.Default.LibraryResultCutOff = cutOffScore;

            var libraryBuildAction = LibraryBuildAction;

            if (validateInputFiles)
            {
                var inputFilesChosen = new List<string>();
                foreach (int i in listInputFiles.CheckedIndices)
                {
                    inputFilesChosen.Add(_inputFileNames[i]);
                }

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

                if (prositDataSourceRadioButton.Checked)
                {
                    // TODO: Need to figure out a better way to do this, use PrositPeptidePrecursorPair?
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
                    
                    try
                    {
                        PrositUIHelpers.CheckPrositSettings(this, _skylineWindow);
                        // Still construct the library builder, otherwise a user might configure Prosit
                        // incorrectly, causing the build to silently fail
                        Builder = new PrositLibraryBuilder(doc, name, outputPath, () => true, IrtStandard,
                            peptidesPerPrecursor, precursors, NCE);
                    }
                    catch (Exception ex)
                    {
                        _helper.ShowTextBoxError(this, ex.Message);
                        return false;
                    }
                }
                else
                {
                    Builder = new BiblioSpecLiteBuilder(name, outputPath, inputFilesChosen, targetPeptidesChosen)
                    {
                        Action = libraryBuildAction,
                        IncludeAmbiguousMatches = cbIncludeAmbiguousMatches.Checked,
                        KeepRedundant = LibraryKeepRedundant,
                        CutOffScore = cutOffScore,
                        Id = Helpers.MakeId(textName.Text),
                        IrtStandard = _driverStandards.SelectedItem,
                        PreferEmbeddedSpectra = PreferEmbeddedSpectra
                    };
                }
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

            using (var dlg = new SaveFileDialog
                {
                    InitialDirectory = Settings.Default.LibraryDirectory,
                    FileName = fileName,
                    OverwritePrompt = true,
                    DefaultExt = BiblioSpecLiteSpec.EXT,
                    Filter = TextUtil.FileDialogFiltersAll(BiblioSpecLiteSpec.FILTER_BLIB)
                })
            {
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
            if (!panelProperties.Visible || prositDataSourceRadioButton.Checked)
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

                panelProperties.Visible = false;
                panelFiles.Visible = true;
                btnPrevious.Enabled = true;
                btnNext.Text = Resources.BuildLibraryDlg_OkWizardPage_Finish;
                AcceptButton = btnNext;
                btnNext.Enabled = (listInputFiles.CheckedItems.Count > 0);
            }            
        }

        private void btnPrevious_Click(object sender, EventArgs e)
        {
            if (panelFiles.Visible)
            {
                panelFiles.Visible = false;
                panelProperties.Visible = true;
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

            using (var dlg = new OpenFileDialog
                {
                    Title = Resources.BuildLibraryDlg_btnAddFile_Click_Add_Input_Files,
                    InitialDirectory = initialDirectory,
                    CheckPathExists = true,
                    SupportMultiDottedExtensions = true,
                    Multiselect = true,
                    DefaultExt = BiblioSpecLibSpec.EXT,
                    Filter = TextUtil.FileDialogFiltersAll(
                        Resources.BuildLibraryDlg_btnAddFile_Click_Matched_Peptides + string.Join(@",", wildExts) + @")|" +
                        string.Join(@";", wildExts),
                        BiblioSpecLiteSpec.FILTER_BLIB)
                })
            {
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
            using (var dlg = new FolderBrowserDialog
                {
                    Description = Resources.BuildLibraryDlg_btnAddDirectory_Click_Add_Input_Directory,
                    ShowNewFolderButton = false,
                    SelectedPath = Settings.Default.LibraryResultsDirectory
                })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.LibraryResultsDirectory = dlg.SelectedPath;

                    AddDirectory(dlg.SelectedPath);
                }
            }
        }

        public void AddDirectory(string dirPath)
        {
            using (var longWaitDlg = new LongWaitDlg
                {
                    Text = Resources.BuildLibraryDlg_AddDirectory_Find_Input_Files,
                })
            {
                try
                {
                    var inputFiles = new List<string>();
                    longWaitDlg.PerformWork(this, 800, broker => FindInputFiles(dirPath, inputFiles, broker));
                    AddInputFiles(inputFiles);
                }
                catch (Exception x)
                {
                    var message = TextUtil.LineSeparate(string.Format(Resources.BuildLibraryDlg_AddDirectory_An_error_occurred_reading_files_in_the_directory__0__,
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
            broker.Message = TextUtil.LineSeparate(Resources.BuildLibraryDlg_FindInputFiles_Finding_library_input_files_in,
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

        public static string[] AddInputFiles(Form parent, IEnumerable<string> inputFileNames, IEnumerable<string> fileNames, bool performDDASearch = false)
        {
            var filesNew = new List<string>(inputFileNames);
            var filesError = new List<string>();
            foreach (var fileName in fileNames)
            {
                if (IsValidInputFile(fileName, performDDASearch))
                {
                    if (!filesNew.Contains(fileName))
                        filesNew.Add(fileName);
                }
                else
                    filesError.Add(fileName);
            }

            if (filesError.Count > 0)
            {
                if (filesError.Count == 1)
                    MessageDlg.Show(parent, string.Format(Resources.BuildLibraryDlg_AddInputFiles_The_file__0__is_not_a_valid_library_input_file, filesError[0]));
                else
                {
                    var message = TextUtil.SpaceSeparate(Resources.BuildLibraryDlg_AddInputFiles_The_following_files_are_not_valid_library_input_files,
                                  string.Empty,
                                  // ReSharper disable LocalizableElement
                                  "\t" + string.Join("\n\t", filesError.ToArray()));
                                  // ReSharper restore LocalizableElement
                    MessageDlg.Show(parent, message);
                }
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

        private void cbSelect_CheckedChanged(object sender, EventArgs e)
        {
            bool checkAll = cbSelect.Checked;
            for (int i = 0; i < listInputFiles.Items.Count; i++)
                listInputFiles.SetItemChecked(i, checkAll);
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

        private void comboAction_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Equals(comboAction.SelectedItem, LibraryBuildAction.Append.GetLocalizedString()))
            {
                cbKeepRedundant.Checked = true;
            }
        }

        private void listInputFiles_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            // If all other checkboxes in the list match the new state,
            // update the select / deselect all checkbox.
            int iChange = e.Index;
            CheckState state = e.NewValue;
            if (state == CheckState.Checked)
                btnNext.Enabled = true;

            for (int i = 0; i < listInputFiles.Items.Count; i++)
            {
                if (i == iChange)
                    continue;
                if (listInputFiles.GetItemCheckState(i) != state)
                    return;
            }
            cbSelect.CheckState = state;
            if (state == CheckState.Unchecked)
                btnNext.Enabled = false;
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

        public double LibraryCutoff
        {
            get
            {
                double cutoff;
                return (double.TryParse(textCutoff.Text, out cutoff) ? cutoff : 0);
            }

            set { textCutoff.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public bool Prosit
        {
            get { return prositDataSourceRadioButton.Checked; }
            set { prositDataSourceRadioButton.Checked = value; }
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

        private void dataSourceFilesRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            panelFilesProps.Visible = dataSourceFilesRadioButton.Checked;

            var useFiles = dataSourceFilesRadioButton.Checked;
            ceCombo.Visible = !useFiles;
            ceLabel.Visible = !useFiles;


            if (useFiles)
            {
                iRTPeptidesLabel.Location = _iRTLabelPos;
                comboStandards.Location = _iRTComboPos;
                if (!Settings.Default.IrtStandardList.Contains(IrtStandard.AUTO))
                {
                    Settings.Default.IrtStandardList.Insert(1, IrtStandard.AUTO);
                }
            }
            else
            {
                iRTPeptidesLabel.Location = _actionLabelPos;
                comboStandards.Location = _actionComboPos;
                Settings.Default.IrtStandardList.Remove(IrtStandard.AUTO);

                PrositUIHelpers.CheckPrositSettings(this, _skylineWindow);
            }
            _driverStandards.LoadList(IrtStandard.EMPTY.GetKey());

            btnNext.Text = dataSourceFilesRadioButton.Checked ? Resources.BuildLibraryDlg_btnPrevious_Click__Next__ : Resources.BuildLibraryDlg_OkWizardPage_Finish;
        }

        private void prositInfoSettingsBtn_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            _skylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Prosit);
        }
    }
}
