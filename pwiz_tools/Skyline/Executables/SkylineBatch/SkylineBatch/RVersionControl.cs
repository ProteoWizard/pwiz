using System;
using System.IO;
using System.Windows.Forms;
using SharedAutoQcBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class RVersionControl : UserControl, IValidatorControl
    {
        // A control used by the InvalidConfigSetupForm to either:
        //      switch to an R Installation that exists on this computer if R is installed, or
        //      ask the user if they would like to remove R scripts from this configuration if R is not installed

        // Implements IValidatorControl:
        //    - GetVariable() returns the currently selected R version, or null if none are installed
        //    - IsValid() uses ReportInfo.ValidateRVersion to determine if the selected version is valid

        private string _version; // a string representing the currently selected R version. It is null iff there are no R installations
        private bool _hasRInstalled; // if R is installed
        public RVersionControl(string scriptName, string oldVersion, bool removeRScripts)
        {
            InitializeComponent();
            
            _hasRInstalled = Settings.Default.RVersions.Keys.Count > 0;
            _version = !_hasRInstalled && removeRScripts ? null : oldVersion;
            
            labelTitle.Text = _hasRInstalled ? string.Format(Resources.RVersionControl_RVersionControl_R_version__0__not_found_, oldVersion) :
                Resources.RVersionControl_RVersionControl_Could_not_find_any_R_installations_on_this_computer_;
            labelMessage.Text = _hasRInstalled ? string.Format(Resources.RVersionControl_RVersionControl_Select_an_R_version_for__0__, Path.GetFileName(scriptName)) :
                Resources.RVersionControl_RVersionControl_Click_next_to_remove_R_scripts_from_this_configuration_;
            foreach (var version in Settings.Default.RVersions.Keys)
            {
                comboRVersions.Items.Add(version);
            }
            // hides R version comboBox if there are no R versions
            if (!_hasRInstalled)
                comboRVersions.Hide();
        }

        public object GetVariable() => _version;

        public bool IsValid(out string errorMessage)
        {
            errorMessage = null;
            if (!_hasRInstalled)
            {
                var valid = _version == null;
                // if R is not installed, the R version is only invalid the first time IsValid is called
                // this allows the user to choose to remove R scripts
                _version = null;
                return valid;
            }
            try
            {
                ReportInfo.ValidateRVersion(_version);
                return true;
            }
            catch (ArgumentException e)
            {
                errorMessage = e.Message;
                return false;
            }
            
        }

        private void comboRVersions_SelectedIndexChanged(object sender, EventArgs e)
        {
            _version = (string)comboRVersions.SelectedItem;
        }
    }
}
