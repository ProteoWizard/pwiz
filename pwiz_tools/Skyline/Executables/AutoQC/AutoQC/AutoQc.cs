using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace AutoQC
{
    public partial class AutoQc : Form
    {
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private List<String> _rawFiles = new List<String>();
        private FileSystemWatcher fileWatcher = new FileSystemWatcher {Filter = "*.raw" };

        public AutoQc()
        {
            InitializeComponent();
            textBoxRScriptPath.Text = Properties.Settings.Default.RScriptPath;
            skylineRunnerPathInput.Text = Properties.Settings.Default.SkylineRunnerPath;
            skylineFilePath.Text = Properties.Settings.Default.SkylineFilePath;
            folderToWatchPath.Text = Properties.Settings.Default.FolderToWatch;
            fileWatcher.Created += (s, e) => FileAdded(e, this);

            comboBoxType.SelectedIndex = 0;
        }

        private void btnSkylingRunnerPath_Click(object sender, EventArgs e)
        {
            OpenFile("Executable Files(*.exe)|*.exe|All Files (*.*)|*.*", skylineRunnerPathInput);
        }

        private void btnSkylineFilePath_Click(object sender, EventArgs e)
        {
            OpenFile("Skyline Files(*.sky)|*.sky|All Files (*.*)|*.*", skylineFilePath);
        }

        private void btnRScriptPath_Click(object sender, EventArgs e)
        {
            OpenFile("Executable Files(*.exe)|*.exe|All Files (*.*)|*.*", textBoxRScriptPath);
        }

        private void btnFolderToWatch_Click(object sender, EventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "Select the directory that Xcalibur will export the raw files to."
            };
            if(dialog.ShowDialog(this) == DialogResult.OK)
            {
                folderToWatchPath.Text = dialog.SelectedPath;
            }
        }

        private void OpenFile(string filter, TextBox textbox)
        {
            var dialog = new OpenFileDialog {Filter = filter, Title = "Please select a file to open."};
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textbox.Text = dialog.FileName;
            }
        }

        // Returns the current configuration
        public QcConfig GetCurrentConfig()
        {
            var config = new QcConfig(folderToWatchPath.Text,
                skylineRunnerPathInput.Text,
                skylineFilePath.Text,
                textBoxRScriptPath.Text);
            return config;
        }

        // Adds n runs to runGridView of selected type
        private void buttonAddRuns_Click(object sender, EventArgs e)
        {
            try
            {
                int currentIndex = runGridView.Rows.Count - 2;
                int rowCount = int.Parse(textBoxNewRows.Text);
                runGridView.Rows.Add(rowCount);
                int finalIndex = runGridView.Rows.Count - 1;
                foreach (DataGridViewRow row in runGridView.Rows)
                {
                    if (row.Index > currentIndex && row.Index < finalIndex)
                    {
                        row.Cells[0].Value = comboBoxType.SelectedItem;
                    }
                }
                RenumberRows();
            }
            catch (FormatException ex)
            {
                Log("You must specify a valid integer value to add.");
            }
           
        }

        private void runGridView_UserAddedRow(object sender, DataGridViewRowEventArgs e)
        {
            RenumberRows();
        }

        private void runGridView_UserDeletedRow(object sender, DataGridViewRowEventArgs e)
        {
            RenumberRows();
        }

        private void RenumberRows()
        {
            for (int i = 0; i < runGridView.Rows.Count; i++)
            {
                DataGridViewRowHeaderCell cell = runGridView.Rows[i].HeaderCell;
                cell.Value = (i + 1).ToString(CultureInfo.InvariantCulture);
                runGridView.Rows[i].HeaderCell = cell;
                runGridView.AutoResizeRowHeadersWidth(i, DataGridViewRowHeadersWidthSizeMode.AutoSizeToFirstHeader);
                runGridView.AutoResizeRowHeadersWidth(i, DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders);
            }
        }

        // SProCoP Automation
        private void btnRunSprocopAuto_Click(object sender, EventArgs e)
        {
            if (ValidateForm())
            {
                if (IsRunning())
                {
                    StopSProCoPqc(true);
                }
                else
                {
                    btnRunSprocopAuto.Text = "Stop SProCoP AutoQC";
                    statusImg.Image = Properties.Resources.greenstatus;
                    labelStatus.Text = "Running";
                    RunSProCoPqc();
                } 
            }
        }

        private bool ValidateForm()
        {
            int thresholdRows = 0;
            foreach (DataGridViewRow row in runGridView.Rows)
            {
                if (null != row.Cells[0].Value)
                {
                    if (row.Cells[0].Value.ToString().Equals("Threshold"))
                        thresholdRows++;
                }
            }
            // Checks to make sure values are valid before program continues with run.
            if (thresholdRows != numericUpDownThreshold.Value)
            {
                Log("There must be the same number of thresholds in your QC sequence as defined above in the configurations tab.");
                return false;
            }
            // Must have at least 3 runs
            if (runGridView.Rows.Count - 1 < 3)
            {
                Log("Your SProCoP Sequence must contain more than 3 runs, refer to the configuration tab.");
                return false;
            }
            // Checks if program has been run before and if so user needs to select a row in the grid view to continue running on
            if (_rawFiles.Count > 0 && runGridView.SelectedRows.Count != 1 && !IsRunning())
            {
                Log("Please select a row in the grid which you would like to start the run on.");
                return false;
            }

            // Validate Panorama options
            if (!ValidatePanoramaSettings())
            {
                return false;
            }
            return true;
        }

        private bool ValidatePanoramaSettings()
        {
            var panoramaUrl = textPanoramaUrl.Text;
            Uri serverUri;
            try
            {
                serverUri = new Uri(PanoramaUtils.ServerNameToUrl(panoramaUrl));
            }
            catch (UriFormatException)
            {
                Log("Panorama server name is invalid.");
                return false;
            }

            var panoramaEmail = textPanoramaEmail.Text;
            var panoramaPasswd = textPanoramaPasswd.Text;

            var panoramaClient = new WebPanoramaClient(serverUri);
            try
            {
                PanoramaUtils.VerifyServerInformation(panoramaClient, serverUri, panoramaEmail, panoramaPasswd);
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                return false;
            }

//            try
//            {
//                PanoramaUtils.verifyFolder(new Server(serverUri, panoramaEmail, panoramaPasswd), textPanoramaFolder.Text);
//            }
//            catch (Exception ex)
//            {
//                Log(ex.Message);
//                throw;
//            }

            return true;
        }

        public void Log(string line, bool clear = false)
        {
            if (clear)
                textBoxLog.Text = string.Empty;
            textBoxLog.Text +=line +  "\r\n";
            textBoxLog.SelectionStart = textBoxLog.TextLength;
            textBoxLog.ScrollToCaret();
        }

        private void RunSProCoPqc()
        {
            if (_rawFiles.Count > 0 && runGridView.SelectedRows.Count == 1)
            {
                int selectedRowIndex  = runGridView.SelectedRows[0].Index;
                int totalRawFiles = _rawFiles.Count;
                for (var i = totalRawFiles - 1; i >= selectedRowIndex; i--)
                {
                        _rawFiles.RemoveAt(i);
                        runGridView.Rows[i].Cells[1].Value = string.Empty;
                }
            }
            var config = GetCurrentConfig();
            buttonClear.Enabled = false;
            Log("SProCoP QC is watching " + config.FolderToWatch);

            fileWatcher.Path = config.FolderToWatch;
            fileWatcher.EnableRaisingEvents = true; // Enables events on the fileWatcher;

            Log("Ready for Xcalibur sequence to be started.");

        }

        private bool IsRunning()
        {
            return labelStatus.Text == "Running";
        }

        private void StopSProCoPqc(bool isForcedStop)
        {
            btnRunSprocopAuto.Text = "Run SProCoP AutoQC";
            statusImg.Image = Properties.Resources.redstatus;
            labelStatus.Text = "Off";
            fileWatcher.EnableRaisingEvents = false;
            buttonClear.Enabled = true;

            if(isForcedStop)
            {
                Log("SProCoPQC was stopped in the middle of a sequence and did not finish processing all files.");
                Log("If you would like to start a new run from the beginning clear your run data.");
            }
            else
            {
                Log("AutoQC has finished running successfully.");
                Log("If you would like to start a new run from the beginning clear your run data.");
            }
            
        }

        void FileAdded(FileSystemEventArgs e,AutoQc emitterChanger)
        {
            BeginInvoke(new Action(() =>
            {
                emitterChanger.Log(e.Name + " created.");
                _rawFiles.Add(e.FullPath);

                var currentIndex = _rawFiles.Count;
                if (null != runGridView.Rows[currentIndex - 1].Cells[0].Value)
                {
                    runGridView.Rows[currentIndex - 1].ReadOnly = true; // So that user can't modify datagrid cells that have already been processed (while program is running).
                    runGridView.Rows[currentIndex - 1].Cells[1].Value = Path.GetFileName(e.FullPath);
                }
                if (currentIndex == 1)
                {
                    return;
                }
                var fileToProcessIndex = currentIndex - 2; // Index of array is -1 and we want to process previous file so minus another 1.
                ProcessFile(_rawFiles[fileToProcessIndex], fileToProcessIndex);
                // If is last raw file will process previous file then execute function LastFileAdded.
                if (_rawFiles.Count == runGridView.RowCount - 1) // Checks if is the last file to be processed
                {
                    var fileInfo = new FileInfo(e.FullPath);
                    LastFileAdded(fileInfo);
                }
            }));
        }

        // pre: last file to process
        // waits for a minute with no change in the size of the file before processing
        void LastFileAdded(FileInfo file)
        {
            long fileSize = 0;
            while (true)
            {
                if (fileSize == 0 || file.Length > fileSize)
                    fileSize = file.Length;
                else
                {
                    ProcessFile(_rawFiles[_rawFiles.Count - 1], _rawFiles.Count - 1);
                    StopSProCoPqc(false);
                    break;
                }
                System.Threading.Thread.Sleep(60000);
            }
        }

        private void ProcessFile(string path, int index)
        {
            var conf = GetCurrentConfig();
            var type = runGridView.Rows[index].Cells[0].Value.ToString();
            Log(path + " being processed as " + type);
            string args = string.Empty;
            string exportReportPath = Path.GetDirectoryName(conf.ReportFilePath)+"\\"+"report.csv";
            string saveQcPath = Path.GetDirectoryName(path) + "\\QC.pdf";
            switch(type)
            {
                case "None":
                    break;
                case "Threshold":
                    args = @"--in=""" + conf.SkylineFilePath + @""" --import-file=""" + path + @""" --save";
                    break;
                case "QC":
                    args = String.Format(@"--in=""" + conf.SkylineFilePath + @""" --import-file=""" + path + @""" --save --report-conflict-resolution=overwrite --report-add=""{0}"" --report-name=""{1}"" --report-file=""{2}""", conf.ReportFilePath, "SProCoP Input", exportReportPath);
                    break;
            }

            if (!type.Equals("None"))
            {
                var process = new Process();
                var startInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = conf.SkylineRunnerPath,
                    Arguments = args,
                    UseShellExecute = false
                };
                process.StartInfo = startInfo;
                process.Start();
                while (!process.HasExited)
                    if (type == "QC")
                    {
                        process.WaitForExit();
                        var process2 = new Process();
                        var startInfo2 = new ProcessStartInfo
                        {
                            FileName = conf.RScriptPath,
                            Arguments = String.Format(@"""{0}"" ""{1}"" {2} {3} 1 {4} ""{5}""", conf.SProCoPrScript, exportReportPath, numericUpDownThreshold.Value, checkBoxIsHighRes.Checked ? 1:0, numericUpDownMMA.Value,saveQcPath),
                            UseShellExecute = false
                        };
                        process2.StartInfo = startInfo2;
                        process2.Start();
                    }
            }
        }

        private void buttonClear_Click(object sender, EventArgs e)
        {
            if (IsRunning())
            {
                Log("Can't clear session data while AutoQC is running.");
            }
            else
            {
                _rawFiles = new List<string>();
                foreach (DataGridViewRow row in runGridView.Rows)
                {
                    row.ReadOnly = false;
                }
                Log("All data in this session ahs been cleared, you may begin a new run at any time.", true);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            var config = GetCurrentConfig();
            Properties.Settings.Default.RScriptPath = config.RScriptPath;
            Properties.Settings.Default.SkylineRunnerPath = config.SkylineRunnerPath;
            Properties.Settings.Default.SkylineFilePath = config.SkylineFilePath;
            Properties.Settings.Default.ReportFilePath = config.ReportFilePath;
            Properties.Settings.Default.FolderToWatch = config.FolderToWatch;
            Properties.Settings.Default.SkylineRunnerPath = config.SkylineRunnerPath;
            Properties.Settings.Default.SProCoPRScript = config.SProCoPrScript;
            Properties.Settings.Default.Save();
        }
    }

    public class QcConfig
    {
        public String FolderToWatch;
        public String SkylineRunnerPath;
        public String SkylineFilePath;
        public String ReportFilePath = Path.Combine(Directory.GetCurrentDirectory(), "SProCoP_report.skyr");
        public String RScriptPath;
        public String SProCoPrScript = Path.Combine(Directory.GetCurrentDirectory(), "QCplotsRgui2.R");
        
        public QcConfig(string folderToWatch, string skylineRunnerPath, string skylineFilePath,
            string rScriptPath)
        {
            FolderToWatch = folderToWatch;
            SkylineRunnerPath = skylineRunnerPath;
            SkylineFilePath = skylineFilePath;
            RScriptPath = rScriptPath;
        }
    }
}
