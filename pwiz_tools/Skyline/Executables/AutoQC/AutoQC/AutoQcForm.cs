/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2015 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AutoQC.Properties;
using log4net;
using log4net.Config;

namespace AutoQC
{
    public partial class AutoQCForm : Form, IAppControl, IProcessControl, IAutoQCLogger
    {
        private const string AUTO_QC_RUNNING = "AutoQC is running";
        private const string AUTO_QC_WAITING = "AutoQC is waiting for background processes to finish";
        private const string AUTO_QC_STOPPED = "AutoQC is stopped";
        private const string VALIDATING_SETTINGS = "Validating settings...";
        private const string RUN_AUTOQC = "Run AutoQC";
        private const string STOP_AUTOQC = "Stop AutoQC";

        public const string SKYLINE_RUNNER = "SkylineRunner.exe";
        public const int MAX_TRY_COUNT = 1;

        // Path to SkylineRunner.exe
        // Expect SkylineRunner to be in the same directory as AutoQC
        public static readonly string SkylineRunnerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            SKYLINE_RUNNER);

        // Background worker to run SkylineRunner
        private readonly AutoQCBackgroundWorker _worker;
        private readonly ProcessRunner _processRunner;

        private readonly List<SettingsTab> _settingsTabs;
        

        private static readonly ILog LOG = LogManager.GetLogger(typeof(AutoQCForm).Name);

        public AutoQCForm()
        {
            InitializeComponent();

            // Remove the SProCoP settings tab for now.  No way to hide the tab in the designer
            tabControl.TabPages.Remove(tabSprocopSettings);

            _settingsTabs = new List<SettingsTab>
            {
                new MainSettingsTab(this, this),
                new PanoramaSettingsTab(this, this)
            };

            // Initialize the tabs from default settings.
            foreach(var settings in _settingsTabs)
            {
                settings.InitializeFromDefaultSettings();    
            }

            _worker = new AutoQCBackgroundWorker(this, this, this);
            _processRunner = new ProcessRunner(this);
        }

        private MainSettingsTab GetMainSettingsTab()
        {
            return (MainSettingsTab)_settingsTabs[0];
        }
      
        private async void Run()
        {
            textOutput.Text = String.Empty;

            LogOutput("Starting AutoQC...");

            SetValidatingControls();

            if (!File.Exists(SkylineRunnerPath))
            {
                LogError(
                    "Could not find {0} at this path {1}. {0} should be in the same directory as AutoQC.",
                    SKYLINE_RUNNER, SkylineRunnerPath);
                SetStoppedControls();
                return;
            }

            var mainSettingsTab = GetMainSettingsTab();
            if (!mainSettingsTab.ValidateSettings())
            {
                SetStoppedControls();
                return;
            }
            
            // Initialize logging to log in the folder with the Skyline document.
            var skylineFileDir = mainSettingsTab.Settings.SkylineFileDir;
            GlobalContext.Properties["WorkingDirectory"] = skylineFileDir;
            XmlConfigurator.Configure();
            Log("Logging to directory: {0}", skylineFileDir);

            // Validate on a background thread. Validating Panorama settings can take a few seconds.
            var validSettings = await Task.Run(() => ValidateAllSettings());
            if (!validSettings)
            {
                SetStoppedControls();
                return;
            }

            SaveSettings();
            PrintSettings();

            SetRunningControls();

            var mainSettings = mainSettingsTab.Settings;

            // Make sure "Integrate all" is checked in the Skyline settings
            if (!(await Task.Run(() => mainSettings.IsIntegrateAllChecked(this))))
            {
                SetStoppedControls();
                return;
            }

            // Export a report from the Skyline document to get the most recent acquisition date on the results files
            // imported into the document.
            if (await Task.Run(() => mainSettings.ReadLastAcquiredFileDate(this, this)))
            {
                _worker.Start(mainSettings);
            }
            else
            {
                SetStoppedControls();
            }
        }

        public string GetLogDirectory()
        {
            var mainSettingsTab = GetMainSettingsTab();
            if (mainSettingsTab != null && mainSettingsTab.Settings != null)
            {
                return mainSettingsTab.Settings.SkylineFileDir;
            }
            return "";
        }

        private bool ValidateAllSettings()
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

        private void SaveSettings()
        {
            _settingsTabs.ForEach(settingsTab => settingsTab.SaveSettings());

            Settings.Default.Save();
        }

        private void PrintSettings()
        {
            _settingsTabs.ForEach(settingsTab => settingsTab.PrintSettings());
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
            groupBoxMain.Enabled = false;
            groupBoxPanorama.Enabled = false;
            cbPublishToPanorama.Enabled = false;

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

        private void Stop()
        {
            LogWithSpace("Stopping AutoQC...");
            _worker.Stop();
        }

        private void SetStoppedControls()
        {
            btnStartStop.Text = RUN_AUTOQC;
            btnStartStop.Enabled = true;
            tabControl.SelectTab(tabOutput);
            statusImg.Image = Resources.redstatus;
       
            labelStatusRunning.Text = AUTO_QC_STOPPED;
            groupBoxMain.Enabled = true;
            groupBoxPanorama.Enabled = true;
            cbPublishToPanorama.Enabled = true;

            tabControl.Update();
        }

        #region [Implementation of IAppControl interface]
        public void SetWaiting()
        {
            RunUI(() =>
            {
                LogWithSpace(AUTO_QC_WAITING);
                SetWaitingControls();
            });
        }

        public void SetStopped()
        {
            RunUI(() =>
            {
                Log(AUTO_QC_STOPPED);
                SetStoppedControls();
            });
        }

        public void SetUIMainSettings(MainSettings mainSettings)
        {
            RunUI(() =>
            {
                textSkylinePath.Text = mainSettings.SkylineFilePath;
                textFolderToWatchPath.Text = mainSettings.FolderToWatch;
                textResultsTimeWindow.Text = mainSettings.ResultsWindowString;
                textAquisitionTime.Text = mainSettings.AcquisitionTimeString;
                comboBoxInstrumentType.SelectedItem = mainSettings.InstrumentType;
                comboBoxInstrumentType.SelectedIndex = comboBoxInstrumentType.FindStringExact(mainSettings.InstrumentType);
            });
        }

        MainSettings IAppControl.GetUIMainSettings()
        {
            return RunUI(() => GetMainSettingsFromUI());
        }

        private MainSettings GetMainSettingsFromUI()
        {
            var mainSettings = new MainSettings()
            {
                SkylineFilePath = textSkylinePath.Text,
                FolderToWatch = textFolderToWatchPath.Text,
                ResultsWindowString = textResultsTimeWindow.Text,
                InstrumentType = comboBoxInstrumentType.SelectedItem.ToString(),
                AcquisitionTimeString = textAquisitionTime.Text
            };
            return mainSettings;
        }

        public void SetUIPanoramaSettings(PanoramaSettings panoramaSettings)
        {
            RunUI(() =>
            {
                textPanoramaUrl.Text = panoramaSettings.PanoramaServerUrl;
                textPanoramaEmail.Text = panoramaSettings.PanoramaUserEmail;
                textPanoramaPasswd.Text = panoramaSettings.PanoramaPassword;
                textPanoramaFolder.Text = panoramaSettings.PanoramaFolder;
                cbPublishToPanorama.Checked = panoramaSettings.PublishToPanorama;
            });
        }

        public PanoramaSettings GetUIPanoramaSettings()
        {
            return RunUI(() => GetPanoramaSettingsFromUI());
        }

        private PanoramaSettings GetPanoramaSettingsFromUI()
        {
            var panoramaSettings = new PanoramaSettings()
            {
                PanoramaServerUrl = textPanoramaUrl.Text,
                PanoramaUserEmail = textPanoramaEmail.Text,
                PanoramaPassword = textPanoramaPasswd.Text,
                PanoramaFolder = textPanoramaFolder.Text,
                PublishToPanorama = cbPublishToPanorama.Checked
            };
            return panoramaSettings;
        }
        public void DisablePanoramaSettings()
        {
            RunUI(() => groupBoxPanorama.Enabled = false);
        }

        public void SetUISprocopSettings(SprocopSettings sprocopSsettings)
        {
            RunUI(() =>
            {
                cbRunsprocop.Checked = sprocopSsettings.RunSprocop;
                textRScriptPath.Text = sprocopSsettings.RScriptPath;
                numericUpDownThreshold.Value = sprocopSsettings.Threshold;
                checkBoxIsHighRes.Checked = sprocopSsettings.IsHighRes;
                numericUpDownMMA.Value = sprocopSsettings.MMA;
            });
        }

        public SprocopSettings GetUISprocopSettings()
        {
            return RunUI(() => GetSprocopSettingsFromUI());
        }

        private SprocopSettings GetSprocopSettingsFromUI()
        {
            var settings = new SprocopSettings()
            {
                RunSprocop = cbRunsprocop.Checked,
                RScriptPath = textRScriptPath.Text,
                Threshold = (int) numericUpDownThreshold.Value,
                MMA = (int) numericUpDownMMA.Value,
                IsHighRes = checkBoxIsHighRes.Checked
            };
            return settings;
        }

        public void DisableSprocopSettings()
        {
            RunUI(() => groupBoxSprocop.Enabled = false);
        }


        #endregion

        

        #region [Logging methods; Implementation of IAutoQCLogger interface]

        private string _lastMessage = String.Empty;

        // Log to the Output tab only
        public void LogOutput(string message, params Object[] args)
        {
            Log(message, false, 0, 0, args);
        }

        public void LogOutput(string message, int blankLinesBefore, int blankLinesAfter, params object[] args)
        {
            Log(message, false, blankLinesBefore, blankLinesAfter, args);
        }

        // Log error to the output tab only
        public void LogErrorOutput(string error, params Object[] args)
        {
            LogError(error, false, 0, 0, args);
        }

        public void LogErrorOutput(string error, int blankLinesBefore, int blankLinesAfter, params object[] args)
        {
            LogError(error, false, blankLinesBefore, blankLinesAfter, args);
        }

        public void Log(string message, params Object[] args)
        {
            Log(message, true, 0, 0, args);
        }

        private void LogWithSpace(string message, params Object[] args)
        {
            Log(message, 1, 1, args);  
        }

        public void Log(string message, int blankLinesBefore, int blankLinesAfter, params object[] args)
        {
            Log(message, true, blankLinesBefore, blankLinesAfter, args);
        }

        private void Log(string line, bool logToFile, int blankLinesBefore = 0, int blankLinesAfter = 0,
            params Object[] args)
        {
            if (args != null && args.Length > 0)
            {
                line = string.Format(line, args);
            }

            if (line.Equals(_lastMessage))
            {
                return;
            }
            _lastMessage = line;

            RunUI(() =>
            {
                while (blankLinesBefore-- > 0)
                {
                    textOutput.AppendText(Environment.NewLine);
                }
                textOutput.AppendText(line + Environment.NewLine);
                while (blankLinesAfter-- > 0)
                {
                    textOutput.AppendText(Environment.NewLine);
                }
                textOutput.SelectionStart = textOutput.TextLength;
                textOutput.ScrollToCaret();
                textOutput.Update();

            });
            if (logToFile)
            {
                LOG.Info(line);
            }
        }

        private void LogException(Exception ex, bool logToFile)
        {
            LogError(ex.Message, false);
            LogError("Exception details can be found in the AutoQC log in {0} ", GetLogDirectory());
            if (logToFile)
            {
                LOG.Error(ex.Message, ex); // Include stacktrace of the exception.
            }
        }

        public void LogException(Exception ex)
        {
            LogException(ex, true);
        }

        public void LogError(string message, params Object[] args)
        {
            LogError(message, true, 0, 0, args);
        }

        public void LogError(string message, int blankLinesBefore, int blankLinesAfter, params object[] args)
        {
            LogError(message, true, blankLinesBefore, blankLinesAfter, args);
        }

        private void LogError(string line, bool logToFile, int blankLinesBefore = 0, int blankLinesAfter = 0, params Object[] args)
        {
            if (args != null && args.Length > 0)
            {
                line = string.Format(line, args);
            }
            RunUI(() =>
            {
                line = "ERROR: " + line;
                textOutput.SelectionStart = textOutput.TextLength;
                textOutput.SelectionLength = 0;
                textOutput.SelectionColor = Color.Red;
                while (blankLinesBefore-- > 0)
                {
                    textOutput.AppendText(Environment.NewLine);
                }
                textOutput.AppendText(line + Environment.NewLine);
                while (blankLinesAfter-- > 0)
                {
                    textOutput.AppendText(Environment.NewLine);
                }
                textOutput.SelectionColor = textOutput.ForeColor;
                textOutput.ScrollToCaret();
                textOutput.Update();
            });
            if (logToFile)
            {
                LOG.Error(line);
            }
        }

        #endregion


        #region [Implementation of IProcessControl interface]
        public IEnumerable<ProcessInfo> GetProcessInfos(ImportContext importContext)
        {
            var processInfos = new List<ProcessInfo>();
            foreach (var settingsTab in _settingsTabs)
            {
                var runBefore = settingsTab.RunBefore(importContext);
                if (runBefore != null)
                {
                    runBefore.WorkingDirectory = importContext.WorkingDir;
                    processInfos.Add(runBefore);
                }
            }

            var skylineRunnerArgs = GetSkylineRunnerArgs(importContext);
            var argsToPrint = GetSkylineRunnerArgs(importContext, true);
            var skylineRunner = new ProcessInfo(SkylineRunnerPath, SKYLINE_RUNNER, skylineRunnerArgs, argsToPrint);
            skylineRunner.SetMaxTryCount(MAX_TRY_COUNT);
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

        public bool RunProcess(ProcessInfo processInfo)
        {
            return _processRunner.RunProcess(processInfo);
        }

        public void StopProcess()
        {
            _processRunner.StopProcess();  
        }

        #endregion

        private string GetSkylineRunnerArgs(ImportContext importContext, bool toPrint = false)
        {
            var args = new StringBuilder();
            
            foreach (var settingsTab in _settingsTabs)
            {
                args.AppendLine();
                args.Append(settingsTab.SkylineRunnerArgs(importContext, toPrint));
            }

            return args.ToString();
        }

        public void RunUI(Action action)
        {
            if (InvokeRequired)
            {
                try
                {
                    Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                }
            }
            else
            {
                action();
            }
        }

        public T RunUI<T>(Func<T> function)
        {
            if (InvokeRequired)
            {
                try
                {
                    return (T) Invoke(function);
                }
                catch (ObjectDisposedException)
                {
                }
            }
            else
            {
                return function();
            }
            return default(T);
        }

        private void OpenFile(string filter, TextBox textbox)
        {
            var dialog = new OpenFileDialog { Filter = filter };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textbox.Text = dialog.FileName;
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
            else if(btnStartStop.Text.Equals(STOP_AUTOQC))
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

    public interface IAppControl
    {
        void SetWaiting();
        void SetStopped();

        void SetUIMainSettings(MainSettings mainSettings);
        MainSettings GetUIMainSettings();

        void SetUIPanoramaSettings(PanoramaSettings panoramaSettings);
        PanoramaSettings GetUIPanoramaSettings();
        void DisablePanoramaSettings();

        void SetUISprocopSettings(SprocopSettings sprocopSettings);
        SprocopSettings GetUISprocopSettings();
        void DisableSprocopSettings();
    }

    public interface IAutoQCLogger
    {
        void Log(string message, params object[] args);
        void Log(string message, int blankLinesBefore, int blankLinesAfter, params object[] args);
        void LogError(string message, params object[] args);
        void LogError(string message, int blankLinesBefore, int blankLinesAfter, params object[] args);
        void LogException(Exception exception);
        // Log to Output tab only
        void LogOutput(string message, params object[] args);
        void LogOutput(string message, int blankLinesBefore, int blankLinesAfter, params object[] args);
        // Log error to Output tab only
        void LogErrorOutput(string error, params object[] args);
        void LogErrorOutput(string error, int blankLinesBefore, int blankLinesAfter, params object[] args);
    }

    public interface IProcessControl
    {
        IEnumerable<ProcessInfo> GetProcessInfos(ImportContext importContext);
        bool RunProcess(ProcessInfo processInfo);
        void StopProcess();
    }
}
