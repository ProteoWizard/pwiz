using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using DataMapping;
using System.Windows.Forms;

namespace BumberDash
{
    public partial class AddJobForm : Form
    {
        #region Globals
        delegate void DirectOutputCallback(string text);
        List<string> OutputFiles = new List<string>();
        internal Dictionary<int, ConfigFile> _myriDropDownItems = new Dictionary<int, ConfigFile>();
        internal Dictionary<int, ConfigFile> _dtDropDownItems = new Dictionary<int, ConfigFile>();
        internal Dictionary<int, ConfigFile> _trDropDownItems = new Dictionary<int, ConfigFile>();
        //string MyriMatchLocation = @"C:\Dev\myrimatch\myrimatch.exe";
        //string DirecTagLocation = @"C:\Dev\directag\directag.exe";
        //string TagReconLocation = @"C:\Dev\tagrecon\tagrecon.exe";
        //string IDPickerLocation = @"C:\Program Files\Bumbershoot\IDPicker 2.6.126.0\IdPickerGui.exe";
        #endregion

        private QueueForm _mainForm;

        public AddJobForm(QueueForm ParentForm)
        {
            InitializeComponent();
            _mainForm = ParentForm;
        }
  
        #region Click Events

            private void DatabaseLocButton_Click(object sender, EventArgs e)
            {
                string Backup = DatabaseLocBox.Text;
                DatabaseLocBox.Text = OpenOneFile("FASTA files (.fasta) | *.fasta", "Database Location");
                if (string.IsNullOrEmpty(DatabaseLocBox.Text))
                    DatabaseLocBox.Text = Backup;
            }

            private void InitialDirectoryButton_Click(object sender, EventArgs e)
            {
                FolderBrowserDialog FolderDialog = new FolderBrowserDialog();
                FolderDialog.Description = "Working Directory";
                FolderDialog.RootFolder = System.Environment.SpecialFolder.MyComputer;
                FolderDialog.SelectedPath = OutputDirectoryBox.Text;
                FolderDialog.ShowNewFolderButton = false;
                if (FolderDialog.ShowDialog() == DialogResult.OK)
                {
                    OutputDirectoryBox.Text = FolderDialog.SelectedPath;
                }

            }

            private void DataFilesButton_Click(object sender, EventArgs e)
            {
                string[] SelectedFiles;

                OpenFileDialog FileLoc = new OpenFileDialog();
                FileLoc.RestoreDirectory = true;
                FileLoc.Filter = "mzML files (.mzML)|*.mzML|mzXML files (.mzXML)|*.mzXML|RAW files (.raw)|*.raw|WIFF files (.wiff)|*.wiff|YEP files (.yep)|*.yep|MGF files (.mgf)|*.mgf";
                FileLoc.CheckFileExists = true;
                FileLoc.CheckPathExists = true;
                FileLoc.Multiselect = true;
                FileLoc.Title = "Data files";
                if (FileLoc.ShowDialog() == DialogResult.OK)
                {
                    SelectedFiles = FileLoc.FileNames;
                    InputFilesBox.Text = string.Empty;
                    OutputDirectoryBox.Text = System.IO.Directory.GetParent(SelectedFiles[0]).ToString();
                    foreach (String foo in SelectedFiles)
                        InputFilesBox.Text += "\"" + foo + "\"" + System.Environment.NewLine;

                    //OverallPBar.Maximum = SelectedFiles.Length;
                    InputFilesBox.Text = InputFilesBox.Text.Trim();
                }

            }

            private void MyriConfigButton_Click(object sender, EventArgs e)
            {
                string Backup = MyriConfigBox.Text;

                MyriConfigBox.Text = OpenOneFile("Config Files (.cfg)|*.cfg|pepXML Files (.pepXML)|*.pepXML", "MyriMatch config file location");
                if (string.IsNullOrEmpty(MyriConfigBox.Text))
                    MyriConfigBox.Text = Backup;
            }

            private void DTConfigButton_Click(object sender, EventArgs e)
            {
                string Backup = DTConfigBox.Text;

                DTConfigBox.Text = OpenOneFile("Config Files (.cfg)|*.cfg|Tags Files (.tags)|*.tags", "DirecTag config file location");
                if (string.IsNullOrEmpty(DTConfigBox.Text))
                    DTConfigBox.Text = Backup;
            }

            private void TRConfigButton_Click(object sender, EventArgs e)
            {
                string Backup = TRConfigBox.Text;

                TRConfigBox.Text = OpenOneFile("Config Files (.cfg)|*.cfg|pepXML Files (.pepXML)|*.pepXML", "TagRecon config file location");
                if (string.IsNullOrEmpty(TRConfigBox.Text))
                    TRConfigBox.Text = Backup;
            }

            private void AddJobCancelButton_Click(object sender, EventArgs e)
            {
                Close();
            }

            private void AddJobRunButton_Click(object sender, EventArgs e)
            {
                _mainForm.QueueJobFromForm();
            }

            private void MyriEditButton_Click(object sender, EventArgs e)
            {
                ConfigForm testConfigForm;

                testConfigForm = new ConfigForm(MyriConfigBox.Text ?? string.Empty, OutputDirectoryBox.Text, "MyriMatch", this);

                if (testConfigForm.ShowDialog(this) == (DialogResult.OK))
                {
                    MyriConfigBox.Text = testConfigForm._configName;
                    MyriMatchInfoBox.Text = testConfigForm._allProperties;
                }
            }

            private void DTEditButton_Click(object sender, EventArgs e)
            {
                ConfigForm testConfigForm;

                testConfigForm = new ConfigForm(DTConfigBox.Text ?? string.Empty, OutputDirectoryBox.Text, "DirecTag", this);

                if (testConfigForm.ShowDialog(this) == (DialogResult.OK))
                {
                    DTConfigBox.Text = testConfigForm._configName;
                    DirecTagInfoBox.Text = testConfigForm._allProperties;
                }
            }

            private void TREditButton_Click(object sender, EventArgs e)
            {
                ConfigForm testConfigForm;

                testConfigForm = new ConfigForm(TRConfigBox.Text ?? string.Empty, OutputDirectoryBox.Text, "TagRecon", this);

                if (testConfigForm.ShowDialog(this) == (DialogResult.OK))
                {
                    TRConfigBox.Text = testConfigForm._configName;
                    TagReconInfoBox.Text = testConfigForm._allProperties;
                }
            }

        #endregion

        private string OpenOneFile(string Filter, string DialogTitle)
        {
            OpenFileDialog FileLoc = new OpenFileDialog();
            FileLoc.InitialDirectory = OutputDirectoryBox.Text;
            FileLoc.RestoreDirectory = true;
            FileLoc.Filter = Filter;
            FileLoc.CheckFileExists = true;
            FileLoc.CheckPathExists = true;
            FileLoc.Multiselect = false;
            FileLoc.Title = DialogTitle;
            if (FileLoc.ShowDialog() == DialogResult.OK)
                return FileLoc.FileName;
            else
                return string.Empty;
        }

        private void DestinationRadio_CheckedChanged(object sender, EventArgs e)
        {
            if (DatabaseRadio.Checked == true)
            {
                ConfigGB.Visible = true;
                ConfigDatabasePanel.Visible = true;
                DatabasePanel.Visible = true;
                ConfigTagPanel.Visible = false;
                TagPanel.Visible = false;
            }
            else if (TagRadio.Checked == true)
            {
                ConfigGB.Visible = true;
                ConfigDatabasePanel.Visible = false;
                DatabasePanel.Visible = false;
                ConfigTagPanel.Visible = true;
                TagPanel.Visible = true;
            }
            else
            {
                ConfigGB.Visible = false;
            }
        }

        private void AddJobForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _mainForm.CancelEdit();
        }

        private void CPUsBox_ValueChanged(object sender, EventArgs e)
        {
            if (CPUsBox.Value == 0)
                CPUsAutoLabel.Visible = true;
            else
                CPUsAutoLabel.Visible = false;
        }

        private void MyriConfigBox_TextChanged(object sender, EventArgs e)
        {
            var customRx = new System.Text.RegularExpressions.Regex(@"^--\w+--$");

            if (File.Exists(MyriConfigBox.Text))
            {
                if ((new FileInfo(MyriConfigBox.Text)).Extension.Equals(".cfg"))
                {
                    MyriEditButton.Text = "Edit";

                    //preview file
                    var fileIn = new StreamReader(MyriConfigBox.Text);
                    MyriMatchInfoBox.Text = fileIn.ReadToEnd();
                    fileIn.Close();
                    fileIn.Dispose();
                }
                else if ((new FileInfo(MyriConfigBox.Text)).Extension.Equals(".pepXML"))
                {
                    MyriEditButton.Text = "Convert";
                    var tempCE = new ConfigForm(MyriConfigBox.Text, (Directory.Exists(OutputDirectoryBox.Text) ? OutputDirectoryBox.Text : Application.StartupPath), "MyriMatch", this);
                    
                    tempCE.SaveAsTemporaryButton_Click(null, e);
                    MyriMatchInfoBox.Text = tempCE._allProperties;
                    tempCE.Close();
                }
                else
                    MyriMatchInfoBox.Text = "Invalid File";
            }
            else if (customRx.IsMatch(MyriConfigBox.Text))
                MyriEditButton.Text = "Change";
            else
            {
                MyriMatchInfoBox.Text = string.Empty;
                MyriEditButton.Text = "New";
            }
        }

        private void DTConfigBox_TextChanged(object sender, EventArgs e)
        {
            var customRx = new System.Text.RegularExpressions.Regex(@"^--\w+--$");

            if (File.Exists(DTConfigBox.Text))
            {
                if ((new FileInfo(DTConfigBox.Text)).Extension.Equals(".cfg"))
                {
                    DTEditButton.Text = "Edit";

                    //preview file
                    var fileIn = new StreamReader(DTConfigBox.Text);
                    DirecTagInfoBox.Text = fileIn.ReadToEnd();
                    fileIn.Close();
                    fileIn.Dispose();
                }
                else if ((new FileInfo(DTConfigBox.Text)).Extension.Equals(".tags"))
                {
                    DTEditButton.Text = "Convert";

                    var tempCE = new ConfigForm(DTConfigBox.Text, (Directory.Exists(OutputDirectoryBox.Text) ? OutputDirectoryBox.Text : Application.StartupPath), "DirecTag", this);

                    tempCE.SaveAsTemporaryButton_Click(null, e);
                    DirecTagInfoBox.Text = tempCE._allProperties;
                    tempCE.Close();
                }
                else
                    DirecTagInfoBox.Text = "Invalid File";
            }
            else if (customRx.IsMatch(DTConfigBox.Text))
                DTEditButton.Text = "Change";
            else
            {
                DirecTagInfoBox.Text = string.Empty;
                DTEditButton.Text = "New";
            }

        }

        private void TRConfigBox_TextChanged(object sender, EventArgs e)
        {
            var customRx = new System.Text.RegularExpressions.Regex(@"^--\w+--$");

            if (File.Exists(TRConfigBox.Text))
            {
                if ((new FileInfo(TRConfigBox.Text)).Extension.Equals(".cfg"))
                {
                    TREditButton.Text = "Edit";

                    //preview file
                    var fileIn = new StreamReader(DTConfigBox.Text);
                    TagReconInfoBox.Text = fileIn.ReadToEnd();
                    fileIn.Close();
                    fileIn.Dispose();
                }
                else if ((new FileInfo(TRConfigBox.Text)).Extension.Equals(".pepXML"))
                {
                    TREditButton.Text = "Convert";
                    var tempCE = new ConfigForm(TRConfigBox.Text, (Directory.Exists(OutputDirectoryBox.Text) ? OutputDirectoryBox.Text : Application.StartupPath), "TagRecon", this);

                    tempCE.SaveAsTemporaryButton_Click(null, e);
                    TagReconInfoBox.Text = tempCE._allProperties;
                    tempCE.Close();
                }
                else
                    TagReconInfoBox.Text = "Invalid File";
            }
            else if (customRx.IsMatch(TRConfigBox.Text))
                TREditButton.Text = "Change";
            else
            {
                TagReconInfoBox.Text = string.Empty;
                TREditButton.Text = "New";
            }
        }

        private void InfoExpandButton_Click(object sender, EventArgs e)
        {
            if (this.Width > 442)
            {
                InfoExpandButton.Text = string.Format(">{0}>{0}>", System.Environment.NewLine);
                this.Width = 442;
                this.Location = new Point(this.Location.X + 110, this.Location.Y);
            }
            else
            {
                InfoExpandButton.Text = string.Format("<{0}<{0}<", System.Environment.NewLine);
                this.Width = 660;
                this.Location = new Point(this.Location.X - 110, this.Location.Y);
            }
        }

        private void MyriConfigBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!File.Exists(MyriConfigBox.Text) || (Path.GetExtension(MyriConfigBox.Text) != ".cfg" && Path.GetExtension(MyriConfigBox.Text) != ".pepXML"))
            {
                MyriMatchInfoBox.Text = string.Empty;
                foreach (var item in _myriDropDownItems[MyriConfigBox.SelectedIndex].PropertyList)
                    MyriMatchInfoBox.Text += String.Format("{0} = {1}{2}", item.Name, item.Value, System.Environment.NewLine);
            }
        }

        private void DTConfigBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!File.Exists(DTConfigBox.Text) || (Path.GetExtension(DTConfigBox.Text) != ".cfg" && Path.GetExtension(DTConfigBox.Text) != ".tags"))
            {
                DirecTagInfoBox.Text = string.Empty;
                foreach (var item in _dtDropDownItems[DTConfigBox.SelectedIndex].PropertyList)
                    DirecTagInfoBox.Text += String.Format("{0} = {1}{2}", item.Name, item.Value, System.Environment.NewLine);
            }
        }

        private void TRConfigBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!File.Exists(TRConfigBox.Text) || (Path.GetExtension(TRConfigBox.Text) != ".cfg" && Path.GetExtension(TRConfigBox.Text) != ".pepXML"))
            {
                TagReconInfoBox.Text = string.Empty;
                foreach (var item in _trDropDownItems[TRConfigBox.SelectedIndex].PropertyList)
                    TagReconInfoBox.Text += String.Format("{0} = {1}{2}", item.Name, item.Value, System.Environment.NewLine);
            }
        }
    }
}
