/*
 * Original author: Yuval Boss <yuval .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.EditUI
{
    public partial class AssociateProteinsDlg : ModeUIInvariantFormEx,  // This dialog has nothing to do with small molecules, always display as proteomic even in mixed mode
                  IAuditLogModifier<AssociateProteinsSettings>
    {
        private readonly SkylineWindow _parent;
        private bool _isFasta;
        private ProteinAssociation _proteinAssociation;
        private readonly SettingsListComboDriver<BackgroundProteomeSpec> _driverBackgroundProteome;
        private SrmDocument _newDocument;

        private bool _reuseLastFasta;
        private string _statusBarResultFormat;
        private static string[] _sharedPeptideOptionNames = Enum.GetNames(typeof(ProteinAssociation.SharedPeptides));

        public string FastaFileName
        {
            get { return tbxFastaTargets.Text; }
            set { tbxFastaTargets.Text = value; }
        }

        public bool IsBusy => _newDocument == null;


        public AssociateProteinsDlg(SkylineWindow parent, bool reuseLastFasta = true)
        {
            InitializeComponent();
            _parent = parent;
            _reuseLastFasta = reuseLastFasta;
            _statusBarResultFormat = string.Format(@"{{0}} {0}, {{1}} {1}, {{2}} {2}, {{3}} {3}",
                Resources.AnnotationDef_AnnotationTarget_Proteins,
                Resources.AnnotationDef_AnnotationTarget_Peptides,
                Resources.AnnotationDef_AnnotationTarget_Precursors,
                Resources.AnnotationDef_AnnotationTarget_Transitions).ToLower();
            btnOk.Enabled = false;
            lblStatusBarResult.Text = string.Empty;

            var peptideSettings = parent.DocumentUI.Settings.PeptideSettings;

            foreach (var sharedPeptides in _sharedPeptideOptionNames)
                comboSharedPeptides.Items.Add(EnumNames.ResourceManager.GetString(@"SharedPeptides_" + sharedPeptides) ?? throw new InvalidOperationException(sharedPeptides));

            GroupProteins = peptideSettings.Filter.ParsimonySettings?.GroupProteins ?? false;
            FindMinimalProteinList = peptideSettings.Filter.ParsimonySettings?.FindMinimalProteinList ?? false;
            RemoveSubsetProteins = peptideSettings.Filter.ParsimonySettings?.RemoveSubsetProteins ?? false;
            SelectedSharedPeptides = peptideSettings.Filter.ParsimonySettings?.SharedPeptides ?? ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins;

            _driverBackgroundProteome = new SettingsListComboDriver<BackgroundProteomeSpec>(comboBackgroundProteome, Settings.Default.BackgroundProteomeList);
            _driverBackgroundProteome.LoadList(peptideSettings.BackgroundProteome.Name);

            helpTip.SetToolTip(cbGroupProteins, helpTip.GetToolTip(lblGroupProteins));
            helpTip.SetToolTip(cbMinimalProteinList, helpTip.GetToolTip(lblMinimalProteinList));
            helpTip.SetToolTip(cbRemoveSubsetProteins, helpTip.GetToolTip(lblRemoveSubsetProteins));
            helpTip.SetToolTip(comboSharedPeptides, helpTip.GetToolTip(lblSharedPeptides));
            helpTip.SetToolTip(numMinPeptides, helpTip.GetToolTip(lblMinPeptides));
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_parent.Document.PeptideCount == 0)
            {
                MessageDlg.Show(this, Resources.ImportFastaControl_ImportFasta_The_document_does_not_contain_any_peptides_);
                Close();
            }

            if (_reuseLastFasta && !Settings.Default.LastProteinAssociationFastaFilepath.IsNullOrEmpty())
                tbxFastaTargets.Text = Settings.Default.LastProteinAssociationFastaFilepath;

        }

        private void Initialize()
        {
            if (_proteinAssociation != null)
                return;

            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.PerformWork(this, 1000, broker =>
                {
                    _proteinAssociation = new ProteinAssociation(_parent.Document, broker);
                });

                if (longWaitDlg.IsCanceled)
                    _proteinAssociation = null;
            }
        }

        public IEnumerable<KeyValuePair<ProteinAssociation.IProteinRecord, ProteinAssociation.PeptideAssociationGroup>> AssociatedProteins => _proteinAssociation?.AssociatedProteins;
        public IEnumerable<KeyValuePair<ProteinAssociation.IProteinRecord, ProteinAssociation.PeptideAssociationGroup>> ParsimoniousProteins => _proteinAssociation?.ParsimoniousProteins;
        public ProteinAssociation.IMappingResults Results => _proteinAssociation?.Results;
        public ProteinAssociation.IMappingResults FinalResults => _proteinAssociation?.FinalResults;

        public bool GroupProteins
        {
            get => cbGroupProteins.Checked;
            set => cbGroupProteins.Checked = value;
        }

        public bool FindMinimalProteinList
        {
            get => cbMinimalProteinList.Checked;
            set => cbMinimalProteinList.Checked = value;
        }

        public bool RemoveSubsetProteins
        {
            get => cbRemoveSubsetProteins.Checked;
            set => cbRemoveSubsetProteins.Checked = value;
        }

        public ProteinAssociation.SharedPeptides SelectedSharedPeptides
        {
            get => (ProteinAssociation.SharedPeptides) comboSharedPeptides.SelectedIndex;
            set => comboSharedPeptides.SelectedIndex = (int) value;
        }

        public int MinPeptidesPerProtein
        {
            get => (int) numMinPeptides.Value;
            set => numMinPeptides.Value = value;
        }

        private void UpdateParsimonyResults()
        {
            _newDocument = null;
            if (Results == null)
                return;

            var groupProteins = GroupProteins;
            var findMinimalProteinList = FindMinimalProteinList;
            var removeSubsetProteins = RemoveSubsetProteins;
            var selectedSharedPeptides = SelectedSharedPeptides;
            var minPeptidesPerProtein = MinPeptidesPerProtein;

            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.PerformWork(this, 1000,
                    broker => _proteinAssociation.ApplyParsimonyOptions(groupProteins, findMinimalProteinList, removeSubsetProteins, selectedSharedPeptides, minPeptidesPerProtein, broker));
                if (longWaitDlg.IsCanceled)
                    return;
            }

            _newDocument = CreateDocTree(_parent.Document);

            dgvAssociateResults.RowCount = 2;
            dgvAssociateResults.Invalidate();

            lblStatusBarResult.Text = GetStatusBarResultString();
        }

        private void checkBoxParsimony_CheckedChanged(object sender, EventArgs e)
        {
            // finding minimal protein list implies removing subset proteins, so force the checkbox on and disable it
            if (FindMinimalProteinList)
            {
                cbRemoveSubsetProteins.Checked = true;
                cbRemoveSubsetProteins.Enabled = false;
            }
            else
                cbRemoveSubsetProteins.Enabled = true;

            UpdateParsimonyResults();
        }

        private void cbGroupProteins_CheckedChanged(object sender, EventArgs e)
        {
            comboSharedPeptides.SelectedIndexChanged -= comboParsimony_SelectedIndexChanged;
            // adjust labels to reflect whether proteins or protein groups are used
            for (int i = 0; i < _sharedPeptideOptionNames.Length; ++i)
                comboSharedPeptides.Items[i] = EnumNames.ResourceManager.GetString(
                                                   (GroupProteins ? @"SharedPeptidesGroup_" : @"SharedPeptides_") +
                                                   _sharedPeptideOptionNames[i]) ??
                                               throw new InvalidOperationException(_sharedPeptideOptionNames[i]);
            comboSharedPeptides.SelectedIndexChanged += comboParsimony_SelectedIndexChanged;

            if (GroupProteins)
            {
                lblMinimalProteinList.Text = Resources.AssociateProteinsDlg_Find_minimal_protein_group_list_that_explains_all_peptides;
                lblRemoveSubsetProteins.Text = Resources.AssociateProteinsDlg_Remove_subset_protein_groups;
                lblMinPeptides.Text = Resources.AssociateProteinsDlg_Min_peptides_per_protein_group;
            }
            else
            {
                var resources = new ComponentResourceManager(typeof(AssociateProteinsDlg));
                lblMinimalProteinList.Text = resources.GetString("lblMinimalProteinList.Text");
                lblRemoveSubsetProteins.Text = resources.GetString("lblRemoveSubsetProteins.Text");
                lblMinPeptides.Text = resources.GetString("lblMinPeptides.Text");
            }

            UpdateParsimonyResults();
        }

        private void comboParsimony_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateParsimonyResults();
        }

        private void numMinPeptides_ValueChanged(object sender, EventArgs e)
        {
            UpdateParsimonyResults();
        }

        // find matches using the background proteome
        public void UseBackgroundProteome()
        {
            var backgroundProteome = new BackgroundProteome(_driverBackgroundProteome.SelectedItem);
            if (backgroundProteome.Equals(BackgroundProteome.NONE))
                return;

            Initialize();

            _isFasta = false;
            //FastaFileName = _parent.Document.Settings.PeptideSettings.BackgroundProteome.DatabasePath;
            
            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.PerformWork(this, 1000, broker => _proteinAssociation.UseBackgroundProteome(backgroundProteome, broker));
                if (longWaitDlg.IsCanceled)
                    return;
            }

            if (Results.PeptidesMapped == 0)
                MessageDlg.Show(this, Resources.AssociateProteinsDlg_UseBackgroundProteome_No_matches_were_found_using_the_background_proteome_);
            UpdateParsimonyResults();
            btnOk.Enabled = true;
        }

        private void btnUseFasta_Click(object sender, EventArgs e)
        {
            ImportFasta();
        }

        private void rbCheckedChanged(object sender, EventArgs e)
        {
            tbxFastaTargets.Enabled = browseFastaTargetsBtn.Enabled = rbFASTA.Checked;
            comboBackgroundProteome.Enabled = rbBackgroundProteome.Checked;
            if (comboBackgroundProteome.Enabled)
                UseBackgroundProteome();
        }

        private void tbxFastaTargets_TextChanged(object sender, EventArgs e)
        {
            if (File.Exists(tbxFastaTargets.Text))
                UseFastaFile(tbxFastaTargets.Text);
        }

        private void comboBackgroundProteome_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverBackgroundProteome.SelectedIndexChangedEvent(sender, e);
            if (comboBackgroundProteome.Enabled)
                UseBackgroundProteome();
        }

        // prompts user to select a fasta file to use for matching proteins
        public void ImportFasta()
        {
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.SkylineWindow_ImportFastaFile_Import_FASTA,
                InitialDirectory = Settings.Default.FastaDirectory,
                CheckPathExists = true,
                Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(Resources.OpenFileDialog_FASTA_files, DataSourceUtil.EXT_FASTA))
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.FastaDirectory = Path.GetDirectoryName(dlg.FileName);
                    FastaFileName = dlg.FileName;
                }
            }
        }

        // find matches using a FASTA file
        // needed for Testing purposes so we can skip ImportFasta() because of the OpenFileDialog
        public void UseFastaFile(string file)
        {
            Initialize();

            _isFasta = true;
            //FastaFileName = file;

            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.PerformWork(this, 1000, broker => _proteinAssociation.UseFastaFile(file, broker));
                if (longWaitDlg.IsCanceled)
                    return;
            }

            if (Results.PeptidesMapped == 0)
                MessageDlg.Show(this, Resources.AssociateProteinsDlg_FindProteinMatchesWithFasta_No_matches_were_found_using_the_imported_fasta_file_);
            UpdateParsimonyResults();
            btnOk.Enabled = true;
        }

        private SrmDocument CreateDocTree(SrmDocument current)
        {
            SrmDocument result = null;
            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.PerformWork(this, 1000, monitor =>
                {
                    result = _proteinAssociation.CreateDocTree(current, monitor);
                });
                if (longWaitDlg.IsCanceled)
                    return null;
            }
            return result;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public AssociateProteinsSettings FormSettings
        {
            get
            {
                var fileName = FastaFileName;
                return new AssociateProteinsSettings(_proteinAssociation.FinalResults, _isFasta ? fileName : null, _isFasta ? null : fileName);
            }
        }

        public void OkDialog()
        {
            if (rbFASTA.Checked)
                Settings.Default.LastProteinAssociationFastaFilepath = tbxFastaTargets.Text;

            lock (_parent.GetDocumentChangeLock())
            {
                _parent.ModifyDocument(Resources.AssociateProteinsDlg_ApplyChanges_Associated_proteins, current => _newDocument, FormSettings.EntryCreator.Create);
            }

            DialogResult = DialogResult.OK;
        }

        private string GetStatusBarResultString()
        {
            if (FinalResults == null || _newDocument == null)
                return string.Empty;

            const int separatorThreshold = 10000;
            var culture = LocalizationHelper.CurrentCulture;
            Func<int, string> resultToString = count => count < separatorThreshold ? count.ToString(culture) : count.ToString(@"N0", culture);

            return string.Format(_statusBarResultFormat, resultToString(FinalResults.FinalProteinCount),
                resultToString(FinalResults.FinalPeptideCount),
                resultToString(_newDocument.PeptideTransitionGroupCount),
                resultToString(_newDocument.PeptideTransitionCount));
        }

        private void dgvAssociateResults_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (FinalResults == null || _newDocument == null)
                return;

            const int separatorThreshold = 10000;
            var culture = LocalizationHelper.CurrentCulture;
            Func<int, string> resultToString = count => count < separatorThreshold ? count.ToString(culture) : count.ToString(@"N0", culture);
            
            const int proteinRowIndex = 0;
            const int peptideRowIndex = 1;

            if (e.ColumnIndex == headerColumn.Index)
            {
                if (e.RowIndex == proteinRowIndex)
                    e.Value = Resources.AnnotationDef_AnnotationTarget_Proteins;
                else if (e.RowIndex == peptideRowIndex)
                    e.Value = Resources.AnnotationDef_AnnotationTarget_Peptides;
            }
            else if (e.ColumnIndex == mappedColumn.Index)
            {
                if (e.RowIndex == proteinRowIndex)
                    e.Value = resultToString(FinalResults.ProteinsMapped);
                else if (e.RowIndex == peptideRowIndex)
                    e.Value = resultToString(FinalResults.PeptidesMapped);
            }
            else if (e.ColumnIndex == unmappedColumn.Index)
            {
                if (e.RowIndex == proteinRowIndex)
                    e.Value = resultToString(FinalResults.ProteinsUnmapped);
                else if (e.RowIndex == peptideRowIndex)
                    e.Value = resultToString(FinalResults.PeptidesUnmapped);
            }
            else if (e.ColumnIndex == targetsColumn.Index)
            {
                if (e.RowIndex == proteinRowIndex)
                    e.Value = resultToString(FinalResults.FinalProteinCount);
                else if (e.RowIndex == peptideRowIndex)
                    e.Value = resultToString(FinalResults.FinalPeptideCount);
            }
        }

        private void lnkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            AssociateProteinsDlg_HelpButtonClicked(sender, e);
        }

        // NB: I'm using a CheckBox/Label pair because there was too much space between just a LinkLabel and the CheckBox text
        private void lblGroupProtein_Click(object sender, EventArgs e)
        {
            cbGroupProteins.Checked = !cbGroupProteins.Checked;
        }

        private void lblMinimalProteinList_Click(object sender, EventArgs e)
        {
            cbMinimalProteinList.Checked = !cbMinimalProteinList.Checked;
        }

        private void lblRemoveSubsetProtein_Click(object sender, EventArgs e)
        {
            cbRemoveSubsetProteins.Checked = !cbRemoveSubsetProteins.Checked;
        }

        private void AssociateProteinsDlg_HelpButtonClicked(object sender, EventArgs e)
        {
            const string proteinAssociationWikiPath = @"/wiki/home/software/Skyline/page.view?name=Skyline%20Protein%20Association%2022.1"; // CONSIDER: get version programatically
            if (sender == lnkHelpFindMinimalProteinList)
                WebHelpers.OpenSkylineLink(this, proteinAssociationWikiPath + @"#minimal-protein-list");
            else if (sender == lnkHelpProteinGroups)
                WebHelpers.OpenSkylineLink(this, proteinAssociationWikiPath + @"#protein-grouping");
            else if (sender == lnkHelpRemoveSubsetProteins)
                WebHelpers.OpenSkylineLink(this, proteinAssociationWikiPath + @"#remove-subset-proteins");
            else if (sender == lnkHelpSharedPeptides)
                WebHelpers.OpenSkylineLink(this, proteinAssociationWikiPath + @"#shared-peptides");
            else
                WebHelpers.OpenSkylineLink(this, proteinAssociationWikiPath);
        }

        private void numMinPeptides_Enter(object sender, EventArgs e)
        {
            // prevent Enter from closing the form when confirming the value of UpDown box
            AcceptButton = null;
        }

        private void numMinPeptides_Leave(object sender, EventArgs e)
        {
            AcceptButton = btnOk;
        }
    }
}
