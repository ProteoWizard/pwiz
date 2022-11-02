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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.SettingsUI.IonMobility
{
    /// <summary>
    /// U.I code for ion mobility settings shared by Import Wizard and Transition Settings
    /// </summary>
    public partial class IonMobilityFilteringUserControl : UserControl
    {
        private SettingsListComboDriver<IonMobilityLibrary> _driverIonMobilityLib;
        private TransitionIonMobilityFiltering _ionMobilityFiltering { get; set; }
        private bool ShowPeakWidthTypeControl { get; set; } // False when offering only resolving power settings, as in peptide import wizard

        public IonMobilityFilteringUserControl()
        {
            ShowPeakWidthTypeControl = true;
            InitializeComponent();
            UpdateIonMobilityFilterWindowWidthControls();
        }

        public void InitializeSettings(IModifyDocumentContainer documentContainer, bool? defaultState = null)
        {

            _ionMobilityFiltering = documentContainer.Document.Settings.TransitionSettings.IonMobilityFiltering;

            var imsWindowCalc = _ionMobilityFiltering.FilterWindowWidthCalculator;
            var hasLibIMS = _ionMobilityFiltering.IonMobilityLibrary != null && !_ionMobilityFiltering.IonMobilityLibrary.IsNone;

            var useSpectralLibraryIonMobilityValues = defaultState ?? _ionMobilityFiltering.UseSpectralLibraryIonMobilityValues;
            var hasIonMobilityFiltering = useSpectralLibraryIonMobilityValues || hasLibIMS;

            // Resolving power is most commonly used window size type, give that a reasonable starting value if none provided
            var resolvingPower = imsWindowCalc?.ResolvingPower ?? 0;
            if (hasIonMobilityFiltering && resolvingPower == 0)
            {
                resolvingPower = 30; // Arbitrarily chosen non-zero value
            }

            if (imsWindowCalc != null)
            {
                comboBoxWindowType.SelectedIndex = (int)imsWindowCalc.WindowWidthMode;
                textIonMobilityFilterResolvingPower.Text = resolvingPower.ToString(LocalizationHelper.CurrentCulture);
                textIonMobilityFilterWidthAtMobility0.Text = imsWindowCalc.PeakWidthAtIonMobilityValueZero.ToString(LocalizationHelper.CurrentCulture);
                textIonMobilityFilterWidthAtMobilityMax.Text = imsWindowCalc.PeakWidthAtIonMobilityValueMax.ToString(LocalizationHelper.CurrentCulture);
                textIonMobilityFilterFixedWidth.Text = imsWindowCalc.FixedWindowWidth.ToString(LocalizationHelper.CurrentCulture);
            }
            else
            {
                comboBoxWindowType.SelectedIndex = (int)IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.none;
            }

            UpdateIonMobilityFilterWindowWidthControls();

            _driverIonMobilityLib = new SettingsListComboDriver<IonMobilityLibrary>(comboIonMobilityLibrary, Settings.Default.IonMobilityLibraryList);
            var libName = (_ionMobilityFiltering.IonMobilityLibrary == null ? null : _ionMobilityFiltering.IonMobilityLibrary.Name);
            _driverIonMobilityLib.LoadList(libName);

            cbUseSpectralLibraryIonMobilities.Checked = useSpectralLibraryIonMobilityValues;

        }

        // For use in import search wizard, where we want to show a very simple interface
        public void ShowOnlyResolvingPowerControls(int groupBoxWidth)
        {
            // Assume that we want to use IM information in spectral libraries
            cbUseSpectralLibraryIonMobilities.Checked = true;
            // Assume that resolving power is the proper choice of window width calculation
            comboBoxWindowType.SelectedIndex = (int)IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power;
            ShowPeakWidthTypeControl = false;
            // Hide everything but resolving power setting
            var retainedControls = new Control[]
            {
                labelResolvingPower,
                textIonMobilityFilterResolvingPower
            };
            var controls = groupBoxIonMobilityFiltering.Controls.Cast<Control>().
                OrderBy(c => c.Top).ToArray();
            var margin = controls[0].Top /2 ;
            var shiftVert = labelResolvingPower.Top - controls[0].Top;
            var shiftHoriz = labelResolvingPower.Left - controls[0].Left;
            for (var i = 0; i < controls.Length; i++)
            {
                var control = controls[i];
                if (retainedControls.Contains(control))
                {
                    control.Visible = control.Enabled = true;
                    control.Top -= shiftVert;
                    control.Left -= shiftHoriz;
                }
                else
                {
                    control.Visible = control.Enabled = false;
                }
            }

            groupBoxIonMobilityFiltering.Height = textIonMobilityFilterResolvingPower.Bottom + margin;
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
                        UpdateIonMobilityFilterWindowWidthControls();
                        return;
                    }
                }

                var insertAt = _driverIonMobilityLib.Combo.Items.Count - 3; // Place before <Add>, <Edit Current...>, and <Edit List....>
                _driverIonMobilityLib.Combo.Items.Insert(insertAt, value);
                _driverIonMobilityLib.Combo.SelectedItem = value;
                UpdateIonMobilityFilterWindowWidthControls(); 
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

        public IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType WindowWidthType
        {
            get { return (IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType)comboBoxWindowType.SelectedIndex; }
            set
            {
                comboBoxWindowType.SelectedIndex = (int) value;
                UpdateIonMobilityFilterWindowWidthControls();
            }
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

        private void UpdateIonMobilityFilterWindowWidthControls()
        {
            if (comboBoxWindowType.SelectedIndex <  0)
            {
                // Initializing
                comboBoxWindowType.SelectedIndex = (int) IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.none;
            }
            // Linear peak width vs Resolving Power vs fixed width
            labelResolvingPower.Visible = textIonMobilityFilterResolvingPower.Visible = 
                comboBoxWindowType.SelectedIndex == (int)IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power;
            labelWidthAtMobilityZero.Visible = labelWidthAtMobilityMax.Visible = 
                textIonMobilityFilterWidthAtMobility0.Visible = textIonMobilityFilterWidthAtMobilityMax.Visible = 
                    comboBoxWindowType.SelectedIndex == (int)IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.linear_range;
            labelFixedWidth.Visible = textIonMobilityFilterFixedWidth.Visible =
                comboBoxWindowType.SelectedIndex == (int)IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.fixed_width;

            // The various window width value text boxes all need to be in the same location
            if (labelWidthAtMobilityZero.Location.X > labelResolvingPower.Location.X)
            {
                labelWidthAtMobilityZero.Location = labelResolvingPower.Location;
                textIonMobilityFilterWidthAtMobility0.Location = textIonMobilityFilterResolvingPower.Location;
                labelWidthAtMobilityMax.Location = new Point(labelResolvingPower.Location.X, labelWidthAtMobilityMax.Location.Y);
                textIonMobilityFilterWidthAtMobilityMax.Location = new Point(textIonMobilityFilterResolvingPower.Location.X, textIonMobilityFilterWidthAtMobilityMax.Location.Y);

                labelFixedWidth.Location = labelResolvingPower.Location;
                textIonMobilityFilterFixedWidth.Location = textIonMobilityFilterResolvingPower.Location;

            }

            // N.B. we formerly had code here to disable the window width controls if there was no library and
            // "Use spectral library values" was unchecked, but this ignored the possibility of document using
            // explicit IM filter values as read from a transition list import. So just leave it enabled, always.

            try
            {
                var helper = new MessageBoxHelper(this.ParentForm, false);
                var result = ValidateIonMobilitySettings(helper);
                if (result != null)
                    _ionMobilityFiltering = _ionMobilityFiltering == null 
                        ? result
                        : _ionMobilityFiltering.ChangeFilterWindowWidthCalculator(result.FilterWindowWidthCalculator);
            }
            catch (Exception e)
            {
                MessageDlg.ShowException(this, e);
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
                    MessageDlg.ShowException(this, e);
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
            var IonMobilityLibrary = GetSelectedIonMobilityLibrary();

            var useSpectralLibraryIonMobilities = cbUseSpectralLibraryIonMobilities.Checked;

            var peakWidthType = ShowPeakWidthTypeControl ? 
                (IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType)comboBoxWindowType.SelectedIndex :
                IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power;
            double resolvingPower = 0;
            double widthAtDt0 = 0;
            double widthAtDtMax = 0;
            double fixedPeakWidth = 0;
            
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
                    if (!ShowPeakWidthTypeControl && resolvingPower == 0)
                    {
                        // As in peptide search import wizard, resolving power is only option and we will accept 0 as meaning "no IMS filtering"
                        peakWidthType = IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.none;
                    }
                    else
                    {
                        errmsg = ValidateResolvingPower(resolvingPower);
                        if (errmsg != null)
                        {
                            helper.ShowTextBoxError(textIonMobilityFilterResolvingPower, errmsg);
                            return null;
                        }
                    }
                    break;
                case IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.fixed_width: // Fixed width
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

            var ionMobilityWindowWidthCalculator =
                new IonMobilityWindowWidthCalculator(peakWidthType, resolvingPower, widthAtDt0, widthAtDtMax, fixedPeakWidth);
            return new TransitionIonMobilityFiltering(IonMobilityLibrary, useSpectralLibraryIonMobilities, ionMobilityWindowWidthCalculator);
        }

        private IonMobilityLibrary GetSelectedIonMobilityLibrary()
        {
            var selectedItem = comboIonMobilityLibrary.SelectedItem;
            if (selectedItem == null)
                return null;
            string nameDt = selectedItem.ToString();
            var IonMobilityLibrary =
                Settings.Default.GetIonMobilityLibraryByName(nameDt);
            return IonMobilityLibrary;
        }

        public string ValidateWidth(double width)
        {
            if (width < 0)
                return Resources.DriftTimeWindowWidthCalculator_Validate_Peak_width_must_be_non_negative_;
            return null;
        }

        public string ValidateResolvingPower(double resolvingPower)
        {
            if (resolvingPower < 0)
                return Resources.EditIonMobilityLibraryDlg_ValidateResolvingPower_Resolving_power_must_be_greater_than_0_;
            return null;
        }
        public string ValidateFixedWindow(double fixedWindow)
        {
            if (fixedWindow < 0)
                return Resources.IonMobilityFilteringUserControl_ValidateFixedWindow_Fixed_window_size_must_be_greater_than_0_;
            return null;
        }

    }
}
