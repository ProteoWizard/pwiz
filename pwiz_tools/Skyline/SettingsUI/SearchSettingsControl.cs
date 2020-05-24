using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NHibernate.Mapping.ByCode;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class SearchSettingsControl : UserControl
    {

        //public MSAmandaSearchWrapper MSAmandaSearchWrapper { get; private set; }
        private ImportPeptideSearch ImportPeptideSearch { get; set; }
        private readonly IModifyDocumentContainer _documentContainer;


        //public string[] FastaFilenames
        //{
        //    get { return MSAmandaSearchWrapper.FastaFiles; }

        //    private set
        //    {
        //        // Set new value
        //        MSAmandaSearchWrapper.FastaFiles = value;

        //        // Always show sorted list of files
        //        Array.Sort(MSAmandaSearchWrapper.FastaFiles);

        //        // Calculate the common root directory
        //        string dirInputRoot = PathEx.GetCommonRoot(MSAmandaSearchWrapper.FastaFiles);

        //        // Populate the input files list
        //        lbFastaFiles.BeginUpdate();
        //        lbFastaFiles.Items.Clear();
        //        foreach (string fileName in MSAmandaSearchWrapper.FastaFiles)
        //        {
        //            lbFastaFiles.Items.Add(PathEx.RemovePrefix(fileName, dirInputRoot));
        //        }
        //        lbFastaFiles.EndUpdate();

        //        //FireInputFilesChanged();
        //    }
        //}

        public SearchSettingsControl(IModifyDocumentContainer documentContainer,ImportPeptideSearch importPeptideSearch)
        {
            InitializeComponent();
            ImportPeptideSearch = importPeptideSearch;
            _documentContainer = documentContainer;
            lblSearchEngineName.Text = ImportPeptideSearch.SearchEngine.EngineName;

            LoadComboboxEntries();

            pBLogo.Image = ImportPeptideSearch.SearchEngine.SearchEngineLogo;
        }

        private void LoadComboboxEntries()
        {
            LoadMassUnitEntries();
            LoadFragmentIonEntries();
            //LoadModifications();
        }

        public void LoadModifications()
        {
            //
            List<string> checkedItemNames = new List<string>();
            foreach (var elem in clbFixedModifs.CheckedItems)
            {
                checkedItemNames.Add(((MatchModificationsControl.ListBoxModification)elem).ToString());
            }
            clbFixedModifs.Items.Clear();
            foreach (var mod in _documentContainer.Document.Settings.PeptideSettings.Modifications.StaticModifications)
            {
                MatchModificationsControl.ListBoxModification modi =
                    new MatchModificationsControl.ListBoxModification(mod);
                if (checkedItemNames.Contains(modi.ToString()) || !modi.Mod.IsVariable)
                    clbFixedModifs.Items.Add(modi, CheckState.Checked);
                else
                    clbFixedModifs.Items.Add(modi, CheckState.Unchecked);

            }

            ///clbFixedModifs.Items.AddRange(_documentContainer.Document.Settings.PeptideSettings.Modifications.StaticModifications.Select(m => m.).ToArray());
        }

        private void LoadMassUnitEntries()
        {
            string[] entries = new string[] {"Da", "ppm"};
            cbMS1TolUnit.Items.AddRange(entries);
            cbMS2TolUnit.Items.AddRange(entries);
        }

        private void LoadFragmentIonEntries(){
            
            cbFragmentIons.Items.AddRange(ImportPeptideSearch.SearchEngine.FragmentIons);
        }

        //private void btnAddFasta_Click(object sender, EventArgs e)
        //{

        //    string initialDir = Settings.Default.FastaDirectory;
        //    if (string.IsNullOrEmpty(initialDir))
        //    {
        //        initialDir = Path.GetDirectoryName(DocumentContainer.DocumentFilePath);
        //    }
        //    using (OpenFileDialog dlg = new OpenFileDialog
        //    {
        //        Title = Resources.ImportFastaControl_browseFastaBtn_Click_Open_FASTA,
        //        InitialDirectory = initialDir,
        //        CheckPathExists = true,
        //        Multiselect = true
        //        // FASTA files often have no extension as well as .fasta and others
        //    })
        //    {
        //        if (dlg.ShowDialog(WizardForm) == DialogResult.OK)
        //        {
        //            FastaFilenames = dlg.FileNames;
        //        }
        //    }

        //}


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
            Dictionary<StaticMod, bool> fixedAndVariableModifs = new Dictionary<StaticMod, bool>();
            for (int i = 0; i <clbFixedModifs.Items.Count; ++i)
            {
                fixedAndVariableModifs.Add(
                    ((MatchModificationsControl.ListBoxModification) clbFixedModifs.Items[i]).Mod,
                    clbFixedModifs.GetItemCheckState(i) == CheckState.Checked);
            }
            ImportPeptideSearch.SearchEngine.SaveModifications(fixedAndVariableModifs);
            return true;
        }

        private bool ValidateEntries()
        {
            var helper = new MessageBoxHelper(this.ParentForm);
            double ms1Tol;
            if (!helper.ValidateDecimalTextBox(txtMS1Tolerance, 0, 100, out ms1Tol))
            {
                helper.ShowTextBoxError(txtMS1Tolerance, /*add resource here */
                    "MS1 Tolerance incorrect");
                return false;
            }
            string ms1Unit;
            if (!ValidateCombobox(cbMS1TolUnit, out ms1Unit))
            {
                helper.ShowTextBoxError(cbMS1TolUnit, /*add resource here */
                    "MS1 tolerance unit must be selected");
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetPrecursorMassTolerance(ms1Tol, ms1Unit);

            double ms2Tol;
            if (!helper.ValidateDecimalTextBox(txtMS2Tolerance, 0, 100, out ms2Tol))
            {
                helper.ShowTextBoxError(txtMS2Tolerance, /*add resource here */
                    "MS2 Tolerance incorrect");
                return false;
            }
            string ms2Unit;
            if (!ValidateCombobox(cbMS2TolUnit, out ms2Unit))
            {
                helper.ShowTextBoxError(cbMS2TolUnit, /*add resource here */
                    "MS2 tolerance unit must be selected");
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetFragmentIonMassTolerance(ms2Tol, ms2Unit);

            string fragmentIons;
            if (!ValidateCombobox(cbFragmentIons, out fragmentIons))
            {
                helper.ShowTextBoxError(cbFragmentIons, /*add resource here */
                    "Fragment ions must be selected");
                return false;
            }
            ImportPeptideSearch.SearchEngine.SetFragmentIons(fragmentIons);

            return true;
        }
    }
    
}
