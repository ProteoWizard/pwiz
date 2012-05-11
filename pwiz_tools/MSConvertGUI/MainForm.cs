//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using CustomDataSourceDialog;
using pwiz.CLI.msdata;

namespace MSConvertGUI
{
    public partial class MainForm : Form
    {
        string[] cmdline_args;
        string SetDefaultsDataType=""; // watch last-added filetype, offer to set defaults for that type

        private string MakeConfigfileName()
        {
            string ext = ".";
            if (SetDefaultsDataType != "") // any current input type?
                ext += SetDefaultsDataType + ".";
            // note not calling these files "*.cfg" in order to distinguish them from the boost program_options files
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\MSConvertGUI" + ext + "cmdline";
        }

        public MainForm(string[] args)
        {
            cmdline_args = args;
            InitializeComponent();
        }

        private void MainForm_Load (object sender, EventArgs e)
        {
            for (int i = 0; i < cmdline_args.Length; i++)
            {
                // mimic user adding file via filebox
                FileBox.Text = cmdline_args[i];
                AddFileButton_Click(sender, e);
                if (FileBox.Text.Length > 0)
                {
                    // if it didn't get added, field will still contain file or dir name
                    MessageBox.Show("Don't know how to read \"" + FileBox.Text + "\", ignored");
                    FileBox.Text = "";
                }
            }
            OutputFormatBox.Text = "mzML";
            FilterBox.Text = "MS Level";
            ActivationTypeBox.Text = "CID";
            // check for a user default config
            String configname = MakeConfigfileName();
            if (File.Exists(configname))
                SetGUIfromCfg(configname); // populate buttons etc from config file
        }

        private void UseCFGButton_CheckedChanged(object sender, EventArgs e)
        {
            if (UseCFGButton.Checked)
            {
                ConfigurationFileGB.Visible = true;
                OptionsGB.Size = new Size(269, 164);
                var newY = SlidingPanel.Location.Y - 45;
                SlidingPanel.Location = new Point(SlidingPanel.Location.X, newY);
                var newHeight = FileListBox.Size.Height - 38;
                FileListBox.Size = new Size(FileListBox.Size.Width, newHeight);
            }
            else
            {
                ConfigurationFileGB.Visible = false;
                OptionsGB.Size = new Size(269, 119);
                var newY = SlidingPanel.Location.Y + 45;
                SlidingPanel.Location = new Point(SlidingPanel.Location.X, newY);
                var newHeight = FileListBox.Size.Height + 40;
                FileListBox.Size = new Size(FileListBox.Size.Width, newHeight);
            }
        }

        private void FilterBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            MSLevelPanel.Visible = false;
            PeakPickingPanel.Visible = false;
            ZeroSamplesPanel.Visible = false;
            ETDFilterPanel.Visible = false;
            ChargeStatePredictorPanel.Visible = false;
            ActivationPanel.Visible = false;
            SubsetPanel.Visible = false;

            switch (FilterBox.Text)
            {
                case "MS Level":
                    MSLevelPanel.Visible = true;
                    break;
                case "Peak Picking":
                    PeakPickingPanel.Visible = true;
                    break;
                case "Zero Samples":
                    ZeroSamplesPanel.Visible = true;
                    break;
                case "ETD Peak Filter":
                    ETDFilterPanel.Visible = true;
                    break;
                case "Charge State Predictor":
                    ChargeStatePredictorPanel.Visible = true;
                    break;
                case "Activation":
                    ActivationPanel.Visible = true;
                    break;
                case "Subset":
                    SubsetPanel.Visible = true;
                    break;
            }
        }

        private bool IsValidSource(string filepath)
        {
            return (File.Exists(filepath) || Directory.Exists(filepath)) &&
                   !FileListBox.Items.Contains(filepath) &&
                   !String.IsNullOrEmpty(ReaderList.FullReaderList.identify(filepath));
        }

        private void FileBox_TextChanged (object sender, EventArgs e)
        {
            AddFileButton.Enabled = IsValidSource(FileBox.Text);
        }

        private void AddFileButton_Click(object sender, EventArgs e)
        {
            if (IsValidSource(FileBox.Text))
            {
                if (String.IsNullOrEmpty(OutputBox.Text))
                    OutputBox.Text = Path.GetDirectoryName(FileBox.Text);
                // update the set-defaults button
                SetDefaultsDataType = ReaderList.FullReaderList.identify(FileBox.Text);
                SetDefaultsButton.Text = "Use these settings next time I start MSConvertGUI with " + SetDefaultsDataType + " data";
                setToolTip(SetDefaultsButton, "Saves the current settings and uses them as the defaults next time you open " + SetDefaultsDataType + " data with MSConvertGUI.");
                // and add to the list
                FileListBox.Items.Add(FileBox.Text);
                FileBox.Clear();
                RemoveFileButton.Enabled = true;
            }
        }

        private void RemoveFileButton_Click(object sender, EventArgs e)
        {
            if (FileListBox.SelectedItems.Count == 1)
            {
                FileBox.Text = (string)FileListBox.Items[FileListBox.SelectedIndex];
                FileListBox.Items.RemoveAt(FileListBox.SelectedIndex);
            }
            else if (FileListBox.SelectedItems.Count > 1)
            {
                FileBox.Text = (string)FileListBox.SelectedItems[0];
                var itemsToDelete = FileListBox.SelectedItems.Cast<object>().ToList();
                foreach (var item in itemsToDelete)
                    FileListBox.Items.Remove(item);
            }
            else
                return;

            RemoveFileButton.Enabled = FileListBox.Items.Count > 0;
            AddFileButton.Enabled = IsValidSource(FileBox.Text);
        }

        private void BrowseFileButton_Click(object sender, EventArgs e)
        {
            var fileList = new List<string>
                               {
                                   "Any spectra format","mzML", "mzXML", "MZ5",
                                   "Thermo RAW", "Waters RAW", "ABSciex WIFF",
                                   "Bruker Analysis", "Agilent MassHunter",
                                   "Mascot Generic", "Bruker Data Exchange"
                               };

            OpenDataSourceDialog browseToFileDialog;
            browseToFileDialog = String.IsNullOrEmpty(FileBox.Text)
                                     ? new OpenDataSourceDialog(fileList)
                                     : new OpenDataSourceDialog(fileList, FileBox.Text);

            #region Set up Delegates
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
            #endregion

            if (browseToFileDialog.ShowDialog() == DialogResult.OK)
            {
                if (browseToFileDialog.DataSources.Count == 1)
                    FileBox.Text = browseToFileDialog.DataSources[0];
                else if (browseToFileDialog.DataSources.Count > 1)
                {
                    foreach (string dataSource in browseToFileDialog.DataSources)
                        FileListBox.Items.Add(dataSource);

                    if (String.IsNullOrEmpty(OutputBox.Text) ||
                        !Directory.Exists(OutputBox.Text))
                        OutputBox.Text = Path.GetDirectoryName(browseToFileDialog.DataSources[0]);

                    RemoveFileButton.Enabled = FileListBox.Items.Count > 0;
                }
            }
        }

        private void FileListBox_KeyUp (object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                if (FileListBox.SelectedIndices.Count > 0)
                {
                    foreach (int selectedIndex in FileListBox.SelectedIndices.Cast<int>().Reverse())
                        FileListBox.Items.RemoveAt(selectedIndex);
                }
                e.Handled = true;
            }
        }

        private void ZeroSamples_ModeChanged(object sender, EventArgs e)
        {
            ZeroSamplesAddMissingFlankCountBox.Enabled = ZeroSamplesAddMissing.Checked;
        }

        private void NumTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void AddFilterButton_Click(object sender, EventArgs e)
        {
            switch (FilterBox.Text)
            {
                case "MS Level":
                    if (!String.IsNullOrEmpty(MSLevelBox1.Text) ||
                        !String.IsNullOrEmpty(MSLevelBox2.Text))
                        FilterDGV.Rows.Add(new[]
                                               {
                                                   "msLevel",
                                                   String.Format("{0}-{1}", MSLevelBox1.Text, MSLevelBox2.Text)
                                               });
                    break;
                case "Peak Picking":
                    if (!String.IsNullOrEmpty(PeakMSLevelBox1.Text) ||
                        !String.IsNullOrEmpty(PeakMSLevelBox2.Text))
                        FilterDGV.Rows.Add(new[]
                                               {
                                                   "peakPicking",
                                                   String.Format("{0} {1}-{2}",
                                                                 PeakPreferVendorBox.Checked.ToString().ToLower(),
                                                                 PeakMSLevelBox1.Text, PeakMSLevelBox2.Text)
                                               });
                    break; 
                case "Zero Samples":
                    String args = ZeroSamplesAddMissing.Checked ? "addMissing" : "removeExtra";
                    if ( ZeroSamplesAddMissing.Checked && (!String.IsNullOrEmpty(ZeroSamplesAddMissingFlankCountBox.Text)))
                        args+=String.Format("={0}",ZeroSamplesAddMissingFlankCountBox.Text);
                    if (!String.IsNullOrEmpty(ZeroSamplesMSLevelBox1.Text) ||
                        !String.IsNullOrEmpty(ZeroSamplesMSLevelBox2.Text))
                        args += String.Format(" {0}-{1}",ZeroSamplesMSLevelBox1.Text,ZeroSamplesMSLevelBox2.Text);
                    else // no mslevels specified means all mslevels
                        args += " 1-";
                    FilterDGV.Rows.Add(new[]
                                           {
                                               "zeroSamples",
                                                args
                                           });
                    break; 
                case "ETD Peak Filter":
                    var tempObject = new[] {"ETDFilter", String.Empty};
                    if (!ETDRemovePrecursorBox.Checked || !ETDRemoveChargeReducedBox.Checked ||
                        !ETDRemoveNeutralLossBox.Checked || !ETDBlanketRemovalBox.Checked)
                        tempObject[1] = String.Format("{0} {1} {2} {3}",
                                                      ETDRemovePrecursorBox.Checked.ToString().ToLower(),
                                                      ETDRemoveChargeReducedBox.Checked.ToString().ToLower(),
                                                      ETDRemoveNeutralLossBox.Checked.ToString().ToLower(),
                                                      ETDBlanketRemovalBox.Checked.ToString().ToLower());
                    FilterDGV.Rows.Add(tempObject);
                    break;
                case "Charge State Predictor":
                    if (!String.IsNullOrEmpty(ChaMCMinBox.Text) &&
                        !String.IsNullOrEmpty(ChaMCMaxBox.Text))
                        FilterDGV.Rows.Add(new[]
                                               {
                                                   "chargeStatePredictor",
                                                   String.Format("{0} {1} {2} {3}",
                                                                 ChaOverwriteCharge.Checked.ToString().ToLower(),
                                                                 ChaMCMaxBox.Text, ChaMCMinBox.Text, ChaSingleBox.Value)
                                               });
                    break;
                case "Activation":
                    if (!String.IsNullOrEmpty(ActivationTypeBox.Text))
                        FilterDGV.Rows.Add(new[] {"activation", ActivationTypeBox.Text});
                    break;
                case "Subset":
                    if (!String.IsNullOrEmpty(ScanNumberLow.Text) || !String.IsNullOrEmpty(ScanNumberHigh.Text))
                        FilterDGV.Rows.Add(new[] { "scanNumber", String.Format("{0}-{1}", ScanNumberLow.Text, ScanNumberHigh.Text) });
                    if (!String.IsNullOrEmpty(ScanTimeLow.Text) || !String.IsNullOrEmpty(ScanTimeHigh.Text))
                        FilterDGV.Rows.Add(new[] { "scanTime", String.Format("[{0},{1}]", ScanTimeLow.Text, ScanTimeHigh.Text) });
                    if (!String.IsNullOrEmpty(mzWinLow.Text) || !String.IsNullOrEmpty(mzWinHigh.Text))
                        FilterDGV.Rows.Add(new[] { "mzWindow", String.Format("[{0},{1}]", mzWinLow.Text, mzWinHigh.Text) });
                    break;
            }
        }

        private void RemoveFilterButton_Click(object sender, EventArgs e)
        {
            if (FilterDGV.SelectedRows.Count > 0)
                FilterDGV.Rows.Remove(FilterDGV.SelectedRows[0]);
        }

        private void OutputFormatBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            OutputExtensionBox.Text = OutputFormatBox.Text;
        }

        private void OutputBrowse_Click(object sender, EventArgs e)
        {
            var ofd = new FolderBrowserDialog();
            if (!String.IsNullOrEmpty(OutputBox.Text) && Directory.Exists(OutputBox.Text))
                ofd.SelectedPath = OutputBox.Text;
            if (ofd.ShowDialog() == DialogResult.OK)
                OutputBox.Text = ofd.SelectedPath;
        }

        private void FileListRadio_CheckedChanged(object sender, EventArgs e)
        {
            if (FileListRadio.Checked)
            {
                AddFileButton.Visible = true;
                RemoveFileButton.Visible = true;
                FileListBox.Enabled = true;
            }
            else
            {
                AddFileButton.Visible = false;
                RemoveFileButton.Visible = false;
                FileListBox.Enabled = false;
            }
        }

        private String ConstructCommandline() 
        // if you update this, you probably need to update SetControlsFromCommandline too
        {
            var commandLine = new StringBuilder();
            //Get config settings

            if (UseCFGButton.Checked &&
                (String.IsNullOrEmpty(ConfigBox.Text) || !File.Exists(ConfigBox.Text)))
                commandLine.AppendFormat("--config|\"{0}\"|", ConfigBox.Text);

            if (OutputFormatBox.Text != OutputExtensionBox.Text)
                commandLine.AppendFormat("--ext|{0}|", OutputExtensionBox.Text);

            switch (OutputFormatBox.Text)
            {
                case "mzXML":
                    commandLine.Append("--mzXML|");
                    break;
                case "mz5":
                    commandLine.Append("--mz5|");
                    break;
                case "mgf":
                    commandLine.Append("--mgf|");
                    break;
                case "text":
                    commandLine.Append("--text|");
                    break;
                case "ms2":
                    commandLine.Append("--ms2|");
                    break;
                case "cms2":
                    commandLine.Append("--cms2|");
                    break;
            }

            if (Precision32.Checked)
                commandLine.Append("--32|");

            if (!WriteIndexBox.Checked)
                commandLine.Append("--noindex|");

            if (UseZlibBox.Checked)
                commandLine.Append("--zlib|");

            if (GzipBox.Checked)
                commandLine.Append("--gzip|");

            var msLevelsTotal = String.Empty;
            var peakPickingTotal = String.Empty;
            var scanNumberTotal = String.Empty;
            var preferVendor = true;
            foreach (DataGridViewRow row in FilterDGV.Rows)
            {
                switch ((string)row.Cells[0].Value)
                {
                    case "msLevel":
                        msLevelsTotal += (string)row.Cells[1].Value + " ";
                        break;
                    case "peakPicking":
                        var splitLine = ((string)row.Cells[1].Value ?? "true").Split();
                        preferVendor = bool.Parse(splitLine[0]);
                        if (splitLine.Length > 1)
                            peakPickingTotal += splitLine[1] + " ";
                        break;
                    case "scanNumber":
                        scanNumberTotal += (string)row.Cells[1].Value + " ";
                        break;
                    default:
                        commandLine.AppendFormat("--filter|{0} {1}|", row.Cells[0].Value, row.Cells[1].Value);
                        break;
                }
            }

            if (!String.IsNullOrEmpty(msLevelsTotal))
                commandLine.AppendFormat("--filter|msLevel {0}|", msLevelsTotal.Trim());
            if (!String.IsNullOrEmpty(peakPickingTotal))
                commandLine.AppendFormat("--filter|peakPicking {0} {1}|", preferVendor.ToString().ToLower(),
                                         peakPickingTotal.Trim());
            if (!String.IsNullOrEmpty(scanNumberTotal))
                commandLine.AppendFormat("--filter|scanNumber {0}|", scanNumberTotal.Trim());

            return commandLine.ToString();
        }

        private void SetControlsFromCommandline(string commandLine)
        // if you update this, you probably need to update ConstructCommandLine too
        {
            // Get config settings
            Precision32.Checked = (commandLine.IndexOf("--32")>=0);
            Precision64.Checked = !Precision32.Checked;
            WriteIndexBox.Checked = !(commandLine.IndexOf("--noindex")>=0);
            UseZlibBox.Checked = (commandLine.IndexOf("--zlib")>=0);
            GzipBox.Checked = (commandLine.IndexOf("--gzip")>=0);
            string OutputExtension = "";

            string[] words = commandLine.Split('|');
            for (int i = 0; i < words.Length; i++)
            {
                switch (words[i])
                {
                    case "--config":
                        UseCFGButton.Checked = true;
                        ConfigBox.Text = words[++i];
                        break;
                    case "--ext":
                        OutputExtension = words[++i];
                        break;
                    case "--mzXML":
                    case "--mz5":
                    case "--mgf":
                    case "--text":
                    case "--ms2":
                    case "--cms2":
                        OutputFormatBox.Text = words[i].Substring(2);
                        break;
                    case "--filter":
                        var space = words[++i].IndexOf(' ');
                        FilterDGV.Rows.Add(new [] { words[i].Substring(0,space),
                            words[i].Substring(space+1) });
                        break;
                    case "--32":
                    case "--noindex":
                    case "--zlib":
                    case "--gzip":
                        break; // already handled these booleans above
                    case "":
                        break; // just that trailing "|"
                    default:
                        MessageBox.Show("skipping unknown config item \""+words[i]+"\"");
                        for (int j=i+1;j<words.Length;j++)  // skip any args
                        {
                            if (words[j].StartsWith("--"))
                            {
                                i = j-1;
                                break;
                            }
                            i++;
                        }
                        break;
                }
                if (OutputExtension != "")
                {
                    OutputExtensionBox.Text = OutputExtension;
                }
            }
        }


        private void SetDefaultsButton_Click(object sender, EventArgs e)
        {
            setCfgFromGUI();
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            var filesToProcess = new List<string>();
            var commandLine = ConstructCommandline();

            //Get files or filelist text
            if (FileListRadio.Checked)
            {
                if (FileListBox.Items.Count == 0)
                {
                    MessageBox.Show("No files to process");
                    return;
                }

                filesToProcess.AddRange(from string item in FileListBox.Items select item);
            }
            else if (String.IsNullOrEmpty(FileBox.Text) || !File.Exists(FileBox.Text))
            {
                MessageBox.Show("No files to process");
                return;
            }
            else
            {
                filesToProcess.Add(String.Format("--filelist|\"{0}\"", FileBox.Text));
            }

            string outputFolder = String.IsNullOrEmpty(OutputBox.Text) ? Application.StartupPath
                                                                       : OutputBox.Text;
            if (!Directory.Exists(outputFolder))
            {
                if (MessageBox.Show("The directory \"" + outputFolder + "\" does not exist. Do you want to create it?",
                                    "Create Directory?",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.No)
                    return;
                Directory.CreateDirectory(outputFolder);
            }


            var pf = new ProgressForm(filesToProcess, outputFolder, commandLine);
            pf.Text = "Conversion Progress";
            pf.ShowDialog();

        }
        private void setCfgFromGUI() // write a config file for current GUI state
        {
            string cfgFileName = MakeConfigfileName();
            if (File.Exists(cfgFileName) &&
                (MessageBox.Show("Config file \"" + cfgFileName + "\" already exists.  Do you want to replace it?",
                                    "MSConvertGUI",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.No))
            {
                return;
            }
            string cmdline = ConstructCommandline();
            File.WriteAllText(cfgFileName, cmdline);
        }
        private void SetGUIfromCfg(string cfgFileName) // populate buttons etc from config file
        {
            if (!File.Exists(cfgFileName))
            {
                MessageBox.Show("Can't find config file \"" + cfgFileName + "\"");
                return;
            }
            string cmdline = File.ReadAllText(cfgFileName);
            SetControlsFromCommandline(cmdline);
        }

    }
}
