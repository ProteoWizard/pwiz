using System;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
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
            ImportPeptideSearch.SearchEngine.SetPrecursorMassTolerance(new MzTolerance(ms1Tol, (MzTolerance.Units) cbMS1TolUnit.SelectedIndex));

            double ms2Tol;
            if (!helper.ValidateDecimalTextBox(txtMS2Tolerance, 0, 100, out ms2Tol))
            {
                helper.ShowTextBoxError(txtMS2Tolerance, 
                    Resources.DdaSearch_SearchSettingsControl_MS2_Tolerance_incorrect);
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetFragmentIonMassTolerance(new MzTolerance(ms2Tol, (MzTolerance.Units) cbMS1TolUnit.SelectedIndex));

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

        public void SetPrecursorTolerance(MzTolerance tolerance)
        {
            txtMS1Tolerance.Text = tolerance.Value.ToString(LocalizationHelper.CurrentCulture);
            cbMS1TolUnit.SelectedIndex = (int) tolerance.Unit;
            ImportPeptideSearch.SearchEngine.SetPrecursorMassTolerance(tolerance);
        }

        public void SetFragmentTolerance(MzTolerance tolerance)
        {
            txtMS2Tolerance.Text = tolerance.Value.ToString(LocalizationHelper.CurrentCulture);
            cbMS2TolUnit.SelectedIndex = (int) tolerance.Unit;
            ImportPeptideSearch.SearchEngine.SetFragmentIonMassTolerance(tolerance);
        }

        public void SetFragmentIons(string fragmentIons)
        {
            int i = cbFragmentIons.Items.IndexOf(fragmentIons);
            Assume.IsTrue(i >= 0, Resources.DdaSearch_SearchSettingsControl_Fragmentions_not_found_in_combobox); 
            cbFragmentIons.SelectedIndex = i;
            ImportPeptideSearch.SearchEngine.SetFragmentIons(fragmentIons);
        }
    }
    
}
