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
using System.Windows.Forms;

namespace BumberDash
{
    public partial class AddJobForm : Form
    {
        #region Globals
        delegate void DirectOutputCallback(string text);
        List<string> OutputFiles = new List<string>();
        //string MyriMatchLocation = @"C:\Dev\myrimatch\myrimatch.exe";
        //string DirecTagLocation = @"C:\Dev\directag\directag.exe";
        //string TagReconLocation = @"C:\Dev\tagrecon\tagrecon.exe";
        //string IDPickerLocation = @"C:\Program Files\Bumbershoot\IDPicker 2.6.126.0\IdPickerGui.exe";
        #endregion

        private QueueForm MainList;

        public AddJobForm(QueueForm ParentForm)
        {
            InitializeComponent();
            MainList = ParentForm;
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
                MainList.QueueJobFromForm();
            }

            private void MyriEditButton_Click(object sender, EventArgs e)
            {
                ConfigForm testConfigForm;

                if (!string.IsNullOrEmpty(MyriConfigBox.Text) && File.Exists(MyriConfigBox.Text))
                    testConfigForm = new ConfigForm(MyriConfigBox.Text, OutputDirectoryBox.Text, "MyriMatch");
                else
                    testConfigForm = new ConfigForm(string.Empty, OutputDirectoryBox.Text, "MyriMatch");
                if (testConfigForm.ShowDialog().Equals(DialogResult.OK))
                {
                    MyriConfigBox.Text = testConfigForm._configName;
                }
            }

            private void DTEditButton_Click(object sender, EventArgs e)
            {
                ConfigForm testConfigForm;

                if (!string.IsNullOrEmpty(DTConfigBox.Text) && File.Exists(DTConfigBox.Text))
                    testConfigForm = new ConfigForm(DTConfigBox.Text, OutputDirectoryBox.Text, "DirecTag");
                else
                    testConfigForm = new ConfigForm(string.Empty, OutputDirectoryBox.Text, "DirecTag");
                if (testConfigForm.ShowDialog().Equals(DialogResult.OK))
                {
                    DTConfigBox.Text = testConfigForm._configName;
                }
            }

            private void TREditButton_Click(object sender, EventArgs e)
            {
                ConfigForm testConfigForm;

                if (!string.IsNullOrEmpty(TRConfigBox.Text) && File.Exists(TRConfigBox.Text))
                    testConfigForm = new ConfigForm(TRConfigBox.Text, OutputDirectoryBox.Text, "TagRecon");
                else
                    testConfigForm = new ConfigForm(string.Empty, OutputDirectoryBox.Text, "TagRecon");
                if (testConfigForm.ShowDialog().Equals(DialogResult.OK))
                {
                    TRConfigBox.Text = testConfigForm._configName;
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
                ConfigTagPanel.Visible = false;
            }
            else if (TagRadio.Checked == true)
            {
                ConfigGB.Visible = true;
                ConfigDatabasePanel.Visible = false;
                ConfigTagPanel.Visible = true;
            }
            else
            {
                ConfigGB.Visible = false;
            }
        }

        private void AddJobForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            MainList.CancelEdit();
        }

        private void OutputDirectoryBox_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(OutputDirectoryBox.Text) && Directory.Exists(OutputDirectoryBox.Text))
            {
                MyriEditButton.Visible = true;
                DTEditButton.Visible = true;
                TREditButton.Visible = true;
            }
            else
            {
                MyriEditButton.Visible = false;
                DTEditButton.Visible = false;
                TREditButton.Visible = false;
            }
        }

        private void CPUsBox_ValueChanged(object sender, EventArgs e)
        {
            if (CPUsBox.Value == 0)
                CPUsAutoLabel.Visible = true;
            else
                CPUsAutoLabel.Visible = false;
        }
    }
}
