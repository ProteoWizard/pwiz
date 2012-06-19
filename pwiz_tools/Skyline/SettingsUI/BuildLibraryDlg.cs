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
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using pwiz.BiblioSpec;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class BuildLibraryDlg : FormEx
    {
        private static readonly string[] RESULTS_EXTS = new[]
            {
                BiblioSpecLiteBuilder.EXT_DAT,
                BiblioSpecLiteBuilder.EXT_PEP_XML,
                BiblioSpecLiteBuilder.EXT_PEP_XML_ONE_DOT,
                BiblioSpecLiteBuilder.EXI_MZID,
                BiblioSpecLiteBuilder.EXT_XTAN_XML,
                BiblioSpecLiteBuilder.EXT_PILOT_XML,
                BiblioSpecLiteBuilder.EXT_IDP_XML,
                BiblioSpecLiteBuilder.EXT_SQT,
                BiblioSpecLiteBuilder.EXT_SSL,
                BiblioSpecLiteBuilder.EXT_PERCOLATOR,
                BiblioSpecLiteBuilder.EXT_PERCOLATOR_XML,
                BiblioSpecLiteBuilder.EXT_WATERS_MSE,
            };

        private BiblioSpecLiteBuilder _builder;

        private string[] _inputFileNames = new string[0];
        private string _dirInputRoot = "";

        private readonly MessageBoxHelper _helper;

        public BuildLibraryDlg()
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            panelFiles.Visible = false;

            textName.Focus();
            textPath.Text = Settings.Default.LibraryDirectory;
            comboAction.SelectedItem = LibraryBuildAction.Create.ToString();
            textCutoff.Text = Settings.Default.LibraryResultCutOff.ToString(CultureInfo.CurrentCulture);
            textAuthority.Text = Settings.Default.LibraryAuthority;

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

        private bool ValidateBuilder(CancelEventArgs e, bool validateInputFiles)
        {
            string name;
            if (!_helper.ValidateNameTextBox(e, textName, out name))
                return false;

            string outputPath = textPath.Text;
            if (string.IsNullOrEmpty(outputPath))
            {
                _helper.ShowTextBoxError(textPath, "You must specify an output file path.", outputPath);
                return false;                
            }
            if (Directory.Exists(outputPath))
            {
                _helper.ShowTextBoxError(textPath, "The output path {0} is a directory.  You must specify a file path.", outputPath);
                return false;                
            }
            string outputDir = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
            {
                _helper.ShowTextBoxError(textPath, "The directory {0} does not exist.", outputDir);
                return false;
            }
            if (!outputPath.EndsWith(BiblioSpecLiteSpec.EXT))
                outputPath += BiblioSpecLiteSpec.EXT;

            double cutOffScore;
            if (!_helper.ValidateDecimalTextBox(e, textCutoff, 0, 1.0, out cutOffScore))
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
                    _helper.ShowTextBoxError(e, textAuthority,
                                             "The lab authority name {0} is not valid.  This should look like an internet server address (e.g. mylab.myu.edu), and be unlikely to be used by any other lab, but need not refer to an actual server.",
                                             authority);
                    return false;
                }
                Settings.Default.LibraryAuthority = authority;

                id = textID.Text;
                if (!Regex.IsMatch(id, @"\w[0-9A-Za-z_\-]*"))
                {
                    _helper.ShowTextBoxError(e, textID, "The library identifier {0} is not valid.  Identifiers start with a letter, number or underscore, and contain only letters, numbers, underscores and dashes.", id);
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

                _builder = new BiblioSpecLiteBuilder(name, outputPath, inputFilesChosen)
                              {
                                  Action = libraryBuildAction,
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
                    outputPath = Path.GetDirectoryName(outputPath) ?? "";                
                }
                catch (Exception)
                {
                    outputPath = "";
                }
            }
            string id = (name.Length == 0 ? "" : Helpers.MakeId(textName.Text));
            textID.Text = id;
            textPath.Text = id.Length == 0
                                ? outputPath
                                : Path.Combine(outputPath, id + ".blib");
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
                fileName = "";
            }

            SaveFileDialog dlg = new SaveFileDialog
            {
                InitialDirectory = Settings.Default.LibraryDirectory,
                FileName = fileName,                
                OverwritePrompt = true,
                DefaultExt = BiblioSpecLiteSpec.EXT,
                Filter = string.Join("|", new []
                    {
                        "BiblioSpec Library (*" + BiblioSpecLiteSpec.EXT + ")|*" + BiblioSpecLiteSpec.EXT,
                        "All Files (*.*)|*.*"
                    })
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                Settings.Default.LibraryDirectory = Path.GetDirectoryName(dlg.FileName);

                textPath.Text = dlg.FileName;
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
                var e = new CancelEventArgs();
                ValidateBuilder(e, true);
                if (!e.Cancel)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            else if (ValidateBuilder(new CancelEventArgs(), false))
            {
                Settings.Default.LibraryDirectory = Path.GetDirectoryName(LibraryPath);

                panelProperties.Visible = false;
                panelFiles.Visible = true;
                btnPrevious.Enabled = true;
                btnNext.Text = "Finish";
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
                btnNext.Text = "Next >";
                btnNext.Enabled = true;
                btnNext.DialogResult = DialogResult.None;
                AcceptButton = null;
                btnPrevious.Enabled = false;
            }
        }

        private void btnAddFile_Click(object sender, EventArgs e)
        {
            var wildExts = new string[RESULTS_EXTS.Length];
            for (int i = 0; i < wildExts.Length; i++)
                wildExts[i] = "*" + RESULTS_EXTS[i];

            OpenFileDialog dlg = new OpenFileDialog
            {
                Title = "Add Input Files",
                InitialDirectory = Settings.Default.LibraryResultsDirectory,
                CheckPathExists = true,
                SupportMultiDottedExtensions = true,
                Multiselect = true,
                DefaultExt = BiblioSpecLibSpec.EXT,
                Filter = string.Join("|", new[]
                    {
                        "Matched Peptides (" + string.Join(",", wildExts) + ")|" + string.Join(";", wildExts),
                        "Spectral Libraries (*" + BiblioSpecLiteSpec.EXT + ")|*" + BiblioSpecLiteSpec.EXT,
                        "All Files (*.*)|*.*"
                    })
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                Settings.Default.LibraryResultsDirectory = Path.GetDirectoryName(dlg.FileName);

                AddInputFiles(dlg.FileNames);
            }
        }

        private void btnAddDirectory_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog
            {
                Description = "Add Input Directory",
                ShowNewFolderButton = false,
                SelectedPath = Settings.Default.LibraryResultsDirectory
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                Settings.Default.LibraryResultsDirectory = dlg.SelectedPath;

                AddDirectory(dlg.SelectedPath);
            }
        }

        public void AddDirectory(string dirPath)
        {
            var longWaitDlg = new LongWaitDlg
            {
                Text = "Find Input Files",
            };

            try
            {
                var inputFiles = new List<string>();
                longWaitDlg.PerformWork(this, 800, broker => FindInputFiles(dirPath, inputFiles, broker));
                AddInputFiles(inputFiles);
            }
            catch (Exception x)
            {
                MessageBox.Show(this, string.Format("An error occurred reading files in the directory {0}.\n{1}", dirPath, x.Message), Program.Name);
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
            broker.Message = string.Format("Finding library input files in\n{0}", PathEx.ShortenPathForDisplay(dir));

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
            var filesNew = new List<string>(InputFileNames);
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

            InputFileNames = filesNew.ToArray();

            if (filesError.Count > 0)
            {
                if (filesError.Count == 1)
                    MessageBox.Show(this, string.Format("The file {0} is not a valid library input file.", filesError[0]), Program.Name);
                else
                    MessageBox.Show(this, string.Format("The following files are not valid library input files:\n\n\t{0}", string.Join("\n\t", filesError.ToArray())));
            }
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
                    existsRedundant = File.Exists(Path.Combine(Path.GetDirectoryName(textPath.Text) ?? "", redundantName));
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
                    comboAction.SelectedItem = LibraryBuildAction.Create.ToString();
                    comboAction.Enabled = false;
                }
            }
        }

        private void comboAction_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool append = Equals(comboAction.SelectedItem, LibraryBuildAction.Append.ToString());
            textAuthority.Enabled = !append;
            textID.Enabled = !append;
            if (append)
            {
                textAuthority.Text = "";
                textID.Text = "";
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

            set { textCutoff.Text = value.ToString(CultureInfo.CurrentCulture); }
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
