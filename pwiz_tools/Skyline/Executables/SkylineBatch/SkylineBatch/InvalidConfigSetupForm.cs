using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SkylineBatch
{
    
    public partial class InvalidConfigSetupForm : Form
    {
        private readonly SkylineBatchConfig _invalidConfig;
        private readonly IMainUiControl _mainControl;

        private bool _replaceRoot;
        private string _oldRoot;
        private string _newRoot;


        public InvalidConfigSetupForm(SkylineBatchConfig invalidConfig, IMainUiControl mainControl)
        {
            InitializeComponent();

            _invalidConfig = invalidConfig;
            _mainControl = mainControl;
            _replaceRoot = false;

            CreateValidConfig();
        }

        public SkylineBatchConfig ValidConfig { get; private set; }


        private async void CreateValidConfig()
        {
            var validMainSettings = await FixInvalidMainSettings();
            var validReportSettings = await FixInvalidReportSettings();
            var validSkylineSettings = await FixInvalidSkylineSettings();


            ValidConfig = new SkylineBatchConfig(_invalidConfig.Name, _invalidConfig.Created, DateTime.Now, 
                validMainSettings, _invalidConfig.FileSettings, validReportSettings, validSkylineSettings);
            DialogResult = DialogResult.OK;
            Close();
        }


        #region Fix Configuration Settings

        private async Task<MainSettings> FixInvalidMainSettings()
        {
            var mainSettings = _invalidConfig.MainSettings;
            var validTemplateFilePath = await GetValidPath("skyline file", mainSettings.TemplateFilePath, false, MainSettings.ValidateSkylineFile);
            var validAnalysisFolderPath = await GetValidPath("analysis folder", mainSettings.AnalysisFolderPath, true, MainSettings.ValidateAnalysisFolder);
            var validDataFolderPath = await GetValidPath("data folder", mainSettings.DataFolderPath, true, MainSettings.ValidateDataFolder);

            return new MainSettings(validTemplateFilePath, validAnalysisFolderPath, validDataFolderPath, _invalidConfig.MainSettings.ReplicateNamingPattern);
        }


        private async Task<ReportSettings> FixInvalidReportSettings()
        {
            var reports = _invalidConfig.ReportSettings.Reports;
            var validReports = new List<ReportInfo>();
            foreach (var report in reports)
            {
                var validReportPath = await GetValidPath(report.Name + " report", report.ReportPath, false,
                    ReportInfo.ValidateReportPath);
                var validScripts = new List<Tuple<string, string>>();
                foreach (var scriptAndVersion in report.RScripts)
                {
                    var validRScript = await GetValidPath(Path.GetFileName(scriptAndVersion.Item1) + " R script", scriptAndVersion.Item1, false,
                        ReportInfo.ValidateRScriptPath);
                    var validVersion = await GetValidRVersion(scriptAndVersion.Item1, scriptAndVersion.Item2);
                    validScripts.Add(new Tuple<string, string>(validRScript, validVersion));
                }
                validReports.Add(new ReportInfo(report.Name, validReportPath, validScripts));
            }

            return new ReportSettings(validReports);
        }

        
        private async Task<SkylineSettings> FixInvalidSkylineSettings()
        {
            var skylineTypeControl = new SkylineTypeControl(_invalidConfig.UsesSkyline, _invalidConfig.UsesSkylineDaily, _invalidConfig.UsesCustomSkylinePath, _invalidConfig.SkylineSettings.CmdPath);
            return (SkylineSettings)await GetValidVariable(_invalidConfig.SkylineSettings, "Invalid Skyline Installation", skylineTypeControl);
        }


        #endregion





        #region Get Valid Variables

        private async Task<string> GetValidPath(string variableName, string invalidPath, bool folder, Validator validator)
        {
            // replace path root
            var path = _replaceRoot ? invalidPath.Replace(_oldRoot, _newRoot) : invalidPath;

            var folderControl = new FilePathControl(variableName, path, folder, validator);
            path = (string) await GetValidVariable(path, "Invalid Path", folderControl, false);

            // the first time a path is changed, ask if user wants all path roots replaced
            if (string.IsNullOrEmpty(_oldRoot))
            {
                GetBestRoot(invalidPath, path);

                if (!string.IsNullOrEmpty(_oldRoot))
                    _replaceRoot = AlertDlg.ShowQuestion(this, "Would you like to use this root for all paths?" + Environment.NewLine +
                                                           _newRoot, "Replace All") == DialogResult.Yes;
            }
            RemoveControl(folderControl);
            
            return path;
        }

        private async Task<string> GetValidRVersion(string scriptName, string invalidVersion)
        {
            var version = invalidVersion;
            var rVersionControl = new RVersionControl(scriptName, version);
            return (string) await GetValidVariable(version, "Invalid R installation", rVersionControl);
        }


        private async Task<object> GetValidVariable(object initialVariable, string errorTitle, IValidatorControl control, bool removeControl = true)
        {
            var variable = initialVariable;
            if (control.IsValid(out string errorMessage)) return variable;
            AddControl((UserControl)control);

            var valid = false;
            while (!valid)
            {
                await btnNext;
                valid = control.IsValid(out errorMessage);
                if (!valid)
                    _mainControl.DisplayError(errorTitle, errorMessage);
            }

            if (removeControl) RemoveControl((UserControl)control);
            return control.GetVariable();
        }

        #endregion





        #region Find Path Root

        private void GetBestRoot(string oldPath, string newPath)
        {
            var oldPathFolders = oldPath.Split('\\');
            var newPathFolders = newPath.Split('\\');
            var maxValidFiles = 0;

            _oldRoot = "";
            _newRoot = "";

            var matchingEndFolders = 1;
            while (matchingEndFolders < Math.Min(oldPathFolders.Length, newPathFolders.Length))
            {
                // If path ends do not match we cannot replace root
                if (!oldPathFolders[oldPathFolders.Length - matchingEndFolders]
                    .Equals(newPathFolders[newPathFolders.Length - matchingEndFolders]))
                    return;

                var testOldRoot = string.Join("\\", oldPathFolders.Take(oldPathFolders.Length - matchingEndFolders).ToArray());
                var testNewRoot = string.Join("\\", newPathFolders.Take(newPathFolders.Length - matchingEndFolders).ToArray());
                var validFiles = GetValidPathNumber(testOldRoot, testNewRoot);
                if (validFiles > maxValidFiles)
                {
                    _oldRoot = testOldRoot;
                    _newRoot = testNewRoot;
                    maxValidFiles = validFiles;
                }

                matchingEndFolders++;
            }
        }


        private int GetValidPathNumber(string oldRoot, string newRoot)
        {
            int validFiles = 0;

            if (ValidPath(_invalidConfig.MainSettings.AnalysisFolderPath, oldRoot, newRoot, MainSettings.ValidateAnalysisFolder))
                validFiles++;
            if (ValidPath(_invalidConfig.MainSettings.DataFolderPath, oldRoot, newRoot, MainSettings.ValidateDataFolder))
                validFiles++;

            foreach (var report in _invalidConfig.ReportSettings.Reports)
            {
                if (ValidPath(report.ReportPath, oldRoot, newRoot, ReportInfo.ValidateReportPath))
                    validFiles++;
                foreach (var rScript in report.RScripts)
                {
                    if (ValidPath(rScript.Item1, oldRoot, newRoot, ReportInfo.ValidateRScriptPath))
                        validFiles++;
                }
            }

            return validFiles;
        }

        private bool ValidPath(string path, string oldRoot, string newRoot, Validator validator)
        {
            if (!path.Contains(oldRoot)) return false;
            var replacedRoot = path.Replace(oldRoot, newRoot);

            try
            {
                validator(replacedRoot);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
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


    // Validates a string variable, throws ArgumentException if invalid
    public delegate void Validator(string variable, string name = "");
    
    // UserControl interface to validate value of an input
    public interface IValidatorControl
    {
        object GetVariable();

        // Uses Validator to determine if variable is valid
        bool IsValid(out string errorMessage);
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
