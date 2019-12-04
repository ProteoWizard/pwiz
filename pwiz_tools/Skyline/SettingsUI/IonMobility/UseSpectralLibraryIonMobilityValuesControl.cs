/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.SettingsUI.IonMobility
{
    public partial class UseSpectralLibraryIonMobilityValuesControl : UserControl
    {

        public UseSpectralLibraryIonMobilityValuesControl()
        {
            InitializeComponent();
        }

        public void InitializeSettings(IModifyDocumentContainer documentContainer, bool? defaultState = null)
        {
            Prediction = documentContainer.Document.Settings.PeptideSettings.Prediction;

            var imsWindowCalc = Prediction.LibraryIonMobilityWindowWidthCalculator;
            var resolvingPower = imsWindowCalc?.ResolvingPower ?? 0;
            if ((defaultState ?? Prediction.UseLibraryIonMobilityValues) && resolvingPower == 0)
            {
                resolvingPower = 30; // Arbitrarily chosen non-zero value
            }
            cbUseSpectralLibraryIonMobilityValues.Checked = textSpectralLibraryIonMobilityValuesResolvingPower.Enabled = defaultState ?? Prediction.UseLibraryIonMobilityValues;
            if (imsWindowCalc != null)
            {
                cbLinear.Checked = imsWindowCalc.PeakWidthMode == IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.linear_range;
                if (cbLinear.Checked)
                {
                    textSpectralLibraryIonMobilityValuesResolvingPower.Text = string.Empty;
                    textSpectralLibraryIonMobilityWindowWidthAtDt0.Text = imsWindowCalc.PeakWidthAtIonMobilityValueZero.ToString(LocalizationHelper.CurrentCulture);
                    textSpectralLibraryIonMobilityWindowWidthAtDtMax.Text = imsWindowCalc.PeakWidthAtIonMobilityValueMax.ToString(LocalizationHelper.CurrentCulture);
                }
                else
                {
                    textSpectralLibraryIonMobilityValuesResolvingPower.Text = resolvingPower != 0
                        ? resolvingPower.ToString(LocalizationHelper.CurrentCulture)
                        : string.Empty;
                    textSpectralLibraryIonMobilityWindowWidthAtDt0.Text = string.Empty;
                    textSpectralLibraryIonMobilityWindowWidthAtDtMax.Text = string.Empty;
                }                
            }

            UpdateLibraryDriftPeakWidthControls();

        }

        public void HideControls()
        {
            foreach (var control in groupBoxUseSpectralLibraryIonMolbilityInfo.Controls.Cast<Control>())
            {
                if (control != cbUseSpectralLibraryIonMobilityValues &&
                    control != labelResolvingPower &&
                    control != textSpectralLibraryIonMobilityValuesResolvingPower)
                {
                    groupBoxUseSpectralLibraryIonMolbilityInfo.Controls.Remove(control);
                }
            }
            Height -= cbLinear.Height;
            groupBoxUseSpectralLibraryIonMolbilityInfo.Height -= cbLinear.Height;
        }

        public PeptidePrediction Prediction { get; set;} 

        public PeptidePrediction ValidateNewSettings(bool showMessages)
        {
            var helper = new MessageBoxHelper(ParentForm, showMessages);
	
	
            bool useLibraryDriftTime = cbUseSpectralLibraryIonMobilityValues.Checked;

            var libraryDriftTimeWindowWidthCalculator = IonMobilityWindowWidthCalculator.EMPTY;
            if (useLibraryDriftTime)
            {
                double resolvingPower = 0;
                double widthAtDt0 = 0;
                double widthAtDtMax = 0;
                IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType peakWidthType;
                if (cbLinear.Checked)
                {
                    if (!helper.ValidateDecimalTextBox(textSpectralLibraryIonMobilityWindowWidthAtDt0, out widthAtDt0))
                        return null;
                    if (!helper.ValidateDecimalTextBox(textSpectralLibraryIonMobilityWindowWidthAtDtMax, out widthAtDtMax))
                        return null;
                    var errmsg = EditDriftTimePredictorDlg.ValidateWidth(widthAtDt0);
                    if (errmsg != null)
                    {
                        helper.ShowTextBoxError(textSpectralLibraryIonMobilityWindowWidthAtDt0, errmsg);
                        return null;
                    }
                    errmsg = EditDriftTimePredictorDlg.ValidateWidth(widthAtDtMax);
                    if (errmsg != null)
                    {
                        helper.ShowTextBoxError(textSpectralLibraryIonMobilityWindowWidthAtDtMax, errmsg);
                        return null;
                    }
                    peakWidthType = IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.linear_range;
                }
                else
                {
                    if (!helper.ValidateDecimalTextBox(textSpectralLibraryIonMobilityValuesResolvingPower, out resolvingPower))
                        return null;
                    var errmsg = EditDriftTimePredictorDlg.ValidateResolvingPower(resolvingPower);
                    if (errmsg != null)
                    {
                        helper.ShowTextBoxError(textSpectralLibraryIonMobilityValuesResolvingPower, errmsg);
                        return null;
                    }
                    peakWidthType = IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.resolving_power;
                }
                libraryDriftTimeWindowWidthCalculator = new IonMobilityWindowWidthCalculator(peakWidthType, resolvingPower, widthAtDt0, widthAtDtMax);
            }

            return Prediction.ChangeLibraryDriftTimesWindowWidthCalculator(libraryDriftTimeWindowWidthCalculator).ChangeUseLibraryIonMobilityValues(useLibraryDriftTime);
        }

        private void UpdateLibraryDriftPeakWidthControls()
        {
            // Linear peak width vs Resolving Power
            labelResolvingPower.Visible = !cbLinear.Checked;
            textSpectralLibraryIonMobilityValuesResolvingPower.Visible = !cbLinear.Checked;
            labelWidthIMZero.Visible = cbLinear.Checked;
            labelWidthIMMax.Visible = cbLinear.Checked;
            textSpectralLibraryIonMobilityWindowWidthAtDt0.Visible = cbLinear.Checked;
            textSpectralLibraryIonMobilityWindowWidthAtDtMax.Visible = cbLinear.Checked;

            cbLinear.Enabled = cbUseSpectralLibraryIonMobilityValues.Checked;
            labelResolvingPower.Enabled = cbUseSpectralLibraryIonMobilityValues.Checked;
            textSpectralLibraryIonMobilityValuesResolvingPower.Enabled = cbUseSpectralLibraryIonMobilityValues.Checked;
            labelWidthIMZero.Enabled = cbUseSpectralLibraryIonMobilityValues.Checked;
            labelWidthIMMax.Enabled = cbUseSpectralLibraryIonMobilityValues.Checked;
            textSpectralLibraryIonMobilityWindowWidthAtDt0.Enabled = cbUseSpectralLibraryIonMobilityValues.Checked;
            textSpectralLibraryIonMobilityWindowWidthAtDtMax.Enabled = cbUseSpectralLibraryIonMobilityValues.Checked;


            if (labelWidthIMZero.Location.X > labelResolvingPower.Location.X)
            {
                var dX = labelWidthIMZero.Location.X - labelResolvingPower.Location.X;
                labelWidthIMZero.Location = new Point(labelWidthIMZero.Location.X - dX, labelWidthIMZero.Location.Y);
                labelWidthIMMax.Location = new Point(labelWidthIMMax.Location.X - dX, labelWidthIMMax.Location.Y);
                textSpectralLibraryIonMobilityWindowWidthAtDt0.Location = new Point(textSpectralLibraryIonMobilityWindowWidthAtDt0.Location.X - dX, textSpectralLibraryIonMobilityWindowWidthAtDt0.Location.Y);
                textSpectralLibraryIonMobilityWindowWidthAtDtMax.Location = new Point(textSpectralLibraryIonMobilityWindowWidthAtDtMax.Location.X - dX, textSpectralLibraryIonMobilityWindowWidthAtDtMax.Location.Y);
            }
        }

        private void cbLinear_CheckedChanged(object sender, EventArgs e)
        {
            UpdateLibraryDriftPeakWidthControls();
        }

        #region for testing

        public void SetUseSpectralLibraryDriftTimes(bool state)
        {
            cbUseSpectralLibraryIonMobilityValues.Checked = state;
            UpdateLibraryDriftPeakWidthControls();
        }

        public void SetResolvingPowerText(string rp)
        {
            textSpectralLibraryIonMobilityValuesResolvingPower.Text = rp;
            UpdateLibraryDriftPeakWidthControls();
        }

        public void SetResolvingPower(double rp)
        {
            textSpectralLibraryIonMobilityValuesResolvingPower.Text = rp.ToString(LocalizationHelper.CurrentCulture);
            UpdateLibraryDriftPeakWidthControls();
        }

        public void SetWidthAtDtZero(double width)
        {
            textSpectralLibraryIonMobilityWindowWidthAtDt0.Text = width.ToString(LocalizationHelper.CurrentCulture);
            UpdateLibraryDriftPeakWidthControls();
        }

        public void SetWidthAtDtMax(double width)
        {
            textSpectralLibraryIonMobilityWindowWidthAtDtMax.Text = width.ToString(LocalizationHelper.CurrentCulture);
            UpdateLibraryDriftPeakWidthControls();
        }

        public void SetLinearRangeCheckboxState(bool checkedState)
        {
            cbLinear.Checked = checkedState;
            UpdateLibraryDriftPeakWidthControls();
        }

        #endregion

        private void cbUseSpectralLibraryIonMobilityValues_CheckChanged(object sender, EventArgs e)
        {
            bool enable = cbUseSpectralLibraryIonMobilityValues.Checked;
            labelResolvingPower.Enabled =
                textSpectralLibraryIonMobilityValuesResolvingPower.Enabled = enable;
            labelWidthIMZero.Enabled =
                textSpectralLibraryIonMobilityWindowWidthAtDt0.Enabled = enable;
            labelWidthIMMax.Enabled =
                textSpectralLibraryIonMobilityWindowWidthAtDtMax.Enabled = enable;
            cbLinear.Enabled = enable;
            // If disabling the text box, and it has content, make sure it is
            // valid content.  Otherwise, clear the current content, which
            // is always valid, if the measured ion mobility values will not be used.
            CleanupIonMobilityInfoText(enable, textSpectralLibraryIonMobilityValuesResolvingPower);
            CleanupIonMobilityInfoText(enable, textSpectralLibraryIonMobilityWindowWidthAtDt0);
            CleanupIonMobilityInfoText(enable, textSpectralLibraryIonMobilityWindowWidthAtDtMax);
        }

        private static void CleanupIonMobilityInfoText(bool enable, TextBox textBox)
        {
            if (!enable && !string.IsNullOrEmpty(textBox.Text))
            {
                double value;
                if (!double.TryParse(textBox.Text, out value) || value <= 0)
                {
                    textBox.Text = string.Empty;
                }
            }
        }

    }
}
