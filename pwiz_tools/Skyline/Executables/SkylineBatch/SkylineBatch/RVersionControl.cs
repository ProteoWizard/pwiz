using System;
using System.IO;
using System.Windows.Forms;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class RVersionControl : UserControl, IValidatorControl
    {

        private string _version;
        private bool _hasRInstalled;
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
