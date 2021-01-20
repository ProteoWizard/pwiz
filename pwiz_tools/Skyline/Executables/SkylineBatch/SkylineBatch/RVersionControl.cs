using System;
using System.IO;
using System.Windows.Forms;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class RVersionControl : UserControl, IValidatorControl
    {

        private string _version;
        public RVersionControl(string scriptName, string oldVersion)
        {
            InitializeComponent();

            _version = oldVersion;
            labelTitle.Text = string.Format(Resources.RVersionControl_R__0__Not_Found, oldVersion);
            labelMessage.Text = string.Format(Resources.RVersionControl_Select_an_R_version_for__0___,
                Path.GetFileName(scriptName));
            foreach (var version in Settings.Default.RVersions.Keys)
            {
                comboRVersions.Items.Add(version);
            }
        }

        public object GetVariable() => _version;

        public bool IsValid(out string errorMessage)
        {
            errorMessage = null;
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
