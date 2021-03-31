using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private readonly SkylineBatchConfigManager _configManager;
        private readonly IMainUiControl _mainControl;
        private readonly RDirectorySelector _rDirectorySelector;

        private string _lastInputPath; // the last user-entered file or folder path

        private bool _askedAboutRootReplacement; // if the user has been asked about replacing path roots for this configuration

        public InvalidConfigSetupForm(IMainUiControl mainControl, SkylineBatchConfig invalidConfig, SkylineBatchConfigManager configManager, RDirectorySelector rDirectorySelector)
        {
            InitializeComponent();
            _invalidConfig = invalidConfig;
            _configManager = configManager;
            _rDirectorySelector = rDirectorySelector;
            _mainControl = mainControl;
            CreateValidConfig();
        }

        public SkylineBatchConfig Config { get; private set; }

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
            Config = new SkylineBatchConfig(_invalidConfig.Name, _invalidConfig.Enabled, DateTime.Now, 
                validMainSettings, _invalidConfig.FileSettings, validRefineSettings, 
                validReportSettings, validSkylineSettings);
            // replace old configuration
            _configManager.UserReplaceSelected(Config);
            _mainControl.UpdateUiConfigurations();
            CloseSetup();
        }

        private void btnSkip_Click(object sender, EventArgs e)
        {
            Config = _invalidConfig;
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
            var validTemplateFilePath = mainSettings.TemplateFilePath;
            if (mainSettings.DependentConfigName == null)
                validTemplateFilePath = await GetValidPath(Resources.InvalidConfigSetupForm_FixInvalidMainSettings_Skyline_template_file, 
                mainSettings.TemplateFilePath, MainSettings.ValidateTemplateFile, PathDialogOptions.File, PathDialogOptions.ExistingOptional);
            var validAnalysisFolderPath = await GetValidPath(Resources.InvalidConfigSetupForm_FixInvalidMainSettings_analysis_folder, 
                mainSettings.AnalysisFolderPath, MainSettings.ValidateAnalysisFolder, PathDialogOptions.Folder);
            var validDataFolderPath = await GetValidPath(Resources.InvalidConfigSetupForm_FixInvalidMainSettings_data_folder, 
                mainSettings.DataFolderPath, MainSettings.ValidateDataFolder, PathDialogOptions.Folder);
            var validAnnotationsFilePath = await GetValidPath(Resources.InvalidConfigSetupForm_FixInvalidMainSettings_annotations_file, mainSettings.AnnotationsFilePath,
                MainSettings.ValidateAnnotationsFile, PathDialogOptions.File);

            return new MainSettings(validTemplateFilePath, validAnalysisFolderPath, validDataFolderPath, validAnnotationsFilePath, mainSettings.ReplicateNamingPattern, mainSettings.DependentConfigName);
        }

        private async Task<RefineSettings> FixInvalidRefineSettings()
        {
            var validOutputPath = await GetValidPath(Resources.InvalidConfigSetupForm_FixInvalidRefineSettings_path_to_the_refined_output_file,
                refineSettings.OutputFilePath, RefineSettings.ValidateOutputFile, PathDialogOptions.File, PathDialogOptions.Save);
            return new RefineSettings(refineSettings.CommandValues, refineSettings.RemoveDecoys, refineSettings.RemoveResults, validOutputPath);
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
                    ReportInfo.ValidateReportPath, PathDialogOptions.File);
                var validScripts = new List<Tuple<string, string>>();
                foreach (var scriptAndVersion in report.RScripts)
                {
                    var validVersion = await GetValidRVersion(scriptAndVersion.Item1, scriptAndVersion.Item2);
                    var validRScript = await GetValidPath(string.Format(Resources.InvalidConfigSetupForm_FixInvalidReportSettings__0__R_script, Path.GetFileNameWithoutExtension(scriptAndVersion.Item1)),
                        scriptAndVersion.Item1, 
                        ReportInfo.ValidateRScriptPath, PathDialogOptions.File);
                    
                    validScripts.Add(new Tuple<string, string>(validRScript, validVersion));
                }
                validReports.Add(new ReportInfo(report.Name, validReportPath, validScripts, report.UseRefineFile));
            }
            return new ReportSettings(validReports);
        }
        
        private async Task<SkylineSettings> FixInvalidSkylineSettings()
        {
            var skylineTypeControl = new SkylineTypeControl(_mainControl, _invalidConfig.UsesSkyline, _invalidConfig.UsesSkylineDaily, _invalidConfig.UsesCustomSkylinePath, _invalidConfig.SkylineSettings.CmdPath);
            return (SkylineSettings)await GetValidVariable(skylineTypeControl);
        }
        
        #endregion
        
        #region Get Valid Variables

        private async Task<string> GetValidPath(string variableName, string invalidPath, Validator validator, params PathDialogOptions[] dialogOptions)
        {
            var path = invalidPath;
            
            var folderControl = new FilePathControl(variableName, path, _lastInputPath, validator, dialogOptions);
            path = (string) await GetValidVariable(folderControl, false);

            if (path.Equals(invalidPath))
                return path;
            _lastInputPath = path;

            GetNewRoot(invalidPath, path);
            
            RemoveControl(folderControl);
            return path;
        }

        private async Task<string> GetValidRVersion(string scriptName, string invalidVersion)
        {
            var version = invalidVersion;
            var rVersionControl = new RVersionControl(scriptName, version, _rDirectorySelector);
            return (string) await GetValidVariable(rVersionControl);
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
                await btnNext;
                valid = control.IsValid(out errorMessage);
                if (!valid)
                    AlertDlg.ShowError(this, Program.AppName(), errorMessage);
            }
            // remove the control and return the valid variable
            if (removeControl) RemoveControl((UserControl)control);
            return control.GetVariable();
        }

        #endregion
        
        #region Find Path Root

        private void GetNewRoot(string oldPath, string newPath)
        {
            var oldPathFolders = oldPath.Split('\\');
            var newPathFolders = newPath.Split('\\');
            string oldRoot = string.Empty;
            string newRoot = string.Empty;

            var matchingEndFolders = 2;
            while (matchingEndFolders <= Math.Min(oldPathFolders.Length, newPathFolders.Length))
            {
                // If path folders do not match we cannot replace root
                if (!oldPathFolders[oldPathFolders.Length - matchingEndFolders]
                    .Equals(newPathFolders[newPathFolders.Length - matchingEndFolders]))
                    break;

                oldRoot = string.Join("\\", oldPathFolders.Take(oldPathFolders.Length - matchingEndFolders).ToArray());
                newRoot = string.Join("\\", newPathFolders.Take(newPathFolders.Length - matchingEndFolders).ToArray());
                matchingEndFolders++;
            }
            // the first time a path is changed, ask if user wants all path roots replaced
            if (!_askedAboutRootReplacement && oldRoot.Length > 0 && !Directory.Exists(oldRoot) && !_configManager.RootReplacement.ContainsKey(oldRoot))
            {
                var replaceRoot = AlertDlg.ShowQuestion(this, Program.AppName(), string.Format(Resources.InvalidConfigSetupForm_GetValidPath_Would_you_like_to_replace__0__with__1___, oldRoot, newRoot)) == DialogResult.Yes;
                _askedAboutRootReplacement = true;
                if (replaceRoot)
                {
                    _configManager.AddRootReplacement(oldRoot, newRoot);
                    _invalidConfig = _configManager.GetSelectedConfig();
                }
            }
        }

        #endregion
        
        private void AddControl(UserControl control)
        {
            control.Dock = DockStyle.Fill;
            control.Show();
            panel1.Controls.Add(control);
        }

        private void RemoveControl(UserControl control)
        {
            control.Hide();
            panel1.Controls.Remove(control);
        }
    }

    // Class that lets you wait for button click (ex: "await btnNext")
    public static class ButtonAwaiterExtensions
    {
        public static ButtonAwaiter GetAwaiter(this Button button)
        {
            return new ButtonAwaiter()
            {
                Button = button
            };
        }
    }
    
    public class ButtonAwaiter : INotifyCompletion
    {

        public bool IsCompleted
        {
            get { return false; }
        }
        
        public void GetResult()
        {
        }
        
        public Button Button { get; set; }

        public void OnCompleted(Action continuation)
        {
            EventHandler h = null;
            h = (o, e) =>
            {
                Button.Click -= h;
                continuation();
            };
            Button.Click += h;
        }
    }
}
