//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the Bumberdash project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari, Matt Chambers
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using BumberDash.Model;
using CustomDataSourceDialog;
using pwiz.CLI.msdata;

namespace BumberDash.Forms
{
    public sealed partial class AddJobForm : Form
    {
        internal Form _parentForm;
        private IList<ConfigFile> _templateList;

        /// <summary>
        ///  Dialogue used to create new job. All fields empty
        ///  </summary>
        /// <param name="oldFiles">List of used History Items</param>
        /// <param name="templates"> List of available templates</param>
        public AddJobForm(IList<ConfigFile> templates, Form parentForm)
        {
            InitializeComponent();
            SearchTypeBox.Text = JobType.Myrimatch;
            InputMethodBox.Text = "File List";
            _templateList = templates;
            _parentForm = parentForm;
        }

        /// <summary>
        /// Dialogue used to create new job. Fields pre-filled and appearance changed to edit mode if true
        /// </summary>
        /// <param name="oldFiles">List of used HistoryItems</param>
        /// <param name="hi">History Item to clone from</param>
        /// <param name="editMode">True if form should visually appear to be an edit box</param>
        /// <param name="templates">List of available templates</param>
        public AddJobForm(HistoryItem hi, IList<ConfigFile> templates, bool editMode, Form parentForm)
        {
            InitializeComponent();
            SearchTypeBox.Text = JobType.Myrimatch;
            SetHistoryItem(hi);
            if (editMode)
            {
                Text = "Edit Job";
                AddJobRunButton.Text = "Save";
            }
            _templateList = templates;
            _parentForm = parentForm;
        }

        #region Events

        /// <summary>
        /// Browse for protein database file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DatabaseLocButton_Click(object sender, EventArgs e)
        {
            var initialDir = Properties.Settings.Default.DatabaseFolder;
            if (string.IsNullOrEmpty(initialDir) || !Directory.Exists(initialDir))
                initialDir = OutputDirectoryBox.Text;
            if (string.IsNullOrEmpty(initialDir) || !Directory.Exists(initialDir))
                initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            var fileLoc = new OpenFileDialog
                              {
                                  InitialDirectory = initialDir,
                                  RestoreDirectory = true,
                                  Filter = "FASTA files|*.fasta;*.fa;*.seq;*.fsa;*.fna;*.ffn;*.faa;*.frn",
                                  SupportMultiDottedExtensions = true,
                                  CheckFileExists = true,
                                  CheckPathExists = true,
                                  Multiselect = false,
                                  Title = "Myrimatch Location"                                  
                              };
            if (fileLoc.ShowDialog() == DialogResult.OK)
            {
                DatabaseLocBox.Text = fileLoc.FileName;
                var parentDir = Path.GetDirectoryName(fileLoc.FileName);
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    Properties.Settings.Default.DatabaseFolder = parentDir;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void SpecLibBrowse_Click(object sender, EventArgs e)
        {
            var fileLoc = new OpenFileDialog
            {
                InitialDirectory = OutputDirectoryBox.Text,
                RestoreDirectory = true,
                Filter = "Spectral library index files|*.sptxt.index|Spectral library text files|*.sptxt|All files|*.*",
                SupportMultiDottedExtensions = true,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                Title = "Spectral Library Location"
            };
            if (fileLoc.ShowDialog() == DialogResult.OK)
                SpecLibBox.Text = fileLoc.FileName;
        }

        /// <summary>
        /// Browse for the output directory
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InitialDirectoryButton_Click(object sender, EventArgs e)
        {
            var folderDialog = new FolderBrowserDialog
                                   {
                                       Description = "Working Directory",
                                       SelectedPath = OutputDirectoryBox.Text,
                                       ShowNewFolderButton = false                                     
                                   };
            if (folderDialog.ShowDialog() == DialogResult.OK)
                OutputDirectoryBox.Text = folderDialog.SelectedPath;
        }

        /// <summary>
        /// Allow user to select what files will be run in the job
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddDataFilesButton_Click(object sender, EventArgs e)
        {
            var browseToFileDialog = new OpenDataSourceDialog(new List<string>
                                                  {
                                                      "Any spectra format",
                                                      "mzML",
                                                      "mzXML",
                                                      "MZ5",
                                                      "Thermo RAW",
                                                      "ABSciex WIFF",
                                                      "Bruker Analysis",
                                                      "Agilent MassHunter",
                                                      "Mascot Generic",
                                                      "Bruker Data Exchange"
                                                  });
            browseToFileDialog.FolderType = x =>
            {
                try
                {
                    string type = ReaderList.FullReaderList.identify(x);
                    if (type == String.Empty)
                        return "File Folder";
                    return type;
                }
                catch { return String.Empty; }
            };
            browseToFileDialog.FileType = x =>
            {
                try
                {
                    return ReaderList.FullReaderList.identify(x);
                }
                catch { return String.Empty; }
            };

            if (browseToFileDialog.ShowDialog() == DialogResult.OK
                && browseToFileDialog.DataSources.Count >= 1)
            {
                var selectedFiles = browseToFileDialog.DataSources;
                OutputDirectoryBox.Text = Directory.GetParent(selectedFiles[0]).ToString();
                var usedFiles = GetInputFileNames();
                foreach (var file in selectedFiles)
                {
                    if (usedFiles.Contains(file))
                        continue;
                    var currentRow = InputFilesList.Rows.Count;
                    var newItem = new[] { Path.GetFileName(file) };
                    InputFilesList.Rows.Insert(currentRow,newItem);
                    InputFilesList.Rows[currentRow].Cells[0].ToolTipText = "\"" + file + "\"";
                }

                if (string.IsNullOrEmpty(NameBox.Text))
                    NameBox.Text = (new DirectoryInfo(OutputDirectoryBox.Text)).Name;
            }

        }

        /// <summary>
        /// Select config file for MyriMatch run
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MyriConfigButton_Click(object sender, EventArgs e)
        {
            var fileLoc = new OpenFileDialog
                              {
                                  InitialDirectory = OutputDirectoryBox.Text,
                                  RestoreDirectory = true,
                                  Filter = "Config Files|*.cfg|pepXML Files|*.pepXML|All files|*.*",
                                  CheckFileExists = true,
                                  CheckPathExists = true,
                                  Multiselect = false,
                                  Title = "MyriMatch config file location"
                              };
            if (fileLoc.ShowDialog() == DialogResult.OK)
            {
                if (Path.GetExtension(fileLoc.FileName) == ".cfg")
                    MyriConfigBox.Tag = new ConfigFile
                        {
                            Name = Path.GetFileName(fileLoc.FileName),
                            FilePath = fileLoc.FileName,
                            DestinationProgram = "MyriMatch",
                        };
                else
                {
                    MyriConfigBox.Tag = PepXMLToCustomConfig(fileLoc.FileName, "MyriMatch");
                    MyriConfigBox.Text = Path.GetFileName(fileLoc.FileName);
                }

                MyriConfigBox.Text = fileLoc.FileName;
            }
        }

        /// <summary>
        /// Select config file for DirecTag run
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DTConfigButton_Click(object sender, EventArgs e)
        {
            var fileLoc = new OpenFileDialog
                              {
                                  InitialDirectory = OutputDirectoryBox.Text,
                                  RestoreDirectory = true,
                                  Filter = "Config Files|*.cfg|Tags Files|*.tags|All files|*.*",
                                  CheckFileExists = true,
                                  CheckPathExists = true,
                                  Multiselect = false,
                                  Title = "DirecTag config file location"
                              };
            if (fileLoc.ShowDialog() == DialogResult.OK)
            {
                if (Path.GetExtension(fileLoc.FileName) == ".cfg")
                    DTConfigBox.Tag = null;
                else
                {
                    DTConfigBox.Tag = TagsFileToEntireFileString(fileLoc.FileName);
                    DirecTagInfoBox.Text = Path.GetFileName(fileLoc.FileName);
                }

                DTConfigBox.Text = fileLoc.FileName;
            }
        }

        /// <summary>
        /// Select config file for TagRecon run
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TRConfigButton_Click(object sender, EventArgs e)
        {
            var fileLoc = new OpenFileDialog
                              {
                                  InitialDirectory = OutputDirectoryBox.Text,
                                  RestoreDirectory = true,
                                  Filter = "Config Files|*.cfg|pepXML Files|*.pepXML|All files|*.*",
                                  CheckFileExists = true,
                                  CheckPathExists = true,
                                  Multiselect = false,
                                  Title = "TagRecon config file location"
                              };
            if (fileLoc.ShowDialog() == DialogResult.OK)
            {
                if (Path.GetExtension(fileLoc.FileName) == ".cfg")
                    TRConfigBox.Tag = null;
                else
                {
                    TRConfigBox.Tag =  PepXMLToCustomConfig(fileLoc.FileName,"TagRecon");
                    TagReconInfoBox.Text = Path.GetFileName(fileLoc.FileName);
                }

                TRConfigBox.Text = fileLoc.FileName;
            }
        }

        private void PepConfigBrowse_Click(object sender, EventArgs e)
        {
            var fileLoc = new OpenFileDialog
            {
                InitialDirectory = OutputDirectoryBox.Text,
                RestoreDirectory = true,
                Filter = "Config Files|*.cfg|pepXML Files|*.pepXML|All files|*.*",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                Title = "Pepitome config file location"
            };
            if (fileLoc.ShowDialog() == DialogResult.OK)
            {
                if (Path.GetExtension(fileLoc.FileName) == ".cfg")
                    PepConfigBox.Tag = null;
                else
                {
                    PepConfigBox.Tag = PepXMLToCustomConfig(fileLoc.FileName,"Pepitome");
                    PepitomeInfoBox.Text = Path.GetFileName(fileLoc.FileName);
                }

                PepConfigBox.Text = fileLoc.FileName;
            }
        }

        /// <summary>
        /// Check if job is valid before sending an OK
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddJobRunButton_Click(object sender, EventArgs e)
        {
            if (!IsValidJob())
                DialogResult = DialogResult.None;
        }

        /// <summary>
        /// Run OldConfigForm on DirecTag config
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConfigEditButton_Click(object sender, EventArgs e)
        {
            Control configBox;
            Control editButton;
            Control infoBox;
            string destinationProgram;

            if (sender == MyriEditButton)
            {
                configBox = MyriConfigBox;
                editButton = MyriEditButton;
                infoBox = MyriMatchInfoBox;
                destinationProgram = "MyriMatch";
            }
            else if (sender == DTEditButton)
            {
                configBox = DTConfigBox;
                editButton = DTEditButton;
                infoBox = DirecTagInfoBox;
                destinationProgram = "DirecTag";
            }
            else if (sender == TREditButton)
            {
                configBox = TRConfigBox;
                editButton = TREditButton;
                infoBox = TagReconInfoBox;
                destinationProgram = "TagRecon";
            }
            else
            {
                configBox = PepConfigBox;
                editButton = PepEditButton;
                infoBox = PepitomeInfoBox;
                destinationProgram = "Pepitome";
            }

            var defaultName = string.IsNullOrEmpty(configBox.Text)
                ? NameBox.Text ?? string.Empty
                : configBox.Text;

            var testConfigForm = (editButton.Text == "Edit" || editButton.Text == "Convert")
                                     ? new ConfigForm(GetConfigFile(destinationProgram), OutputDirectoryBox.Text ?? string.Empty, defaultName, _templateList)
                                     : new ConfigForm(destinationProgram, OutputDirectoryBox.Text ?? string.Empty, NameBox.Text ?? string.Empty, _templateList);
            if (CometConfigBox.Tag != null)
                testConfigForm.ProgramSelectComet.Checked = true;
            if (MSGFConfigBox.Tag != null)
                testConfigForm.ProgramSelectMSGF.Checked = true;

            if (testConfigForm.ShowDialog(this) == DialogResult.OK)
            {
                ConfigFile mainConfig = testConfigForm.GetMainConfigFile();
                if (destinationProgram == "MyriMatch")
                {
                    MyriActiveBox.Checked = mainConfig != null;
                    ConfigFile secondaryConfig = testConfigForm.GetCometConfig();
                    CometActiveBox.Checked = secondaryConfig != null;
                    if (secondaryConfig == null)
                    {
                        CometConfigBox.Tag = null;
                        CometConfigBox.Text = string.Empty;
                    }
                    else
                    {
                        CometConfigBox.Tag = secondaryConfig;
                        CometConfigBox.Text = "--Custom--";
                    }
                    secondaryConfig = testConfigForm.GetMSGFConfig();
                    MSGFActiveBox.Checked = secondaryConfig != null;
                    if (secondaryConfig == null)
                    {
                        MSGFConfigBox.Tag = null;
                        MSGFConfigBox.Text = string.Empty;
                    }
                    else
                    {
                        MSGFConfigBox.Tag = secondaryConfig;
                        MSGFConfigBox.Text = "--Custom--";
                    }
                }
                if (mainConfig == null)
                {
                    configBox.Tag = null;
                    configBox.Text = string.Empty;
                    infoBox.Text = string.Empty;
                    editButton.Text = "New";
                }
                else
                {
                    configBox.Tag = mainConfig;
                    configBox.Text = mainConfig.Name;
                    infoBox.Text = mainConfig.GetDescription();
                    editButton.Text = "Edit";
                }
            }
        }

        /// <summary>
        /// Display "Auto" if CPUs value is 0
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CPUsBox_ValueChanged(object sender, EventArgs e)
        {
            CPUsAutoLabel.Visible = CPUsBox.Value == 0;
        }

        private void ConfigBox_TextChanged(object sender, EventArgs e)
        {
            var configBox = (Control)sender;
            Control info;
            Control button;
            if (configBox == MyriConfigBox)
            {
                info = MyriMatchInfoBox;
                button = MyriEditButton;
            }
            else if (configBox == DTConfigBox)
            {
                info = DirecTagInfoBox;
                button = DTEditButton;
            }
            else if (configBox == TRConfigBox)
            {
                info = TagReconInfoBox;
                button = TREditButton;
            }
            else
            {
                info = PepitomeInfoBox;
                button = PepEditButton;
            }
            

            if (configBox.Tag == null)
            {
                if (string.IsNullOrEmpty(configBox.Text))
                {
                    info.Text = string.Empty;
                    button.Text = "New";
                }
                else if ((new FileInfo(configBox.Text)).Extension.Equals(".cfg"))
                {
                    button.Text = "Edit";

                    //preview file
                    if (File.Exists(configBox.Text))
                    {
                        var fileIn = new StreamReader(configBox.Text);
                        var contents = fileIn.ReadToEnd();
                        if (System.Text.RegularExpressions.Regex.IsMatch(contents.ToLower(), "deisotopingmode *= *[12]")
                            &&
                            !System.Text.RegularExpressions.Regex.IsMatch(info.Text.ToLower(),
                                                                          "deisotopingmode *= *[12]"))
                            MessageBox.Show(
                                "Warning- Deisotoping mode is currently unstable. Use of this parameter may cause unexpected behavior.");
                        info.Text = contents;
                        fileIn.Close();
                        fileIn.Dispose();
                    }
                    else
                        info.Text = "File Not Found";
                }
                else
                    info.Text = "Invalid File";
            }
            else if (((string)configBox.Tag) == "--Custom--")
                button.Text = "Edit";
            else
            {
                info.Text = string.Empty;
                button.Text = "New";
            }
        }

        /// <summary>
        /// Do not allow any manual editing to the config box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConfigBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        #endregion

        /// <summary>
        /// Checks if form currently contains valid job parameters
        /// </summary>
        /// <returns></returns>
        private bool IsValidJob()
        {
            var allValid = true;
            //var extensionList = new List<string>
            //                            {
            //                                ".raw", ".wiff", ".yep",
            //                                ".mzml", ".mz5", ".mgf", ".mzxml"
            //                            };


            // Get all input files and validate that they exist
            var inputFiles = GetInputFileNames();
            if (InputMethodBox.Text == "File List" && inputFiles.Any(fileName => !File.Exists(fileName.Trim('"'))))
            {
                allValid = false;
                InputFilesList.BackgroundColor = Color.LightPink;
            }
            else if (InputMethodBox.Text == "File Mask" &&
                (!Directory.Exists(InputDirectoryBox.Text) || String.IsNullOrEmpty(FileMaskBox.Text) ||
                MaskMessageLabel.Text.StartsWith("Error")))
            {
                allValid = false;
                InputFilesList.BackgroundColor = Color.LightPink;
            }

            if (allValid)
                InputFilesList.BackgroundColor = Color.White;

            // Validate Output Directory
            if (Directory.Exists(OutputDirectoryBox.Text) &&
                !string.IsNullOrEmpty(OutputDirectoryBox.Text))
                OutputDirectoryBox.BackColor = Color.White;
            else
            {
                allValid = false;
                OutputDirectoryBox.BackColor = Color.LightPink;
            }

            // Validate Myrimatch Location
            if (File.Exists(DatabaseLocBox.Text) &&
                (Path.GetExtension(DatabaseLocBox.Text) ?? string.Empty).ToLower() == (".fasta"))
                DatabaseLocBox.BackColor = Color.White;
            else
            {
                allValid = false;
                DatabaseLocBox.BackColor = Color.LightPink;
            }

            // Validate Config Files
            //If Myrimatch Search
            if (SearchTypeBox.Text == JobType.Myrimatch)
            {
                if (!MyriActiveBox.Checked || GetConfigFile("MyriMatch") != null)
                    MyriConfigBox.BackColor = Color.White;
                else
                {
                    allValid = false;
                    MyriConfigBox.BackColor = Color.LightPink;
                }
                if (!CometActiveBox.Checked || GetConfigFile("Comet") != null)
                    CometConfigBox.BackColor = Color.White;
                else
                {
                    allValid = false;
                    CometConfigBox.BackColor = Color.LightPink;
                }
                if (!MSGFActiveBox.Checked || GetConfigFile("MSGF") != null)
                    MSGFConfigBox.BackColor = Color.White;
                else
                {
                    allValid = false;
                    MSGFConfigBox.BackColor = Color.LightPink;
                }
            }
            //If Tag Sequencing
            else if (SearchTypeBox.Text == JobType.Tag)
            {
                if (GetConfigFile("DirecTag") != null)
                    DTConfigBox.BackColor = Color.White;
                else
                {
                    allValid = false;
                    DTConfigBox.BackColor = Color.LightPink;
                }

                if (GetConfigFile("TagRecon") != null)
                    TRConfigBox.BackColor = Color.White;
                else
                {
                    allValid = false;
                    TRConfigBox.BackColor = Color.LightPink;
                }

            }
            //If library searching
            else if (SearchTypeBox.Text == JobType.Library)
            {
                if (File.Exists(SpecLibBox.Text))
                    SpecLibBox.BackColor = Color.White;
                else
                {
                    allValid = false;
                    DatabaseLocBox.BackColor = Color.LightPink;
                }

                if (GetConfigFile("Pepitome") != null)
                    PepConfigBox.BackColor = Color.White;
                else
                {
                    allValid = false;
                    PepConfigBox.BackColor = Color.LightPink;
                }

            }

            return allValid;
        }

        /// <summary>
        /// Populates fields with recieved values
        /// </summary>
        /// <param name="hi"></param>
        private void SetHistoryItem(HistoryItem hi)
        {
            var fileList = hi.FileList.Select(file => file.FilePath).Distinct().ToList();

            NameBox.Text = hi.JobName;
            CPUsBox.Value = hi.Cpus;

            if (hi.FileList.Count == 1 && hi.FileList[0].FilePath.StartsWith("!"))
            {
                InputMethodBox.Text = "File Mask";
                var fullMask = hi.FileList[0].FilePath.Trim('!');
                var initialDir = Path.GetDirectoryName(fullMask);
                var mask = Path.GetFileName(fullMask);
                InputDirectoryBox.Text = initialDir;
                FileMaskBox.Text = mask;
            }
            else
            {
                InputMethodBox.Text = "File List";
                var usedFiles = GetInputFileNames();
                foreach (var file in fileList)
                {
                    if (usedFiles.Contains(file))
                        continue;
                    var currentRow = InputFilesList.Rows.Count;
                    var newItem = new[] { Path.GetFileName(file.Trim('"')) };
                    InputFilesList.Rows.Insert(currentRow, newItem);
                    InputFilesList.Rows[currentRow].Cells[0].ToolTipText = file;
                }
            }
            
            DatabaseLocBox.Text = hi.ProteinDatabase;

            //Output directory handled differently depending on if new folder is/was created
            if (hi.OutputDirectory.EndsWith("+"))
            {
                //folder has not been made yet, just indicate it is to be made
                newFolderBox.Checked = true;
                OutputDirectoryBox.Text = hi.OutputDirectory.TrimEnd('+');
            }
            else if (hi.OutputDirectory.EndsWith("*"))
            {
                //Folder has already been made, trim created folder and indicate new folder is to be made
                var baseDirectory = new DirectoryInfo(hi.OutputDirectory.TrimEnd('*'));

                newFolderBox.Checked = true;
                OutputDirectoryBox.Text = baseDirectory.Parent != null
                                              ? baseDirectory.Parent.FullName
                                              : hi.OutputDirectory.TrimEnd('*');
            }
            else
            {
                newFolderBox.Checked = false;
                OutputDirectoryBox.Text = hi.OutputDirectory;
            }

            if (hi.JobType == JobType.Tag || (hi.JobType == null && hi.TagConfigFile != null))
            {
                if (hi.InitialConfigFile.Name == null)
                    DTConfigBox.Text = hi.InitialConfigFile.FilePath;
                else
                {
                    DTConfigBox.Tag = "--Custom--";
                    DTConfigBox.Text = hi.InitialConfigFile.Name;
                }
                DirecTagInfoBox.Clear();
                foreach (var property in hi.InitialConfigFile.PropertyList)
                    DirecTagInfoBox.Text += string.Format("{0} = {1}{2}", property.Name, property.Value, Environment.NewLine);

                if (hi.TagConfigFile.Name == null)
                    TRConfigBox.Text = hi.TagConfigFile.FilePath;
                else
                {
                    TRConfigBox.Tag = "--Custom--";
                    TRConfigBox.Text = hi.TagConfigFile.Name;
                }
                TagReconInfoBox.Clear();
                foreach (var property in hi.TagConfigFile.PropertyList)
                    TagReconInfoBox.Text += string.Format("{0} = {1}{2}", property.Name, property.Value, Environment.NewLine);

                SearchTypeBox.Text = JobType.Tag;
            }
            else if (hi.JobType == JobType.Library || (hi.JobType == null && hi.SpectralLibrary != null))
            {
                SpecLibBox.Text = hi.SpectralLibrary;

                if (hi.InitialConfigFile.Name == null)
                    PepConfigBox.Text = hi.InitialConfigFile.FilePath;
                else
                {
                    PepConfigBox.Tag = "--Custom--";
                    PepConfigBox.Text = hi.InitialConfigFile.Name;
                }
                PepitomeInfoBox.Clear();
                foreach (var property in hi.InitialConfigFile.PropertyList)
                    PepitomeInfoBox.Text += string.Format("{0} = {1}{2}", property.Name, property.Value, Environment.NewLine);
                SearchTypeBox.Text = JobType.Library;
            }
            else
            {
                if (hi.InitialConfigFile.Name == null)
                    MyriConfigBox.Text = hi.InitialConfigFile.FilePath;
                else
                {
                    MyriConfigBox.Tag = "--Custom--";
                    MyriConfigBox.Text = hi.InitialConfigFile.Name;
                }
                MyriMatchInfoBox.Clear();
                foreach (var property in hi.InitialConfigFile.PropertyList)
                    MyriMatchInfoBox.Text += string.Format("{0} = {1}{2}", property.Name, property.Value, Environment.NewLine);
                SearchTypeBox.Text = JobType.Myrimatch;
            }
        }

        /// <summary>
        /// Returns new history item from form
        /// </summary>
        /// <returns></returns>
        internal HistoryItem GetHistoryItem()
        {
            var hi = new HistoryItem
                         {
                             JobName = NameBox.Text,
                             JobType = SearchTypeBox.Text,
                             OutputDirectory = OutputDirectoryBox.Text +
                                               (newFolderBox.Checked ? "+" : string.Empty),
                             ProteinDatabase = DatabaseLocBox.Text,
                             SpectralLibrary = SearchTypeBox.Text == JobType.Library
                                                   ? SpecLibBox.Text
                                                   : null,
                             Cpus = (int) CPUsBox.Value,
                             CurrentStatus = string.Empty,
                             StartTime = null,
                             EndTime = null,
                             RowNumber = 0,
                             InitialConfigFile = GetConfigFile(
                                 SearchTypeBox.Text == JobType.Myrimatch
                                     ? "MyriMatch"
                                     : (SearchTypeBox.Text == JobType.Tag
                                            ? "DirecTag"
                                            : "Pepitome")),
                             TagConfigFile = SearchTypeBox.Text == JobType.Tag
                                                 ? GetConfigFile("TagRecon")
                                                 : null
                         };

            //File List
            if (hi.FileList == null)
                hi.FileList = new List<InputFile>();
            else
                hi.FileList.Clear();

            var files = GetInputFileNames();
            foreach (var item in files)
                hi.FileList.Add(new InputFile { FilePath = item, HistoryItem = hi });

            return hi;
        }

        public List<HistoryItem> GetHistoryItems()
        {
            var hiList = new List<HistoryItem>();
            var files = GetInputFileNames();
            var outputDir = OutputDirectoryBox.Text;

            //generic hi setup
            var initialHi = new HistoryItem
            {
                JobName = NameBox.Text,
                OutputDirectory = newFolderBox.Checked ? outputDir + "+" : outputDir,
                ProteinDatabase = DatabaseLocBox.Text,
                Cpus = 0,
                CurrentStatus = string.Empty,
                StartTime = null,
                EndTime = null,
                RowNumber = 0,
                TagConfigFile = null,
                FileList = new List<InputFile>()
            };
            foreach (var item in files)
                initialHi.FileList.Add(new InputFile { FilePath = item, HistoryItem = initialHi });

            if (SearchTypeBox.Text == JobType.Library)
            {
                initialHi.JobType = JobType.Library;
                initialHi.SpectralLibrary = SpecLibBox.Text;
                initialHi.InitialConfigFile = GetConfigFile("Pepitome");

                //HACK: Until fix gets pushed out Pepitome should only run with 1 cpu core
                initialHi.Cpus = 1;
            }
            else if (SearchTypeBox.Text == JobType.Tag)
            {
                initialHi.JobType = JobType.Tag;
                initialHi.InitialConfigFile = GetConfigFile("DirecTag");
                initialHi.TagConfigFile = GetConfigFile("TagRecon");
            }
            else if (SearchTypeBox.Text == JobType.Myrimatch)
            {
                var newOutput = initialHi.OutputDirectory;
                initialHi.JobType = JobType.Myrimatch;
                initialHi.InitialConfigFile = GetConfigFile("MyriMatch");
                if (MyriActiveBox.Checked && newFolderBox.Checked)
                    newOutput = Path.Combine(outputDir ?? string.Empty, NameBox.Text);

                var cometHi = new HistoryItem
                {
                    JobType = JobType.Comet,
                    OutputDirectory = newOutput,
                    JobName = initialHi.JobName,
                    ProteinDatabase = initialHi.ProteinDatabase,
                    Cpus = 0,
                    CurrentStatus = string.Empty,
                    StartTime = null,
                    EndTime = null,
                    RowNumber = 0,
                    InitialConfigFile = GetConfigFile("Comet"),
                    FileList = new List<InputFile>()
                };
                var msgfHi = new HistoryItem
                {
                    JobType = JobType.MSGF,
                    OutputDirectory = newOutput,
                    JobName = initialHi.JobName,
                    ProteinDatabase = initialHi.ProteinDatabase,
                    Cpus = 0,
                    CurrentStatus = string.Empty,
                    StartTime = null,
                    EndTime = null,
                    RowNumber = 0,
                    InitialConfigFile = GetConfigFile("MSGF"),
                    FileList = new List<InputFile>()
                };

                foreach (var item in files)
                {
                    cometHi.FileList.Add(new InputFile { FilePath = item, HistoryItem = cometHi });
                    msgfHi.FileList.Add(new InputFile { FilePath = item, HistoryItem = msgfHi });
                }
                if (CometActiveBox.Checked)
                    hiList.Add(cometHi);
                if (MSGFActiveBox.Checked)
                    hiList.Add(msgfHi);
            }
            else
                throw new Exception("None of the known search types were checked." +
                                    " Was there a new one added that hasn't been accounted for?");
            if (SearchTypeBox.Text != JobType.Myrimatch || MyriActiveBox.Checked)
                hiList.Insert(0, initialHi);
            return hiList;
        }

        private ConfigFile GetConfigFile(string destinationProgram)
        {
            var configBox = destinationProgram == "MyriMatch"
                                ? MyriConfigBox
                                : destinationProgram == "Comet"
                                      ? CometConfigBox
                                      : destinationProgram == "MSGF"
                                            ? MSGFConfigBox
                                            : (destinationProgram == "DirecTag"
                                                   ? DTConfigBox
                                                   : (destinationProgram == "Pepitome"
                                                          ? PepConfigBox
                                                          : TRConfigBox));
            return (configBox.Tag as ConfigFile);

        }

        public static ConfigFile PepXMLToCustomConfig(string file, string destinationProgram)
        {
            var newConfig = new ConfigFile
                {
                    DestinationProgram = destinationProgram,
                    Name = Path.GetFileName(file),
                    PropertyList = new List<ConfigProperty>()
                };
            var parameterTypes = Util.parameterTypes;
            var cutFile = string.Empty;
            var fileStream = new StreamReader(file);

            while (!fileStream.EndOfStream)
            {
                var tempString = fileStream.ReadLine() ?? string.Empty;
                if (tempString.Contains("<parameter name=\"Config:"))
                    cutFile += tempString + System.Environment.NewLine;
                else if (cutFile.Length > 0)
                    break;
            }
            fileStream.Close();

            var entireLine = cutFile.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            string[] propertySplit;

            for (int x = 0; x < entireLine.Length; x++)
            {
                //get the two meaningful values
                entireLine[x] = entireLine[x].Replace("<parameter name=\"Config:", " ");
                entireLine[x] = entireLine[x].Replace("\" value=", " ");
                entireLine[x] = entireLine[x].Replace("/>", " ");
                propertySplit = entireLine[x].Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                propertySplit[0] = propertySplit[0].Trim();
                propertySplit[1] = propertySplit[1].Trim();

                //check if value is actually meaningful to the editor
                if (parameterTypes.ContainsKey(propertySplit[0]))
                {
                    if (parameterTypes[propertySplit[0]] == "bool")
                    {
                        propertySplit[1] = propertySplit[1].Trim('\"');
                        propertySplit[1] = Convert.ToBoolean(int.Parse(propertySplit[1])).ToString().ToLower();
                        newConfig.PropertyList.Add(new ConfigProperty
                            {
                                Name = propertySplit[0],
                                Value = propertySplit[1],
                                Type = parameterTypes[propertySplit[0]]
                            });
                    }
                    else if (parameterTypes[propertySplit[0]] == "int"
                             || parameterTypes[propertySplit[0]] == "double")
                    {
                        propertySplit[1] = propertySplit[1].Trim('\"');
                        newConfig.PropertyList.Add(new ConfigProperty
                        {
                            Name = propertySplit[0],
                            Value = propertySplit[1],
                            Type = parameterTypes[propertySplit[0]]
                        });
                    }
                    else if (parameterTypes[propertySplit[0]] == "string")
                    {
                        if (propertySplit.Length > 2)
                        {
                            for (int i = 2; i < propertySplit.Length; i++)
                                propertySplit[1] += " " + propertySplit[i];
                        }
                        newConfig.PropertyList.Add(new ConfigProperty
                        {
                            Name = propertySplit[0],
                            Value = propertySplit[1],
                            Type = parameterTypes[propertySplit[0]]
                        });
                    }
                }
            }


            return newConfig;
        }

        public static ConfigFile TagsFileToEntireFileString(string file)
        {
            var newConfig = new ConfigFile
                {
                    DestinationProgram = "DirecTag",
                    Name = Path.GetFileName(file),
                    PropertyList = new List<ConfigProperty>()
                };
            var parameterTypes = Util.parameterTypes;
            var cutFile = string.Empty;
            var fileStream = new StreamReader(file);
            while (!fileStream.EndOfStream)
            {
                var tempString = fileStream.ReadLine() ?? string.Empty;
                if (tempString.Contains("TagsParameters"))
                {
                    tempString = fileStream.ReadLine();
                    while (!string.IsNullOrEmpty(tempString))
                    {
                        tempString = tempString.Remove(0, 2);
                        cutFile += tempString + ",";
                        tempString = fileStream.ReadLine();
                    }
                    cutFile = cutFile.Trim();
                    break;
                }
            }
            fileStream.Close();

            var entireLine = cutFile.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            string[] propertySplit;

            foreach (var parameter in entireLine)
            {
                //get the two meaningful values
                propertySplit = parameter.Split(":".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                propertySplit[0] = propertySplit[0].Trim();
                propertySplit[1] = propertySplit[1].Trim();

                //check if value is actually meaningful to the editor
                if (parameterTypes.ContainsKey(propertySplit[0]))
                {
                    if (parameterTypes[propertySplit[0]] == "bool")
                    {
                        propertySplit[1] = propertySplit[1].Trim('\"');
                        propertySplit[1] = Convert.ToBoolean(int.Parse(propertySplit[1])).ToString().ToLower();
                        newConfig.PropertyList.Add(new ConfigProperty
                        {
                            Name = propertySplit[0],
                            Value = propertySplit[1],
                            Type = parameterTypes[propertySplit[0]]
                        });
                    }
                    else if (parameterTypes[propertySplit[0]] == "int"
                             || parameterTypes[propertySplit[0]] == "double")
                    {
                        propertySplit[1] = propertySplit[1].Trim('\"');
                        newConfig.PropertyList.Add(new ConfigProperty
                        {
                            Name = propertySplit[0],
                            Value = propertySplit[1],
                            Type = parameterTypes[propertySplit[0]]
                        });
                    }
                    else if (parameterTypes[propertySplit[0]] == "string")
                    {
                        if (propertySplit.Length > 2)
                        {
                            for (int foo = 2; foo < propertySplit.Length; foo++)
                                propertySplit[1] += " " + propertySplit[foo];
                        }
                        newConfig.PropertyList.Add(new ConfigProperty
                        {
                            Name = propertySplit[0],
                            Value = propertySplit[1],
                            Type = parameterTypes[propertySplit[0]]
                        });
                    }
                }
            }

            return newConfig;
        }

        private void SearchTypeBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (SearchTypeBox.Text)
            {
                case JobType.Myrimatch:
                    ConfigGB.Visible = true;
                    PepPanel.Visible = false;
                    DatabaseConfigPanel.Visible = true;
                    DatabaseConfigInfoPanel.Visible = true;
                    ConfigTagPanel.Visible = false;
                    PepConfigInfoPanel.Visible = false;
                    TagConfigInfoPanel.Visible = false;
                    break;
                case JobType.Tag:
                    ConfigGB.Visible = true;
                    PepPanel.Visible = false;
                    DatabaseConfigPanel.Visible = false;
                    DatabaseConfigInfoPanel.Visible = false;
                    ConfigTagPanel.Visible = true;
                    PepConfigInfoPanel.Visible = false;
                    TagConfigInfoPanel.Visible = true;
                    break;
                case JobType.Library:
                    ConfigGB.Visible = false;
                    PepPanel.Visible = true;
                    DatabaseConfigInfoPanel.Visible = true;
                    TagConfigInfoPanel.Visible = false;
                    PepConfigInfoPanel.Visible = true;
                    break;
            }
        }

        private void RemoveDataFilesButton_Click(object sender, EventArgs e)
        {
            var selectedItems = InputFilesList.SelectedRows;
            foreach (DataGridViewRow item in selectedItems)
                InputFilesList.Rows.Remove(item);
        }

        private List<String> GetInputFileNames()
        {
            if (InputMethodBox.Text == "File List")
            {
                var fileList = new List<string>();
                foreach (DataGridViewRow row in InputFilesList.Rows)
                    fileList.Add(row.Cells[0].ToolTipText);
                return fileList;
            }
            else
            {
                return new List<string>{"!" + Path.Combine(InputDirectoryBox.Text, FileMaskBox.Text)};
            }
        }

        private void InputMethodBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            FileListPanel.Visible = (string)InputMethodBox.SelectedItem == "File List";
            FileMaskPanel.Visible = (string)InputMethodBox.SelectedItem == "File Mask";
        }

        private void InputDirectoryButton_Click(object sender, EventArgs e)
        {
            var folderDialog = new FolderBrowserDialog
                                   {
                                       Description = "Input Directory",
                                       SelectedPath = InputDirectoryBox.Text,
                                       ShowNewFolderButton = false                                     
                                   };
            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                InputDirectoryBox.Text = folderDialog.SelectedPath;
                OutputDirectoryBox.Text = folderDialog.SelectedPath;
            }

            CountMaskedFiles(sender,e);
        }

        private void CountMaskedFiles(object sender, EventArgs e)
        {
            if (InputMethodBox.Text == "File Mask" &&
                Directory.Exists(InputDirectoryBox.Text) &&
                !String.IsNullOrEmpty(FileMaskBox.Text))
            {
                try
                {
                    var fileList = Directory.GetFiles(InputDirectoryBox.Text, FileMaskBox.Text);
                    if (fileList.Length > 0)
                    {
                        MaskMessageLabel.ForeColor = Color.DarkGreen;
                        MaskMessageLabel.Text = String.Format("Found {0} files with the given mask", fileList.Length);
                    }
                    else
                    {
                        MaskMessageLabel.ForeColor = Color.DarkRed;
                        MaskMessageLabel.Text = "Error: No files found with given mask";
                    }
                }
                catch (Exception)
                {
                    MaskMessageLabel.ForeColor = Color.DarkRed;
                    MaskMessageLabel.Text = "Error: Unable to process given mask";
                }
            }
        }

        private void FileMaskBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                CountMaskedFiles(sender, e);
            }
        }

        private void CometConfigBrowse_Click(object sender, EventArgs e)
        {
            var fileLoc = new OpenFileDialog
            {
                InitialDirectory = OutputDirectoryBox.Text,
                RestoreDirectory = true,
                Filter = "Config Files|*.params|All files|*.*",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                Title = "Comet config file location"
            };
            if (fileLoc.ShowDialog() == DialogResult.OK)
            {
                var newConfig = new ConfigFile
                    {
                        Name = Path.GetFileName(fileLoc.FileName),
                        FilePath = fileLoc.FileName,
                        DestinationProgram = "Comet"
                    };
                CometConfigBox.Tag = newConfig;
                CometConfigBox.Text = newConfig.Name;
            }
        }

        private void AdvancedModeBox_CheckedChanged(object sender, EventArgs e)
        {
            if (AdvancedModeBox.Checked)
                return;
            AdvancedModeBox.Checked = true;
            _parentForm.Visible = true;
            DialogResult = DialogResult.Cancel;
        }

        private void DBTabBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (DBTabBox.SelectedIndex == MyriTab.TabIndex)
            {
                MyriMatchInfoLabel.Text = "MyriMatch Configuration";
                MyriMatchInfoBox.Text = (MyriConfigBox.Tag as ConfigFile ?? new ConfigFile()).GetDescription();
            }
            else if (DBTabBox.SelectedIndex == CometTab.TabIndex)
            {
                MyriMatchInfoLabel.Text = "Comet Configuration";
                MyriMatchInfoBox.Text = (CometConfigBox.Tag as ConfigFile ?? new ConfigFile()).GetDescription();
            }
            else if (DBTabBox.SelectedIndex == MSGFTab.TabIndex)
            {
                MyriMatchInfoLabel.Text = "MS-GF+ Configuration";
                MyriMatchInfoBox.Text = (MSGFConfigBox.Tag as ConfigFile ?? new ConfigFile()).GetDescription();
            }
        }
    }
}
