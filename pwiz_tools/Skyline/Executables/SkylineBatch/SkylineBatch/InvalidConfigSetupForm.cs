using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class InvalidConfigSetupForm : Form
    {
        // The Configuration Setup Manager Form
        // Allows users to correct file paths, R versions, and Skyline types of an invalid configuration.

        private SkylineBatchConfig _invalidConfig;
        private SkylineBatchConfig _validConfig;
        
        private readonly IMainUiControl _mainControl;
        private readonly RDirectorySelector _rDirectorySelector;

        private string _lastInputPath; // the last user-entered file or folder path

        private bool _askedAboutRootReplacement; // if the user has been asked about replacing path roots for this configuration

        private TaskCompletionSource<bool> clickNextButton; // allows awaiting btnNext click

        public InvalidConfigSetupForm(IMainUiControl mainControl, SkylineBatchConfig invalidConfig, SkylineBatchConfigManagerState state, RDirectorySelector rDirectorySelector)
        {
            InitializeComponent();
            Icon = Program.Icon();
            _invalidConfig = invalidConfig;
            State = state;
            _rDirectorySelector = rDirectorySelector;
            _mainControl = mainControl;
            CreateValidConfig();
        }

        public SkylineBatchConfigManagerState State { get; private set; }

        public IValidatorControl CurrentControl { get; private set; }

        private MainSettings mainSettings => _invalidConfig.MainSettings;
        private RefineSettings refineSettings => _invalidConfig.RefineSettings;
        private ReportSettings reportSettings => _invalidConfig.ReportSettings;

        private async void CreateValidConfig()
        {
            // get valid settings
            var validMainSettings = await FixInvalidMainSettings();
            var validRefineSettings = await FixInvalidRefineSettings();
            var validReportSettings = await FixInvalidReportSettings();
            var validSkylineSettings = await FixInvalidSkylineSettings();
            // create valid configuration
            _validConfig = new SkylineBatchConfig(_invalidConfig.Name, _invalidConfig.Enabled, _invalidConfig.LogTestFormat, DateTime.Now, 
                validMainSettings, _invalidConfig.FileSettings, validRefineSettings, 
                validReportSettings, validSkylineSettings);
            // replace old configuration
            State.UserReplaceSelected(_validConfig, _mainControl);
            CloseSetup();
        }

        private void btnSkip_Click(object sender, EventArgs e)
        {
            _validConfig = _invalidConfig;
            CloseSetup();
        }

        private void CloseSetup()
        {
            DialogResult = DialogResult.OK;
            Close();
        }
        
        #region Fix Configuration Settings

        private async Task<MainSettings> FixInvalidMainSettings()
        {
            var validTemplate = mainSettings.Template;
            if (mainSettings.Template.IsIndependent())
            {
                if (mainSettings.Template.PanoramaFile == null)
                {
                    var validTemplateFile = await GetValidPath(
                        Resources.InvalidConfigSetupForm_FixInvalidMainSettings_Skyline_template_file,
                        mainSettings.Template.FilePath, SkylineTemplate.ValidateTemplateFileNotDownloading, TextUtil.FILTER_SKY + "|" + TextUtil.FILTER_SKY_ZIP, PathDialogOptions.File);
                    validTemplate = SkylineTemplate.FromUi(validTemplateFile, mainSettings.Template.DependentConfigName,
                        mainSettings.Template.PanoramaFile);
                }
                else
                {
                    var invalidPanoramaFile = mainSettings.Template.PanoramaFile;
                    var validDownloadFolder = await GetValidPath(
                        Resources.InvalidConfigSetupForm_FixInvalidMainSettings_folder_to_download_the_Skyline_template_into,
                        invalidPanoramaFile.DownloadFolder, PanoramaFile.ValidateDownloadFolder, null, PathDialogOptions.Folder);
                    validTemplate = SkylineTemplate.FromUi(null, mainSettings.Template.DependentConfigName,
                       invalidPanoramaFile.ReplaceFolder(validDownloadFolder));
                }
            }

            var validAnalysisFolderPath = await GetValidPath(Resources.InvalidConfigSetupForm_FixInvalidMainSettings_analysis_folder, 
                mainSettings.AnalysisFolderPath, MainSettings.ValidateAnalysisFolder, null, PathDialogOptions.Folder);
            var dataValidator = mainSettings.Server != null ? MainSettings.ValidateDataFolderWithServer : (Validator)MainSettings.ValidateDataFolderWithoutServer;
            var validDataFolderPath = await GetValidPath(Resources.InvalidConfigSetupForm_FixInvalidMainSettings_data_folder, 
                mainSettings.DataFolderPath, dataValidator, null, PathDialogOptions.Folder);
            var annotationsValidator = mainSettings.AnnotationsDownload != null ? MainSettings.ValidateAnnotationsWithServer : (Validator)MainSettings.ValidateAnnotationsWithoutServer;
            var validAnnotationsFilePath = await GetValidPath(Resources.InvalidConfigSetupForm_FixInvalidMainSettings_annotations_file, mainSettings.AnnotationsFilePath,
                annotationsValidator, TextUtil.FILTER_CSV, PathDialogOptions.File);
            var validAnnotationsDownload =
                !string.IsNullOrEmpty(validAnnotationsFilePath) && mainSettings.AnnotationsDownload != null
                    ? mainSettings.AnnotationsDownload.ReplaceFolder(Path.GetDirectoryName(validAnnotationsFilePath))
                    : null;

            return new MainSettings(validTemplate, validAnalysisFolderPath, mainSettings.UseAnalysisFolderName, validDataFolderPath, mainSettings.Server,
                validAnnotationsFilePath, validAnnotationsDownload, mainSettings.ReplicateNamingPattern);
        }

        private async Task<RefineSettings> FixInvalidRefineSettings()
        {
            var validOutputPath = await GetValidPath(Resources.InvalidConfigSetupForm_FixInvalidRefineSettings_path_to_the_refined_output_file,
                refineSettings.OutputFilePath, RefineSettings.ValidateOutputFile, TextUtil.FILTER_SKY, PathDialogOptions.File, PathDialogOptions.Save);
            return RefineSettings.GetPathChanged(refineSettings, validOutputPath);
        }

        private async Task<ReportSettings> FixInvalidReportSettings()
        {
            var reportNumber = reportSettings.Reports.Count;
            var validReports = new List<ReportInfo>();
            for (int i = 0; i < reportNumber; i++)
            {
                var report = reportSettings.Reports[i];
                var validReportPath = await GetValidPath(string.Format(Resources.InvalidConfigSetupForm_FixInvalidReportSettings__0__report, 
                        report.Name), report.ReportPath, 
                    ReportInfo.ValidateReportPath, TextUtil.FILTER_SKYR, PathDialogOptions.File);
                var validScripts = new List<Tuple<string, string>>();
                var validRemoteFiles = new Dictionary<string, PanoramaFile>();
                foreach (var scriptAndVersion in report.RScripts)
                {
                    var validVersion = await GetValidRVersion(scriptAndVersion.Item1, scriptAndVersion.Item2);
                    string validRScript;
                    if (report.RScriptServers.ContainsKey(scriptAndVersion.Item1))
                    {
                        var validFolder = await GetValidPath(string.Format(Resources.InvalidConfigSetupForm_FixInvalidReportSettings__0__R_script, Path.GetFileNameWithoutExtension(scriptAndVersion.Item1)),
                            FileUtil.GetDirectorySafe(scriptAndVersion.Item1),
                            ReportInfo.ValidateRScriptWithServer, null, PathDialogOptions.Folder);
                        validRScript = Path.Combine(validFolder,
                            report.RScriptServers[scriptAndVersion.Item1].FileName);
                    }
                    else
                    {
                        validRScript = await GetValidPath(string.Format(Resources.InvalidConfigSetupForm_FixInvalidReportSettings__0__R_script, Path.GetFileNameWithoutExtension(scriptAndVersion.Item1)),
                            scriptAndVersion.Item1,
                            ReportInfo.ValidateRScriptWithoutServer, TextUtil.FILTER_R, PathDialogOptions.File);
                    }
                    validScripts.Add(new Tuple<string, string>(validRScript, validVersion));
                    if (report.RScriptServers.ContainsKey(scriptAndVersion.Item1))
                        validRemoteFiles.Add(validRScript, report.RScriptServers[scriptAndVersion.Item1].ReplaceFolder(Path.GetDirectoryName(validRScript)));
                }
                validReports.Add(new ReportInfo(report.Name, report.CultureSpecific, validReportPath, validScripts, validRemoteFiles, report.UseRefineFile));
            }
            return new ReportSettings(validReports);
        }
        
        private async Task<SkylineSettings> FixInvalidSkylineSettings()
        {
            if (!string.IsNullOrEmpty(SharedBatch.Properties.Settings.Default.SkylineLocalCommandPath))
                return new SkylineSettings(SkylineType.Local, null, SharedBatch.Properties.Settings.Default.SkylineLocalCommandPath);
            var runningConfigs = State.ConfigsBusy();
            var skylineTypeControl = new SkylineTypeControl(_mainControl, _invalidConfig.UsesSkyline, _invalidConfig.UsesSkylineDaily, _invalidConfig.UsesCustomSkylinePath, _invalidConfig.SkylineSettings.CmdPath, runningConfigs, State.BaseState);
            var validSkylineSettings = (SkylineSettings)await GetValidVariable(skylineTypeControl);
            return validSkylineSettings;
        }
        
        #endregion
        
        #region Get Valid Variables

        private async Task<string> GetValidPath(string variableName, string invalidPath, Validator validator, string filter, params PathDialogOptions[] dialogOptions)
        {
            var path = invalidPath;
            
            var folderControl = new FilePathControl(variableName, path, _lastInputPath, validator, filter, dialogOptions);
            path = (string) await GetValidVariable(folderControl, false);

            if (path.Equals(invalidPath))
                return path;
            _lastInputPath = path;
            
            if (!_askedAboutRootReplacement)
            {
                State.AddRootReplacement(invalidPath, path, true, _mainControl, out bool addedRootReplacement, out string oldRoot, 
                    out _askedAboutRootReplacement);
                if (addedRootReplacement)
                {
                    State.RootReplaceConfigs(oldRoot, State.BaseState.RootReplacement[oldRoot], _mainControl);
                    _invalidConfig = State.GetSelectedConfig();
                }
            }

            RemoveControl(folderControl);
            return path;
        }

        private async Task<string> GetValidRVersion(string scriptName, string invalidVersion)
        {
            var version = invalidVersion;
            var rVersionControl = new RVersionControl(scriptName, version, _rDirectorySelector, State);
            var validPath = (string) await GetValidVariable(rVersionControl);
            State = rVersionControl.State;
            return validPath;
        }
        
        private async Task<object> GetValidVariable(IValidatorControl control, bool removeControl = true)
        {
            // return existing variable if it is valid
            if (control.IsValid(out string errorMessage))
                return control.GetVariable();
            // display the control to get user input for invalid variable
            var valid = false;
            AddControl((UserControl)control);
            while (!valid)
            {
                clickNextButton = new TaskCompletionSource<bool>();
                await clickNextButton.Task;
                valid = control.IsValid(out errorMessage);
                if (!valid)
                    AlertDlg.ShowMessage(this, errorMessage);
            }
            // remove the control and return the valid variable
            if (removeControl) RemoveControl((UserControl)control);
            return control.GetVariable();
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            clickNextButton.TrySetResult(true);
        }

        #endregion
        
        private void AddControl(UserControl control)
        {
            var newHeight = Height - panel1.Height + control.Height;
            var newWidth = Width - panel1.Width + control.Width;
            Size = new Size(newWidth, newHeight);
            control.Dock = DockStyle.Fill;
            control.Show();
            panel1.Controls.Add(control);
            CurrentControl = (IValidatorControl)control;
        }

        private void RemoveControl(UserControl control)
        {
            control.Hide();
            panel1.Controls.Remove(control);
            CurrentControl = null;
        }
    }
}
