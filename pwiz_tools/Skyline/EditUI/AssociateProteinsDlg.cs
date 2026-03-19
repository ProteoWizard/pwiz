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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.CommonMsData;
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
        private SrmDocument _document;
        private Receiver<AssociateProteinsResults.Parameters, AssociateProteinsResults> _receiver;
        private ProteinAssociation _proteinAssociation;
        private readonly SettingsListComboDriver<BackgroundProteomeSpec> _driverBackgroundProteome;
        public SrmDocument DocumentFinal { get; private set; }

        private string _overrideFastaPath;
        private bool _fastaFileIsTemporary;
        private bool _hasExistingProteinAssociations;
        private readonly IrtStandard _irtStandard;
        private readonly string _decoyGenerationMethod;
        private readonly double _decoysPerTarget;
        private bool _updatingLabels;

        private string _statusBarResultFormat;
        private static string[] _sharedPeptideOptionNames = Enum.GetNames(typeof(ProteinAssociation.SharedPeptides));
        private SkylineWindow _skylineWindow;

        // Store original label text values from the designer for restoration when not grouping proteins
        private readonly string _originalMinimalProteinListText;
        private readonly string _originalRemoveSubsetProteinsText;
        private readonly string _originalMinPeptidesText;

        public string FastaFileName
        {
            get { return tbxFastaTargets.Text; }
            set { tbxFastaTargets.Text = value; }
        }

        public bool DocumentFinalCalculated => IsComplete;

        public AssociateProteinsDlg(SkylineWindow skylineWindow) : this(skylineWindow.DocumentUI)
        {
            _skylineWindow = skylineWindow;
        }


        /// <summary>
        /// Show the Associate Proteins dialog.
        /// </summary>
        /// <param name="document">The Skyline document for which to associate peptides to proteins.</param>
        /// <param name="reuseLastFasta">Set to false to prevent the dialog from using the previously used FASTA filepath as the default textbox value.</param>
        private AssociateProteinsDlg(SrmDocument document, bool reuseLastFasta = true)
        {
            InitializeComponent();
            
            // Save original label text values from the designer for restoration when not grouping proteins
            _originalMinimalProteinListText = lblMinimalProteinList.Text;
            _originalRemoveSubsetProteinsText = lblRemoveSubsetProteins.Text;
            _originalMinPeptidesText = lblMinPeptides.Text;
            
            _document = document;
            if (reuseLastFasta && !string.IsNullOrEmpty(Settings.Default.LastProteinAssociationFastaFilepath))
            {
                tbxFastaTargets.Text = Settings.Default.LastProteinAssociationFastaFilepath;
            }
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

            GroupProteins = peptideSettings.ProteinAssociationSettings?.GroupProteins ?? false;
            GeneLevelParsimony = peptideSettings.ProteinAssociationSettings?.GeneLevelParsimony ?? false;
            FindMinimalProteinList = peptideSettings.ProteinAssociationSettings?.FindMinimalProteinList ?? false;
            RemoveSubsetProteins = peptideSettings.ProteinAssociationSettings?.RemoveSubsetProteins ?? false;
            SelectedSharedPeptides = peptideSettings.ProteinAssociationSettings?.SharedPeptides ?? ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins;
            MinPeptidesPerProtein = peptideSettings.ProteinAssociationSettings?.MinPeptidesPerProtein ?? 1;
            comboSharedPeptides.SelectedIndexChanged += comboParsimony_SelectedIndexChanged;

            _driverBackgroundProteome = new SettingsListComboDriver<BackgroundProteomeSpec>(comboBackgroundProteome, Settings.Default.BackgroundProteomeList);
            _driverBackgroundProteome.LoadList(peptideSettings.BackgroundProteome.Name);

            helpTip.SetToolTip(cbGroupProteins, helpTip.GetToolTip(lblGroupProteins));
            helpTip.SetToolTip(cbGeneLevel, helpTip.GetToolTip(lblGroupAtGeneLevel));
            helpTip.SetToolTip(cbMinimalProteinList, helpTip.GetToolTip(lblMinimalProteinList));
            helpTip.SetToolTip(cbRemoveSubsetProteins, helpTip.GetToolTip(lblRemoveSubsetProteins));
            helpTip.SetToolTip(comboSharedPeptides, helpTip.GetToolTip(lblSharedPeptides));
            helpTip.SetToolTip(numMinPeptides, helpTip.GetToolTip(lblMinPeptides));
        }

        /// <summary>
        /// If results for the current settings are available, then display them.
        /// Otherwise, ensure that the results being worked on match the current settings and
        /// update the progress bar.
        /// </summary>
        private void DisplayResults()
        {
            var results = GetCurrentResults();
            btnOk.Enabled = results?.DocumentFinal != null;
            ErrorMessage = results?.ErrorMessage;
            ErrorException = results?.ErrorException;
            IsComplete = false;
            if (results == null)
            {
                progressBar1.Visible = true;
                btnError.Visible = false;
                progressBar1.Value = _receiver?.GetProgressValue() ?? 0;
                return;
            }

            DocumentFinal = results.DocumentFinal;
            _proteinAssociation = results.ProteinAssociation;
            progressBar1.Visible = false;
            IsComplete = true;

            UpdateTargetCounts();

            if (results.IsErrorResult)
            {
                btnError.Visible = true;
                var message = results.ErrorMessage;
                if (results.ErrorException != null)
                {
                    message = TextUtil.LineSeparate(message, EditUIResources.AssociateProteinsDlg_DisplayResults__Click_for_more_information_);
                }
                helpTip.SetToolTip(btnError, message);
                return;
            }
            if (DocumentFinal != null)
            {
                if (cbGeneLevel.Checked)
                    Settings.Default.ShowPeptidesDisplayMode = ProteinMetadataManager.ProteinDisplayMode.ByGene.ToString();
            }
        }

        private void ProgressChange()
        {
            var progressValue = _receiver?.GetProgressValue() ?? 0;
            progressBar1.Value = progressValue;
        }

        private AssociateProteinsResults GetCurrentResults()
        {
            if (_receiver == null)
            {
                return null;
            }
            var parameters = GetParameters();
            if (_receiver.TryGetProduct(parameters, out var results))
            {
                return results;
            }
            var error = _receiver.GetError();
            if (error != null)
            {
                return new AssociateProteinsResults(parameters).ChangeError(null, error);
            }

            return null;
        }

        /// <summary>
        /// Show the Associate Proteins dialog without allowing the user to control the source of the FASTA records (e.g. when called from the Import Peptide Search wizard).
        /// </summary>
        /// <param name="document">The Skyline document for which to associate peptides to proteins.</param>
        /// <param name="overrideFastaPath">Set to a FASTA filepath to force using that FASTA and remove the user's ability to set the FASTA filepath (the protein source controls will be hidden).</param>
        /// <param name="irtStandard">The iRT standard to preserve iRT peptides at the top of the document.</param>
        /// <param name="decoyGenerationMethod">Decoy generation method.</param>
        /// <param name="decoysPerTarget">Number of decoys per target.</param>
        /// <param name="fastaFileIsTemporary">Set to true if the FASTA filepath should not be reused the next time the form is opened.</param>
        /// <param name="hasExistingProteinAssociations">Set to true if the document already has protein associations (other than from Import Peptide Search's FASTA import).</param>
        public AssociateProteinsDlg(SrmDocument document, string overrideFastaPath, IrtStandard irtStandard,
            string decoyGenerationMethod, double decoysPerTarget, bool fastaFileIsTemporary = false, bool hasExistingProteinAssociations = false) : this(document)
        {
            _overrideFastaPath = overrideFastaPath;
            _irtStandard = irtStandard;
            _decoyGenerationMethod = decoyGenerationMethod;
            _decoysPerTarget = decoysPerTarget;
            _fastaFileIsTemporary = fastaFileIsTemporary;
            _hasExistingProteinAssociations = hasExistingProteinAssociations;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_skylineWindow != null)
            {
                _skylineWindow.DocumentUIChangedEvent += SkylineWindowOnDocumentUIChangedEvent;
                if (!ReferenceEquals(_document, _skylineWindow.DocumentUI))
                {
                    SkylineWindowOnDocumentUIChangedEvent(_skylineWindow, new DocumentChangedEventArgs(_document));
                }
            }

            if (_overrideFastaPath != null)
            {
                tbxFastaTargets.Text = _overrideFastaPath;
                proteinSourcePanel.Visible = false;
                gbParsimonyOptions.Location = FormUtil.Offset(gbParsimonyOptions.Location, 0, -proteinSourcePanel.Height);
                MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height - proteinSourcePanel.Height);
                Height -= proteinSourcePanel.Height;
                lblDescription.Text = EditUIResources.AssociateProteinsDlg_OnShown_Organize_all_document_peptides_into_associated_proteins_or_protein_groups;
                if (_hasExistingProteinAssociations)
                    lblDescription.Text += @" " + EditUIResources.AssociateProteinsDlg_OnShown_Existing_protein_associations_will_be_discarded_;
            }
            else
            {
                numMinPeptides.Visible = lblMinPeptides.Visible = false;
                int minPeptidesHeight = numMinPeptides.Height + lblMinPeptides.Height;
                gbParsimonyOptions.Height -= minPeptidesHeight;
                Height -= minPeptidesHeight;
            }

            if (_document.PeptideCount == 0 && _overrideFastaPath == null)
            {
                MessageDlg.Show(this, Resources.ImportFastaControl_ImportFasta_The_document_does_not_contain_any_peptides_);
                Close();
                return;
            }
            _receiver = AssociateProteinsResults.PRODUCER.RegisterCustomer(this, DisplayResults);
            _receiver.ProgressChange += ProgressChange;

            UpdateParsimonyResults();
        }

        
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_skylineWindow != null)
            {
                _skylineWindow.DocumentUIChangedEvent -= SkylineWindowOnDocumentUIChangedEvent;
            }
            base.OnFormClosed(e);
        }

        private void SkylineWindowOnDocumentUIChangedEvent(object sender, DocumentChangedEventArgs e)
        {
            _document = _skylineWindow.DocumentUI;
            UpdateParsimonyResults();
        }
        
        private AssociateProteinsResults.Parameters GetParameters()
        {
            var parameters = new AssociateProteinsResults.Parameters(_document)
                .ChangeIrtStandard(_irtStandard)
                .ChangeDecoyGenerationMethod(_decoyGenerationMethod)
                .ChangeDecoysPerTarget(_decoysPerTarget)
                .ChangeSharedPeptides(SelectedSharedPeptides);
            if (rbFASTA.Checked)
            {
                parameters = parameters.ChangeFastaFilePath(tbxFastaTargets.Text);
            }

            if (rbBackgroundProteome.Checked)
            {
                var backgroundProteome = new BackgroundProteome(_driverBackgroundProteome.SelectedItem);
                if (!backgroundProteome.Equals(BackgroundProteome.NONE))
                {
                    parameters = parameters.ChangeBackgroundProteome(backgroundProteome);
                }
            }

            var parsimonySettings = new ProteinAssociation.ParsimonySettings(GroupProteins, GeneLevelParsimony,
                FindMinimalProteinList, RemoveSubsetProteins, SelectedSharedPeptides, MinPeptidesPerProtein);
            parameters = parameters.ChangeParsimonySettings(parsimonySettings);
            
            return parameters;
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
        public bool GeneLevelParsimony
        {
            get => cbGeneLevel.Checked;
            set => cbGeneLevel.Checked = value;
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
            set => numMinPeptides.Value = Math.Max(numMinPeptides.Minimum, value);
        }

        private void UpdateTargetCounts()
        {
            var finalDocument = DocumentFinal;
            lblStatusBarResult.Text = GetStatusBarResultString();
            if (finalDocument == null)
            {
                dgvAssociateResults.Rows.Clear();
                return;
            }

            if (dgvAssociateResults.RowCount != 3)
            {
                dgvAssociateResults.Rows.Clear();
                dgvAssociateResults.Rows.Add(3);
                dgvAssociateResults.ClearSelection();
            }

            var proteinRow = dgvAssociateResults.Rows[0];
            var peptideRow = dgvAssociateResults.Rows[1];
            var sharedRow = dgvAssociateResults.Rows[2];
            proteinRow.Cells[headerColumn.Index].Value = Resources.AnnotationDef_AnnotationTarget_Proteins;
            SetCellValue(proteinRow.Cells[mappedColumn.Index], FinalResults.ProteinsMapped);
            SetCellValue(proteinRow.Cells[unmappedColumn.Index], FinalResults.ProteinsUnmapped);
            SetCellValue(proteinRow.Cells[targetsColumn.Index], FinalResults.FinalProteinCount);
            
            peptideRow.Cells[headerColumn.Index].Value = Resources.AnnotationDef_AnnotationTarget_Peptides;
            SetCellValue(peptideRow.Cells[mappedColumn.Index], FinalResults.PeptidesMapped);
            SetCellValue(peptideRow.Cells[unmappedColumn.Index], FinalResults.PeptidesUnmapped);
            SetCellValue(peptideRow.Cells[targetsColumn.Index], FinalResults.FinalPeptideCount);

            sharedRow.Cells[headerColumn.Index].Value =
                EditUIResources.AssociateProteinsDlg_CellValueNeeded_Shared_Peptides;
            SetCellValue(sharedRow.Cells[mappedColumn.Index], FinalResults.TotalSharedPeptideCount);
            sharedRow.Cells[unmappedColumn.Index].Value = null;
            SetCellValue(sharedRow.Cells[targetsColumn.Index], FinalResults.FinalSharedPeptideCount);
        }

        private void SetCellValue(DataGridViewCell cell, int value)
        {
            cell.Value = value;
            cell.Style.Format = value >= 10_000 ? @"N0" : string.Empty;
        }

        private void UpdateParsimonyResults()
        {
            DisplayResults();
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
        private void IfNotUpdatingLabels(Action action)
        {
            if (!_updatingLabels)
            {
                try
                {
                    _updatingLabels = true;
                    action();
                }
                finally
                {
                    _updatingLabels = false;
                }
            }
        }

        private void cbGroupProteins_CheckedChanged(object sender, EventArgs e)
        {
            // setting gene level parsimony forces protein grouping on, so do nothing in that case
            if (GeneLevelParsimony)
                return;

            IfNotUpdatingLabels(() =>
            {
                // adjust labels to reflect whether proteins or protein groups are used
                for (int i = 0; i < _sharedPeptideOptionNames.Length; ++i)
                    comboSharedPeptides.Items[i] = EnumNames.ResourceManager.GetString(
                                                       (GroupProteins ? @"SharedPeptidesGroup_" : @"SharedPeptides_") +
                                                       _sharedPeptideOptionNames[i]) ??
                                                   throw new InvalidOperationException(_sharedPeptideOptionNames[i]);
            });

            if (GroupProteins)
            {
                lblMinimalProteinList.Text = EditUIResources.AssociateProteinsDlg_Find_minimal_protein_group_list_that_explains_all_peptides;
                lblRemoveSubsetProteins.Text = EditUIResources.AssociateProteinsDlg_Remove_subset_protein_groups;
                lblMinPeptides.Text = EditUIResources.AssociateProteinsDlg_Min_peptides_per_protein_group;
            }
            else
            {
                // Restore original label text from the designer (saved in constructor)
                lblMinimalProteinList.Text = _originalMinimalProteinListText;
                lblRemoveSubsetProteins.Text = _originalRemoveSubsetProteinsText;
                lblMinPeptides.Text = _originalMinPeptidesText;
            }

            UpdateParsimonyResults();
        }

        private void cbGeneLevel_CheckedChanged(object sender, EventArgs e)
        {
            IfNotUpdatingLabels(() =>
            {
                // adjust labels to reflect whether genes or protein groups are used
                for (int i = 0; i < _sharedPeptideOptionNames.Length; ++i)
                comboSharedPeptides.Items[i] = EnumNames.ResourceManager.GetString(
                                                   (GeneLevelParsimony ? @"SharedPeptidesGene_" : @"SharedPeptidesGroup_") +
                                                   _sharedPeptideOptionNames[i]) ??
                                               throw new InvalidOperationException(_sharedPeptideOptionNames[i]);
            });

            // gene level parsimony implies grouping, so force the checkbox on and disable it
            if (GeneLevelParsimony)
            {
                cbGroupProteins.Checked = true;
                cbGroupProteins.Enabled = false;
                lblMinimalProteinList.Text = EditUIResources.AssociateProteinsDlg_Find_minimal_gene_group_list_that_explains_all_peptides;
                lblRemoveSubsetProteins.Text = EditUIResources.AssociateProteinsDlg_Remove_subset_genes;
                lblMinPeptides.Text = EditUIResources.AssociateProteinsDlg_Min_peptides_per_gene;
            }
            else
            {
                cbGroupProteins.Enabled = true;
                lblMinimalProteinList.Text = EditUIResources.AssociateProteinsDlg_Find_minimal_protein_group_list_that_explains_all_peptides;
                lblRemoveSubsetProteins.Text = EditUIResources.AssociateProteinsDlg_Remove_subset_protein_groups;
                lblMinPeptides.Text = EditUIResources.AssociateProteinsDlg_Min_peptides_per_protein_group;
            }

            UpdateParsimonyResults();
        }

        private void comboParsimony_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_updatingLabels)
                return;

            UpdateParsimonyResults();
        }

        private void numMinPeptides_ValueChanged(object sender, EventArgs e)
        {
            UpdateParsimonyResults();
        }

        // find matches using the background proteome
        public void UseBackgroundProteome()
        {
            UpdateParsimonyResults();
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
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = Resources.SkylineWindow_ImportFastaFile_Import_FASTA;
                dlg.InitialDirectory = Settings.Default.FastaDirectory;
                dlg.CheckPathExists = true;
                dlg.Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(EditUIResources.OpenFileDialog_FASTA_files, DataSourceUtil.EXT_FASTA));
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
            tbxFastaTargets.Text = file;
            DisplayResults();
        }

        private SrmDocument CreateDocTree(SrmDocument current)
        {
            SrmDocument result = null;
            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.PerformWork(this, 1000, monitor =>
                {
                    result = _proteinAssociation.CreateDocTree(current, monitor);
                    if (result != null)
                        result = AddIrtAndDecoys(result);

                });
                if (longWaitDlg.IsCanceled)
                    return null;
            }

            if (cbGeneLevel.Checked)
                Settings.Default.ShowPeptidesDisplayMode = ProteinMetadataManager.ProteinDisplayMode.ByGene.ToString();

            return result;
        }

        private SrmDocument AddIrtAndDecoys(SrmDocument document)
        {
            var result = AddDecoys(document);

            if (_overrideFastaPath != null)
                result = ImportPeptideSearch.AddStandardsToDocument(result, _irtStandard);

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
                var parameters = GetParameters();
                string fastaPath = parameters.FastaFilePath;
                if (_overrideFastaPath != null)
                {
                    fastaPath = null;
                }
                return new AssociateProteinsSettings(_proteinAssociation, fastaPath,
                    parameters.BackgroundProteome?.DatabasePath);
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

        public bool IsOkEnabled
        {
            get => btnOk.Enabled;
        }

        public void OkDialog()
        {
            if (!IsOkEnabled)
            {
                throw new InvalidOperationException();
            }

            if (!ModifyDocumentInDocumentContainer())
            {
                return;
            }
            if (rbFASTA.Checked && !_fastaFileIsTemporary)
                Settings.Default.LastProteinAssociationFastaFilepath = tbxFastaTargets.Text;

            DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// If <see cref="_skylineWindow"/> is not null, call "ModifyDocument" with the result of
        /// the protein association.
        /// Returns false if the operation was cancelled.
        /// </summary>
        private bool ModifyDocumentInDocumentContainer()
        {
            if (_skylineWindow == null)
            {
                return true;
            }

            lock (_skylineWindow.GetDocumentChangeLock())
            {
                if (!ReferenceEquals(_skylineWindow.Document, _document))
                {
                    using var longWaitDlg = new LongWaitDlg();
                    longWaitDlg.PerformWork(this, 1000, WaitUntilDocumentCurrent);
                    if (!ReferenceEquals(_skylineWindow.Document, _document) || DocumentFinal == null)
                    {
                        return false;
                    }
                }

                _skylineWindow.ModifyDocument(Resources.AssociateProteinsDlg_ApplyChanges_Associated_proteins,
                    current =>
                    {
                        Assume.IsTrue(ReferenceEquals(current, _document));
                        return DocumentFinal;
                    },
                    FormSettings.EntryCreator.Create);
                return true;
            }
        }

        private void WaitUntilDocumentCurrent(ILongWaitBroker longWaitBroker)
        {
            object notifyObject = new object();
            Action progressChange = () => longWaitBroker.ProgressValue = _receiver.GetProgressValue();
            Action productAvailable = () =>
            {
                lock (notifyObject)
                {
                    Monitor.Pulse(notifyObject);
                }
            };
            try
            {
                _receiver.ProgressChange += progressChange;
                _receiver.ProductAvailable += productAvailable;
                using var cancellationTokenRegistration = longWaitBroker.CancellationToken.Register(productAvailable);
                while (true)
                {
                    lock (notifyObject)
                    {
                        if (longWaitBroker.IsCanceled)
                        {
                            return;
                        }
                        if (DocumentFinal != null && ReferenceEquals(_document, _skylineWindow.Document))
                        {
                            return;
                        }
                        Monitor.Wait(notifyObject);
                    }
                }
            }
            finally
            {
                _receiver.ProductAvailable -= productAvailable;
                _receiver.ProgressChange -= progressChange;
            }
        }

        private string GetStatusBarResultString()
        {
            if (FinalResults == null || DocumentFinal == null)
                return string.Empty;

            const int separatorThreshold = 10000;
            var culture = LocalizationHelper.CurrentCulture;
            Func<int, string> resultToString = count => count < separatorThreshold ? count.ToString(culture) : count.ToString(@"N0", culture);

            return string.Format(_statusBarResultFormat, resultToString(DocumentFinal.PeptideGroupCount),
                resultToString(DocumentFinal.PeptideCount),
                resultToString(DocumentFinal.PeptideTransitionGroupCount),
                resultToString(DocumentFinal.PeptideTransitionCount));
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

        private void lblGroupAtGeneLevel_Click(object sender, EventArgs e)
        {
            cbGeneLevel.Checked = !cbGeneLevel.Checked;
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
            NewTargetsFinalSync(out proteins, out peptides, out precursors, out transitions, out _);
        }

        public void NewTargetsFinalSync(out int proteins, out int peptides, out int precursors, out int transitions, out int unmappedOrRemoved)
        {
            Assume.IsTrue(IsComplete, @"NewTargetsFinalSync: IsComplete");
            var doc = DocumentFinal;
            Assume.IsNotNull(doc, @"NewTargetsFinalSync: DocumentFinal");
            Assume.IsNotNull(_proteinAssociation, @"NewTargetsFinalSync: _proteinAssociation");
            var unmappedPeptideGroup = doc.PeptideGroups.FirstOrDefault(pg => pg.Name == Resources.ProteinAssociation_CreateDocTree_Unmapped_Peptides);
            unmappedOrRemoved = _proteinAssociation.PeptidesRemovedByFiltersCount + (unmappedPeptideGroup?.PeptideCount ?? 0);
            proteins = doc.PeptideGroupCount;
            peptides = doc.PeptideCount;
            precursors = doc.PeptideTransitionGroupCount;
            transitions = doc.PeptideTransitionCount;
        }

        private void btnError_Click(object sender, EventArgs e)
        {
            var exception = ErrorException;
            if (exception == null)
            {
                return;
            }
            MessageDlg.ShowWithException(this, ErrorMessage, exception);
        }

        public bool IsComplete
        {
            get; private set;
        }

        public string ErrorMessage { get; private set; }
        public Exception ErrorException { get; private set; }

        public void ClickErrorButton()
        {
            if (!btnError.Visible)
            {
                throw new InvalidOperationException();
            }
            btnError.PerformClick();
        }

        private Bitmap ScaleIcon(Icon icon, int width, int height)
        {
            var bitmap = new Bitmap(width, height);
            using var g = Graphics.FromImage(bitmap);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawIcon(icon, new Rectangle(0, 0, width, height));
            return bitmap;
        }
    }
}
