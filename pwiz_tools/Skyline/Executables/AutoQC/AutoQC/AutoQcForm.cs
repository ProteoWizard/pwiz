using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AutoQC.Properties;
using log4net;
using log4net.Config;
using MSFileReaderLib;

namespace AutoQC
{
    public partial class AutoQCForm : Form
    {
        private const string AUTO_QC_RUNNING = "AutoQC is running";
        private const string AUTO_QC_WAITING = "AutoQC is waiting for background processes to finish";
        private const string AUTO_QC_STOPPED = "AutoQC is stopped";
        private const string VALIDATING_SETTINGS = "Validating settings...";
        private const string RUN_AUTOQC = "Run AutoQC";
        private const string STOP_AUTOQC = "Stop AutoQC";

        
        private const string SKYLINE_RUNNER = "SkylineRunner.exe";

        // TODO: We need to support other instrument vendors
        private const string THERMO_EXT = ".raw";

        // Path to SkylineRunner.exe
        private readonly string _skylineRunnerPath;

        // Collection of new mass spec files to be processed.
        private readonly ConcurrentQueue<FileInfo> _dataFiles;
 
        private readonly FileSystemWatcher _fileWatcher;

        // Background worker to run SkylineRunner
        private BackgroundWorker _worker;

        public const int MAX_TRY_COUNT = 2;
        private Boolean _tryAgain;

        private readonly MainSettings _mainSettings;
        private readonly List<TabSettings> _settingsTabs; 

        private static readonly ILog LOG = LogManager.GetLogger(typeof(AutoQCForm).Name);

        public AutoQCForm()
        {
            InitializeComponent();

            // Remove the SProCoP settings tab for now.  No way to hide the tab in the designer
            tabControl.TabPages.Remove(tabSprocopSettings);

            // Expect SkylineRunner to be in the same directory as AutoQC
            _skylineRunnerPath = Path.Combine(Directory.GetCurrentDirectory(), SKYLINE_RUNNER);

            TabSettings.MainForm = this;
            _mainSettings = new MainSettings();
            _settingsTabs = new List<TabSettings> {new PanoramaSettings()};

            // Initialize the tabs from default settings.
            _mainSettings.InitializeFromDefaultSettings();
            foreach(var settings in _settingsTabs)
            {
                settings.InitializeFromDefaultSettings();    
            }
            
            _dataFiles = new ConcurrentQueue<FileInfo>();

            _fileWatcher = new FileSystemWatcher {Filter = "*" + THERMO_EXT };
            _fileWatcher.Created += (s, e) => FileAdded(e, this);
        }

        private void OpenFile(string filter, TextBox textbox)
        {
            var dialog = new OpenFileDialog {Filter = filter};
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textbox.Text = dialog.FileName;
            }
        }
      
        private async void Run()
        {
            textOutput.Text = string.Empty;
            LogOutput("Starting AutoQC...");

            SetValidatingControls();

            if (!File.Exists(_skylineRunnerPath))
            {
                LogError(string.Format("Could not find {0} at this path {1}. {0} should be in the same directory as AutoQC.", SKYLINE_RUNNER, _skylineRunnerPath));
                SetStoppedControls();
                return;
            }

            bool validSettings = false;
            await Task.Run(() => validSettings = _mainSettings.ValidateSettings());

            if (!validSettings)
            {
                SetStoppedControls();
                return;
            }
            
            GlobalContext.Properties["WorkingDirectory"] = textFolderToWatchPath.Text;
            XmlConfigurator.Configure();
//            var fileAppender =
//                LogManager.GetRepository().GetAppenders().First(/*appender => appender is RollingFileAppender*/);
            

            Log("Watching folder " + textFolderToWatchPath.Text);
            Log("Mass spec. files will be imported to " + textSkylinePath.Text);

            await Task.Run(() => validSettings = ValidateForm());
            if (!validSettings)
            {
                SetStoppedControls();
                return;
            }

            SaveSettings();

            SetRunningControls();

            // Import existing data files in the directory, if required
            if (_mainSettings.ImportExistingFiles)
            {
                ProcessExistingFiles();
            }
            else
            {
                ProcessFiles();
            }

            _fileWatcher.Path = _mainSettings.FolderToWatch;
            _fileWatcher.EnableRaisingEvents = true; // Enables events on the _fileWatcher
        }

        private void Stop()
        {
            Log("Cancelling...");
            _fileWatcher.EnableRaisingEvents = false;
            _worker.CancelAsync();
            SetWaitingControls();
        }
        
        private void SetValidatingControls()
        {
            btnStartStop.Enabled = false;
            tabControl.SelectTab(tabOutput);
            statusImg.Image = Resources.orangestatus;
            labelStatusRunning.Text = VALIDATING_SETTINGS;
            tabControl.Update();
        }

        private void SetRunningControls()
        {
            btnStartStop.Text = STOP_AUTOQC;
            btnStartStop.Enabled = true;
            tabControl.SelectTab(tabOutput);
            statusImg.Image = Resources.greenstatus;
            labelStatusRunning.Text = AUTO_QC_RUNNING;
            tabControl.Update();
        }

        private void SetWaitingControls()
        {
            btnStartStop.Enabled = false;
            tabControl.SelectTab(tabOutput);
            statusImg.Image = Resources.orangestatus;
            labelStatusRunning.Text = AUTO_QC_WAITING;
            tabControl.Update();
        }

        private void SetStoppedControls()
        {
            btnStartStop.Text = RUN_AUTOQC;
            btnStartStop.Enabled = true;
            tabControl.SelectTab(tabOutput);
            statusImg.Image = Resources.redstatus;
            labelStatusRunning.Text = AUTO_QC_STOPPED;
            tabControl.Update();
        }

        private bool ValidateForm()
        {
            var validated = true;
            foreach (var settingsTab in _settingsTabs.Where(settingsTab => !settingsTab.ValidateSettings()))
            {
                validated = false;
            }
            return validated;
        }

        // Log to the Output tab only
        public void LogOutput(string line)
        {
            Log(line, false);
        }

        // Log error to the output tab only
        public void LogErrorOutput(string line)
        {
            LogError(line, false);
        }

        public void Log(string line, bool logToFile = true)
        {
            RunUI(() =>
            {
                textOutput.AppendText(line + Environment.NewLine);
                textOutput.SelectionStart = textOutput.TextLength;
                textOutput.ScrollToCaret();
                textOutput.Update();
                
            });
            if (logToFile)
            {
                LOG.Info(line);
            }
        }

        public void LogError(Exception ex, bool logToFile = true)
        {
            LogError(ex.Message, false);
            LOG.Error(ex.Message, ex);
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
                LOG.Error(line);
            }
        }

        private void SaveSettings()
        {
            _mainSettings.SaveSettings();
            _settingsTabs.ForEach(settingsTab => settingsTab.SaveSettings());

            Settings.Default.Save();
        }

        private void ProcessExistingFiles()
        {
            Log("Processing existing files...");
            
            if (_worker != null && _worker.IsBusy)
            {
                // TODO: Make sure this does not happen?
                LogError("Working is running!!!");
                return;
            }
            _worker = new BackgroundWorker { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
            _worker.DoWork += BackgroundWorker_DoWorkProcessExisting;
            _worker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompletedProcessExisting;
            _worker.RunWorkerAsync(); 
        }

        private void ProcessFiles()
        {
            Log("Processing new files...");
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
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;
            if (worker == null)
            {
                return;
            }

            var inWait = false;
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
                        ImportContext importContext = new ImportContext(fileInfo);
                        ProcessFile(importContext);    
                    }
                    inWait = false;
                }
                else
                {
                    if (!inWait)
                    {
                        Log("\n\nWaiting for files...\n\n");
                    }

                    inWait = true;
                    Thread.Sleep(10000);
                }
            }
        }

        private void BackgroundWorker_RunWorkerCompletedProcessExisting(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!e.Cancelled)
            {
                Log("Finished processing existing files.\n\n");
                ProcessFiles(); // Start processing new files.
            }
            else
            {
                Log("Cancelled processing files.");
                SetStoppedControls();
                Log("Stopping AutoQC.\n\n\n");
            }
        }

        private void BackgroundWorker_DoWorkProcessExisting(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;
            if (worker == null)
            {
                return;
            }

            if (worker.CancellationPending)
            {
                e.Cancel = true;
                return;
            }

            // Queue up any existing data (e.g. *.raw) files in the folder
            var files =
                new DirectoryInfo(_mainSettings.FolderToWatch).GetFiles("*" + THERMO_EXT).OrderBy(f => f.LastWriteTime).ToList();

            Log(string.Format("Found {0} files.", files.Count));

            var importContext = new ImportContext(files);
            while (importContext.GetNextFile() != null)
            {
                ProcessFile(importContext);
                if (worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
            }  
        }


        void FileAdded(FileSystemEventArgs e, AutoQCForm autoQcForm)
        {
            BeginInvoke(new Action(() =>
            {
                autoQcForm.Log(string.Format("File {0} added to directory.", e.Name));
                _dataFiles.Enqueue(new FileInfo(e.FullPath));
            }));
        }

        void ProcessFile(ImportContext importContext)
        {
            FileInfo file = importContext.getCurrentFile();
            var counter = 0;
            while (true)
            {
                if (_worker != null && _worker.CancellationPending)
                {
                    return;
                }

                if (IsFileReady(file))
                {
                    break;
                }
                if (counter%10 == 0)
                {
                    Log("File is being acquired. Waiting...");
                }
                counter++;
                // Wait for 60 seconds.
                Thread.Sleep(60000);
            }
            
            Log("File is ready");
            ProcessOneFile(importContext);    
        }

        static bool IsFileReady(FileInfo file)
        {
            // Log(string.Format("File size is {0}", file.Length));

            IXRawfile rawFile = null;
            try
            {
                rawFile = new MSFileReader_XRawfileClass();
                rawFile.Open(file.FullName);
                var inAcq = 1;
                rawFile.InAcquisition(ref inAcq);
                if (inAcq == 1)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                LOG.Error(string.Format("Error getting acquisition state of file {0}", file.FullName), ex);
                throw;
            }
            finally
            {
                if (rawFile != null)
                {
                    rawFile.Close();
                    
                }
            }
            return true;
        }

        private void ProcessOneFile(ImportContext importContext)
        {
            var args = GetArgs(importContext);

            var argsToPrint = GetArgs(importContext, true);
            Log("Running SkylineRunner with args: ");
            Log(argsToPrint);

            while (true)
            {
                importContext.incrementTryCount();
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden, 
                        FileName = _skylineRunnerPath, 
                        Arguments = args,
                        UseShellExecute = false, 
                        CreateNoWindow = true, 
                        RedirectStandardOutput = true, 
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };

                process.StartInfo.WorkingDirectory = _mainSettings.FolderToWatch;
                process.OutputDataReceived += WriteToLog;
                process.ErrorDataReceived += WriteToLog;
                process.Exited += ProcessExit;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (_tryAgain)
                {
                    if (importContext.canRetry())
                    {
                        LogOutput("\n");
                        LogError("SkylineRunner returned an error. Trying again...");
                        LogOutput("\n");
                        continue;
                    }

                    // TODO: Consider stopping AutoQC?
                    LogOutput("\n");
                    LogError("SkylineRunner returned an error. Exceeded maximum try count.  Giving up...");
                    LogOutput("\n");
                }
                break;
            }
            _tryAgain = false;
        }

        private string GetArgs(ImportContext importContext, bool toPrint = false)
        {
            StringBuilder args = new StringBuilder();
            foreach (var arg in _mainSettings.SkylineRunnerArgs(importContext, toPrint))
            {
                args.Append(arg).Append(" ");
            }

            foreach (var settingsTab in _settingsTabs)
            {
                foreach (var arg in settingsTab.SkylineRunnerArgs(importContext, toPrint))
                {
                    args.Append(arg).Append(" ");
                }
            }

            return args.ToString();
        }

        // Handle a line of output/error data from the process.
        private void WriteToLog(object sender, DataReceivedEventArgs e)
        {
            if (DetectError(e.Data))
            {
                LogError(e.Data);
            }
            else
            {
                Log(e.Data);   
            }
        }

        private Boolean DetectError(string message)
        {
            if (message == null || !message.StartsWith("Error")) return false;
            if (message.Contains("Failed importing the results file"))
            {
                _tryAgain = true;
            }
            return true;
        }

        public void RunUI(Action action)
        {
            try
            {
                Invoke(action);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        void ProcessExit(object sender, EventArgs e)
        {
            var process = sender as Process;
            if (process == null)
            {
                LogError("!!!Process cannot be null!!!");
                return;
            }

            var exitCode = process.ExitCode;

            if (exitCode == 0)
            {
                Log(string.Format("{0} exited successfully.", SKYLINE_RUNNER));
            }
            else
            {

                LogError(string.Format("{0} exited with errors.  Stopping AutoQC...\n\n", SKYLINE_RUNNER));
                Stop();
            }
        }

        #region [UI event handlers]
       
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
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textFolderToWatchPath.Text = dialog.SelectedPath;
            }
        }

        private void btnStartStopAutoQC_Click(object sender, EventArgs e)
        {
            if (btnStartStop.Text.Equals(RUN_AUTOQC))
            {
                Run();
            }
            else
            {
                Stop();
            }
        }

        private void cbRunsprocop_CheckedChanged(object sender, EventArgs e)
        {
            groupBoxSprocop.Enabled = cbRunsprocop.Checked;
        }

        private void cbPublishToPanorama_CheckedChanged(object sender, EventArgs e)
        {
            groupBoxPanorama.Enabled = cbPublishToPanorama.Checked;
        }

        #endregion
    }
}
