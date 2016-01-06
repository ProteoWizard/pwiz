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
using System.Text.RegularExpressions;
using System.Windows.Forms;
using pwiz.BiblioSpec;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
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
            BiblioSpecLiteBuilder.EXI_MZID,
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
        };

        private BiblioSpecLiteBuilder _builder;

        private string[] _inputFileNames = new string[0];
        private string _dirInputRoot = string.Empty;

        private readonly MessageBoxHelper _helper;
        private readonly IDocumentUIContainer _documentUiContainer;

        public BuildLibraryDlg(IDocumentUIContainer documentContainer)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _documentUiContainer = documentContainer;

            panelFiles.Visible = false;

            textName.Focus();
            textPath.Text = Settings.Default.LibraryDirectory;
            comboAction.SelectedItem = LibraryBuildAction.Create.GetLocalizedString();
            textCutoff.Text = Settings.Default.LibraryResultCutOff.ToString(LocalizationHelper.CurrentCulture);
            textAuthority.Text = Settings.Default.LibraryAuthority;

            if (documentContainer.Document.PeptideCount == 0)
                cbFilter.Hide();
            else
                cbFilter.Checked = Settings.Default.LibraryFilterDocumentPeptides;

            cbKeepRedundant.Checked = Settings.Default.LibraryKeepRedundant;

            _helper = new MessageBoxHelper(this);
        }

        public ILibraryBuilder Builder { get { return _builder;  } }

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
                    listInputFiles.Items.Add(fileName.Substring(_dirInputRoot.Length), checkFile);
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
            string authority = null;
            string id = null;
            if (libraryBuildAction == LibraryBuildAction.Create)
            {
                authority = LibraryAuthority;
                if (Uri.CheckHostName(authority) != UriHostNameType.Dns)
                {
                    _helper.ShowTextBoxError(textAuthority, Resources.BuildLibraryDlg_ValidateBuilder_The_lab_authority_name__0__is_not_valid_This_should_look_like_an_internet_server_address_e_g_mylab_myu_edu_and_be_unlikely_to_be_used_by_any_other_lab_but_need_not_refer_to_an_actual_server,
                                             authority);
                    return false;
                }
                Settings.Default.LibraryAuthority = authority;

                id = textID.Text;
                if (!Regex.IsMatch(id, @"\w[0-9A-Za-z_\-]*")) // Not L10N: Easier to keep IDs restricted to these values.
                {
                    _helper.ShowTextBoxError(textID, Resources.BuildLibraryDlg_ValidateBuilder_The_library_identifier__0__is_not_valid_Identifiers_start_with_a_letter_number_or_underscore_and_contain_only_letters_numbers_underscores_and_dashes, id);
                    return false;
                }
            }

            if (validateInputFiles)
            {
                var inputFilesChosen = new List<string>();
                foreach (int i in listInputFiles.CheckedIndices)
                {
                    inputFilesChosen.Add(_inputFileNames[i]);
                }

                List<string> targetPeptidesChosen = null;
                if (cbFilter.Checked)
                {
                    targetPeptidesChosen = new List<string>();
                    var doc = _documentUiContainer.Document;
                    foreach (PeptideDocNode nodePep in doc.Peptides)
                    {
                        // Add light modified sequences
                        targetPeptidesChosen.Add(nodePep.ModifiedSequence);
                        // Add heavy modified sequences
                        foreach (var nodeGroup in nodePep.TransitionGroups)
                        {
                            if (nodeGroup.TransitionGroup.LabelType.IsLight)
                                continue;
                            targetPeptidesChosen.Add(doc.Settings.GetModifiedSequence(nodePep.Peptide.Sequence,
                                                                                      nodeGroup.TransitionGroup.LabelType,
                                                                                      nodePep.ExplicitMods));
                        }
                    }
                }

                _builder = new BiblioSpecLiteBuilder(name, outputPath, inputFilesChosen, targetPeptidesChosen)
                              {
                                  Action = libraryBuildAction,
                                  IncludeAmbiguousMatches = cbIncludeAmbiguousMatches.Checked,
                                  KeepRedundant = LibraryKeepRedundant,
                                  CutOffScore = cutOffScore,
                                  Authority = authority,
                                  Id = id
                              };
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
                    outputPath = Path.GetDirectoryName(outputPath) ?? string.Empty;                
                }
                catch (Exception)
                {
                    outputPath = string.Empty;
                }
            }
            string id = (name.Length == 0 ? string.Empty : Helpers.MakeId(textName.Text));
            textID.Text = id;
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
            if (!panelProperties.Visible)
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
                wildExts[i] = "*" + RESULTS_EXTS[i]; // Not L10N

            using (var dlg = new OpenFileDialog
                {
                    Title = Resources.BuildLibraryDlg_btnAddFile_Click_Add_Input_Files,
                    InitialDirectory = initialDirectory,
                    CheckPathExists = true,
                    SupportMultiDottedExtensions = true,
                    Multiselect = true,
                    DefaultExt = BiblioSpecLibSpec.EXT,
                    Filter = TextUtil.FileDialogFiltersAll(
                        Resources.BuildLibraryDlg_btnAddFile_Click_Matched_Peptides + string.Join(",", wildExts) + ")|" + // Not L10N
                        string.Join(";", wildExts), // Not L10N
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

        public static string[] AddInputFiles(Form parent, IEnumerable<string> inputFileNames, IEnumerable<string> fileNames)
        {
            var filesNew = new List<string>(inputFileNames);
            var filesError = new List<string>();
            foreach (var fileName in fileNames)
            {
                if (IsValidInputFile(fileName))
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
                                  "\t" + string.Join("\n\t", filesError.ToArray())); // Not L10N                    
                    MessageDlg.Show(parent, message);
                }
            }

            return filesNew.ToArray();
        }

        private static bool IsValidInputFile(string fileName)
        {
            foreach (string extResult in RESULTS_EXTS)
            {
                if (PathEx.HasExtension(fileName, extResult))
                    return true;
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
            bool append = Equals(comboAction.SelectedItem, LibraryBuildAction.Append.GetLocalizedString());
            textAuthority.Enabled = !append;
            textID.Enabled = !append;
            if (append)
            {
                textAuthority.Text = string.Empty;
                textID.Text = string.Empty;
                cbKeepRedundant.Checked = true;
            }
            else
            {
                textID.Text = Helpers.MakeId(textName.Text);
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

        public string LibraryAuthority
        {
            get { return textAuthority.Text; }
            set { textAuthority.Text = value; }
        }

        public string LibraryId
        {
            get { return textID.Text; }
            set { textID.Text = value; }
        }

        public bool LibraryKeepRedundant
        {
            get { return cbKeepRedundant.Checked; }
            set { cbKeepRedundant.Checked = value; }
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
    }
}
