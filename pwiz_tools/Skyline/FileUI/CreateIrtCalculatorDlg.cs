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
        private static readonly string[] COMMON_IRT_STANDARDS = { "ADVTPADFSEWSK",  // Not L10N
                                                                  "DGLDAASYYAPVR",  // Not L10N
                                                                  "GAGSSEPVTGLDAK", // Not L10N
                                                                  "GTFIIDPAAVIR",   // Not L10N
                                                                  "GTFIIDPGGVIR",   // Not L10N
                                                                  "LFLQFGAQGSPFLK", // Not L10N
                                                                  "LGGNEQVTR",      // Not L10N
                                                                  "TPVISGGPYEYR",   // Not L10N
                                                                  "TPVITGAPYEYR",   // Not L10N
                                                                  "VEATFGVDESNAK",  // Not L10N
                                                                  "YILAGVENSK"};    // Not L10N

        public string IrtFile { get; private set; }

        /// <summary>
        /// In the case where we specify one of the imported proteins as the iRT protein, make a list of its peptides
        /// </summary>
        private HashSet<string> _irtPeptideSequences;

        private List<SpectrumMzInfo> _librarySpectra;
        private List<DbIrtPeptide> _dbIrtPeptides;

        private static int CompareNames(PeptideGroupDocNode group1, PeptideGroupDocNode group2)
        {
            return String.Compare(group1.Name, group2.Name, CultureInfo.CurrentCulture, CompareOptions.None);
        }

        private static bool ContainsCommonIrts(PeptideGroupDocNode protein)
        {
            return protein.Peptides.Select(pep => pep.ModifiedSequence).Intersect(COMMON_IRT_STANDARDS).Count() > CalibrateIrtDlg.MIN_STANDARD_PEPTIDES;
        }

        public CreateIrtCalculatorDlg(SrmDocument document, string documentFilePath, IList<RetentionScoreCalculatorSpec> existing, IEnumerable<PeptideGroupDocNode> peptideGroups)
        {
            Document = document;
            DocumentFilePath = documentFilePath;
            _existing = existing;
            InitializeComponent();
            _librarySpectra = new List<SpectrumMzInfo>();
            _dbIrtPeptides = new List<DbIrtPeptide>();
            var possibleStandardProteins = peptideGroups.Where(group => group.PeptideCount > CalibrateIrtDlg.MIN_STANDARD_PEPTIDES).ToList();
            var proteinsContainingCommonIrts = possibleStandardProteins.Where(ContainsCommonIrts);
            var proteinsNotContainingCommonIrts = possibleStandardProteins.Where(group => !ContainsCommonIrts(group));
            possibleStandardProteins.Sort(CompareNames);
            comboBoxProteins.Items.AddRange(proteinsContainingCommonIrts.ToArray());
            comboBoxProteins.Items.AddRange(proteinsNotContainingCommonIrts.ToArray());
            UpdateSelection(IrtType.existing);
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
                        throw new DatabaseOpeningException(string.Format(Resources.CreateIrtCalculatorDlg_OkDialog_Cannot_read_the_database_file__0_, textOpenDatabase.Text));
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
                    MessageDlg.Show(this, Resources.CreateIrtCalculatorDlg_OkDialog_Please_select_a_protein_containing_the_list_of_standard_peptides_for_the_iRT_calculator_);
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
            var retentionTimeRegression = new RetentionTimeRegression(calculator.Name, calculator, null, null, 10, new List<MeasuredRetentionTime>());
            var docNew = Document.ChangeSettings(Document.Settings.ChangePeptidePrediction(prediction =>
                prediction.ChangeRetentionTime(retentionTimeRegression)));
            // Import transition list of standards, if applicable
            if (irtType == IrtType.separate_list)
            {
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
                        throw new InvalidDataException(errorList[0].ErrorMessage);
                    }
                    _dbIrtPeptides = irtPeptides.Select(rt => new DbIrtPeptide(rt.PeptideSequence, rt.RetentionTime, true, TimeSource.scan)).ToList();
                    IrtFile = textImportText.Text;
                }
                catch (Exception x)
                {
                    MessageDlg.ShowWithException(this, string.Format(Resources.CreateIrtCalculatorDlg_OkDialog_Error_reading_iRT_standards_transition_list___0_, x.Message), x);
                    return;
                }
            }
            else if (irtType == IrtType.protein)
            {
                PeptideGroupDocNode selectedGroup = comboBoxProteins.SelectedItem as PeptideGroupDocNode;
// ReSharper disable PossibleNullReferenceException
                _irtPeptideSequences = new HashSet<string>(selectedGroup.Peptides.Select(pep => pep.ModifiedSequence));
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
                dbIrtPeptidesFilter.ForEach(pep => pep.Standard = _irtPeptideSequences.Contains(pep.PeptideModSeq));
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
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.EditIrtCalcDlg_btnBrowseDb_Click_Open_iRT_Database,
                InitialDirectory = Path.GetDirectoryName(DocumentFilePath),
                DefaultExt = IrtDb.EXT,
                Filter = TextUtil.FileDialogFiltersAll(IrtDb.FILTER_IRTDB)
            })
            {
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
            using (var dlg = new SaveFileDialog
            {
                Title = Resources.EditIrtCalcDlg_btnCreateDb_Click_Create_iRT_Database,
                InitialDirectory = Path.GetDirectoryName(DocumentFilePath),
                OverwritePrompt = true,
                DefaultExt = IrtDb.EXT,
                Filter = TextUtil.FileDialogFiltersAll(IrtDb.FILTER_IRTDB)
            })
            {
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
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.CreateIrtCalculatorDlg_ImportTextFile_Import_Transition_List__iRT_standards_,
                InitialDirectory = Path.GetDirectoryName(DocumentFilePath),
                DefaultExt = TextUtil.EXT_CSV,
                Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(
                    Resources.SkylineWindow_importMassListMenuItem_Click_Transition_List, TextUtil.EXT_CSV, TextUtil.EXT_TSV))
            })
            {
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
                throw new ArgumentException("Invalid protein selection"); // Not L10N
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
