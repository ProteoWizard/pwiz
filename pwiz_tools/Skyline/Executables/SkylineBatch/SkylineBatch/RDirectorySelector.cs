using System;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class RDirectorySelector
    {

        private readonly IMainUiControl _mainUiControl;

        public RDirectorySelector(IMainUiControl mainUiControl)
        {
            _mainUiControl = mainUiControl;
        }

        public SkylineBatchConfigManagerState State { get; private set; }
        public bool ShownDialog { get; private set; }

        public void AddIfNecassary(SkylineBatchConfigManagerState state, out bool stateChanged)
        {
            stateChanged = false;
            var configurationRVersions = state.RVersionsUsed();
            if (configurationRVersions.IsSubsetOf(Settings.Default.RVersions.Keys))
                return; // do not show dialog if no configuration is invalid due to an R version that wasn't found

            configurationRVersions.ExceptWith(Settings.Default.RVersions.Keys);
            ShownDialog = true;
            var addDirectory = DialogResult.Yes == _mainUiControl.DisplayQuestion(
                string.Format(Resources.RDirectorySelector_AddIfNecassary_The_following_R_installations_were_not_found_by__0__, Program.AppName()) + Environment.NewLine +
                TextUtil.LineSeparate(configurationRVersions) + Environment.NewLine +
                Resources.RDirectorySelector_AddIfNecassary_Would_you_like_to_add_an_R_installation_directory_);
            if (addDirectory)
                stateChanged = ShowAddDirectoryDialog(state);
        }

        public bool RequiredDirectoryAdded(SkylineBatchConfigManagerState state)
        {
            _mainUiControl.DisplayError(
                "No R installations were found in the following directories:" + Environment.NewLine +
                TextUtil.LineSeparate(Settings.Default.RDirs) + Environment.NewLine +
                "Please add an R installation directory to continue.");
            return ShowAddDirectoryDialog(state);
        }
        
        public bool ShowAddDirectoryDialog(SkylineBatchConfigManagerState state)
        {
            State = state;
            // TODO(Ali): handle R installation after dialog appears
            var dialog = new FolderBrowserDialog();
            var initialPath = Settings.Default.RDirs.Count > 0 ? FileUtil.GetInitialDirectory(Settings.Default.RDirs[Settings.Default.RDirs.Count - 1]) : null;
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
                    State.UpdateConfigValidation();
                else
                    return ShowAddDirectoryDialog(State); // Show dialog again if there was an error
            }

            return directoryAdded;
        }

    }
}
