using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;

namespace AutoQC
{
    public partial class AutoQc : Form
    {
        private const string AUTO_QC_RUNNING = "AutoQC is running";
        private const string AUTO_QC_WAITING = "AutoQC is waiting for background processes to finish";
        private const string AUTO_QC_STOPPED = "AutoQC is stopped";
        private const int ACCUM_TIME_WINDOW = 31;

        // TODO: We need to support other instrument vendors
        private const string THERMO_EXT = ".raw";


        // Collection of mass spec files to be processed.
        private readonly ConcurrentQueue<FileInfo> _dataFiles;
 
        private readonly FileSystemWatcher _fileWatcher;
        private readonly QcConfig config;

        // Background worker to run SkylineRunner
        private BackgroundWorker _worker;

        // Log
        private readonly StringBuilder _logBuffer = new StringBuilder();
        private const string LOG_FILE = "AutoQC.log";
        private string _logFile;

        private static readonly ILog LOG = LogManager.GetLogger(typeof(AutoQc).Name);

        public AutoQc()
        {
            InitializeComponent();
            config = new QcConfig();

            // Initialize from default settings.
            textSkylineRunnerPath.Text = config.SkylineRunnerPath;
            textSkylinePath.Text = config.SkylineFilePath;
            textFolderToWatchPath.Text = config.FolderToWatch;
            textAccumulationTimeWindow.Text = config.AccumulationWindow != 0
                ? config.AccumulationWindow.ToString()
                : ACCUM_TIME_WINDOW.ToString();

            comboBoxInstrumentType.SelectedItem = !string.IsNullOrWhiteSpace(config.InstrumentType)
                ? config.InstrumentType
                : "Thermo";

            cbPublishToPanorama.Checked = config.PublishToPanorama;
            groupBoxPanorama.Enabled = cbPublishToPanorama.Checked;
            textPanoramaUrl.Text = config.PanoramaServerUrl;
            textPanoramaEmail.Text = config.PanoramaUserName;
            textPanoramaPasswd.Text = config.PanoramaPassword;
            textPanoramaFolder.Text = config.PanoramaFolder;
            cbRunsprocop.Checked = config.RunSprocop;
            groupBoxSprocop.Enabled = cbRunsprocop.Checked;
            textRScriptPath.Text = config.RScriptPath;

            _dataFiles = new ConcurrentQueue<FileInfo>();

            _fileWatcher = new FileSystemWatcher {Filter = "*" + THERMO_EXT };
            _fileWatcher.Created += (s, e) => FileAdded(e, this);
        }

        private void btnSkylingRunnerPath_Click(object sender, EventArgs e)
        {
            OpenFile("Executable Files(*.exe)|*.exe|All Files (*.*)|*.*", textSkylineRunnerPath);
        }

        private void btnSkylineFilePath_Click(object sender, EventArgs e)
        {
            OpenFile("Skyline Files(*.sky)|*.sky|All Files (*.*)|*.*", textSkylinePath);
        }

        private void btnRScriptPath_Click(object sender, EventArgs e)
        {
            OpenFile("Executable Files(*.exe)|*.exe|All Files (*.*)|*.*", textRScriptPath);
        }

        private void btnFolderToWatch_Click(object sender, EventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "Directory where the instrument will write QC files."
            };
            if(dialog.ShowDialog(this) == DialogResult.OK)
            {
                textFolderToWatchPath.Text = dialog.SelectedPath;
            }
        }

        private void OpenFile(string filter, TextBox textbox)
        {
            var dialog = new OpenFileDialog {Filter = filter};
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textbox.Text = dialog.FileName;
            }
        }
      

        private void btnRunAutoQC_Click(object sender, EventArgs e)
        {
            Run();
        }

        private void btnStopAutoQC_Click(object sender, EventArgs e)
        {
            Stop();
        }

        private void Run()
        {
            Log("Starting AutoQC...", true, false);
            SetRunningControls();

            Log("Validating settings...", false, false);
            if (!ValidatePaths())
            {
                SetStoppedControls();
                return;
            }

            log4net.GlobalContext.Properties["WorkingDirectory"] = textFolderToWatchPath.Text;
            log4net.Config.XmlConfigurator.Configure();
//            var fileAppender =
//                LogManager.GetRepository().GetAppenders().First(/*appender => appender is RollingFileAppender*/);
            

            _logFile = Path.Combine(textFolderToWatchPath.Text, LOG_FILE);
            // Delete the log file if it exists. TODO: rollover log file?
            if (File.Exists(_logFile))
            {
                try
                {
                    File.Delete(_logFile);
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                }
            }

            Log("Watching folder " + config.FolderToWatch);
            Log("Mass spec. files will be imported to " + config.SkylineFilePath);

            if (!ValidateForm())
            {
                SetStoppedControls();
                FlushLogFile();
                return;
            }

            SaveConfig();

            // Queue up any existing data (e.g. *.raw) files in the folder
            var files =
                new DirectoryInfo(config.FolderToWatch).GetFiles("*" + THERMO_EXT).OrderBy(f => f.LastWriteTime).ToList();
            foreach (var file in files)
            {
                _dataFiles.Enqueue(file);
            }

            _fileWatcher.Path = config.FolderToWatch;
            _fileWatcher.EnableRaisingEvents = true; // Enables events on the _fileWatcher

            ProcessFiles();
        }

        private void Stop()
        {
            Log("Cancelling...");
            _fileWatcher.EnableRaisingEvents = false;
            _worker.CancelAsync();
            SetWaitingControls();
        }

        private void SetStoppedControls()
        {
            buttonRun.Enabled = true;
            buttonStop.Enabled = false;
            tabControl1.SelectTab(tabOutput);
            statusImg.Image = Properties.Resources.redstatus;
            labelStatusRunning.Text = AUTO_QC_STOPPED;
            tabControl1.Update();
        }

        private void SetRunningControls()
        {
            buttonRun.Enabled = false;
            buttonStop.Enabled = true;
            tabControl1.SelectTab(tabOutput);
            statusImg.Image = Properties.Resources.greenstatus;
            labelStatusRunning.Text = AUTO_QC_RUNNING;
            tabControl1.Update();
        }

        private void SetWaitingControls()
        {
            buttonRun.Enabled = false;
            buttonStop.Enabled = false;
            tabControl1.SelectTab(tabOutput);
            statusImg.Image = Properties.Resources.orangestatus;
            labelStatusRunning.Text = AUTO_QC_WAITING;
            tabControl1.Update();
        }

        private bool ValidateForm()
        {
            return ValidateSprocopSettings() && ValidatePanoramaSettings();
        }

        private bool ValidatePaths()
        {
            bool error = false;
            if (string.IsNullOrWhiteSpace(config.SkylineRunnerPath))
            {
                LogError("Please specify path to SkylineRunner.exe.");
                error = true;
            }
            else if (!File.Exists(config.SkylineRunnerPath))
            {
                LogError(string.Format("{0} does not exist.", config.SkylineRunnerPath));
                error = true;
            }
            if (string.IsNullOrWhiteSpace(config.SkylineFilePath))
            {
                LogError("Please specify path to a Skyline file.");
                error = true;   
            }
            else if (!File.Exists(config.SkylineFilePath))
            {
                LogError(string.Format("{0} does not exist.", config.SkylineFilePath));
                error = true;
            }
            if (string.IsNullOrWhiteSpace(config.FolderToWatch))
            {
                LogError("Please specify path to a folder where mass spec files will be written.");
                error = true;
            }
            else if (!Directory.Exists(config.FolderToWatch))
            {
                LogError(string.Format("Directory {0} does not exist.", config.SkylineFilePath));
                error = true;
            }
            
            return !error;
        }

        private bool ValidateSprocopSettings()
        {
            if (!cbRunsprocop.Checked)
            {
                Log("Will NOT run SProCoP.");
                return true;
            }
            if (string.IsNullOrWhiteSpace(textRScriptPath.Text))
            {
                LogError("Please specify path to Rscript.exe.");
                return false;
            }
            return true;
        }

        private bool ValidatePanoramaSettings()
        {
            if (!cbPublishToPanorama.Checked)
            {
                Log("Will NOT publish Skyline document to Panorama.");
                return true;
            }

            Log("Validating Panorama settings...");
            var panoramaUrl = textPanoramaUrl.Text;
            Uri serverUri;
            try
            {
                serverUri = new Uri(PanoramaUtil.ServerNameToUrl(panoramaUrl));
            }
            catch (UriFormatException)
            {
                LogError("Panorama server name is invalid.");
                return false;
            }

            var panoramaEmail = textPanoramaEmail.Text;
            var panoramaPasswd = textPanoramaPasswd.Text;
            var panoramaFolder = textPanoramaFolder.Text;
            if (string.IsNullOrWhiteSpace(panoramaEmail))
            {
                LogError("Please specify a Panorama user name.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(panoramaPasswd))
            {
                LogError("Please specify a Panorama user password.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(panoramaFolder))
            {
                LogError("Please specify a folder on the Panorama server.");
                return false;   
            }
            
            var panoramaClient = new WebPanoramaClient(serverUri);
            try
            {
                PanoramaUtil.VerifyServerInformation(panoramaClient, serverUri, panoramaEmail, panoramaPasswd);
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
                return false;
            }

            try
            {
                PanoramaUtil.VerifyFolder(panoramaClient, new Server(serverUri, panoramaEmail, panoramaPasswd), textPanoramaFolder.Text);
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
                return false;
            }

            return true;
        }

        public void Log(string line, bool clear = false, bool logToFile = true)
        {
            RunUI(() =>
            {
                if (clear)
                {
                    textOutput.Text = string.Empty;
                }
                textOutput.AppendText(line + Environment.NewLine);
                textOutput.SelectionStart = textOutput.TextLength;
                textOutput.ScrollToCaret();
                textOutput.Update();
                
            });
            if (logToFile)
            {
                LogToFile(line);
            }
        }

        public void LogError(string line, bool logToFile = true)
        {
            line = "ERROR: " + line;
            RunUI(() =>
            {
                textOutput.SelectionStart = textOutput.TextLength;
                textOutput.SelectionLength = 0;
                textOutput.SelectionColor = Color.Red;
                textOutput.AppendText(line + Environment.NewLine);
                textOutput.SelectionColor = textOutput.ForeColor;
                textOutput.ScrollToCaret();
                textOutput.Update();
            });
            if (logToFile)
            {
                LogToFile(line);
            }
        }

        private void LogToFile(string text)
        {
            LOG.Info(text);
            // Write to log file
//            lock (_logBuffer)
//            {
//                _logBuffer.Append(DateTime.Now).Append(" -- ").Append(text).Append(Environment.NewLine);
//                if (_logBuffer.Length > 1000)
//                {
//                    File.AppendAllText(_logFile, _logBuffer.ToString());
//                    _logBuffer.Clear();
//                }
//            }
        }

        private void FlushLogFile()
        {
//            lock (_logBuffer)
//            {
//                if (_logBuffer.Length > 0)
//                {
//                    File.AppendAllText(_logFile, _logBuffer.ToString());
//                    _logBuffer.Clear();
//                }
//            }  
        }

        private void SaveConfig()
        {
            config.FolderToWatch = textFolderToWatchPath.Text;
            config.SkylineRunnerPath = textSkylineRunnerPath.Text;
            config.SkylineFilePath = textSkylinePath.Text;
            config.InstrumentType = comboBoxInstrumentType.SelectedText;

            config.PublishToPanorama = cbPublishToPanorama.Checked;
            config.PanoramaServerUrl = textPanoramaUrl.Text;
            config.PanoramaUserName = textPanoramaEmail.Text;
            config.PanoramaPassword = textPanoramaPasswd.Text;
            config.PanoramaFolder = textPanoramaFolder.Text;

            config.RunSprocop = cbRunsprocop.Checked;
            config.RScriptPath = textRScriptPath.Text;
            
            config.SaveConfig();
        }

        private void ProcessFiles()
        {
            Log("Running....");
            if (_worker != null && _worker.IsBusy)
            {
                // TODO: Make sure this does not happen!
                LogError("Working is running!!!");
                return;
            }
            _worker = new BackgroundWorker {WorkerSupportsCancellation = true, WorkerReportsProgress = true};
            _worker.DoWork += BackgroundWorker_DoWork;
            _worker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            _worker.RunWorkerAsync();
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetStoppedControls();
            Log("Stopping AutoQC.\n\n\n");
            FlushLogFile();
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            if (worker == null)
            {
                return;
            }

            while (true)
            {
                if (worker.CancellationPending)
                {
                    e.Cancel = true;
                    break;
                }

                if (!_dataFiles.IsEmpty)
                {
                    FileInfo fileInfo;
                    _dataFiles.TryDequeue(out fileInfo);
                    if (fileInfo != null)
                    {
                        ProcessFile(fileInfo);    
                    }
                }
                else
                {
                    Log("Waiting for files...");
                    System.Threading.Thread.Sleep(10000);
                }
            }
        }


        void FileAdded(FileSystemEventArgs e, AutoQc autoQc)
        {
            BeginInvoke(new Action(() =>
            {
                autoQc.Log(e.Name + " added to directory.");
                _dataFiles.Enqueue(new FileInfo(e.FullPath));
            }));
        }

        void ProcessFile(FileInfo file)
        {
            if (IsFileReady(file))
            {
                ProcessOneFile(file.FullName);    
            }
        }

        static bool IsFileReady(FileInfo file)
        {
            // TODO: fix this
            long fileSize = 0;
            while (true)
            {
                if (fileSize == 0 || file.Length > fileSize)
                {
                    fileSize = file.Length;
                }
                else
                {
                    // File size hasn't changed since the last time we checked.
                    // Assume file has been fully written.
                    return true;
                }
                // Wait for 30 seconds.
                System.Threading.Thread.Sleep(3000);
            }
        }

        private void ProcessOneFile(string filePath)
        {
            var args = @"--in=""" + config.SkylineFilePath + @""" --import-file=""" + filePath + @""" --save";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = config.SkylineRunnerPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            process.StartInfo.WorkingDirectory = config.FolderToWatch;
            process.OutputDataReceived += WriteToLog;
            process.ErrorDataReceived += WriteToLog;
            process.Exited += ProcessExit;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }

        private void ProcessFile(string path, int index)
        {
            // Log(path + " being processed as " + type);
            string args = string.Empty;
            string exportReportPath = Path.GetDirectoryName(config.ReportFilePath)+"\\"+"report.csv";
            string saveQcPath = Path.GetDirectoryName(path) + "\\QC.pdf";
            var type = "QC"; // TODO fix this
            switch(type)
            {
                case "None":
                    break;
                case "Threshold":
                    args = @"--in=""" + textSkylinePath + @""" --import-file=""" + path + @""" --save";
                    break;
                case "QC":
                    args = String.Format(@"--in=""" + textSkylinePath + @""" --import-file=""" + path + @""" --save --report-conflict-resolution=overwrite --report-add=""{0}"" --report-name=""{1}"" --report-file=""{2}""", config.ReportFilePath, "SProCoP Input", exportReportPath);
                    break;
            }

            if (!type.Equals("None"))
            {
                var process = new Process();
                var startInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = config.SkylineRunnerPath,
                    Arguments = args,
                    UseShellExecute = false
                };
                process.StartInfo = startInfo;
                process.Start();
                while (!process.HasExited)
                    if (type == "QC")
                    {
                        process.WaitForExit();
//                        var process2 = new Process();
//                        var startInfo2 = new ProcessStartInfo
//                        {
//                            FileName = config.RScriptPath,
//                            Arguments = String.Format(@"""{0}"" ""{1}"" {2} {3} 1 {4} ""{5}""", config.SProCoPrScript, exportReportPath, numericUpDownThreshold.Value, checkBoxIsHighRes.Checked ? 1:0, numericUpDownMMA.Value,saveQcPath),
//                            UseShellExecute = false
//                        };
//                        process2.StartInfo = startInfo2;
//                        process2.Start();
                    }
            }
        }

        /// <summary>
        /// Handle a line of output/error data from the process.
        /// </summary>
        private void WriteToLog(object sender, DataReceivedEventArgs e)
        {
            Log(e.Data);
        }

        private void RunUI(Action action)
        {
            try
            {
                Invoke(action);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// Handle process exit (success, error, or interrupt).
        /// </summary>
        void ProcessExit(object sender, EventArgs e)
        {
               Log("Process exited.");
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void textOutput_TextChanged(object sender, EventArgs e)
        {

        }

        private void cbRunsprocop_CheckedChanged(object sender, EventArgs e)
        {
            groupBoxSprocop.Enabled = cbRunsprocop.Checked;
        }

        private void cbPublishToPanorama_CheckedChanged(object sender, EventArgs e)
        {
            groupBoxPanorama.Enabled = cbPublishToPanorama.Checked;
        }
    }

    public class QcConfig
    {
        #region [Settings]
        public string FolderToWatch
        {
            get { return Properties.Settings.Default.FolderToWatch; }
            set { Properties.Settings.Default.FolderToWatch = value; }
        }

        public bool ImportExistingFiles
        {
            get { return Properties.Settings.Default.ImportExistingFiles; }
            set { Properties.Settings.Default.ImportExistingFiles = value; }   
        }

        public int AccumulationWindow
        {
            get { return Properties.Settings.Default.AccumulationWindow; }
            set { Properties.Settings.Default.AccumulationWindow = value; }          
        }

        public string SkylineRunnerPath
        {
            get { return Properties.Settings.Default.SkylineRunnerPath; }
            set { Properties.Settings.Default.SkylineRunnerPath = value; }
        }

        public string SkylineFilePath
        {
            get { return Properties.Settings.Default.SkylineFilePath; }
            set { Properties.Settings.Default.SkylineFilePath = value; }
        }

        public string InstrumentType
        {
            get { return Properties.Settings.Default.InstrumentType; }
            set { Properties.Settings.Default.InstrumentType = value; }
        }
        #endregion

        #region [Panorama settings]
        public bool PublishToPanorama
        {
            get { return Properties.Settings.Default.PublishToPanorama; }
            set { Properties.Settings.Default.PublishToPanorama = value; }
        }

        public string PanoramaServerUrl
        {
            get { return Properties.Settings.Default.PanoramaUrl; }
            set { Properties.Settings.Default.PanoramaUrl = value; }
        }

        public string PanoramaUserName
        {
            get { return Properties.Settings.Default.PanoramaUserEmail; }
            set { Properties.Settings.Default.PanoramaUserEmail = value; }
        }

        public string PanoramaPassword
        {
            get { return Properties.Settings.Default.PanoramaPassword; }
            set { Properties.Settings.Default.PanoramaPassword = value; }
        }

        public string PanoramaFolder
        {
            get { return Properties.Settings.Default.PanoramaFolder; }
            set { Properties.Settings.Default.PanoramaFolder = value; }
        }
        #endregion

        #region [SProCop settings]
        public bool RunSprocop
        {
            get { return Properties.Settings.Default.RunSprocop; }
            set { Properties.Settings.Default.RunSprocop = value; }
        }

        public string RScriptPath
        {
            get { return Properties.Settings.Default.RScriptPath; }
            set { Properties.Settings.Default.RScriptPath = value; }
        }
        public String ReportFilePath
        {
            get { return Path.Combine(Directory.GetCurrentDirectory(), "SProCoP_report.skyr"); }
        }

        public String SProCoPrScript
        {
            get { return Path.Combine(Directory.GetCurrentDirectory(), "QCplotsRgui2.R"); }
        }
        #endregion

        public void SaveConfig()
        {
            Properties.Settings.Default.Save();
        }
    }
}
