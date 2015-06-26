using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using BumberDash.Model;
using CustomDataSourceDialog;
using pwiz.CLI.msdata;

namespace BumberDash.Forms
{
    public partial class ConfigWizard : Form
    {
        private Dictionary<int, Panel> _panelReference;
        private int _currentPanel = 1;
        private int _finalPanel = 0;
        private HistoryItem _currentJob;
        private IList<ConfigFile> _templateList;

        public ConfigWizard(IList<ConfigFile> templateList)
        {
            InitializeComponent();
            _currentJob = null;
            _templateList = templateList;
        }

        private bool _editMode = false;
        public ConfigWizard(HistoryItem hi, IList<ConfigFile> templateList, bool editMode)
        {
            InitializeComponent();
            _currentJob = hi;
            _templateList = templateList;
            _editMode = editMode;
            if (editMode)
                Text = "Edit Job";
        }

        private void ConfigWizard_Load(object sender, EventArgs e)
        {
            //set up panel reference
            _panelReference = new Dictionary<int, Panel>();
            var index = 1;
            var indexSet = new HashSet<int>();
            foreach (Panel panel in splitContainer1.Panel1.Controls.Cast<Panel>().ToList().OrderBy(x=>x.TabIndex))
            {
                //Note, panel order is determined by TabIndex
                if (!indexSet.Add(panel.TabIndex))
                    throw new Exception("Duplicate panel index found. "+
                        "If this error is encountered outside development please contact "+
                        "the person in charge of BumberDash upkeep.");
                _panelReference.Add(index, panel);
                index++;
            }
            _finalPanel = _panelReference.Count;

            //set default values
            ModTypeBox.Text = "Dynamic";
            PrecursorToleranceUnitsBox.Text = "ppm";
            FragmentToleranceUnitsBox.Text = "ppm";
            SpecificityBox.Text = "Fully-Specific";
            ModBox.Rows.Add(new object[] {"C", 57.021464.ToString(), "Static"});
            ModBox.Rows.Add(new object[] { "M", 15.994915.ToString(), "Dynamic" });
            DBSavedBrowse.Enabled = (!string.IsNullOrEmpty(Properties.Settings.Default.DatabaseFolder)
                                     && Directory.Exists(Properties.Settings.Default.DatabaseFolder));
            LibSavedBrowse.Enabled = (!string.IsNullOrEmpty(Properties.Settings.Default.LibraryFolder)
                                      && Directory.Exists(Properties.Settings.Default.LibraryFolder));

            //set up unimod suggestions
            var uniModSuggestions = new List<string>();
            foreach (var item in Util.UnimodLookup.FullUnimodList)
                uniModSuggestions.Add(item.MonoMass + "     " + item.Name);
            var source = new AutoCompleteStringCollection();
            source.AddRange(uniModSuggestions.ToArray());
            ModMassBox.AutoCompleteMode = AutoCompleteMode.Suggest;
            ModMassBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            ModMassBox.AutoCompleteCustomSource = source;
            ModMassBox.TextChanged += (x, y) =>
            {
                if (ModMassBox.Text.Contains("     "))
                    ModMassBox.Text = ModMassBox.Text.Remove(ModMassBox.Text.IndexOf(' '));
            };



            foreach (var kvp in _panelReference)
            {
                if (kvp.Key == _currentPanel)
                {
                    kvp.Value.Dock = DockStyle.Fill;
                    kvp.Value.Visible = true;
                    continue;
                }
                kvp.Value.Visible = false;
                kvp.Value.Dock = DockStyle.None;
            }

            if (_currentJob == null)
                _currentJob = new HistoryItem();
            else
                SetHistoryItem(_currentJob);
        }

        private void NavigateButton_Click(object sender, EventArgs e)
        {
            if (sender == NextButton)
            {
                if (!ValidateCurrentPanel())
                    return;
                if (_currentPanel == _finalPanel - 1)
                    NextButton.Text = "Finish";
                else if (_currentPanel == _finalPanel)
                {
                    if (SaveDBLocationBox.Checked)
                    {
                        Properties.Settings.Default.DatabaseFolder = Path.GetDirectoryName(DatabaseBox.Text);
                        Properties.Settings.Default.Save();
                    }

                    if (PepRadio.Checked && SaveLibLocationBox.Checked)
                    {
                        Properties.Settings.Default.LibraryFolder = Path.GetDirectoryName(LibraryBox.Text);
                        Properties.Settings.Default.Save();
                    }

                    this.DialogResult = DialogResult.OK;
                    return;
                }
                _currentPanel++;
            }
            else
            {
                NextButton.Text = "Next";
                _currentPanel--;
            }
            PreviousButton.Visible = _currentPanel > 1;

            foreach (var kvp in _panelReference)
            {
                if (kvp.Key == _currentPanel)
                {
                    kvp.Value.Dock = DockStyle.Fill;
                    kvp.Value.Visible = true;
                    continue;
                }
                kvp.Value.Visible = false;
                kvp.Value.Dock = DockStyle.None;
            }
        }

        private bool ValidateCurrentPanel()
        {
            var currentPanel = _panelReference[_currentPanel];
            if (currentPanel == SearchTypePanel)
            {
                if (!DBRadio.Checked && !TagRadio.Checked && !PepRadio.Checked)
                    return false;
                if (DBRadio.Checked && ((MyriBox.Checked ? 1 : 0) + (CometBox.Checked ? 1 : 0) + (MSGFBox.Checked ? 1 : 0)) ==0)
                    return false;
                if (_editMode && DBRadio.Checked &&
                    ((MyriBox.Checked ? 1 : 0) + (CometBox.Checked ? 1 : 0) + (MSGFBox.Checked ? 1 : 0)) > 1)
                {
                    MessageBox.Show("When editing a job please select only one search engine.");
                    return false;
                }
            }
            else if (currentPanel == FilesPanel)
            {
                if (FileSelectModeBox.Checked)
                {
                    if (InputFilesList.Rows.Count == 0)
                        return false;
                    var files = GetInputFileNames();
                    return files.All(file => File.Exists(file) || Directory.Exists(file));
                }
                if (string.IsNullOrEmpty(InputDirectoryBox.Text) || !Directory.Exists(InputDirectoryBox.Text))
                {
                    MessageBox.Show("Input directory not found.");
                    return false;
                }
                if (GetFileMask() == string.Empty)
                    return false;
                if (!mz5Radio.Checked && !mzMLRadio.Checked && !mzXMLRadio.Checked && !mgfRadio.Checked &&
                    !rawRadio.Checked && !dRadio.Checked)
                    return false;
                if ((mz5Radio.Checked && Directory.GetFiles(InputDirectoryBox.Text, "*.mz5").Length == 0)
                    || (mzMLRadio.Checked && Directory.GetFiles(InputDirectoryBox.Text, "*.mzML").Length == 0)
                    || (mzXMLRadio.Checked && Directory.GetFiles(InputDirectoryBox.Text, "*.mzXML").Length == 0)
                    || (mgfRadio.Checked && Directory.GetFiles(InputDirectoryBox.Text, "*.mgf").Length == 0)
                    || (rawRadio.Checked && Directory.GetFiles(InputDirectoryBox.Text, "*.raw").Length == 0)
                    || (dRadio.Checked && Directory.GetDirectories(InputDirectoryBox.Text, "*.d").Length == 0))
                {
                    MessageBox.Show("Found no files with specified mask.");
                    return false;
                }
            }
            else if (currentPanel == ResourcesPanel)
            {
                if (string.IsNullOrEmpty(DatabaseBox.Text) || !File.Exists(DatabaseBox.Text))
                {
                    MessageBox.Show("Protein database directory not found.");
                    return false;
                }
                if (PepRadio.Checked && (string.IsNullOrEmpty(LibraryBox.Text) || !File.Exists(LibraryBox.Text)))
                {
                    MessageBox.Show("Spectral library directory not found.");
                    return false;
                }
            }
            else if (currentPanel == OutputPanel)
            {
                if (!Directory.Exists(OutputFolderBox.Text))
                {
                    OutputFolderBox.BackColor = Color.Salmon;
                    return false;
                }
                OutputFolderBox.BackColor = SystemColors.Window;
                if (OutputNewCheckBox.Checked && string.IsNullOrEmpty(OutputNewFolderBox.Text))
                    return false;
                if (SuffixBox.Checked)
                {
                    if (MyriBox.Checked &&
                        (string.IsNullOrEmpty(CometSuffixBox.Text) || string.IsNullOrEmpty(MSGFSuffixBox.Text)))
                        return false;
                    if (string.IsNullOrEmpty(PrimarySuffixBox.Text))
                        return false;
                }
            }
            else if (currentPanel == InstrumentPanel)
            {
                if (!PrecursorLowRadio.Checked && !PrecursorMidRadio.Checked && !PrecursorHighRadio.Checked)
                    return false;
                if (!FragmentLowRadio.Checked && !FragmentMidRadio.Checked && !FragmentHighRadio.Checked)
                    return false;
            }
            return true;
        }

        private void NumericTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {

            if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                if (!(e.KeyChar.Equals('-') && ((TextBox)sender).SelectionStart == 0 && !((TextBox)sender).Text.Contains('-')))
                {
                    if (!(e.KeyChar.Equals('.') && !((TextBox)sender).Text.Contains('.')) &&
                        !(e.KeyChar.Equals(',') && !((TextBox)sender).Text.Contains(',')))
                    {
                        e.Handled = true;
                    }
                }
            }

            if (((TextBox)sender).SelectionStart == 0 && ((TextBox)sender).SelectionLength == 0 && ((TextBox)sender).Text.Contains('-'))
                e.Handled = true;
        }

        private void AddModButton_Click(object sender, EventArgs e)
        {
            double modMass;
            if (string.IsNullOrEmpty(ResidueBox.Text))
            {
                ResidueBox.BackColor = Color.Salmon;
                return;
            }
            if (!double.TryParse(ModMassBox.Text, out modMass))
            {
                ModMassBox.BackColor = Color.Salmon;
                return;
            }
            ResidueBox.BackColor = SystemColors.Window;
            ModMassBox.BackColor = SystemColors.Window;
            ModBox.Rows.Add(new object[] { ResidueBox.Text , modMass, ModTypeBox.Text});
            ResidueBox.Text = string.Empty;
            ModMassBox.Text = string.Empty;
        }

        private void RemoveModButton_Click(object sender, EventArgs e)
        {
            if (ModBox.SelectedRows.Count > 0)
            {
                var selection = ModBox.SelectedRows[0].Index;

                ResidueBox.Text = ModBox.Rows[selection].Cells[0].Value.ToString();
                ModMassBox.Text = ModBox.Rows[selection].Cells[1].Value.ToString();
                ModTypeBox.Text = ModBox.Rows[selection].Cells[2].Value.ToString();

                ModBox.Rows.RemoveAt(selection);

                ModBox.ClearSelection();
            }
        }

        private void SuffixBox_CheckedChanged(object sender, EventArgs e)
        {
            SuffixPanel.Visible = SuffixBox.Checked;
        }

        private void DBBrowse_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog
                {
                    RestoreDirectory = true,
                    Filter = "FASTA Myrimatch|*.fasta"
                };
            var inputFile = GetInputFileNames()[0];
            if (!FileSelectModeBox.Checked)
                inputFile = inputFile.Substring(1);
            if (Directory.Exists(Path.GetDirectoryName(inputFile) ?? "<Invalid Result>"))
                ofd.InitialDirectory = Path.GetDirectoryName(inputFile);
            if (sender == DBSavedBrowse
                && !string.IsNullOrEmpty(Properties.Settings.Default.DatabaseFolder)
                && Directory.Exists(Properties.Settings.Default.DatabaseFolder))
                ofd.InitialDirectory = Properties.Settings.Default.DatabaseFolder;
            if (ofd.ShowDialog() == DialogResult.OK)
                DatabaseBox.Text = ofd.FileName;
        }

        private void LibBrowse_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Spectral Library|*.sptxt;*.sptxt.index"
            };
            var inputFile = GetInputFileNames()[0];
            if (!FileSelectModeBox.Checked)
                inputFile = inputFile.Substring(1);
            if (Directory.Exists(Path.GetDirectoryName(inputFile) ?? "<Invalid Result>"))
                ofd.InitialDirectory = Path.GetDirectoryName(inputFile);
            if (sender == LibSavedBrowse
                && !string.IsNullOrEmpty(Properties.Settings.Default.LibraryFolder)
                && Directory.Exists(Properties.Settings.Default.LibraryFolder))
                ofd.InitialDirectory = Properties.Settings.Default.LibraryFolder;
            if (ofd.ShowDialog() == DialogResult.OK)
                LibraryBox.Text = ofd.FileName;
        }

        private void FileSelectModeBox_CheckedChanged(object sender, EventArgs e)
        {
            FileMaskPanel.Visible = !FileSelectModeBox.Checked;
            FileSelectPanel.Visible = FileSelectModeBox.Checked;
        }

        private void FolderBrowseButton_Click(object sender, EventArgs e)
        {
            var dirBox = sender == InputBrowseButton ? InputDirectoryBox : OutputFolderBox;
            var browserDialog = new FolderBrowserDialog();
            if (Directory.Exists(InputDirectoryBox.Text))
                browserDialog.SelectedPath = dirBox.Text;
            if (browserDialog.ShowDialog() == DialogResult.OK)
                dirBox.Text = browserDialog.SelectedPath;
            if (sender == InputBrowseButton && string.IsNullOrEmpty(OutputFolderBox.Text))
                OutputFolderBox.Text = browserDialog.SelectedPath;
        }

        private void AddInputButton_Click(object sender, EventArgs e)
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
                var usedFiles = GetInputFileNames();
                foreach (var file in selectedFiles)
                {
                    if (usedFiles.Contains(file) || !File.Exists(file) || !Directory.Exists(file))
                        continue;
                    var currentRow = InputFilesList.Rows.Count;
                    var newItem = new[] { Path.GetFileName(file) };
                    InputFilesList.Rows.Insert(currentRow, newItem);
                    InputFilesList.Rows[currentRow].Cells[0].ToolTipText = "\"" + file + "\"";
                }

                if (string.IsNullOrEmpty(OutputNewFolderBox.Text))
                    OutputNewFolderBox.Text = (new DirectoryInfo(Directory.GetParent(selectedFiles[0]).ToString())).Name;
            }

        }

        private List<String> GetInputFileNames()
        {
            if (FileSelectModeBox.Checked)
            {
                var fileList = new List<string>();
                foreach (DataGridViewRow row in InputFilesList.Rows)
                    fileList.Add(row.Cells[0].ToolTipText);
                return fileList;
            }
            else
            {
                return new List<string>() { "!" + Path.Combine(InputDirectoryBox.Text, GetFileMask()) };
            }
        }

        private string GetFileMask()
        {
            if (mz5Radio.Checked)
                return "*.mz5";
            if (mzMLRadio.Checked)
                return "*.mzML";
            if (mzXMLRadio.Checked)
                return "*.mzXML";
            if (mgfRadio.Checked)
                return "*.mgf";
            if (rawRadio.Checked)
                return "*.raw";
            if (dRadio.Checked)
                return "*.d";
            return string.Empty;
        }

        private void RemoveInputButton_Click(object sender, EventArgs e)
        {
            var selectedItems = InputFilesList.SelectedRows;
            foreach (DataGridViewRow item in selectedItems)
                InputFilesList.Rows.Remove(item);
        }

        private void ProgramRadio_CheckedChanged(object sender, EventArgs e)
        {
            LibraryPanel.Visible = PepRadio.Checked;
            BlindModBox.Visible = TagRadio.Checked;
            MyriBox.Enabled = DBRadio.Checked;
            CometBox.Enabled = DBRadio.Checked;
            MSGFBox.Enabled = DBRadio.Checked;
            ExtraDBSearchBox_CheckedChanged(sender, e);
            BlindModBox_CheckedChanged(sender, e);
            SuffixBox.Enabled = true;

            if (DBRadio.Checked)
            {
                PrimarySuffixLabel.Text = "MyriMatch suffix";
                if (DBRadio.Checked && ((MyriBox.Checked ? 1 : 0) + (CometBox.Checked ? 1 : 0) + (MSGFBox.Checked ? 1 : 0)) > 1)
                {
                    SuffixBox.Checked = true;
                    SuffixBox.Enabled = false;
                }
                PrimarySuffixBox.Text = "_MM";
            }
            else if (TagRadio.Checked)
            {
                PrimarySuffixLabel.Text = "TagRecon suffix";
                PrimarySuffixBox.Text = "_TR";
            }
            else if (PepRadio.Checked)
            {
                PrimarySuffixLabel.Text = "Pepitome suffix";
                PrimarySuffixBox.Text = "_PP";
            }
        }

        private void ExtraDBSearchBox_CheckedChanged(object sender, EventArgs e)
        {
            var extraMode = DBRadio.Checked && ((MyriBox.Checked ? 1 : 0) + (CometBox.Checked ? 1 : 0) + (MSGFBox.Checked ? 1 : 0)) > 1;

            PrimarySuffixBox.Visible = MyriBox.Checked || !DBRadio.Checked;
            PrimarySuffixLabel.Visible = MyriBox.Checked || !DBRadio.Checked;
            CometSuffixLabel.Visible = CometBox.Checked && DBRadio.Checked;
            CometSuffixBox.Visible = CometBox.Checked && DBRadio.Checked;
            MSGFSuffixLabel.Visible = MSGFBox.Checked && DBRadio.Checked;
            MSGFSuffixBox.Visible = MSGFBox.Checked && DBRadio.Checked;
            SuffixBox.Checked = extraMode;
            SuffixBox.Enabled = !extraMode;
        }

        private void OutputNewCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            OutputNewFolderLabel.Visible = OutputNewCheckBox.Checked;
            OutputNewFolderBox.Visible = OutputNewCheckBox.Checked;
        }

        private void FineTuneBox_CheckedChanged(object sender, EventArgs e)
        {
            FineTunePanel.Visible = FineTuneBox.Checked;
        }

        private void ModMassBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == 13)
            {
                e.Handled = true;
                AddModButton_Click(sender, e);
            }
        }

        private void Tolerance_CheckChanged(object sender, EventArgs e)
        {
            FineTuneBox.Visible = ((PrecursorLowRadio.Checked || PrecursorMidRadio.Checked || PrecursorHighRadio.Checked)
                                   && (FragmentLowRadio.Checked || FragmentMidRadio.Checked || FragmentHighRadio.Checked));
            if (PrecursorLowRadio.Checked)
            {
                PrecursorToleranceBox.Text = 1.5.ToString();
                PrecursorToleranceUnitsBox.Text = "mz";
            }
            else if (PrecursorMidRadio.Checked)
            {
                PrecursorToleranceBox.Text = "25";
                PrecursorToleranceUnitsBox.Text = "ppm";
            }
            else if (PrecursorHighRadio.Checked)
            {
                PrecursorToleranceBox.Text = "10";
                PrecursorToleranceUnitsBox.Text = "ppm";
            }

            if (FragmentLowRadio.Checked)
            {
                FragmentToleranceBox.Text = 0.5.ToString();
                FragmentToleranceUnitsBox.Text = "mz";
            }
            else if (FragmentMidRadio.Checked)
            {
                FragmentToleranceBox.Text = "50";
                FragmentToleranceUnitsBox.Text = "ppm";
            }
            else if (FragmentHighRadio.Checked)
            {
                FragmentToleranceBox.Text = "20";
                FragmentToleranceUnitsBox.Text = "ppm";
            }
        }

        private void BlindModBox_CheckedChanged(object sender, EventArgs e)
        {
            ResidueBox.Enabled = ModMassBox.Enabled =
                ModTypeBox.Enabled = AddModButton.Enabled =
                RemoveInputButton.Enabled = ModBox.Enabled = !TagRadio.Checked || !BlindModBox.Checked;
        }

        private void InputDirectoryBox_TextChanged(object sender, EventArgs e)
        {
            if (Directory.Exists(InputDirectoryBox.Text))
            {
                InputDirectoryBox.BackColor = SystemColors.Window;
                mz5Radio.Enabled = Directory.GetFiles(InputDirectoryBox.Text,"*.mz5").Length >0;
                mzMLRadio.Enabled = Directory.GetFiles(InputDirectoryBox.Text, "*.mzML").Length > 0;
                mzXMLRadio.Enabled = Directory.GetFiles(InputDirectoryBox.Text, "*.mzXML").Length > 0;
                mgfRadio.Enabled = Directory.GetFiles(InputDirectoryBox.Text, "*.mgf").Length > 0;
                rawRadio.Enabled = Directory.GetFiles(InputDirectoryBox.Text, "*.raw").Length > 0;
                dRadio.Enabled = Directory.GetDirectories(InputDirectoryBox.Text, "*.d").Length > 0;
            }
            else
            {
                InputDirectoryBox.BackColor = Color.Salmon;
                mz5Radio.Enabled = false;
                mzMLRadio.Enabled = false;
                mzXMLRadio.Enabled = false;
                mgfRadio.Enabled = false;
                rawRadio.Enabled = false;
                dRadio.Enabled = false;
            }
        }

        public void SetHistoryItem(HistoryItem recievedHi)
        {
            ModBox.Rows.Clear();

            //set generic values first
            if (!string.IsNullOrEmpty(recievedHi.JobName))
                OutputNewFolderBox.Text = recievedHi.JobName;
            if (!string.IsNullOrEmpty(recievedHi.ProteinDatabase))
                DatabaseBox.Text = recievedHi.ProteinDatabase;
            if (!string.IsNullOrEmpty(recievedHi.SpectralLibrary))
                LibraryBox.Text = recievedHi.SpectralLibrary;
            if (!string.IsNullOrEmpty(recievedHi.OutputDirectory))
            {
                if (recievedHi.OutputDirectory.EndsWith("+") || recievedHi.OutputDirectory.EndsWith("*"))
                    OutputNewCheckBox.Checked = true;
                OutputFolderBox.Text = recievedHi.OutputDirectory.EndsWith("*")
                                           ? Path.GetDirectoryName(recievedHi.OutputDirectory.TrimEnd('*'))
                                           : recievedHi.OutputDirectory.TrimEnd('+');
            }

            if (recievedHi.FileList != null)
                foreach (var file in recievedHi.FileList)
                {
                    if (file.FilePath.StartsWith("!"))
                    {
                        InputDirectoryBox.Text = Path.GetDirectoryName(file.FilePath.TrimStart('!'));
                        try
                        {
                            var mask = Path.GetFileName(file.FilePath.TrimStart('!'));
                            if (mask.ToLower().Contains(".mz5"))
                                mz5Radio.Checked = true;
                            else if (mask.ToLower().Contains(".mzml"))
                                mzMLRadio.Checked = true;
                            else if (mask.ToLower().Contains(".mzxml"))
                                mzXMLRadio.Checked = true;
                            else if (mask.ToLower().Contains(".mgf"))
                                mgfRadio.Checked = true;
                            else if (mask.ToLower().Contains(".raw"))
                                rawRadio.Checked = true;
                            else if (mask.ToLower().Contains(".d"))
                                dRadio.Checked = true;
                        }
                        catch
                        {
                            //ignore, let the user figure out why folder doesn't contain the right files
                        }
                        continue;
                    }
                    InputFilesList.Rows.Add(new object[]{file.FilePath});
                }

            if (recievedHi.JobType == JobType.Myrimatch)
            {
                DBRadio.Checked = true;
                MyriBox.Checked = true;
            }
            else if (recievedHi.JobType == JobType.Comet)
            {
                DBRadio.Checked = true;
                MyriBox.Checked = false;
                CometBox.Checked = true;
            }
            else if (recievedHi.JobType == JobType.MSGF)
            {
                DBRadio.Checked = true;
                MyriBox.Checked = false;
                MSGFBox.Checked = true;
            }
            else if (recievedHi.JobType == JobType.Tag)
                TagRadio.Checked = true;
            else if (recievedHi.JobType == JobType.Library)
                PepRadio.Checked = true;

            //get program-specific parameters
            var configFile = recievedHi.InitialConfigFile.PropertyList;
            if (recievedHi.JobType == JobType.Tag)
                configFile = recievedHi.TagConfigFile.PropertyList;
            if (recievedHi.JobType == JobType.Myrimatch ||
                recievedHi.JobType == JobType.Tag ||
                recievedHi.JobType == JobType.Library)
            {
                var currentParam = configFile.FirstOrDefault(x => x.Name == "OutputSuffix");
                if (currentParam != null)
                {
                    SuffixBox.Checked = true;
                    PrimarySuffixBox.Text = currentParam.Value.Trim('"');
                }
                currentParam = configFile.FirstOrDefault(x => x.Name == "CleavageRules");
                if (currentParam != null)
                    CleavageAgentBox.Text = currentParam.Value.Trim('"');
                currentParam = configFile.FirstOrDefault(x => x.Name == "MinTerminiCleavages");
                if (currentParam != null)
                {
                    int value;
                    if (int.TryParse(currentParam.Value, out value))
                        SpecificityBox.SelectedIndex = value;
                }
                currentParam = configFile.FirstOrDefault(x => x.Name == "ExplainUnknownMassShiftsAs");
                if (recievedHi.JobType == JobType.Tag && currentParam != null)
                    BlindModBox.Checked = currentParam.Value.Trim('"') == "blindptms";

                //tolerances
                currentParam = recievedHi.JobType == JobType.Tag ? configFile.FirstOrDefault(x => x.Name == "PrecursorMzTolerance") : configFile.FirstOrDefault(x => x.Name == "MonoPrecursorMzTolerance");
                var precursor = string.Empty;
                var precursorUnit = string.Empty;
                if (currentParam != null)
                {
                    double value;
                    if (currentParam.Value.Trim('"').EndsWith("mz")
                        && double.TryParse(currentParam.Value.Trim('"').Replace("mz", string.Empty),NumberStyles.Number,CultureInfo.InvariantCulture, out value))
                    {
                        PrecursorLowRadio.Checked = true;
                        precursor = value.ToString();
                        precursorUnit = "mz";
                    }
                    else if (currentParam.Value.Trim('"').EndsWith("ppm")
                        && double.TryParse(currentParam.Value.Trim('"').Replace("ppm", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                    {
                        if (value > 15)
                            PrecursorMidRadio.Checked = true;
                        else
                            PrecursorHighRadio.Checked = true;
                        precursor = value.ToString();
                        precursorUnit = "ppm";
                    }
                }
                currentParam = configFile.FirstOrDefault(x => x.Name == "FragmentMzTolerance");
                if (currentParam != null)
                {
                    double value;
                    if (currentParam.Value.Trim('"').EndsWith("mz")
                        && double.TryParse(currentParam.Value.Trim('"').Replace("mz", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                    {
                        FragmentLowRadio.Checked = true;
                        FragmentToleranceBox.Text = value.ToString();
                        FragmentToleranceUnitsBox.Text = "mz";
                    }
                    else if (currentParam.Value.Trim('"').EndsWith("ppm")
                        && double.TryParse(currentParam.Value.Trim('"').Replace("ppm", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                    {
                        if (value > 30)
                            FragmentMidRadio.Checked = true;
                        else
                            FragmentHighRadio.Checked = true;
                        FragmentToleranceBox.Text = value.ToString();
                        FragmentToleranceUnitsBox.Text = "ppm";
                    }
                }
                if (!string.IsNullOrEmpty(precursor))
                    PrecursorToleranceBox.Text = precursor;
                if (!string.IsNullOrEmpty(precursorUnit))
                    PrecursorToleranceUnitsBox.Text = precursorUnit;

                //mods
                currentParam = configFile.FirstOrDefault(x => x.Name == "StaticMods");
                if (currentParam != null)
                {
                    var splitString = currentParam.Value.Trim('"').Split();
                    for (var x = 0; x + 1 < splitString.Length; x += 2)
                    {
                        double mass;
                        if (!double.TryParse(splitString[x + 1], NumberStyles.Number, CultureInfo.InvariantCulture, out mass))
                            continue;
                        ModBox.Rows.Add(new object[] { splitString[x], mass, "Static" });
                    }
                }
                currentParam = configFile.FirstOrDefault(x => x.Name == "DynamicMods");
                if (currentParam != null)
                {
                    var splitString = currentParam.Value.Trim('"').Split();
                    for (var x = 0; x + 2 < splitString.Length; x += 3)
                    {
                        double mass;
                        if (!double.TryParse(splitString[x + 2], NumberStyles.Number, CultureInfo.InvariantCulture, out mass))
                            continue;
                        ModBox.Rows.Add(new object[] { splitString[x], mass, "Dynamic" });
                    }
                }
            }
            else if (recievedHi.JobType == JobType.Comet)
            {
                var currentParam = configFile.FirstOrDefault(x => x.Name.ToLower() == "config");
                if (currentParam == null)
                    return;
                var cometConfig = CometHandler.FileContentsToCometParams(currentParam.Value);

                CometSuffixBox.Text = cometConfig.OutputSuffix;
                CleavageAgentBox.Text = CometParams.CleavageAgentOptions.First(x=>x.Value == cometConfig.CleavageAgent).Key;
                SpecificityBox.SelectedIndex = cometConfig.Specificity;

                //tolerance
                if (cometConfig.FragmentBinTolerance > 0.5)
                    FragmentLowRadio.Checked = true;
                else
                    FragmentHighRadio.Checked = true;
                if (cometConfig.PrecursorUnit == CometParams.PrecursorUnitOptions.Daltons)
                {
                    PrecursorLowRadio.Checked = true;
                    PrecursorToleranceUnitsBox.Text = "mz";
                }
                else
                {
                    PrecursorHighRadio.Checked = true;
                    PrecursorToleranceUnitsBox.Text = "ppm";
                }
                PrecursorToleranceBox.Text = cometConfig.PrecursorTolerance.ToString();

                //mods
                if (cometConfig.StaticCysteineMod > 0)
                    ModBox.Rows.Add(new object[] {"C", cometConfig.StaticCysteineMod, "Static"});
                foreach (var mod in cometConfig.DynamicModifications)
                {
                    if (mod.Residue == "X")
                        continue;
                    var residue = (mod.isNterminal() ? "(" : string.Empty) +
                                  mod.Residue.Replace("*", string.Empty) +
                                  (mod.isCterminal() ? ")" : string.Empty);
                    ModBox.Rows.Add(new object[] {residue, mod.MassChange, "Dynamic"});
                }
            }
            else if (recievedHi.JobType == JobType.MSGF)
            {
                var currentParam = configFile.FirstOrDefault(x => x.Name.ToLower() == "config");
                if (currentParam == null)
                    return;
                var msgfConfig = MSGFHandler.OverloadToMSGFParams(currentParam.Value);
                currentParam = configFile.FirstOrDefault(x => x.Name.ToLower() == "mods");
                if (currentParam == null)
                    return;
                var msgfMods = MSGFHandler.ModStringToModList(currentParam.Value);

                CometSuffixBox.Text = msgfConfig.OutputSuffix;
                CleavageAgentBox.Text = MSGFParams.CleavageAgentOptions.First(x => x.Value == msgfConfig.CleavageAgent).Key;
                SpecificityBox.SelectedIndex = msgfConfig.Specificity;

                ////tolerance
                //if (msgfConfig.FragmentationMethod == MSGFParams.FragmentationMethodOptions.CID)
                //    FragmentLowRadio.Checked = true;
                //else
                //    FragmentHighRadio.Checked = true;
                if (msgfConfig.PrecursorToleranceUnits == MSGFParams.PrecursorToleranceUnitOptions.Daltons)
                {
                    PrecursorLowRadio.Checked = true;
                    PrecursorToleranceUnitsBox.Text = "mz";
                }
                else
                {
                    PrecursorHighRadio.Checked = true;
                    PrecursorToleranceUnitsBox.Text = "ppm";
                }
                PrecursorToleranceBox.Text = msgfConfig.PrecursorTolerance.ToString();

                //mods
                foreach (var mod in msgfMods)
                    ModBox.Rows.Add(new object[] {mod.Residue, mod.Mass, mod.Type});
            }

        }

        private List<HistoryItem> _externalHiList;
        public List<HistoryItem> GetHistoryItems()
        {
            if (AdvancedModeBox.Checked && _externalHiList != null)
                return _externalHiList;
            var hiList = new List<HistoryItem>();
            var files = GetInputFileNames();
            var outputDir = OutputFolderBox.Text;

            //generic hi setup
            var initialHi = new HistoryItem
                {
                    JobName = OutputNewCheckBox.Checked ? OutputNewFolderBox.Text : Path.GetFileName(outputDir),
                    OutputDirectory = OutputNewCheckBox.Checked ? outputDir + "+" : outputDir,
                    ProteinDatabase = DatabaseBox.Text,
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

            if (PepRadio.Checked)
            {
                initialHi.JobType = JobType.Library;
                initialHi.SpectralLibrary = LibraryBox.Text;
                initialHi.InitialConfigFile = GetConfigFile("Pepitome");

                //HACK: Until fix gets pushed out Pepitome should only run with 1 cpu core
                initialHi.Cpus = 1;
            }
            else if (TagRadio.Checked)
            {
                initialHi.JobType = JobType.Tag;
                initialHi.InitialConfigFile = GetConfigFile("DirecTag");
                initialHi.TagConfigFile = GetConfigFile("TagRecon");
            }
            else if (DBRadio.Checked)
            {
                initialHi.JobType = JobType.Myrimatch;
                initialHi.InitialConfigFile = GetConfigFile("MyriMatch");
                if (MyriBox.Checked && OutputNewCheckBox.Checked)
                    initialHi.OutputDirectory = Path.Combine(outputDir ?? string.Empty, OutputNewFolderBox.Text);

                var cometHi = new HistoryItem
                    {
                        JobType = JobType.Comet,
                        OutputDirectory = initialHi.OutputDirectory,
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
                    OutputDirectory = initialHi.OutputDirectory,
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
                if (CometBox.Checked)
                    hiList.Add(cometHi);
                if (MSGFBox.Checked)
                    hiList.Add(msgfHi);
            }
            else
                throw new Exception("None of the known search types were checked." +
                                    " Was there a new one added that hasn't been accounted for?");
            if (!DBRadio.Checked || MyriBox.Checked)
                hiList.Insert(0,initialHi);
            return hiList;
        }

        private ConfigFile GetConfigFile(string destinationProgram)
        {
            if (destinationProgram == "MyriMatch" ||
                destinationProgram == "DirecTag" ||
                destinationProgram == "TagRecon" ||
                destinationProgram == "Pepitome")
            {
                var config = new ConfigFile()
                {
                    DestinationProgram = destinationProgram,
                    PropertyList = new List<ConfigProperty>(),
                    FilePath = "--Custom--"
                };
                var tolerance = double.Parse(PrecursorToleranceBox.Text);
                if (destinationProgram == "DirecTag")
                {
                    if (PrecursorToleranceUnitsBox.Text == "ppm")
                        tolerance /= 1000;
                    config.PropertyList.Add(new ConfigProperty
                    {
                        Name = "PrecursorMzTolerance",
                        Value = tolerance.ToString(CultureInfo.InvariantCulture),
                        Type = "string",
                        ConfigAssociation = config
                    });
                }
                else if (destinationProgram == "TagRecon")
                    config.PropertyList.Add(new ConfigProperty
                        {
                            Name = "PrecursorMzTolerance",
                            Value = "\"" + tolerance.ToString(CultureInfo.InvariantCulture) + PrecursorToleranceUnitsBox.Text + "\"",
                            Type = "string",
                            ConfigAssociation = config
                        });
                else
                    config.PropertyList.Add(new ConfigProperty
                    {
                        Name = "MonoPrecursorMzTolerance",
                        Value = "\"" + tolerance.ToString(CultureInfo.InvariantCulture) + PrecursorToleranceUnitsBox.Text + "\"",
                        Type = "string",
                        ConfigAssociation = config
                    });
                if (!destinationProgram.Contains("Tag") && PrecursorToleranceUnitsBox.Text == "mz" && double.Parse(PrecursorToleranceBox.Text) > 0.2)
                    config.PropertyList.Add(new ConfigProperty
                    {
                        Name = "MonoisotopeAdjustmentSet",
                        Value = "\"0\"",
                        Type = "string",
                        ConfigAssociation = config
                    });
                tolerance = double.Parse(FragmentToleranceBox.Text);
                if (destinationProgram == "DirecTag")
                {
                    if (FragmentToleranceUnitsBox.Text == "ppm")
                        tolerance /= 1000;
                    config.PropertyList.Add(new ConfigProperty
                    {
                        Name = "FragmentMzTolerance",
                        Value = tolerance.ToString(CultureInfo.InvariantCulture),
                        Type = "string",
                        ConfigAssociation = config
                    });
                    config.PropertyList.Add(new ConfigProperty
                    {
                        Name = "ComplementMzTolerance",
                        Value = tolerance.ToString(CultureInfo.InvariantCulture),
                        Type = "string",
                        ConfigAssociation = config
                    });
                    config.PropertyList.Add(new ConfigProperty
                    {
                        Name = "IsotopeMzTolerance",
                        Value = tolerance.ToString(CultureInfo.InvariantCulture),
                        Type = "string",
                        ConfigAssociation = config
                    });
                }
                else
                    config.PropertyList.Add(new ConfigProperty
                        {
                            Name = "FragmentMzTolerance",
                            Value = "\"" + tolerance.ToString(CultureInfo.InvariantCulture) + FragmentToleranceUnitsBox.Text + "\"",
                            Type = "string",
                            ConfigAssociation = config
                        });

                if (destinationProgram != "DirecTag")
                {
                    if (SuffixBox.Checked)
                        config.PropertyList.Add(new ConfigProperty
                            {
                                Name = "OutputSuffix",
                                Value = "\"" + PrimarySuffixBox.Text + "\"",
                                Type = "string",
                                ConfigAssociation = config
                            });
                    config.PropertyList.Add(new ConfigProperty
                        {
                            Name = "CleavageRules",
                            Value = "\"" + CleavageAgentBox.Text + "\"",
                            Type = "string",
                            ConfigAssociation = config
                        });

                    config.PropertyList.Add(new ConfigProperty
                        {
                            Name = "MinTerminiCleavages",
                            Value = SpecificityBox.SelectedIndex.ToString(CultureInfo.InvariantCulture),
                            Type = "int",
                            ConfigAssociation = config
                        });
                    config.PropertyList.Add(new ConfigProperty
                    {
                        Name = "DecoyPrefix",
                        Value = "XXX_",
                        Type = "string",
                        ConfigAssociation = config
                    });
                }
                if (destinationProgram == "TagRecon" && BlindModBox.Checked)
                    config.PropertyList.Add(new ConfigProperty
                    {
                        Name = "ExplainUnknownMassShiftsAs",
                        Value = "\"blindptms\"",
                        Type = "string",
                        ConfigAssociation = config
                    });
                var staticMods = new List<string>();
                var dynamicMods = new List<string>();
                for (var x = 0; x < ModBox.Rows.Count; x++)
                {
                    var residue = ModBox.Rows[x].Cells[0].Value.ToString();
                    var massString =  ModBox.Rows[x].Cells[1].Value.ToString();
                    var type = ModBox.Rows[x].Cells[2].Value.ToString();
                    double mass;

                    if (string.IsNullOrEmpty(residue) || string.IsNullOrEmpty(massString) ||
                        !double.TryParse(massString, out mass) || string.IsNullOrEmpty(type))
                        continue;
                    if (type == "Static")
                    {
                        staticMods.Add(residue);
                        staticMods.Add(mass.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        dynamicMods.Add(residue);
                        dynamicMods.Add("*");
                        dynamicMods.Add(mass.ToString(CultureInfo.InvariantCulture));
                    }
                }
                if (staticMods.Count > 0)
                    config.PropertyList.Add(new ConfigProperty
                        {
                            Name = "StaticMods",
                            Value = "\"" + string.Join(" ", staticMods) + "\"",
                            Type = "string",
                            ConfigAssociation = config
                        });
                if (dynamicMods.Count > 0)
                    config.PropertyList.Add(new ConfigProperty
                        {
                            Name = "DynamicMods",
                            Value = "\"" + string.Join(" ", dynamicMods) + "\"",
                            Type = "string",
                            ConfigAssociation = config
                        });
                
                return config;
            }
            if (destinationProgram == "Comet")
            {
                CometParams cometParams;
                if (FragmentLowRadio.Checked)
                    cometParams = CometParams.GetIonTrapParams();
                else if (FragmentMidRadio.Checked)
                    cometParams = CometParams.GetTofParams();
                else
                    cometParams = CometParams.GetHighResParams();
                cometParams.PrecursorTolerance = double.Parse(PrecursorToleranceBox.Text);
                cometParams.PrecursorUnit = PrecursorToleranceUnitsBox.Text == "mz"
                                                ? CometParams.PrecursorUnitOptions.Daltons
                                                : CometParams.PrecursorUnitOptions.PPM;
                cometParams.OutputSuffix = CometSuffixBox.Text;
                if (CometParams.CleavageAgentOptions.ContainsKey(CleavageAgentBox.Text))
                    cometParams.CleavageAgent = CometParams.CleavageAgentOptions[CleavageAgentBox.Text];
                else
                    MessageBox.Show("[Comet] Cannot use " + CleavageAgentBox.Text +
                                    " as a digestive enzyme for comet searches. Results may be unpredictable.");
                cometParams.Specificity = SpecificityBox.Text == "Fully-Specific"
                                              ? CometParams.SpecificityOptions.Tryptic
                                              : CometParams.SpecificityOptions.SemiTryptic;
                for (var x = 0; x < ModBox.Rows.Count; x++)
                {
                    var residue = ModBox.Rows[x].Cells[0].Value.ToString();
                    var massString = ModBox.Rows[x].Cells[1].Value.ToString();
                    var type = ModBox.Rows[x].Cells[2].Value.ToString();
                    double mass;

                    if (string.IsNullOrEmpty(residue) || string.IsNullOrEmpty(massString) ||
                        !double.TryParse(massString, out mass) || string.IsNullOrEmpty(type))
                        continue;

                    if (type == "Static" && residue == "C")
                    {
                        cometParams.StaticCysteineMod = mass;
                        continue;
                    }
                    //for now can only do static on cystine
                    if (type == "Static")
                        MessageBox.Show("[Comet] Warning, BumberDash can only apply static modifications "+
                            "to cystine at this time. Applying \'" +residue + ";" + mass + "\' as dynamic");
                    cometParams.DynamicModifications.Add(new CometParams.Modification(residue, mass));
                }
                var config = new ConfigFile
                    {
                        DestinationProgram = "Comet",
                        FilePath = "--Custom--"
                    };
                config.PropertyList = new List<ConfigProperty>
                    {
                        new ConfigProperty
                            {
                                Name = "config",
                                Value = CometHandler.CometParamsToFileContents(cometParams),
                                Type = "string",
                                ConfigAssociation = config
                            }
                    };
                return config;
            }
            if (destinationProgram == "MSGF")
            {
                var msgfParams = new MSGFParams();
                msgfParams.PrecursorTolerance = double.Parse(PrecursorToleranceBox.Text);
                msgfParams.PrecursorToleranceUnits = PrecursorToleranceUnitsBox.Text == "mz"
                                                         ? MSGFParams.PrecursorToleranceUnitOptions.Daltons
                                                         : MSGFParams.PrecursorToleranceUnitOptions.PPM;
                //msgfParams.FragmentationMethod = FragmentLowRadio.Checked
                //                                     ? MSGFParams.FragmentationMethodOptions.CID
                //                                     : MSGFParams.FragmentationMethodOptions.HCD;
                if (PrecursorLowRadio.Checked || FragmentLowRadio.Checked)
                    msgfParams.Instrument = MSGFParams.InstrumentOptions.LowResLTQ;
                else if (PrecursorMidRadio.Checked || FragmentMidRadio.Checked)
                    msgfParams.Instrument = MSGFParams.InstrumentOptions.TOF;
                else
                    msgfParams.Instrument = MSGFParams.InstrumentOptions.HighResLTQ;
                msgfParams.OutputSuffix = MSGFSuffixBox.Text;
                if (MSGFParams.CleavageAgentOptions.ContainsKey(CleavageAgentBox.Text))
                    msgfParams.CleavageAgent = MSGFParams.CleavageAgentOptions[CleavageAgentBox.Text];
                else if (CleavageAgentBox.Text != "Trypsin/P")
                    MessageBox.Show("[MSGF] Cannot use " + CleavageAgentBox.Text +
                                    " as a digestive enzyme for MS-GF+ searches. Results may be unpredictable.");
                msgfParams.Specificity = SpecificityBox.Text == "Fully-Specific"
                                              ? MSGFParams.SpecificityOptions.Tryptic
                                              : MSGFParams.SpecificityOptions.SemiTryptic;
                var msgfMods = new List<Util.Modification>();
                for (var x = 0; x < ModBox.Rows.Count; x++)
                {
                    var residue = ModBox.Rows[x].Cells[0].Value.ToString();
                    var massString = ModBox.Rows[x].Cells[1].Value.ToString();
                    var type = ModBox.Rows[x].Cells[2].Value.ToString();
                    double mass;

                    if (string.IsNullOrEmpty(residue) || string.IsNullOrEmpty(massString) ||
                        !double.TryParse(massString, out mass) || string.IsNullOrEmpty(type))
                        continue;

                    msgfMods.Add(new Util.Modification {Residue = residue, Mass = mass, Type = type});
                }
                var config = new ConfigFile
                    {
                        DestinationProgram = "MSGF",
                        FilePath = "--Custom--"
                    };
                config.PropertyList = new List<ConfigProperty>
                    {
                        new ConfigProperty
                            {
                                Name = "config",
                                Value = MSGFHandler.MSGFParamsToOverload(msgfParams),
                                Type = "string",
                                ConfigAssociation = config
                            },
                        new ConfigProperty
                            {
                                Name = "mods",
                                Value = MSGFHandler.ModListToModString(msgfMods, 2),
                                Type = "string",
                                ConfigAssociation = config
                            }
                    };
                return config;
            }
            throw new Exception("Invalid destination program, can't construct configuration");
        }

        private void ToleranceBox_Leave(object sender, EventArgs e)
        {
            double output;
            var box = sender as TextBox ?? new TextBox();
            if (!double.TryParse(box.Text, out output))
                box.Text = string.Empty;
        }

        private void AdvancedModeBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!AdvancedModeBox.Checked)
                return;
            AddJobForm advancedForm;
            if (_currentJob.FileList == null)
                advancedForm = new AddJobForm(_templateList, this);
            else
                advancedForm = new AddJobForm(_currentJob, _templateList, _editMode, this);
            this.Visible = false;
            if (advancedForm.ShowDialog() == DialogResult.OK)
            {
                _externalHiList = advancedForm.GetHistoryItems();
                DialogResult = DialogResult.OK;
            }
            else
                AdvancedModeBox.Checked = false;
        }
    }
}
