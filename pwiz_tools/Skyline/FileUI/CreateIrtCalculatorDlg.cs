/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class CreateIrtCalculatorDlg : FormEx
    {
        public string IrtFile { get; private set; }

        /// <summary>
        /// In the case where we specify one of the imported proteins as the iRT protein, make a list of its peptides
        /// </summary>
        private HashSet<Target> _irtPeptideSequences;

        private List<SpectrumMzInfo> _librarySpectra;
        private List<DbIrtPeptide> _dbIrtPeptides;

        private static int CompareNames(PeptideGroupDocNode group1, PeptideGroupDocNode group2)
        {
            return string.Compare(group1.Name, group2.Name, CultureInfo.CurrentCulture, CompareOptions.None);
        }

        public static void SeparateProteinGroups(IEnumerable<PeptideGroupDocNode> proteins,
            out PeptideGroupDocNode[] standardProteins, out PeptideGroupDocNode[] nonStandardProteins)
        {
            var standardProteinsList = new List<PeptideGroupDocNode>();
            var nonStandardProteinsList = new List<PeptideGroupDocNode>();
            foreach (var protein in proteins.Where(protein => protein.MoleculeCount >= CalibrateIrtDlg.MIN_STANDARD_PEPTIDES))
            {
                if (protein.Molecules.Select(pep => pep.ModifiedTarget).Count(IrtStandard.AnyContains) >= CalibrateIrtDlg.MIN_STANDARD_PEPTIDES)
                    standardProteinsList.Add(protein);
                else
                    nonStandardProteinsList.Add(protein);
            }
            standardProteinsList.Sort(CompareNames);
            standardProteins = standardProteinsList.ToArray();
            nonStandardProteins = nonStandardProteinsList.ToArray();
        }

        public CreateIrtCalculatorDlg(SrmDocument document, string documentFilePath, IList<RetentionScoreCalculatorSpec> existing, IEnumerable<PeptideGroupDocNode> peptideGroups)
        {
            Document = document;
            DocumentFilePath = documentFilePath;
            _existing = existing;
            InitializeComponent();
            _librarySpectra = new List<SpectrumMzInfo>();
            _dbIrtPeptides = new List<DbIrtPeptide>();
            PeptideGroupDocNode[] proteinsContainingCommonIrts, proteinsNotContainingCommonIrts;
            SeparateProteinGroups(peptideGroups, out proteinsContainingCommonIrts, out proteinsNotContainingCommonIrts);
            comboBoxProteins.Items.AddRange(proteinsContainingCommonIrts);
            comboBoxProteins.Items.AddRange(proteinsNotContainingCommonIrts);
            if (proteinsContainingCommonIrts.Any())
                comboBoxProteins.SelectedIndex = 0;
            UpdateSelection(IrtType.protein);
        }

        public SrmDocument Document { get; private set; }
        public string DocumentFilePath { get; private set; }

        private readonly IList<RetentionScoreCalculatorSpec> _existing;

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            IrtType irtType = GetIrtType();
            if (textCalculatorName.Text.Length == 0)
            {
                MessageDlg.Show(this, Resources.CreateIrtCalculatorDlg_OkDialog_Calculator_name_cannot_be_empty);
                return;
            }
            if (_existing.Select(spec => spec.Name).Contains(textCalculatorName.Text))
            {
                var replaceResult = MultiButtonMsgDlg.Show(this, Resources.CreateIrtCalculatorDlg_OkDialog_A_calculator_with_that_name_already_exists___Do_you_want_to_replace_it_,
                                       MultiButtonMsgDlg.BUTTON_YES,           
                                       MultiButtonMsgDlg.BUTTON_NO, 
                                       false);
                if (replaceResult == DialogResult.No)
                    return;
            }
            if (irtType == IrtType.existing)
            {
                try
                {
                    if (!File.Exists(textOpenDatabase.Text))
                    {
                        MessageDlg.Show(this, Resources.CreateIrtCalculatorDlg_OkDialog_iRT_database_field_must_contain_a_path_to_a_valid_file_);
                        textOpenDatabase.Focus();
                        return;
                    }
                    var db = IrtDb.GetIrtDb(textOpenDatabase.Text, null);
                    if (db == null)
                    {
                        throw new DatabaseOpeningException(string.Format(FileUIResources.CreateIrtCalculatorDlg_OkDialog_Cannot_read_the_database_file__0_, textOpenDatabase.Text));
                    }
                }
                catch (Exception x)
                {
                    MessageDlg.ShowWithException(this, string.Format(Resources.CreateIrtCalculatorDlg_OkDialog_Failed_to_open_the_database_file___0_, x.Message), x);
                    return;
                }
            }
            else if (irtType == IrtType.separate_list)
            {
                if (textNewDatabase.Text.Length == 0)
                {
                    MessageDlg.Show(this, Resources.CreateIrtCalculatorDlg_OkDialog_iRT_database_field_must_not_be_empty_);
                    textNewDatabase.Focus();
                    return;
                }
                if (!CreateIrtDatabase(textNewDatabase.Text))
                    return;
            }
            else
            {
                if (textNewDatabaseProteins.Text.Length == 0)
                {
                    MessageDlg.Show(this, Resources.CreateIrtCalculatorDlg_OkDialog_iRT_database_field_must_not_be_empty_);
                    textNewDatabaseProteins.Focus();
                    return;
                }
                if (comboBoxProteins.SelectedIndex == -1)
                {
                    MessageDlg.Show(this, FileUIResources.CreateIrtCalculatorDlg_OkDialog_Please_select_a_protein_containing_the_list_of_standard_peptides_for_the_iRT_calculator_);
                    comboBoxProteins.Focus();
                    return;
                }
                if (!CreateIrtDatabase(textNewDatabaseProteins.Text))
                    return;
            }
            // Make a version of the document with the new calculator in it
            var databaseFileName = irtType == IrtType.existing ? textOpenDatabase.Text : 
                                   irtType == IrtType.separate_list ? textNewDatabase.Text :
                                    textNewDatabaseProteins.Text;
            var calculator = new RCalcIrt(textCalculatorName.Text, databaseFileName);
            // CONSIDER: Probably can't use just a static default like 10 below
            var retentionTimeRegression = new RetentionTimeRegression(calculator.Name, calculator, null, null, RetentionTimeRegression.DEFAULT_WINDOW, new List<MeasuredRetentionTime>());
            var docNew = Document.ChangeSettings(Document.Settings.ChangePeptidePrediction(prediction =>
                prediction.ChangeRetentionTime(retentionTimeRegression)));
            // Import transition list of standards, if applicable
            if (irtType == IrtType.separate_list)
            {
                var userCanceled = false;
                try
                {
                    if (!File.Exists(textImportText.Text))
                    {
                        MessageDlg.Show(this, Resources.CreateIrtCalculatorDlg_OkDialog_Transition_list_field_must_contain_a_path_to_a_valid_file_);
                        return;
                    }
                    IdentityPath selectPath;
                    List<MeasuredRetentionTime> irtPeptides;
                    List<TransitionImportErrorInfo> errorList;
                    var inputs = new MassListInputs(textImportText.Text);
                    docNew = docNew.ImportMassList(inputs, null, out selectPath, out irtPeptides, out _librarySpectra, out errorList);
                    if (errorList.Any())
                    {
                        // Allow the user to assign column types
                        var importer = docNew.PreImportMassList(inputs, null, true, SrmDocument.DOCUMENT_TYPE.none, true, ModeUI);
                        using (var columnDlg = new ImportTransitionListColumnSelectDlg(importer, docNew, inputs, selectPath, false))
                        {
                            if (columnDlg.ShowDialog(this) == DialogResult.OK)
                            {
                                var insParams = columnDlg.InsertionParams;
                                docNew = insParams.Document;
                                selectPath = insParams.SelectPath;
                                irtPeptides = insParams.IrtPeptides;
                                _librarySpectra = insParams.LibrarySpectra;
                            }
                            else
                            {
                                userCanceled = true;
                                throw new InvalidDataException(errorList[0].ErrorMessage);
                            }
                        }
                    }
                    _dbIrtPeptides = irtPeptides.Select(rt => new DbIrtPeptide(rt.PeptideSequence, rt.RetentionTime, true, TimeSource.scan)).ToList();
                    IrtFile = textImportText.Text;
                }
                catch (Exception x)
                {
                    if (!userCanceled)
                    {
                        MessageDlg.ShowWithException(this, string.Format(Resources.CreateIrtCalculatorDlg_OkDialog_Error_reading_iRT_standards_transition_list___0_, x.Message), x);
                    }
                    return; // Go back and try something else
                }
            }
            else if (irtType == IrtType.protein)
            {
                PeptideGroupDocNode selectedGroup = comboBoxProteins.SelectedItem as PeptideGroupDocNode;
// ReSharper disable PossibleNullReferenceException
                _irtPeptideSequences = new HashSet<Target>(selectedGroup.Molecules.Select(pep => pep.ModifiedTarget));
// ReSharper restore PossibleNullReferenceException
            }
            Document = docNew;
            DialogResult = DialogResult.OK;
        }

        public void UpdateLists(List<SpectrumMzInfo> librarySpectra, List<DbIrtPeptide> dbIrtPeptidesFilter)
        {
            librarySpectra.AddRange(_librarySpectra);
            dbIrtPeptidesFilter.AddRange(_dbIrtPeptides);
            if (_irtPeptideSequences != null)
                dbIrtPeptidesFilter.ForEach(pep => pep.Standard = _irtPeptideSequences.Contains(pep.ModifiedTarget));
        }

        public bool CreateIrtDatabase(string path)
        {
            try
            {
                ImportAssayLibraryHelper.CreateIrtDatabase(path);
            }
            catch (Exception x)
            {
                MessageDlg.ShowException(this, x);
                return false;
            }
            return true;
        }

        private void btnBrowseDb_Click(object sender, EventArgs e)
        {
            BrowseDb();
        }

        public void BrowseDb()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = Resources.EditIrtCalcDlg_btnBrowseDb_Click_Open_iRT_Database;
                dlg.InitialDirectory = Path.GetDirectoryName(DocumentFilePath);
                dlg.DefaultExt = IrtDb.EXT;
                dlg.Filter = TextUtil.FileDialogFiltersAll(IrtDb.FILTER_IRTDB);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);
                    textOpenDatabase.Text = dlg.FileName;
                    textOpenDatabase.Focus();
                }
            }
        }

        private void btnCreateDb_Click(object sender, EventArgs e)
        {
            CreateDb(textNewDatabase);
        }

        private void btnCreateDbProteins_Click(object sender, EventArgs e)
        {
            CreateDb(textNewDatabaseProteins);
        }

        public void CreateDb(TextBox textBox)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = Resources.EditIrtCalcDlg_btnCreateDb_Click_Create_iRT_Database;
                dlg.InitialDirectory = Path.GetDirectoryName(DocumentFilePath);
                dlg.OverwritePrompt = true;
                dlg.DefaultExt = IrtDb.EXT;
                dlg.Filter = TextUtil.FileDialogFiltersAll(IrtDb.FILTER_IRTDB);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);
                    if (string.IsNullOrEmpty(textCalculatorName.Text))
                        textCalculatorName.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
                    textBox.Text = dlg.FileName;
                    textBox.Focus();
                }
            }
        }

        private void btnBrowseText_Click(object sender, EventArgs e)
        {
            ImportTextFile();
        }

        public void ImportTextFile()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = Resources.CreateIrtCalculatorDlg_ImportTextFile_Import_Transition_List__iRT_standards_;
                dlg.InitialDirectory = Path.GetDirectoryName(DocumentFilePath);
                dlg.DefaultExt = TextUtil.EXT_CSV;
                dlg.Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(
                    Resources.SkylineWindow_importMassListMenuItem_Click_Transition_List, TextUtil.EXT_CSV, TextUtil.EXT_TSV));
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);
                    textImportText.Text = dlg.FileName;
                    textImportText.Focus();
                }
            }
        }

        private void radioUseExisting_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSelection(IrtType.existing);
        }

        private void radioCreateNew_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSelection(IrtType.separate_list);
        }

        private void radioUseProtein_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSelection(IrtType.protein);
        }

        public void UpdateSelection(IrtType irtType)
        {
            bool existing = irtType == IrtType.existing;
            bool separate = irtType == IrtType.separate_list;
            bool protein = irtType == IrtType.protein;

            textNewDatabaseProteins.Enabled = protein;
            comboBoxProteins.Enabled = protein;
            btnCreateDbProteins.Enabled = protein;

            textImportText.Enabled = separate;
            textNewDatabase.Enabled = separate;
            btnBrowseText.Enabled = separate;
            btnCreateDb.Enabled = separate;

            textOpenDatabase.Enabled = existing;
            btnBrowseDb.Enabled = existing;
        }

        private IrtType GetIrtType()
        {
            return radioUseExisting.Checked ? IrtType.existing :
                   radioUseList.Checked ? IrtType.separate_list :
                                            IrtType.protein;
        }

        public enum IrtType { existing, separate_list, protein}

        #region test helpers

        public string CalculatorName
        {
            get { return textCalculatorName.Text; }
            set { textCalculatorName.Text = value; }
        }

        public string ExistingDatabaseName
        {
            get { return textOpenDatabase.Text; }
            set { textOpenDatabase.Text = value; }
        }

        public string NewDatabaseName
        {
            get { return textNewDatabase.Text; }
            set { textNewDatabase.Text = value; }
        }

        public string NewDatabaseNameProtein
        {
            get { return textNewDatabaseProteins.Text; }
            set { textNewDatabaseProteins.Text = value; }
        }

        public string TextFilename
        {
            get { return textImportText.Text; }
            set { textImportText.Text = value; }
        }

        public IrtType IrtImportType
        {
            get { return GetIrtType(); }
            set
            {
                radioUseExisting.Checked = value == IrtType.existing;
                radioUseList.Checked = value == IrtType.separate_list;
                radioUseProtein.Checked = value == IrtType.protein;
                UpdateSelection(value);
            }
        }

        public string SelectedProtein
        {
            get { return comboBoxProteins.SelectedItem.ToString(); }
            set
            {
                foreach (var item in comboBoxProteins.Items)
                {
                    var node = item as PeptideGroupDocNode;
// ReSharper disable once PossibleNullReferenceException
                    if (node.Name == value)
                    {
                        comboBoxProteins.SelectedItem = item;
                        return;
                    }
                }
                throw new ArgumentException(@"Invalid protein selection");
            }
        }

        public int CountProteins { get { return comboBoxProteins.Items.Count; } }

        public string GetProtein(int index)
        {
            var node = comboBoxProteins.Items[index] as PeptideGroupDocNode;
// ReSharper disable once PossibleNullReferenceException
            return node.Name.ToString(CultureInfo.CurrentCulture);
        }

        #endregion
    }
}
