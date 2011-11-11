using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using BumberDash.Model;

namespace BumberDash.Forms
{
    public sealed partial class AddJobForm : Form
    {
        #region Globals
        internal Dictionary<int, ConfigFile> MyriDropDownItems = new Dictionary<int, ConfigFile>();
        internal Dictionary<int, ConfigFile> DTDropDownItems = new Dictionary<int, ConfigFile>();
        internal Dictionary<int, ConfigFile> TRDropDownItems = new Dictionary<int, ConfigFile>();
        internal Dictionary<int, ConfigFile> PepDropDownItems = new Dictionary<int, ConfigFile>();
        private IList<ConfigFile> _templateList;
        #endregion

        /// <summary>
        ///  Dialogue used to create new job. All fields empty
        ///  </summary>
        /// <param name="oldFiles">List of used History Items</param>
        /// <param name="templates"> List of available templates</param>
        public AddJobForm(IEnumerable<HistoryItem> oldFiles, IList<ConfigFile> templates)
        {
            InitializeComponent();
            SearchTypeBox.Text = JobType.Database;
            SetDropDownItems(oldFiles);
            _templateList = templates;
        }

        /// <summary>
        /// Dialogue used to create new job. Fields pre-filled and appearance changed to edit mode if true
        /// </summary>
        /// <param name="oldFiles">List of used HistoryItems</param>
        /// <param name="hi">History Item to clone from</param>
        /// <param name="editMode">True if form should visually appear to be an edit box</param>
        /// <param name="templates">List of available templates</param>
        public AddJobForm(IEnumerable<HistoryItem> oldFiles, HistoryItem hi, bool editMode, IList<ConfigFile> templates)
        {
            InitializeComponent();
            SearchTypeBox.Text = JobType.Database;
            SetDropDownItems(oldFiles);
            SetHistoryItem(hi);
            if (editMode)
            {
                Text = "Edit Job";
                AddJobRunButton.Text = "Save";
            }
            _templateList = templates;
        }

        #region Events

        /// <summary>
        /// Browse for protein database file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DatabaseLocButton_Click(object sender, EventArgs e)
        {
            var fileLoc = new OpenFileDialog
                              {
                                  InitialDirectory = OutputDirectoryBox.Text,
                                  RestoreDirectory = true,
                                  Filter = "FASTA files|*.fasta;*.fa;*.seq;*.fsa;*.fna;*.ffn;*.faa;*.frn",
                                  SupportMultiDottedExtensions = true,
                                  CheckFileExists = true,
                                  CheckPathExists = true,
                                  Multiselect = false,
                                  Title = "Database Location"                                  
                              };
            if (fileLoc.ShowDialog() == DialogResult.OK)
                DatabaseLocBox.Text = fileLoc.FileName;
        }

        private void SpecLibBrowse_Click(object sender, EventArgs e)
        {
            var fileLoc = new OpenFileDialog
            {
                InitialDirectory = OutputDirectoryBox.Text,
                RestoreDirectory = true,
                Filter = "All files|*.*|Spectral library text files|*.sptxt",
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
        private void DataFilesButton_Click(object sender, EventArgs e)
        {
            var fileLoc = new OpenFileDialog
                              {
                                  RestoreDirectory = true,
                                  Filter =
                                      "Bumbershoot compatable files|*.mzML;*.mzXML;*.wiff;*.raw;*.yep;*.mgf|mzML files (.mzML)|*.mzML|mzXML files (.mzXML)|*.mzXML|RAW files (.raw)|*.raw|WIFF files (.wiff)|*.wiff|YEP files (.yep)|*.yep|MGF files (.mgf)|*.mgf",
                                  CheckFileExists = true,
                                  CheckPathExists = true,
                                  SupportMultiDottedExtensions = true,
                                  Multiselect = true,
                                  Title = "Data files"
                              };
            if (fileLoc.ShowDialog() == DialogResult.OK)
            {
                var selectedFiles = fileLoc.FileNames;
                InputFilesBox.Text = string.Empty;
                OutputDirectoryBox.Text = Directory.GetParent(selectedFiles[0]).ToString();
                foreach (var foo in selectedFiles)
                    InputFilesBox.Text += string.Format("\"{0}\"{1}", foo, Environment.NewLine);

                //OverallPBar.Maximum = SelectedFiles.Length;
                InputFilesBox.Text = InputFilesBox.Text.Trim();

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
                                  Filter = "Config Files (.cfg)|*.cfg|pepXML Files (.pepXML)|*.pepXML",
                                  CheckFileExists = true,
                                  CheckPathExists = true,
                                  Multiselect = false,
                                  Title = "MyriMatch config file location"
                              };
            if (fileLoc.ShowDialog() == DialogResult.OK)
            {
                if (Path.GetExtension(fileLoc.FileName) == ".cfg")
                    MyriConfigBox.Tag = null;
                else
                {
                    MyriConfigBox.Tag = "--Custom--";
                    MyriMatchInfoBox.Text = PepXMLtoEntireFileString(fileLoc.FileName);
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
                                  Filter = "Config Files (.cfg)|*.cfg|Tags Files (.tags)|*.tags",
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
                    DTConfigBox.Tag = "--Custom--";
                    DirecTagInfoBox.Text = TagsFileToEntireFileString(fileLoc.FileName);
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
                                  Filter = "Config Files (.cfg)|*.cfg|pepXML Files (.pepXML)|*.pepXML",
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
                    TRConfigBox.Tag = "--Custom--";
                    TagReconInfoBox.Text = PepXMLtoEntireFileString(fileLoc.FileName);
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
                Filter = "Config Files (.cfg)|*.cfg|pepXML Files (.pepXML)|*.pepXML",
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
                    PepConfigBox.Tag = "--Custom--";
                    PepitomeInfoBox.Text = PepXMLtoEntireFileString(fileLoc.FileName);
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


            if (testConfigForm.ShowDialog(this) == DialogResult.OK)
            {
                var filepath = testConfigForm.GetFilePath();
                configBox.Tag = testConfigForm.IsTemporaryConfiguration()
                                      ? "--Custom--"
                                      : null;

                configBox.Text = filepath;
                infoBox.Text = testConfigForm.GetConfigString(!testConfigForm.IsTemporaryConfiguration());
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
                    var fileIn = new StreamReader(configBox.Text);
                    var contents = fileIn.ReadToEnd();
                    if (System.Text.RegularExpressions.Regex.IsMatch(contents.ToLower(), "deisotopingmode *= *[12]")
                        && !System.Text.RegularExpressions.Regex.IsMatch(info.Text.ToLower(), "deisotopingmode *= *[12]"))
                        MessageBox.Show(
                            "Warning- Deisotoping mode is currently unstable. Use of this parameter may cause unexpected behavior.");
                    info.Text = contents;
                    fileIn.Close();
                    fileIn.Dispose();
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
        /// Load info box with selected config item's properties
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MyriConfigBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            MyriConfigBox.Tag = null;
            if (MyriConfigBox.SelectedIndex == 0)
            {
                ConfigBox_TextChanged(MyriConfigBox, null);
                return;
            }
            if (!File.Exists(MyriConfigBox.Text) ||
                Path.GetExtension(MyriConfigBox.Text) != ".cfg")
            {
                MyriMatchInfoBox.Text = string.Empty;
                foreach (var item in MyriDropDownItems[MyriConfigBox.SelectedIndex].PropertyList)
                    MyriMatchInfoBox.Text += String.Format("{0} = {1}{2}", item.Name, item.Value, Environment.NewLine);
                MyriConfigBox.Tag = "--Custom--";
            }
            else if (File.Exists(MyriConfigBox.Text) && Path.GetExtension(MyriConfigBox.Text) == ".pepXML")
                MyriConfigBox.Tag = "--Custom--";
            ConfigBox_TextChanged(MyriConfigBox, null);
        }

        /// <summary>
        /// Load info box with selected config item's properties
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DTConfigBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            DTConfigBox.Tag = null;
            if (DTConfigBox.SelectedIndex == 0)
            {
                ConfigBox_TextChanged(DTConfigBox, null);
                return;
            }
            if (!File.Exists(DTConfigBox.Text) 
                || Path.GetExtension(DTConfigBox.Text) != ".cfg")
            {
                DirecTagInfoBox.Text = string.Empty;
                foreach (var item in DTDropDownItems[DTConfigBox.SelectedIndex].PropertyList)
                    DirecTagInfoBox.Text += String.Format("{0} = {1}{2}", item.Name, item.Value, Environment.NewLine);
                DTConfigBox.Tag = "--Custom--";
            }
            else if (File.Exists(DTConfigBox.Text) && Path.GetExtension(DTConfigBox.Text) == ".tags")
                DTConfigBox.Tag = "--Custom--";
            ConfigBox_TextChanged(DTConfigBox, null);
        }

        /// <summary>
        /// Load info box with selected config item's properties
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TRConfigBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            TRConfigBox.Tag = null;
            if (TRConfigBox.SelectedIndex == 0)
            {
                ConfigBox_TextChanged(TRConfigBox, null);
                return;
            }

            if (!File.Exists(TRConfigBox.Text) 
                || Path.GetExtension(TRConfigBox.Text) != ".cfg")
            {
                TagReconInfoBox.Text = string.Empty;
                foreach (var item in TRDropDownItems[TRConfigBox.SelectedIndex].PropertyList)
                    TagReconInfoBox.Text += String.Format("{0} = {1}{2}", item.Name, item.Value, Environment.NewLine);
                TRConfigBox.Tag = "--Custom--";
            }
            else if (File.Exists(TRConfigBox.Text) && Path.GetExtension(TRConfigBox.Text) == ".pepXML")
                TRConfigBox.Tag = "--Custom--";
            ConfigBox_TextChanged(TRConfigBox, null);
        }

        private void PepConfigBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            PepConfigBox.Tag = null;
            if (PepConfigBox.SelectedIndex == 0)
            {
                ConfigBox_TextChanged(PepConfigBox, null);
                return;
            }

            if (!File.Exists(PepConfigBox.Text)
                || Path.GetExtension(PepConfigBox.Text) != ".cfg")
            {
                PepitomeInfoBox.Text = string.Empty;
                foreach (var item in PepDropDownItems[PepConfigBox.SelectedIndex].PropertyList)
                    PepitomeInfoBox.Text += String.Format("{0} = {1}{2}", item.Name, item.Value, Environment.NewLine);
                PepConfigBox.Tag = "--Custom--";
            }
            else if (File.Exists(PepConfigBox.Text) && Path.GetExtension(PepConfigBox.Text) == ".pepXML")
                PepConfigBox.Tag = "--Custom--";
            ConfigBox_TextChanged(PepConfigBox, null);
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
            var extensionList = new List<string>
                                        {
                                            ".raw", ".wiff", ".yep",
                                            ".mzml", ".mgf", ".mzxml"
                                        };


            // Get all input files and validate that they exist
            var inputFiles = InputFilesBox.Text.Split(Environment.NewLine.ToCharArray(),
                                                      StringSplitOptions.RemoveEmptyEntries);
            if (inputFiles.Select(str => str.Trim("\"".ToCharArray()))
                .Any(fileName => !File.Exists(fileName) || !(extensionList.Contains((Path.GetExtension(fileName) ?? string.Empty).ToLower()))))
            {
                allValid = false;
                InputFilesBox.BackColor = Color.LightPink;
            }

            if (allValid)
                InputFilesBox.BackColor = Color.White;

            // Validate Output Directory
            if (Directory.Exists(OutputDirectoryBox.Text) &&
                !string.IsNullOrEmpty(OutputDirectoryBox.Text))
                OutputDirectoryBox.BackColor = Color.White;
            else
            {
                allValid = false;
                OutputDirectoryBox.BackColor = Color.LightPink;
            }

            // Validate Database Location
            if (File.Exists(DatabaseLocBox.Text) &&
                (Path.GetExtension(DatabaseLocBox.Text) ?? string.Empty).ToLower() == (".fasta"))
                DatabaseLocBox.BackColor = Color.White;
            else
            {
                allValid = false;
                DatabaseLocBox.BackColor = Color.LightPink;
            }

            // Validate Config Files
            //If Database Search
            if (SearchTypeBox.Text == JobType.Database)
            {
                if (MyriConfigBox.Text.Length > 0 && !MyriConfigBox.Text.Contains(" / "))
                    MyriConfigBox.BackColor = Color.White;
                else
                {
                    allValid = false;
                    MyriConfigBox.BackColor = Color.LightPink;
                }
            }
            //If Tag Sequencing
            else if (SearchTypeBox.Text == JobType.Tag)
            {
                if (DTConfigBox.Text.Length > 0 && !DTConfigBox.Text.Contains(" / "))
                    DTConfigBox.BackColor = Color.White;
                else
                {
                    allValid = false;
                    DTConfigBox.BackColor = Color.LightPink;
                }

                if (TRConfigBox.Text.Length > 0 && !TRConfigBox.Text.Contains(" / "))
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

                if (PepConfigBox.Text.Length > 0 && !PepConfigBox.Text.Contains(" / "))
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
        /// Populates drop down boxeas with related items from old HistoryItems
        /// </summary>
        /// <param name="oldFiles"></param>
        private void SetDropDownItems(IEnumerable<HistoryItem> oldFiles)
        {
            var usedInputList = new List<string>();
            var usedOutputList = new List<string>();
            var usedDatabaseList = new List<string>();
            var usedLibList = new List<string>();
            var usedMyriList = new List<string>();
            var usedDTList = new List<string>();
            var usedTRList = new List<string>();
            var usedPepList = new List<string>();

            foreach (var hi in oldFiles)
            {
                foreach (var i in hi.FileList)
                {
                    if (!usedInputList.Contains(i.FilePath))
                    {
                        usedInputList.Add(i.FilePath);
                        InputFilesBox.Items.Add(i.FilePath);
                    }
                }

                if (!usedOutputList.Contains(hi.OutputDirectory))
                {
                    usedOutputList.Add(hi.OutputDirectory);
                    OutputDirectoryBox.Items.Add(hi.OutputDirectory);
                }

                if (!usedDatabaseList.Contains(hi.ProteinDatabase))
                {
                    usedDatabaseList.Add(hi.ProteinDatabase);
                    DatabaseLocBox.Items.Add(hi.ProteinDatabase);
                }

                if (hi.JobType == JobType.Tag || (hi.JobType == null && hi.TagConfigFile != null))
                {
                    if (!usedDTList.Contains(hi.InitialConfigFile.FilePath))
                    {
                        usedDTList.Add(hi.InitialConfigFile.FilePath);
                        DTDropDownItems.Add(DTConfigBox.Items.Count, hi.InitialConfigFile);
                        DTConfigBox.Items.Add(hi.InitialConfigFile.FilePath == "--Custom--"
                                                  ? hi.InitialConfigFile.Name ?? "--Custom--"
                                                  : hi.InitialConfigFile.FilePath);
                    }

                    if (!usedTRList.Contains(hi.TagConfigFile.FilePath))
                    {
                        usedTRList.Add(hi.TagConfigFile.FilePath);
                        TRDropDownItems.Add(TRConfigBox.Items.Count, hi.TagConfigFile);
                        TRConfigBox.Items.Add(hi.TagConfigFile.FilePath == "--Custom--"
                                                  ? hi.TagConfigFile.Name ?? "--Custom--"
                                                  : hi.TagConfigFile.FilePath);
                    }
                }
                else if (hi.SpectralLibrary != null)
                {
                    if (!usedLibList.Contains(hi.SpectralLibrary))
                    {
                        usedLibList.Add(hi.SpectralLibrary);
                        SpecLibBox.Items.Add(hi.SpectralLibrary);
                    }
                    if (!usedPepList.Contains(hi.InitialConfigFile.FilePath))
                    {
                        PepDropDownItems.Add(PepConfigBox.Items.Count, hi.InitialConfigFile);
                        PepConfigBox.Items.Add(hi.InitialConfigFile.FilePath == "--Custom--"
                                                  ? hi.InitialConfigFile.Name ?? "--Custom--"
                                                  : hi.InitialConfigFile.FilePath);
                    }
                }
                else
                {
                    if (!usedMyriList.Contains(hi.InitialConfigFile.FilePath))
                    {
                        usedMyriList.Add(hi.InitialConfigFile.FilePath);
                        MyriDropDownItems.Add(MyriConfigBox.Items.Count, hi.InitialConfigFile);
                        MyriConfigBox.Items.Add(hi.InitialConfigFile.FilePath == "--Custom--"
                                                  ? hi.InitialConfigFile.Name ?? "--Custom--"
                                                  : hi.InitialConfigFile.FilePath);
                    }
                }
            }
        }

        /// <summary>
        /// Populates fields with recieved values
        /// </summary>
        /// <param name="hi"></param>
        private void SetHistoryItem(HistoryItem hi)
        {
            var fileList = string.Empty;
            foreach (var file in hi.FileList)
                fileList += file.FilePath + Environment.NewLine;
            fileList = fileList.TrimEnd();

            NameBox.Text = hi.JobName;
            CPUsBox.Value = hi.Cpus;
            InputFilesBox.Text = fileList;
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
                SearchTypeBox.Text = JobType.Database;
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
                                 SearchTypeBox.Text == JobType.Database
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

            var files = InputFilesBox.Text.Split(Environment.NewLine.ToCharArray(),
                                                  StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in files)
                hi.FileList.Add(new InputFile { FilePath = item, HistoryItem = hi });

            return hi;
        }

        private ConfigFile GetConfigFile(string destinationProgram)
        {
            var parameterType = lib.Util.parameterTypes;

            var configBox = destinationProgram == "MyriMatch"
                                ? MyriConfigBox
                                : (destinationProgram == "DirecTag"
                                       ? DTConfigBox
                                       : (destinationProgram == "Pepitome"
                                              ? PepConfigBox
                                              : TRConfigBox));

            var config = new ConfigFile
            {
                DestinationProgram = destinationProgram,
                PropertyList = new List<ConfigProperty>()                
            };

            if (configBox.Tag == null)
                config.FilePath = configBox.Text;
            else
            {
                config.FilePath = "--Custom--";
                if ((string.IsNullOrEmpty(configBox.Text)
                || configBox.Text == "(Default)")
                && !string.IsNullOrEmpty(NameBox.Text))
                    config.Name = NameBox.Text;
                else
                    config.Name = configBox.Text;
            }

            var properties = destinationProgram == "MyriMatch"
                                 ? MyriMatchInfoBox.Text.Split(Environment.NewLine.ToCharArray(),
                                                               StringSplitOptions.RemoveEmptyEntries)
                                 : (destinationProgram == "DirecTag"
                                        ? DirecTagInfoBox.Text.Split(Environment.NewLine.ToCharArray(),
                                                                     StringSplitOptions.RemoveEmptyEntries)
                                        : (destinationProgram == "Pepitome"
                                               ? PepitomeInfoBox.Text.Split(Environment.NewLine.ToCharArray(),
                                                                            StringSplitOptions.RemoveEmptyEntries)
                                               : TagReconInfoBox.Text.Split(Environment.NewLine.ToCharArray(),
                                                                            StringSplitOptions.RemoveEmptyEntries)));
            foreach (var item in properties)
            {
                var splitProperty = item.Split("=".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                if (splitProperty.Length == 2)
                    config.PropertyList.Add(new ConfigProperty
                                                {
                                                    Name = splitProperty[0].Trim(),
                                                    Value = splitProperty[1].Trim(),
                                                    Type = parameterType.ContainsKey(splitProperty[0].Trim())
                                                               ? parameterType[splitProperty[0].Trim()]
                                                               : "unknown",
                                                    ConfigAssociation = config
                                                });
            }
            return config;
        }

        private string PepXMLtoEntireFileString(string file)
        {
            var parameterTypes = lib.Util.parameterTypes;
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

            var formattedFile = string.Empty;
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
                        formattedFile += propertySplit[0] + " = " + propertySplit[1] + Environment.NewLine;
                    }
                    else if (parameterTypes[propertySplit[0]] == "int"
                             || parameterTypes[propertySplit[0]] == "double")
                    {
                        propertySplit[1] = propertySplit[1].Trim('\"');
                        formattedFile += propertySplit[0] + " = " + propertySplit[1] + Environment.NewLine;
                    }
                    else if (parameterTypes[propertySplit[0]] == "string")
                    {
                        if (propertySplit.Length > 2)
                        {
                            for (int i = 2; i < propertySplit.Length; i++)
                                propertySplit[1] += " " + propertySplit[i];
                        }
                    }
                }
            }


            return formattedFile;
        }

        private string TagsFileToEntireFileString(string file)
        {
            var parameterTypes = lib.Util.parameterTypes;

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

            var formattedFile = string.Empty;
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
                        formattedFile += propertySplit[0] + " = " + bool.Parse(propertySplit[1]).ToString().ToLower() + Environment.NewLine;
                    }
                    else if (parameterTypes[propertySplit[0]] == "int"
                             || parameterTypes[propertySplit[0]] == "double")
                    {
                        propertySplit[1] = propertySplit[1].Trim('\"');
                        formattedFile += propertySplit[0] + " = " + propertySplit[1] + Environment.NewLine;
                    }
                    else if (parameterTypes[propertySplit[0]] == "string")
                    {
                        if (propertySplit.Length > 2)
                        {
                            for (int foo = 2; foo < propertySplit.Length; foo++)
                                propertySplit[1] += " " + propertySplit[foo];
                        }
                        formattedFile += propertySplit[0] + " = " + propertySplit[1] + Environment.NewLine;
                    }
                }
            }

            formattedFile = formattedFile.Trim();

            return formattedFile;
        }

        private void SearchTypeBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (SearchTypeBox.Text)
            {
                case JobType.Database:
                    ConfigGB.Visible = true;
                    PepPanel.Visible = false;
                    ConfigDatabasePanel.Visible = true;
                    DatabaseConfigInfoPanel.Visible = true;
                    ConfigTagPanel.Visible = false;
                    PepConfigInfoPanel.Visible = false;
                    TagConfigInfoPanel.Visible = false;
                    break;
                case JobType.Tag:
                    ConfigGB.Visible = true;
                    PepPanel.Visible = false;
                    ConfigDatabasePanel.Visible = false;
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
    }
}
