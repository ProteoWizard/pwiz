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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Irt;
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
        private readonly SrmDocument _document;
        private bool _isFasta;
        private ProteinAssociation _proteinAssociation;
        private readonly SettingsListComboDriver<BackgroundProteomeSpec> _driverBackgroundProteome;
        public SrmDocument DocumentFinal { get; private set; }

        private bool _reuseLastFasta;
        private string _overrideFastaPath;
        private readonly IrtStandard _irtStandard;
        private readonly string _decoyGenerationMethod;
        private readonly double _decoysPerTarget;

        private string _statusBarResultFormat;
        private static string[] _sharedPeptideOptionNames = Enum.GetNames(typeof(ProteinAssociation.SharedPeptides));

        public string FastaFileName
        {
            get { return tbxFastaTargets.Text; }
            set { tbxFastaTargets.Text = value; }
        }

        public bool IsBusy => DocumentFinal == null;
        public bool DocumentFinalCalculated => !IsBusy || _document.PeptideCount == 0;


        /// <summary>
        /// Show the Associate Proteins dialog.
        /// </summary>
        /// <param name="document">The Skyline document for which to associate peptides to proteins.</param>
        /// <param name="reuseLastFasta">Set to false to prevent the dialog from using the previously used FASTA filepath as the default textbox value.</param>
        public AssociateProteinsDlg(SrmDocument document, bool reuseLastFasta = true)
        {
            InitializeComponent();
            _document = document;
            _reuseLastFasta = reuseLastFasta;
            _statusBarResultFormat = string.Format(@"{{0}} {0}, {{1}} {1}, {{2}} {2}, {{3}} {3}",
                Resources.AnnotationDef_AnnotationTarget_Proteins,
                Resources.AnnotationDef_AnnotationTarget_Peptides,
                Resources.AnnotationDef_AnnotationTarget_Precursors,
                Resources.AnnotationDef_AnnotationTarget_Transitions).ToLower();
            btnOk.Enabled = false;
            lblStatusBarResult.Text = string.Empty;

            var peptideSettings = document.Settings.PeptideSettings;

            comboSharedPeptides.SelectedIndexChanged -= comboParsimony_SelectedIndexChanged;
            foreach (var sharedPeptides in _sharedPeptideOptionNames)
                comboSharedPeptides.Items.Add(EnumNames.ResourceManager.GetString(@"SharedPeptides_" + sharedPeptides) ?? throw new InvalidOperationException(sharedPeptides));

            GroupProteins = peptideSettings.Filter.ParsimonySettings?.GroupProteins ?? false;
            FindMinimalProteinList = peptideSettings.Filter.ParsimonySettings?.FindMinimalProteinList ?? false;
            RemoveSubsetProteins = peptideSettings.Filter.ParsimonySettings?.RemoveSubsetProteins ?? false;
            SelectedSharedPeptides = peptideSettings.Filter.ParsimonySettings?.SharedPeptides ?? ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins;
            comboSharedPeptides.SelectedIndexChanged += comboParsimony_SelectedIndexChanged;

            _driverBackgroundProteome = new SettingsListComboDriver<BackgroundProteomeSpec>(comboBackgroundProteome, Settings.Default.BackgroundProteomeList);
            _driverBackgroundProteome.LoadList(peptideSettings.BackgroundProteome.Name);

            helpTip.SetToolTip(cbGroupProteins, helpTip.GetToolTip(lblGroupProteins));
            helpTip.SetToolTip(cbMinimalProteinList, helpTip.GetToolTip(lblMinimalProteinList));
            helpTip.SetToolTip(cbRemoveSubsetProteins, helpTip.GetToolTip(lblRemoveSubsetProteins));
            helpTip.SetToolTip(comboSharedPeptides, helpTip.GetToolTip(lblSharedPeptides));
            helpTip.SetToolTip(numMinPeptides, helpTip.GetToolTip(lblMinPeptides));
        }

        /// <summary>
        /// Show the Associate Proteins dialog.
        /// </summary>
        /// <param name="document">The Skyline document for which to associate peptides to proteins.</param>
        /// <param name="overrideFastaPath">Set to a FASTA filepath to force using that FASTA and remove the user's ability to set the FASTA filepath (the protein source controls will be hidden).</param>
        /// <param name="irtStandard">The iRT standard to preserve iRT peptides at the top of the document.</param>
        /// <param name="decoyGenerationMethod">Decoy generation method.</param>
        /// <param name="decoysPerTarget">Number of decoys per target.</param>
        public AssociateProteinsDlg(SrmDocument document, string overrideFastaPath, IrtStandard irtStandard, string decoyGenerationMethod, double decoysPerTarget) : this(document)
        {
            _overrideFastaPath = overrideFastaPath;
            _irtStandard = irtStandard;
            _decoyGenerationMethod = decoyGenerationMethod;
            _decoysPerTarget = decoysPerTarget;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_overrideFastaPath != null)
            {
                proteinSourcePanel.Visible = false;
                gbParsimonyOptions.Location = FormUtil.Offset(gbParsimonyOptions.Location, 0, -proteinSourcePanel.Height);
                MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height - proteinSourcePanel.Height);
                Height -= proteinSourcePanel.Height;
                lblDescription.Text = Resources.AssociateProteinsDlg_OnShown_Organize_all_document_peptides_into_associated_proteins_or_protein_groups;
                if (_document.PeptideGroups.Any(pg => pg.IsProtein))
                    lblDescription.Text += @" " + Resources.AssociateProteinsDlg_OnShown_Existing_protein_associations_will_be_discarded_;
            }

            if (_document.PeptideCount == 0)
            {
                if (_overrideFastaPath == null)
                {
                    MessageDlg.Show(this, Resources.ImportFastaControl_ImportFasta_The_document_does_not_contain_any_peptides_);
                    Close();
                }
                else
                {
                    DocumentFinal = AddIrtAndDecoys(_document);
                    return;
                }
            }

            if (_overrideFastaPath != null)
                tbxFastaTargets.Text = _overrideFastaPath;
            else if (_reuseLastFasta && !Settings.Default.LastProteinAssociationFastaFilepath.IsNullOrEmpty())
                tbxFastaTargets.Text = Settings.Default.LastProteinAssociationFastaFilepath;

        }

        private void Initialize()
        {
            if (_proteinAssociation != null || _document.PeptideCount == 0)
                return;

            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.PerformWork(this, 1000, broker =>
                {
                    _proteinAssociation = new ProteinAssociation(_document, broker);
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
            DocumentFinal = null;
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

            DocumentFinal = CreateDocTree(_document);

            dgvAssociateResults.RowCount = 2;
            dgvAssociateResults.ClearSelection();
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

        private IEnumerable<Peptide> DigestProteinToPeptides(FastaSequence sequence)
        {
            var peptideSettings = _document.Settings.PeptideSettings;
            return peptideSettings.Enzyme.Digest(sequence, peptideSettings.DigestSettings);
            // CONSIDER: should AssociateProteinsDlg use the length filters? The old PeptidePerProteinDlg doesn't seem to.
                //peptideSettings.Filter.MaxPeptideLength, peptideSettings.Filter.MinPeptideLength);
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

            if (_document.PeptideCount == 0)
                return;

            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.PerformWork(this, 1000, broker => _proteinAssociation.UseBackgroundProteome(backgroundProteome, DigestProteinToPeptides, broker));
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

            if (_document.PeptideCount == 0)
                return;

            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.PerformWork(this, 1000, broker => _proteinAssociation.UseFastaFile(file, DigestProteinToPeptides, broker));
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
                    result = AddIrtAndDecoys(result);

                });
                if (longWaitDlg.IsCanceled)
                    return null;
            }
            return result;
        }

        private SrmDocument AddIrtAndDecoys(SrmDocument document)
        {
            var result = AddDecoys(document);

            if (_overrideFastaPath != null)
                result = ImportPeptideSearch.AddStandardsToDocument(result, _irtStandard);

            // Move iRT proteins to top
            var irtPeptides = new HashSet<Target>(RCalcIrt.IrtPeptides(result));
            var proteins = new List<PeptideGroupDocNode>(result.PeptideGroups);
            var proteinsIrt = new List<PeptideGroupDocNode>();
            for (var i = 0; i < proteins.Count; i++)
            {
                var nodePepGroup = proteins[i];
                if (nodePepGroup.Peptides.All(nodePep => irtPeptides.Contains(new Target(nodePep.ModifiedSequence))))
                {
                    proteinsIrt.Add(nodePepGroup);
                    proteins.RemoveAt(i--);
                }
            }
            if (proteinsIrt.Any())
                return (SrmDocument)result.ChangeChildrenChecked(proteinsIrt.Concat(proteins).Cast<DocNode>().ToArray());

            return result;
        }

        private int NumDecoys(IEnumerable<PeptideGroupDocNode> pepGroups)
        {
            return !string.IsNullOrEmpty(_decoyGenerationMethod) && _decoysPerTarget > 0
                ? (int)Math.Round(pepGroups.Sum(pepGroup => pepGroup.PeptideCount) * _decoysPerTarget)
                : 0;
        }

        private SrmDocument AddDecoys(SrmDocument document)
        {
            var numDecoys = NumDecoys(document.PeptideGroups);
            return numDecoys > 0
                ? new RefinementSettings { DecoysMethod = _decoyGenerationMethod, NumberOfDecoys = numDecoys }.GenerateDecoys(document)
                : document;
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
                return new AssociateProteinsSettings(_proteinAssociation, _isFasta && _overrideFastaPath == null ? fileName : null, _isFasta ? null : fileName);
            }
        }

        public int MinPeptides
        {
            //get => MinPeptidesPerProtein;
            set => MinPeptidesPerProtein = value;
        }

        public void SetRepeatedDuplicatePeptides(bool removeRepeated, bool removeDuplicate)
        {
            if (removeDuplicate)
                SelectedSharedPeptides = ProteinAssociation.SharedPeptides.Removed;
            else if (removeRepeated)
                SelectedSharedPeptides = ProteinAssociation.SharedPeptides.AssignedToFirstProtein;
            else
                SelectedSharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins;
        }

        public bool RemoveRepeatedPeptides
        {
            get => SelectedSharedPeptides == ProteinAssociation.SharedPeptides.AssignedToFirstProtein ||
                   SelectedSharedPeptides == ProteinAssociation.SharedPeptides.Removed;
            set => SetRepeatedDuplicatePeptides(value, RemoveDuplicatePeptides);
        }

        public bool RemoveDuplicatePeptides
        {
            get => SelectedSharedPeptides == ProteinAssociation.SharedPeptides.Removed;
            set => SetRepeatedDuplicatePeptides(RemoveRepeatedPeptides, value);
        }

        public void OkDialog()
        {
            if (rbFASTA.Checked)
                Settings.Default.LastProteinAssociationFastaFilepath = tbxFastaTargets.Text;

            DialogResult = DialogResult.OK;
        }

        private string GetStatusBarResultString()
        {
            if (FinalResults == null || DocumentFinal == null)
                return string.Empty;

            const int separatorThreshold = 10000;
            var culture = LocalizationHelper.CurrentCulture;
            Func<int, string> resultToString = count => count < separatorThreshold ? count.ToString(culture) : count.ToString(@"N0", culture);

            return string.Format(_statusBarResultFormat, resultToString(FinalResults.FinalProteinCount),
                resultToString(FinalResults.FinalPeptideCount),
                resultToString(DocumentFinal.PeptideTransitionGroupCount),
                resultToString(DocumentFinal.PeptideTransitionCount));
        }

        private void dgvAssociateResults_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (FinalResults == null || DocumentFinal == null)
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


        /*public void NewTargetsAll(out int proteins, out int peptides, out int precursors, out int transitions)
        {
            var pepGroups = new List<PeptideGroupDocNode>(_document.PeptideGroups);
            var numDecoys = NumDecoys(pepGroups);
            if (numDecoys > 0)
            {
                var decoyGroups = AddDecoys(_document).PeptideGroups.Where(pepGroup => Equals(pepGroup.Name, PeptideGroup.DECOYS));
                pepGroups.AddRange(decoyGroups);
            }

            var docWithStandards = ImportPeptideSearch.AddStandardsToDocument(_document, _irtStandard);
            if (!ReferenceEquals(docWithStandards, _document))
            {
                pepGroups.Add(docWithStandards.PeptideGroups.First());
            }

            proteins = pepGroups.Count + Results.ProteinsUnmapped;
            peptides = pepGroups.Sum(pepGroup => pepGroup.PeptideCount);
            precursors = pepGroups.Sum(pepGroup => pepGroup.TransitionGroupCount);
            transitions = pepGroups.Sum(pepGroup => pepGroup.TransitionCount);
        }*/

        public void NewTargetsFinal(out int proteins, out int peptides, out int precursors, out int transitions)
        {
            if (DocumentFinal == null)
                throw new Exception();
            proteins = DocumentFinal.PeptideGroupCount;
            peptides = DocumentFinal.PeptideCount;
            precursors = DocumentFinal.PeptideTransitionGroupCount;
            transitions = DocumentFinal.PeptideTransitionCount;
        }

        public void NewTargetsFinalSync(out int proteins, out int peptides, out int precursors, out int transitions)
        {
            int? emptyProteins;
            NewTargetsFinalSync(out proteins, out peptides, out precursors, out transitions, out emptyProteins);
        }

        public void NewTargetsFinalSync(out int proteins, out int peptides, out int precursors, out int transitions, out int? emptyProteins)
        {
            var doc = DocumentFinal;
            emptyProteins = 0;
            proteins = doc.PeptideGroupCount;
            peptides = doc.PeptideCount;
            precursors = doc.PeptideTransitionGroupCount;
            transitions = doc.PeptideTransitionCount;
        }
    }
}
