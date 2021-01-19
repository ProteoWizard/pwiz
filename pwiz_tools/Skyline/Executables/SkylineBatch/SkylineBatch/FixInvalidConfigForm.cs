using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SkylineBatch
{
    
    public partial class FixInvalidConfigForm : Form
    {
        private readonly SkylineBatchConfig _invalidConfig;
        private readonly IMainUiControl _mainControl;

        private bool _replaceRoot;
        private string _oldRoot;
        private string _newRoot;


        public FixInvalidConfigForm(SkylineBatchConfig invalidConfig, IMainUiControl mainControl)
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


        private async Task<object> GetValidVariable(object initialVariable, string errorTitle, IVariableInputControl control, bool removeControl = true)
        {
            var variable = initialVariable;
            if (control.IsValid(out string errorMessage)) return variable;
            AddControl((UserControl)control);

            var valid = false;
            while (!valid)
            {
                await btnNext;
                valid = control.IsValid(out errorMessage); //IsValid(variable, validator, out errorMessage);
                if (!valid)
                    _mainControl.DisplayError(errorTitle, errorMessage);
            }

            if (removeControl) RemoveControl((UserControl)control);
            return control.GetVariable();
        }



        

        private async Task<string> GetValidPath(string variableName, string invalidPath, bool folder, Validator validator)
        {
            // replace path root
            var path = _replaceRoot ? ReplacePathRoot(invalidPath) : invalidPath;

            var folderControl = new FilePathControl(variableName, path, folder, validator);
            path = (string) await GetValidVariable(path, "Invalid Path", folderControl, false);

            // the first time a path is changed, ask if user wants all path roots replaced
            if (_oldRoot == null && !invalidPath.Equals(path))
            {
                GetRootReplacement(invalidPath, path);

                _replaceRoot = _mainControl.DisplayQuestion("Replace All",
                    "Would you like to use this root for all paths?" + Environment.NewLine +
                    _newRoot) == DialogResult.Yes;
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

        private async Task<SkylineSettings> FixInvalidSkylineSettings()
        {
            var skylineTypeControl = new SkylineTypeControl(_invalidConfig.UsesSkyline, _invalidConfig.UsesSkylineDaily, _invalidConfig.UsesCustomSkylinePath, _invalidConfig.SkylineSettings.CmdPath);
            return (SkylineSettings) await GetValidVariable(_invalidConfig.SkylineSettings, "Invalid Skyline Installation", skylineTypeControl);
        }


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


        private void GetRootReplacement(string oldPath, string newPath)
        {
            var oldPathFolders = oldPath.Split('\\');
            var newPathFolders = newPath.Split('\\');
            var i = 1;
            var sameTail = true;
            while (sameTail)
            {
                var oldFolder = oldPathFolders[oldPathFolders.Length - i];
                var newFolder = newPathFolders[newPathFolders.Length - i];
                sameTail = newFolder.Equals(oldFolder);
                i++;
            }

            var numberSharedFolders = i - 2;
            _oldRoot = string.Join("\\", oldPathFolders.Take(oldPathFolders.Length - numberSharedFolders).ToArray());
            _newRoot = string.Join("\\", newPathFolders.Take(newPathFolders.Length - numberSharedFolders).ToArray());
        }


        private string ReplacePathRoot(string path)
        {
            var oldRootFolders = _oldRoot.Split('\\');
            var newRootFolders = _newRoot.Split('\\');
            var pathFolders = path.Split('\\');

            var i = 0;
            var sameRoot = true;
            while (sameRoot && i < Math.Min(pathFolders.Length, oldRootFolders.Length))
            {
                var oldFolder = oldRootFolders[i];
                var currentFolder = pathFolders[i];
                sameRoot = oldFolder.Equals(currentFolder);
                i++;
            }
            var sharedFolders = i - 1;

            if (sharedFolders < oldRootFolders.Length - 1)
            {
                var fewerFolders = oldRootFolders.Length - sharedFolders;
                var currentRoot = string.Join("\\", oldRootFolders.Take(sharedFolders).ToArray());
                var newRoot = string.Join("\\", newRootFolders.Take(newRootFolders.Length - fewerFolders).ToArray());
                return path.Replace(currentRoot, newRoot);
            }
            
            return path.Replace(_oldRoot, _newRoot);
        }


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
                    var validRScript = await GetValidPath(report.Name + " R script path", scriptAndVersion.Item1, false,
                        ReportInfo.ValidateRScriptPath);
                    var validVersion = await GetValidRVersion(scriptAndVersion.Item1, scriptAndVersion.Item2);
                    validScripts.Add(new Tuple<string, string>(validRScript, validVersion));
                }
                validReports.Add(new ReportInfo(report.Name, validReportPath, validScripts));
            }

            return new ReportSettings(validReports);
        }


    }



    public delegate void Validator(string variable, string name = "");


    public interface IVariableInputControl
    {
        object GetVariable();

        // Uses Validator to determine if variable is valid
        bool IsValid(out string errorMessage);
    }


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
