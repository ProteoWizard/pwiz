/*
 * Original author: Viktoria Dorfer <viktoria.dorfer .at. fh-hagenberg.at>,
 *                  Bioinformatics Research Group, University of Applied Sciences Upper Austria
 *
 * Copyright 2020 University of Applied Sciences Upper Austria
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class SearchSettingsControl : UserControl
    {
        private ImportPeptideSearch ImportPeptideSearch { get; set; }
        private readonly IModifyDocumentContainer _documentContainer;
    
        public SearchSettingsControl(IModifyDocumentContainer documentContainer, ImportPeptideSearch importPeptideSearch)
        {
            InitializeComponent();
            ImportPeptideSearch = importPeptideSearch;
            _documentContainer = documentContainer;

            txtMS1Tolerance.LostFocus += txtMS1Tolerance_LostFocus;
            txtMS2Tolerance.LostFocus += txtMS2Tolerance_LostFocus;
        }


        public DdaSearchSettings SearchSettings
        {
            get { return new DdaSearchSettings(this); }
        }

        public class DdaSearchSettings
        {
            public DdaSearchSettings(SearchSettingsControl control) : this(control.PrecursorTolerance,
                control.FragmentTolerance, control.MaxVariableMods, control.FragmentIons)
            {
            }

            public static DdaSearchSettings GetDefault()
            {
                return new DdaSearchSettings();
            }

            public DdaSearchSettings()
            {
            }

            public DdaSearchSettings(MzTolerance precursorTolerance, MzTolerance fragmentTolerance, int maxVariableMods, string fragmentIons)
            {
                PrecursorTolerance = precursorTolerance;
                FragmentTolerance = fragmentTolerance;
                MaxVariableMods = maxVariableMods;
                FragmentIons = fragmentIons;
            }

            [Track]
            public MzTolerance PrecursorTolerance { get; private set; }
            [Track]
            public MzTolerance FragmentTolerance { get; private set; }
            [Track]
            public int MaxVariableMods { get; private set; }
            [Track]
            public string FragmentIons { get; private set; }
        }

        private void txtMS1Tolerance_LostFocus(object sender, EventArgs e)
        {
            if (cbMS1TolUnit.SelectedItem != null)
                return;

            if (double.TryParse(txtMS1Tolerance.Text, out double tmp))
                cbMS1TolUnit.SelectedIndex = tmp <= 3 ? 0 : 1;
        }

        private void txtMS2Tolerance_LostFocus(object sender, EventArgs e)
        {
            if (cbMS2TolUnit.SelectedItem != null)
                return;

            if (double.TryParse(txtMS2Tolerance.Text, out double tmp))
                cbMS2TolUnit.SelectedIndex = tmp <= 3 ? 0 : 1;
        }

        public void InitializeEngine()
        {
            lblSearchEngineName.Text = ImportPeptideSearch.SearchEngine.EngineName;
            LoadComboboxEntries();
            pBLogo.Image = ImportPeptideSearch.SearchEngine.SearchEngineLogo;
            btnAdditionalSettings.Enabled = ImportPeptideSearch.SearchEngine.AdditionalSettings != null;
        }

        private void LoadComboboxEntries()
        {
            LoadMassUnitEntries();
            LoadFragmentIonEntries();

            var modSettings = _documentContainer.Document.Settings.PeptideSettings.Modifications;
            cbMaxVariableMods.SelectedItem = modSettings.MaxVariableMods.ToString(LocalizationHelper.CurrentCulture);
            if (cbMaxVariableMods.SelectedIndex < 0)
                cbMaxVariableMods.SelectedIndex = 2; // default max = 2
        }

        private void LoadMassUnitEntries()
        {
            string[] entries = {@"Da", @"ppm"};
            cbMS1TolUnit.Items.AddRange(entries);
            cbMS2TolUnit.Items.AddRange(entries);
        }

        private void LoadFragmentIonEntries()
        {
            cbFragmentIons.Items.AddRange(ImportPeptideSearch.SearchEngine.FragmentIons);
            ComboHelper.AutoSizeDropDown(cbFragmentIons);
            cbFragmentIons.SelectedIndex = 0;
        }
    
        private Form WizardForm
        {
            get { return FormEx.GetParentForm(this); }
        }
    
        private bool ValidateCombobox(ComboBox comboBox, out string selectedElement)
        {
            selectedElement = "";
            if (comboBox.SelectedItem == null)
                return false;
            selectedElement = comboBox.SelectedItem.ToString();
            return true;
        }
   
        public bool SaveAllSettings()
        {
            bool valid = ValidateEntries();
            if (!valid)
                return false;

            var modSettings = _documentContainer.Document.Settings.PeptideSettings.Modifications;
            var allMods = modSettings.StaticModifications.Union(modSettings.AllHeavyModifications);
            ImportPeptideSearch.SearchEngine.SetModifications(allMods, Convert.ToInt32(cbMaxVariableMods.SelectedItem));
            return true;
        }

        private bool ValidateEntries()
        {
            var helper = new MessageBoxHelper(this.ParentForm);
            double ms1Tol;
            if (!helper.ValidateDecimalTextBox(txtMS1Tolerance, 0, 100, out ms1Tol))
            {
                helper.ShowTextBoxError(txtMS1Tolerance, 
                    Resources.DdaSearch_SearchSettingsControl_MS1_Tolerance_incorrect);
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetPrecursorMassTolerance(PrecursorTolerance);

            double ms2Tol;
            if (!helper.ValidateDecimalTextBox(txtMS2Tolerance, 0, 100, out ms2Tol))
            {
                helper.ShowTextBoxError(txtMS2Tolerance, 
                    Resources.DdaSearch_SearchSettingsControl_MS2_Tolerance_incorrect);
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetFragmentIonMassTolerance(FragmentTolerance);

            string fragmentIons;
            if (!ValidateCombobox(cbFragmentIons, out fragmentIons))
            {
                helper.ShowTextBoxError(cbFragmentIons, 
                    Resources.DdaSearch_SearchSettingsControl_Fragment_ions_must_be_selected);
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetFragmentIons(fragmentIons);
            return true;
        }

        public MzTolerance PrecursorTolerance
        {
            get { return new MzTolerance(double.Parse(txtMS1Tolerance.Text), (MzTolerance.Units) cbMS1TolUnit.SelectedIndex); }

            set
            {
                txtMS1Tolerance.Text = value.Value.ToString(LocalizationHelper.CurrentCulture);
                cbMS1TolUnit.SelectedIndex = (int)value.Unit;
            }
        }

        public MzTolerance FragmentTolerance
        {
            get { return new MzTolerance(double.Parse(txtMS2Tolerance.Text), (MzTolerance.Units) cbMS2TolUnit.SelectedIndex); }

            set
            {
                txtMS2Tolerance.Text = value.Value.ToString(LocalizationHelper.CurrentCulture);
                cbMS2TolUnit.SelectedIndex = (int)value.Unit;
            }
        }

        public int MaxVariableMods
        {
            get { return Convert.ToInt32(cbMaxVariableMods.SelectedItem); }
            set { cbMaxVariableMods.SelectedIndex = cbMaxVariableMods.Items.IndexOf(value.ToString()); }
        }

        public string FragmentIons
        {
            get { return cbFragmentIons.SelectedItem.ToString(); }

            set
            {
                int i = cbFragmentIons.Items.IndexOf(value);
            Assume.IsTrue(i >= 0, $@"fragmentIons value ""{value}"" not found in ComboBox items");
                cbFragmentIons.SelectedIndex = i;
                ImportPeptideSearch.SearchEngine.SetFragmentIons(value);
            }
        }

        private void btnAdditionalSettings_Click(object sender, EventArgs e)
        {
            Assume.IsNotNull(ImportPeptideSearch.SearchEngine.AdditionalSettings);

            KeyValueGridDlg.Show(Resources.SearchSettingsControl_Additional_Settings,
                ImportPeptideSearch.SearchEngine.AdditionalSettings,
                (setting) => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value));
        }
    }
    
}
