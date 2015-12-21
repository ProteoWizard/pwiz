using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditCoVDlg : FormEx
    {
        private CompensationVoltageParameters _parameters;  
        private readonly IEnumerable<CompensationVoltageParameters> _existing;

        public EditCoVDlg(CompensationVoltageParameters covParams, IEnumerable<CompensationVoltageParameters> existing)
        {
            InitializeComponent();

            _existing = existing;
            Parameters = covParams;
        }

        public CompensationVoltageParameters Parameters
        {
            get { return _parameters; }
            set
            {
                _parameters = value;
                UpdateUI(_parameters);
            }
        }

        public double? Min
        {
            get
            {
                double min;
                return double.TryParse(textMin.Text, out min) ? (double?)min : null;
            }
            set { textMin.Text = value.HasValue ? value.Value.ToString(CultureInfo.CurrentCulture) : string.Empty; }
        }

        public double? Max
        {
            get
            {
                double max;
                return double.TryParse(textMax.Text, out max) ? (double?)max : null;
            }
            set { textMax.Text = value.HasValue ? value.Value.ToString(CultureInfo.CurrentCulture) : string.Empty; }
        }

        public int? StepsRough
        {
            get
            {
                int steps;
                return int.TryParse(textStepsRough.Text, out steps) ? (int?)steps : null;
            }
            set { textStepsRough.Text = value.HasValue ? value.Value.ToString(CultureInfo.CurrentCulture) : string.Empty; }
        }

        public int? StepsMedium
        {
            get
            {
                int steps;
                return int.TryParse(textStepsMedium.Text, out steps) ? (int?)steps : null;
            }
            set { textStepsMedium.Text = value.HasValue ? value.Value.ToString(CultureInfo.CurrentCulture) : string.Empty; }
        }

        public int? StepsFine
        {
            get
            {
                int steps;
                return int.TryParse(textStepsFine.Text, out steps) ? (int?)steps : null;
            }
            set { textStepsFine.Text = value.HasValue ? value.Value.ToString(CultureInfo.CurrentCulture) : string.Empty; }
        }

        private void UpdateUI(CompensationVoltageParameters covParams)
        {
            if (covParams != null)
            {
                var culture = LocalizationHelper.CurrentCulture;
                textName.Text = covParams.Name;
                textMin.Text = covParams.MinCov.ToString(culture);
                textMax.Text = covParams.MaxCov.ToString(culture);
                textStepsRough.Text = covParams.StepCountRough.ToString(culture);
                textStepsMedium.Text = covParams.StepCountMedium.ToString(culture);
                textStepsFine.Text = covParams.StepCountFine.ToString(culture);
            }
        }

        private void btnOk_Click(object sender, System.EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(textName, out name))
                return;

            if (_existing.Contains(r => !ReferenceEquals(_parameters, r) && Equals(name, r.Name)))
            {
                helper.ShowTextBoxError(textName, Resources.EditCoVDlg_btnOk_Click_The_compensation_voltage_parameters___0___already_exist_, name);
                return;
            }

            double covMin;
            if (!helper.ValidateDecimalTextBox(textMin, 0, null, out covMin))
                return;

            double covMax;
            if (!helper.ValidateDecimalTextBox(textMax, 0, null, out covMax))
                return;

            if (covMax < covMin)
            {
                helper.ShowTextBoxError(textMax, Resources.EditCoVDlg_btnOk_Click_Maximum_compensation_voltage_cannot_be_less_than_minimum_compensation_volatage_);
                return;
            }

            int stepCountRough;
            if (!helper.ValidateNumberTextBox(textStepsRough, CompensationVoltageParameters.MIN_STEP_COUNT, CompensationVoltageParameters.MAX_STEP_COUNT, out stepCountRough))
                return;

            int stepCountMedium;
            if (!helper.ValidateNumberTextBox(textStepsMedium, CompensationVoltageParameters.MIN_STEP_COUNT, CompensationVoltageParameters.MAX_STEP_COUNT, out stepCountMedium))
                return;

            int stepCountFine;
            if (!helper.ValidateNumberTextBox(textStepsFine, CompensationVoltageParameters.MIN_STEP_COUNT, CompensationVoltageParameters.MAX_STEP_COUNT, out stepCountFine))
                return;

            _parameters = new CompensationVoltageParameters(name, covMin, covMax, stepCountRough, stepCountMedium, stepCountFine);
            DialogResult = DialogResult.OK;
        }
    }
}
