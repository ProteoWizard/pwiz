using System;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class RDirectorySelector
    {

        private readonly IMainUiControl _mainUiControl;
        private readonly SkylineBatchConfigManager _configManager;

        public RDirectorySelector(IMainUiControl mainUiControl, SkylineBatchConfigManager configManager)
        {
            _mainUiControl = mainUiControl;
            _configManager = configManager;
        }

        public bool ShownDialog { get; private set; }

        public void AddIfNecassary()
        {
            var configurationRVersions = _configManager.RVersionsUsed();
            if (configurationRVersions.IsSubsetOf(Settings.Default.RVersions.Keys))
                return; // do not show dialog if no configuration is invalid due to an R version that wasn't found

            configurationRVersions.ExceptWith(Settings.Default.RVersions.Keys);
            ShownDialog = true;
            var addDirectory = DialogResult.Yes == _mainUiControl.DisplayQuestion(
                string.Format("The following R installations were not found by {0}:", Program.AppName()) + Environment.NewLine +
                TextUtil.LineSeparate(configurationRVersions) + Environment.NewLine +
                "Would you like to add an R installation directory?");
            if (addDirectory)
                ShowAddDirectoryDialog();
        }

        public bool RequiredDirectoryAdded()
        {
            _mainUiControl.DisplayError(
                "No R installations were found in the following directories:" + Environment.NewLine +
                TextUtil.LineSeparate(Settings.Default.RDirs) + Environment.NewLine +
                "Please add an R installation directory to continue.");
            return ShowAddDirectoryDialog();
        }
        
        public bool ShowAddDirectoryDialog()
        {
            var dialog = new FolderBrowserDialog();
            var initialPath = Settings.Default.RDirs.Count > 0 ? TextUtil.GetInitialDirectory(Settings.Default.RDirs[Settings.Default.RDirs.Count - 1]) : null;
            dialog.SelectedPath = initialPath;
            var directoryAdded = false;
            DialogResult result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                try
                {
                    RInstallations.AddRDirectory(dialog.SelectedPath);
                    directoryAdded = true;
                }
                catch (ArgumentException e)
                {
                    _mainUiControl.DisplayError(e.Message);
                }

                if (directoryAdded)
                    _configManager.UpdateConfigValidation();
                else
                    return ShowAddDirectoryDialog(); // Show dialog again if there was an error
            }

            return directoryAdded;
        }

    }
}
