using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using AutoQC.Properties;
using SharedBatch;

namespace AutoQC
{
    public partial class InvalidConfigSetupForm : Form
    {
        // The Configuration Setup Manager Form
        // Allows users to correct file paths, R versions, and Skyline types of an invalid configuration.

        private readonly AutoQcConfig _invalidConfig;
        private readonly AutoQcConfigManager _configManager;
        private readonly IMainUiControl _mainControl;

        private string _lastInputPath; // the last user-entered file or folder path
        private string _oldRoot;
        private string _newRoot;

        private bool _askedAboutRootReplacement; // if the user has been asked about replacing path roots for this configuration

        public InvalidConfigSetupForm(AutoQcConfig invalidConfig, AutoQcConfigManager configManager, IMainUiControl mainControl, AutoQcConfigManagerState state)
        {
            InitializeComponent();
            State = state;
            _invalidConfig = invalidConfig;
            _configManager = configManager;
            _mainControl = mainControl;
            _oldRoot = string.Empty;
            _newRoot = string.Empty; 
            CreateValidConfig();
        }

        public AutoQcConfigManagerState State { get; private set; }

        public IValidatorControl CurrentControl { get; private set; } // for functional testing


        private async void CreateValidConfig()
        {
            // get valid settings
            var validMainSettings = await FixInvalidMainSettings();
            var validPanoramaSettings = await FixInvalidPanoramaSettings();
            var validSkylineSettings = await FixInvalidSkylineSettings();
            // create valid configuration
            var validConfig = new AutoQcConfig(_invalidConfig.Name, _invalidConfig.IsEnabled, _invalidConfig.Created, DateTime.Now,
                validMainSettings, validPanoramaSettings, validSkylineSettings);
            // replace old configuration
            State.ReplaceSelectedConfig(validConfig, _mainControl);
            _mainControl.UpdateUiConfigurations();
            CloseSetup();
        }

        #region Fix Configuration Settings

        private async Task<MainSettings> FixInvalidMainSettings()
        {
            var mainSettings = _invalidConfig.MainSettings;
            var validSkylinePath = await GetValidPath("Skyline file",
                mainSettings.SkylineFilePath, MainSettings.ValidateSkylineFile, TextUtil.FILTER_SKY, PathDialogOptions.File);
            var validFolderToWatch = await GetValidPath("folder to watch",
                mainSettings.FolderToWatch, MainSettings.ValidateFolderToWatch, null, PathDialogOptions.Folder);

            string validAnnotationsFilePath = null;
            if (mainSettings.HasAnnotationsFile())
            {
                validAnnotationsFilePath = await GetValidPath("Annotations file", mainSettings.AnnotationsFilePath,
                    MainSettings.ValidateAnnotationsFile, TextUtil.EXT_CSV, PathDialogOptions.File);
            }
            
            return new MainSettings(validSkylinePath, validFolderToWatch, mainSettings.IncludeSubfolders, mainSettings.QcFileFilter, mainSettings.RemoveResults, 
                mainSettings.ResultsWindow.ToString(), mainSettings.InstrumentType, mainSettings.AcquisitionTime.ToString(), validAnnotationsFilePath);
        }

        private async Task <PanoramaSettings> FixInvalidPanoramaSettings()
        {
            var panoramaSettings = _invalidConfig.PanoramaSettings;
            var validPanoramaSettings = await GetValidVariable(new PanoramaControl(panoramaSettings));
            return (PanoramaSettings)validPanoramaSettings;
        }




        private async Task<SkylineSettings> FixInvalidSkylineSettings()
        {
            var skylineTypeControl = new SkylineTypeControl(_mainControl, _invalidConfig.UsesSkyline, _invalidConfig.UsesSkylineDaily, _invalidConfig.UsesCustomSkylinePath, _invalidConfig.SkylineSettings.CmdPath, State.GetRunningConfigs(), State.BaseState);
            return (SkylineSettings)await GetValidVariable(skylineTypeControl);
        }

        #endregion

        #region Get Valid Variables

        private async Task<string> GetValidPath(string variableName, string invalidPath, Validator validator, string filter, params PathDialogOptions[] pathDialogOptions)
        {
            string path = TextUtil.TryReplaceStart(_oldRoot, _newRoot, invalidPath);

            var folderControl = new FilePathControl(variableName, path, _lastInputPath, validator, filter, pathDialogOptions);
            path = (string)await GetValidVariable(folderControl, false);

            if (path.Equals(invalidPath))
                return path;
            _lastInputPath = path;

            GetNewRoot(invalidPath, path);

            RemoveControl(folderControl);
            return path;
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
                    AlertDlg.ShowError(this, errorMessage);
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
            if (!_askedAboutRootReplacement && oldRoot.Length > 0 && !Directory.Exists(oldRoot))
            {
                var replaceRoot = AlertDlg.ShowQuestion(this, string.Format(Resources.InvalidConfigSetupForm_GetValidPath_Would_you_like_to_replace__0__with__1___, oldRoot, newRoot)) == DialogResult.Yes;
                _askedAboutRootReplacement = true;
                if (replaceRoot)
                {
                    _oldRoot = oldRoot;
                    _newRoot = newRoot;
                }
            }
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

        private void btnSkip_Click(object sender, EventArgs e)
        {
            CloseSetup();
        }

        private void CloseSetup()
        {
            DialogResult = DialogResult.OK;
            Close();
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
