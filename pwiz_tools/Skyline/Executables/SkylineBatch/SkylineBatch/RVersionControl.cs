using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SharedBatch;
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
        private string _oldVersion; // the old R version, initially invalid
        private bool _hasRInstalled; // if R is installed

        private readonly RDirectorySelector _rDirectorySelector;

        public RVersionControl(string scriptName, string oldVersion, RDirectorySelector rDirectorySelector)
        {
            InitializeComponent();
            
            _hasRInstalled = Settings.Default.RVersions.Keys.Count > 0;
            _oldVersion = oldVersion;

            _version = _hasRInstalled ? oldVersion : null;
            _rDirectorySelector = rDirectorySelector;

            labelTitle.Text = _hasRInstalled ? string.Format(Resources.RVersionControl_RVersionControl_R_version__0__not_found_, oldVersion) :
                Resources.RVersionControl_RVersionControl_Could_not_find_any_R_installations_on_this_computer_;
            labelMessage.Text = _hasRInstalled ? string.Format(Resources.RVersionControl_RVersionControl_Select_an_R_version_for__0__, Path.GetFileName(scriptName)) :
                Resources.RVersionControl_RVersionControl_Please_add_an_R_installation_directory_;

            if (_hasRInstalled)
            {
                UpdateComboRVersions();
            }
            else
            {
                comboRVersions.Hide();
            }
        }

        public object GetVariable() => _version;

        public bool IsValid(out string errorMessage)
        {
            errorMessage = null;
            if (_version == null)
            {
                errorMessage = Resources.RVersionControl_IsValid_No_R_version_selected__Please_choose_an_R_version_;
                return false;
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

        private void UpdateComboRVersions()
        {
            comboRVersions.Items.Clear();
            var sortedRVersions = Settings.Default.RVersions.Keys.ToList();
            sortedRVersions.Sort();
            foreach (var version in sortedRVersions)
                comboRVersions.Items.Add(version);
        }

        private void comboRVersions_SelectedIndexChanged(object sender, EventArgs e)
        {
            _version = (string)comboRVersions.SelectedItem;
        }

        private void btnAddDirectory_Click(object sender, EventArgs e)
        {
            if (_rDirectorySelector.ShowAddDirectoryDialog())
            {
                UpdateComboRVersions();
                comboRVersions.Show();
                if (Settings.Default.RVersions.ContainsKey(_oldVersion))
                    comboRVersions.SelectedItem = _oldVersion;
            }
        }

        public void SetInput(object value)
        {
            if (comboRVersions.Items.Contains(value))
            {
                comboRVersions.SelectedIndex = comboRVersions.Items.IndexOf((string)value);
            }
            else
            {
                throw new Exception($"Test could not set R version. {value} not found in list.");
            }
        }
    }
}
