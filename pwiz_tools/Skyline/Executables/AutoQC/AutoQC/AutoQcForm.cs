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
        public string SkylineRunnerPath { get; private set; }

        // Collection of new mass spec files to be processed.
        private readonly ConcurrentQueue<FileInfo> _dataFiles;
 
        private readonly FileSystemWatcher _fileWatcher;

        // Background worker to run SkylineRunner
        private BackgroundWorker _worker;

        public const int MAX_TRY_COUNT = 2;
        private bool _tryAgain;

        private readonly List<SettingsTab> _settingsTabs;
        private int _totalImportCount;

        private static readonly ILog LOG = LogManager.GetLogger(typeof(AutoQCForm).Name);

        public AutoQCForm()
        {
            InitializeComponent();

            // Remove the SProCoP settings tab for now.  No way to hide the tab in the designer
            tabControl.TabPages.Remove(tabSprocopSettings);

            // Expect SkylineRunner to be in the same directory as AutoQC
            SkylineRunnerPath = Path.Combine(Directory.GetCurrentDirectory(), SKYLINE_RUNNER);

            SettingsTab.MainForm = this;
            _settingsTabs = new List<SettingsTab> {new MainSettings(), new PanoramaSettings()};

            // Initialize the tabs from default settings.
            foreach(var settings in _settingsTabs)
            {
                settings.InitializeFromDefaultSettings();    
            }
            
            _dataFiles = new ConcurrentQueue<FileInfo>();

            _fileWatcher = new FileSystemWatcher {Filter = "*" + THERMO_EXT };
            _fileWatcher.Created += (s, e) => FileAdded(e, this);
        }

        private MainSettings GetMainSettings()
        {
            return (MainSettings) _settingsTabs[0];
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
            LogOutput("Starting AutoQC...");

            SetValidatingControls();

            if (!File.Exists(SkylineRunnerPath))
            {
                LogError(string.Format("Could not find {0} at this path {1}. {0} should be in the same directory as AutoQC.", SKYLINE_RUNNER, SkylineRunnerPath));
                SetStoppedControls();
                return;
            }

            var mainSettings = GetMainSettings();
            if (!mainSettings.ValidateSettings())
            {
                SetStoppedControls();
                return;
            }
            
            // Initialize logging to log in the folder where results files will be written.
            GlobalContext.Properties["WorkingDirectory"] = textFolderToWatchPath.Text;
            XmlConfigurator.Configure();
            
            Log("Watching folder " + textFolderToWatchPath.Text);
            Log("Mass spec. files will be imported to " + textSkylinePath.Text);

            var validSettings = await Task.Run(() => ValidateForm());
            if (!validSettings)
            {
                SetStoppedControls();
                return;
            }

            SaveSettings();

            SetRunningControls();

            // Import existing data files in the directory, if required
            if (mainSettings.ImportExistingFiles)
            {
                ProcessExistingFiles();
            }
            else
            {
                ProcessNewFiles();
            }

            _fileWatcher.Path = mainSettings.FolderToWatch;
            _fileWatcher.EnableRaisingEvents = true; // Enables events on the _fileWatcher
        }

        private void Stop()
        {
            Log("Stopping AutoQC...\n");
            _fileWatcher.EnableRaisingEvents = false;
            if (_worker != null && _worker.IsBusy)
            {
                _worker.CancelAsync();
                SetWaitingControls();
            }
            else
            {
                SetStoppedControls();
            }
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
            // MainSettings tab is the first tab in _settingsTabs.  It has already been validated. Don't re-validate.
            for (var i = 1; i < _settingsTabs.Count; i++)
            {
                var settingsTab = _settingsTabs[i];
                if (!settingsTab.ValidateSettings())
                {
                    validated = false;
                }
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
            LOG.Error(ex.Message, ex); // Include stacktrace of the exception.
        }

        public void LogError(string line, bool logToFile = true)
        {
            RunUI(() =>
            {
                line = "ERROR: " + line;
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
            _settingsTabs.ForEach(settingsTab => settingsTab.SaveSettings());

            Settings.Default.Save();
        }

        private void ProcessExistingFiles()
        {
            Log("Processing existing files...");
            RunBackgroundWorker(BackgroundWorker_DoWorkProcessExisting,
                BackgroundWorker_RunWorkerCompletedProcessExisting); 
        }

        private void ProcessNewFiles()
        {
            Log("Processing new files...");
            RunBackgroundWorker(BackgroundWorker_DoWork, BackgroundWorker_RunWorkerCompleted);
        }

        private void RunBackgroundWorker(DoWorkEventHandler doWork, RunWorkerCompletedEventHandler doOnComplete)
        {
            if (_worker != null && _worker.IsBusy)
            {
                LogError("Background worker is running!!!");
                Stop();
                return;
            }
            _worker = new BackgroundWorker {WorkerSupportsCancellation = true, WorkerReportsProgress = true};
            _worker.DoWork += doWork;
            _worker.RunWorkerCompleted += doOnComplete;
            _worker.RunWorkerAsync(); 
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
                        var importContext = new ImportContext(fileInfo);
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
            if (e.Cancelled)
            {
                Log("Cancelled processing files.");
                Stop();
            }
            else
            {
                Log("Finished processing existing files.\n");
                ProcessNewFiles(); // Start processing new files.
            }
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                SetStoppedControls();
                Log("AutoQC stopped.");
            }
            else
            {
                Stop();
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

            // Queue up any existing data files in the folder
            var files =
                new DirectoryInfo(GetMainSettings().FolderToWatch).GetFiles(GetFileFilter()).OrderBy(f => f.LastWriteTime).ToList();

            Log(string.Format("Found {0} files.", files.Count));

            var importContext = new ImportContext(files);
            importContext.TotalImportCount = _totalImportCount;
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

        private string GetFileFilter()
        {
            // TODO: We need to support other instrument vendors
            return "*" + THERMO_EXT;
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
            var file = importContext.getCurrentFile();
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
            var processInfos = GetProcessInfos(importContext);

            foreach (var procInfo in processInfos)
            {
                RunProcess(procInfo);
            }
            _totalImportCount++;
        }

        private List<ProcessInfo> GetProcessInfos(ImportContext importContext)
        {
            var processInfos = new List<ProcessInfo>();

            foreach (var settingsTab in _settingsTabs)
            {
                var runBefore = settingsTab.RunBefore(importContext);
                if (runBefore != null)
                {
                    processInfos.Add(runBefore);
                }
            }

            var skylineRunnerArgs = GetSkylineRunnerArgs(importContext);
            var argsToPrint = GetSkylineRunnerArgs(importContext, true);
            var skylineRunner = new ProcessInfo(SkylineRunnerPath, SKYLINE_RUNNER, skylineRunnerArgs, argsToPrint);
            skylineRunner.allowRetry();
            processInfos.Add(skylineRunner);

            foreach (var settingsTab in _settingsTabs)
            {
                var runAfter = settingsTab.RunAfter(importContext);
                if (runAfter != null)
                {
                    processInfos.Add(runAfter);
                }
            }
            return processInfos;
        }

        private void RunProcess(ProcessInfo procInfo)
        {
            Log(string.Format("Running {0} with args: ", procInfo.ExeName));
            Log(procInfo.ArgsToPrint);

            while (true)
            {
                procInfo.incrementTryCount();

                var process = CreateProcess(procInfo);
                process.StartInfo.WorkingDirectory = GetMainSettings().FolderToWatch;
                process.OutputDataReceived += WriteToLog;
                process.ErrorDataReceived += WriteToLog;
                process.Exited += ProcessExit;
                process.Start();
                process.BeginOutputReadLine();
                process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (_tryAgain)
                {
                    if (procInfo.canRetry())
                    {
                        LogOutput("\n");
                        LogError(string.Format("{0} returned an error. Trying again...", procInfo.Executable));
                        LogOutput("\n");
                        continue;
                    }

                    LogOutput("\n");
                    LogError(string.Format("{0} returned an error. Exceeded maximum try count.  Giving up...", procInfo.ExeName));
                    LogOutput("\n");
                    Stop();
                }
                break;
            }
            _tryAgain = false;
        }

        private static Process CreateProcess(ProcessInfo procInfo)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = procInfo.Executable,
                    Arguments = procInfo.Args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };
            return process;
        }

        private string GetSkylineRunnerArgs(ImportContext importContext, bool toPrint = false)
        {
            var args = new StringBuilder();
            args.Append(GetMainSettings().SkylineRunnerArgs(importContext, toPrint));
        
            foreach (var settingsTab in _settingsTabs)
            {
                args.AppendLine();
                args.Append(settingsTab.SkylineRunnerArgs(importContext, toPrint));
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
                Stop();
                return;
            }

            var exitCode = process.ExitCode;

            if (exitCode == 0)
            {
                Log(string.Format("{0} exited successfully.", SKYLINE_RUNNER));
            }
            else
            {

                LogError(string.Format("{0} exited with errors.", SKYLINE_RUNNER));
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
