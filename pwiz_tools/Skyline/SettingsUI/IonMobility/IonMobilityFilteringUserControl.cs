/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.SettingsUI.IonMobility
{
    public partial class IonMobilityFilteringUserControl : UserControl
    {
        private SettingsListComboDriver<IonMobilityLibrarySpec> _driverIonMobilityLib;
        private TransitionIonMobilityFiltering _ionMobilityFiltering { get; set; }

        public IonMobilityFilteringUserControl()
        {
            InitializeComponent();
            UpdateIonMobilityFilterWindowWidthControls();
        }

        public void InitializeSettings(IModifyDocumentContainer documentContainer, bool? defaultState = null)
        {

            _ionMobilityFiltering = documentContainer.Document.Settings.TransitionSettings.IonMobilityFiltering;

            _driverIonMobilityLib = new SettingsListComboDriver<IonMobilityLibrarySpec>(comboIonMobilityLibrary, Settings.Default.IonMobilityLibraryList);
            string selDT = (_ionMobilityFiltering.IonMobilityLibrary == null ? null : _ionMobilityFiltering.IonMobilityLibrary.Name);
            _driverIonMobilityLib.LoadList(selDT);

            cbUseSpectralLibraryIonMobilities.Checked = textIonMobilityFilterResolvingPower.Enabled =
                defaultState ?? _ionMobilityFiltering.UseSpectralLibraryIonMobilityValues; 
            var imsWindowCalc = _ionMobilityFiltering.FilterWindowWidthCalculator;

            var resolvingPower = imsWindowCalc?.ResolvingPower ?? 0;
            if ((defaultState ?? _ionMobilityFiltering.UseSpectralLibraryIonMobilityValues) && resolvingPower == 0)
            {
                resolvingPower = 30; // Arbitrarily chosen non-zero value
            }

            if (imsWindowCalc != null)
            {
                var mode = imsWindowCalc.WindowWidthMode == IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.none
                    ? IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power
                    : imsWindowCalc.WindowWidthMode;
                comboBoxWindowType.SelectedIndex = (int)mode;
                textIonMobilityFilterResolvingPower.Text = resolvingPower.ToString(LocalizationHelper.CurrentCulture);
                textIonMobilityFilterWidthAtMobility0.Text = imsWindowCalc.PeakWidthAtIonMobilityValueZero.ToString(LocalizationHelper.CurrentCulture);
                textIonMobilityFilterWidthAtMobilityMax.Text = imsWindowCalc.PeakWidthAtIonMobilityValueMax.ToString(LocalizationHelper.CurrentCulture);
                textIonMobilityFilterFixedWidth.Text = imsWindowCalc.FixedWindowWidth.ToString(LocalizationHelper.CurrentCulture);
            }

            UpdateIonMobilityFilterWindowWidthControls();
        }

        // For use in import search wizard, where we want to show a very simple interface
        public void ShowOnlyResolvingPowerControls(int groupBoxWidth)
        {
            // Assume that resolving power is the proper choice of window width calculation
            comboBoxWindowType.SelectedIndex = (int)IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power;
            // Hide other width calculator types, hide ion mobility library chooser
            var retainedControls = new Control[]
            {
                cbUseSpectralLibraryIonMobilities,
                labelResolvingPower,
                textIonMobilityFilterResolvingPower
            };

            // Hide any controls not needed, rearrange the rest to close the gaps
            var controls = groupBoxIonMobilityFiltering.Controls.Cast<Control>().
                OrderBy(c => c.Top).ToArray();
            var overlappingControls = controls.Where(c => retainedControls.Any(r => Math.Abs(r.Top-c.Top) < 5)).ToArray();
            var margin = controls[0].Top;

            Control lastVisible = controls[0];
            for (var i = 0; i < controls.Length; i++)
            {
                var control = controls[i];
                if (retainedControls.Contains(control))
                {
                    control.Visible = control.Enabled = true;
                    lastVisible = control;
                }
                else
                {
                    control.Visible = control.Enabled = false;
                    if (!overlappingControls.Contains(control))
                    {
                        var gap = (i < controls.Length - 1) ? controls[i + 1].Top - control.Top : control.Height;
                        for (var j = i + 1; j < controls.Length; j++)
                        {
                            var controlNext = controls[j];
                            controlNext.Location = new Point(controlNext.Location.X, controlNext.Location.Y - gap);
                        }
                    }
                }
            }

            groupBoxIonMobilityFiltering.Height = lastVisible.Bottom + margin;
            Height = groupBoxIonMobilityFiltering.Height;
            groupBoxIonMobilityFiltering.Width = groupBoxWidth;
        }

        public void CheckDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(@"Form disposed");
            }
        }

        #region for testing

        public string SelectedIonMobilityLibrary
        {
            get { return _driverIonMobilityLib.Combo.SelectedItem.ToString(); }
            set
            {
                foreach (var item in _driverIonMobilityLib.Combo.Items)
                {
                    if (Equals(value, item.ToString()))
                    {
                        _driverIonMobilityLib.Combo.SelectedItem = item;
                        return;
                    }
                }
                _driverIonMobilityLib.Combo.Items.Add(value);
                _driverIonMobilityLib.Combo.SelectedItem = value;
            }
        }

        public void AddIonMobilityLibrary()
        {
            CheckDisposed();
            var list = Settings.Default.IonMobilityLibraryList;
            var libNew = list.EditItem(this, null, list, null);
            if (libNew != null)
            {
                list.SetValue(libNew);
                SelectedIonMobilityLibrary = libNew.Name;
            }
        }

        public void EditIonMobilityLibrary()
        {
            var list = Settings.Default.IonMobilityLibraryList;
            var libNew = list.EditItem(this, _driverIonMobilityLib.SelectedItem, list, null);
            if (libNew != null)
            {
                list.SetValue(libNew);
                SelectedIonMobilityLibrary = libNew.Name;
            }
        }

        public void SetUseSpectralLibraryIonMobilities(bool state)
        {
            cbUseSpectralLibraryIonMobilities.Checked = state;
            UpdateIonMobilityFilterWindowWidthControls();
        }

        public void SetResolvingPowerText(string rp)
        {
            textIonMobilityFilterResolvingPower.Text = rp;
            UpdateIonMobilityFilterWindowWidthControls();
        }

        public void SetResolvingPower(double rp)
        {
            textIonMobilityFilterResolvingPower.Text = rp.ToString(LocalizationHelper.CurrentCulture);
            UpdateIonMobilityFilterWindowWidthControls();
        }

        public void SetWidthAtIonMobilityZero(double width)
        {
            textIonMobilityFilterWidthAtMobility0.Text = width.ToString(LocalizationHelper.CurrentCulture);
            UpdateIonMobilityFilterWindowWidthControls();
        }

        public void SetWidthAtIonMobilityMax(double width)
        {
            textIonMobilityFilterWidthAtMobilityMax.Text = width.ToString(LocalizationHelper.CurrentCulture);
            UpdateIonMobilityFilterWindowWidthControls();
        }

        public void SetFixedWidth(double width)
        {
            textIonMobilityFilterFixedWidth.Text = width.ToString(LocalizationHelper.CurrentCulture);
            UpdateIonMobilityFilterWindowWidthControls();
        }

        public void SetWindowWidthType(IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType widthType)
        {
            comboBoxWindowType.SelectedIndex = (int)widthType;
            UpdateIonMobilityFilterWindowWidthControls();
        }

        public bool IsUseSpectralLibraryIonMobilities
        {
            get { return cbUseSpectralLibraryIonMobilities.Checked; }
            set { cbUseSpectralLibraryIonMobilities.Checked = value; }
        }

        public double? IonMobilityFilterResolvingPower
        {
            get
            {

                if (string.IsNullOrEmpty(textIonMobilityFilterResolvingPower.Text))
                    return null;
                return Convert.ToDouble(textIonMobilityFilterResolvingPower.Text);
            }
            set
            {
                textIonMobilityFilterResolvingPower.Text = value.HasValue ? value.ToString() : string.Empty;
            }
        }

        #endregion


        private void cbUseSpectralLibraryIonMobilities_CheckChanged(object sender, EventArgs e)
        {
            UpdateIonMobilityFilterWindowWidthControls();
        }

        private void CleanupDriftInfoText(bool enable, TextBox textBox)
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

        private void UpdateIonMobilityFilterWindowWidthControls()
        {
            // Linear peak width vs Resolving Power vs fixed width
            labelResolvingPower.Visible = textIonMobilityFilterResolvingPower.Visible = 
                comboBoxWindowType.SelectedIndex == (int)IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power;
            labelWidthAtMobilityZero.Visible = labelWidthAtMobilityMax.Visible = 
                textIonMobilityFilterWidthAtMobility0.Visible = textIonMobilityFilterWidthAtMobilityMax.Visible = 
                    comboBoxWindowType.SelectedIndex == (int)IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.linear_range;
            labelFixedWidth.Visible = textIonMobilityFilterFixedWidth.Visible =
                comboBoxWindowType.SelectedIndex == (int)IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.fixed_width;

            if (labelWidthAtMobilityZero.Location.X > labelResolvingPower.Location.X)
            {
                var dX = labelWidthAtMobilityZero.Location.X - labelResolvingPower.Location.X;
                labelWidthAtMobilityZero.Location = new Point(labelWidthAtMobilityZero.Location.X - dX, labelWidthAtMobilityZero.Location.Y);
                labelWidthAtMobilityMax.Location = new Point(labelWidthAtMobilityMax.Location.X - dX, labelWidthAtMobilityMax.Location.Y);
                textIonMobilityFilterWidthAtMobility0.Location = new Point(textIonMobilityFilterWidthAtMobility0.Location.X - dX, textIonMobilityFilterWidthAtMobility0.Location.Y);
                textIonMobilityFilterWidthAtMobilityMax.Location = new Point(textIonMobilityFilterWidthAtMobilityMax.Location.X - dX, textIonMobilityFilterWidthAtMobilityMax.Location.Y);

                dX = labelFixedWidth.Location.X - labelResolvingPower.Location.X;
                labelFixedWidth.Location = new Point(labelFixedWidth.Location.X - dX, labelFixedWidth.Location.Y);
                textIonMobilityFilterFixedWidth.Location = new Point(textIonMobilityFilterFixedWidth.Location.X - dX, textIonMobilityFilterFixedWidth.Location.Y);
            }

            var library = SelectedIonMobilityLibrarySpec();
            var enable = cbUseSpectralLibraryIonMobilities.Checked ||
                         comboIonMobilityLibrary.Visible && (library != null && !library.IsNone);
            labelResolvingPower.Enabled = textIonMobilityFilterResolvingPower.Enabled = enable;
            labelWidthAtMobilityZero.Enabled = textIonMobilityFilterWidthAtMobility0.Enabled = enable;
            labelWidthAtMobilityMax.Enabled = textIonMobilityFilterWidthAtMobilityMax.Enabled = enable;
            labelFixedWidth.Enabled = textIonMobilityFilterFixedWidth.Enabled = enable;
            labelWindowType.Enabled = comboBoxWindowType.Enabled = enable;

            // If disabling the text box, and it has content, make sure it is
            // valid content.  Otherwise, clear the current content, which
            // is always valid, if the measured drift time values will not be used.
            CleanupDriftInfoText(enable, textIonMobilityFilterResolvingPower);
            CleanupDriftInfoText(enable, textIonMobilityFilterWidthAtMobility0);
            CleanupDriftInfoText(enable, textIonMobilityFilterWidthAtMobilityMax);
            CleanupDriftInfoText(enable, textIonMobilityFilterFixedWidth);

            try
            {
                var helper = new MessageBoxHelper(this.ParentForm, false);
                var result = ValidateIonMobilitySettings(helper);
                if (result != null)
                    _ionMobilityFiltering = result;
            }
            catch (Exception e)
            {
                Alerts.MessageDlg.ShowException(this, e);
            }
        }

        private void comboBoxWindowType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateIonMobilityFilterWindowWidthControls();
        }

        private void comboIonMobilityLibrary_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_driverIonMobilityLib.SelectedIndexChangedEvent(sender, e))
                IonMobilityLibraryChanged();
        }

        private void IonMobilityLibraryChanged()
        {
            var lib = _driverIonMobilityLib.SelectedItem;
            if (lib != null)
            {
                try
                {
                    _ionMobilityFiltering = _ionMobilityFiltering.ChangeLibrary(lib);
                }
                catch (Exception e)
                {
                    Alerts.MessageDlg.ShowException(this, e);
                }
            }
            UpdateIonMobilityFilterWindowWidthControls();
        }

        public TransitionIonMobilityFiltering ValidateIonMobilitySettings(bool showMessages)
        {
            var helper = new MessageBoxHelper(ParentForm, showMessages);
            if (!ValidateIonMobilitySettings(helper, out var result))
                return null;
            return result;
        }

        public bool ValidateIonMobilitySettings(MessageBoxHelper helper, out TransitionIonMobilityFiltering result)
        {
            result = ValidateIonMobilitySettings(helper);
            return result != null;
        }

        private TransitionIonMobilityFiltering ValidateIonMobilitySettings(MessageBoxHelper helper)
        { 
            var ionMobilityLibrarySpec = SelectedIonMobilityLibrarySpec();

            var useSpectralLibraryIonMobilities = cbUseSpectralLibraryIonMobilities.Checked;

            var ionMobilityWindowWidthCalculator = IonMobilityWindowWidthCalculator.EMPTY;
            if (useSpectralLibraryIonMobilities)
            {
                double resolvingPower = 0;
                double widthAtDt0 = 0;
                double widthAtDtMax = 0;
                double fixedPeakWidth = 0;
                var peakWidthType = (IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType) comboBoxWindowType.SelectedIndex;
                string errmsg;
                switch (peakWidthType)
                {
                    case IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.linear_range:
                        if (!helper.ValidateDecimalTextBox(textIonMobilityFilterWidthAtMobility0, out widthAtDt0))
                            return null;
                        if (!helper.ValidateDecimalTextBox(textIonMobilityFilterWidthAtMobilityMax, out widthAtDtMax))
                            return null;
                        errmsg = ValidateWidth(widthAtDt0);
                        if (errmsg != null)
                        {
                            helper.ShowTextBoxError(textIonMobilityFilterWidthAtMobility0, errmsg);
                            return null;
                        }

                        errmsg = ValidateWidth(widthAtDtMax);
                        if (errmsg != null)
                        {
                            helper.ShowTextBoxError(textIonMobilityFilterWidthAtMobilityMax, errmsg);
                            return null;
                        }

                        break;
                    case IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power:
                        if (!helper.ValidateDecimalTextBox(textIonMobilityFilterResolvingPower, out resolvingPower))
                            return null;
                        errmsg = ValidateResolvingPower(resolvingPower);
                        if (errmsg != null)
                        {
                            helper.ShowTextBoxError(textIonMobilityFilterResolvingPower, errmsg);
                            return null;
                        }

                        break;
                    default: // Fixed width
                        if (!helper.ValidateDecimalTextBox(textIonMobilityFilterFixedWidth, out fixedPeakWidth))
                            return null;
                        errmsg = ValidateFixedWindow(fixedPeakWidth);
                        if (errmsg != null)
                        {
                            helper.ShowTextBoxError(textIonMobilityFilterFixedWidth, errmsg);
                            return null;
                        }

                        break;
                }

                ionMobilityWindowWidthCalculator =
                    new IonMobilityWindowWidthCalculator(peakWidthType, resolvingPower, widthAtDt0, widthAtDtMax, fixedPeakWidth);
            }
            return new TransitionIonMobilityFiltering(ionMobilityLibrarySpec, useSpectralLibraryIonMobilities, ionMobilityWindowWidthCalculator);
        }

        private IonMobilityLibrarySpec SelectedIonMobilityLibrarySpec()
        {
            var selectedItem = comboIonMobilityLibrary.SelectedItem;
            if (selectedItem == null)
                return null;
            string nameDt = selectedItem.ToString();
            var ionMobilityLibrarySpec =
                Settings.Default.GetIonMobilityLibraryByName(nameDt);
            return ionMobilityLibrarySpec;
        }

        public static string ValidateWidth(double width)
        {
            if (width <= 0)
                return Resources.DriftTimeWindowWidthCalculator_Validate_Peak_width_must_be_non_negative_;
            return null;
        }

        public static string ValidateResolvingPower(double resolvingPower)
        {
            if (resolvingPower <= 0)
                return Resources.EditIonMobilityLibraryDlg_ValidateResolvingPower_Resolving_power_must_be_greater_than_0_;
            return null;
        }
        public static string ValidateFixedWindow(double fixedWindow)
        {
            if (fixedWindow <= 0)
                return Resources.IonMobilityFilteringUserControl_ValidateFixedWindow_Fixed_window_size_must_be_greater_than_0_;
            return null;
        }

    }
}
