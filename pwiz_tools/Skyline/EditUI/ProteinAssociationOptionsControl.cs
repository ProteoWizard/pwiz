/*
 * Original author: Yuval Boss <yuval .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Windows.Forms;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    /// <summary>
    /// The protein parsimony options: protein grouping, gene-level parsimony, how shared peptides are
    /// assigned, the minimal protein list, subset proteins and the minimum peptides per protein.
    /// Shared by <see cref="AssociateProteinsDlg"/>, which shows their effect live, and the Run
    /// Peptide Search wizard, which applies them after the search.
    /// </summary>
    public partial class ProteinAssociationOptionsControl : UserControl
    {
        public const string PROTEIN_ASSOCIATION_WIKI_PATH = @"/wiki/home/software/Skyline/page.view?name=Skyline%20Protein%20Association%2022.1"; // CONSIDER: get version programatically

        private static readonly string[] SHARED_PEPTIDE_OPTION_NAMES = Enum.GetNames(typeof(ProteinAssociation.SharedPeptides));

        // Original label text values from the designer, restored when not grouping proteins
        private readonly string _originalMinimalProteinListText;
        private readonly string _originalRemoveSubsetProteinsText;
        private readonly string _originalMinPeptidesText;
        private bool _updatingLabels;
        private IButtonControl _acceptButton;

        public ProteinAssociationOptionsControl()
        {
            InitializeComponent();

            gbParsimonyOptions.Dock = DockStyle.Fill;
            _originalMinimalProteinListText = lblMinimalProteinList.Text;
            _originalRemoveSubsetProteinsText = lblRemoveSubsetProteins.Text;
            _originalMinPeptidesText = lblMinPeptides.Text;

            foreach (var sharedPeptides in SHARED_PEPTIDE_OPTION_NAMES)
                comboSharedPeptides.Items.Add(EnumNames.ResourceManager.GetString(@"SharedPeptides_" + sharedPeptides) ?? throw new InvalidOperationException(sharedPeptides));
            SelectedSharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins;
            comboSharedPeptides.SelectedIndexChanged += comboParsimony_SelectedIndexChanged;

            helpTip.SetToolTip(cbGroupProteins, helpTip.GetToolTip(lblGroupProteins));
            helpTip.SetToolTip(cbGeneLevel, helpTip.GetToolTip(lblGroupAtGeneLevel));
            helpTip.SetToolTip(cbMinimalProteinList, helpTip.GetToolTip(lblMinimalProteinList));
            helpTip.SetToolTip(cbRemoveSubsetProteins, helpTip.GetToolTip(lblRemoveSubsetProteins));
            helpTip.SetToolTip(comboSharedPeptides, helpTip.GetToolTip(lblSharedPeptides));
            helpTip.SetToolTip(numMinPeptides, helpTip.GetToolTip(lblMinPeptides));
        }

        /// <summary>
        /// Raised when the user (or code) changes any of the options.
        /// </summary>
        public event EventHandler OptionsChanged;

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

        public ProteinAssociation.ParsimonySettings ParsimonySettings
        {
            get => new ProteinAssociation.ParsimonySettings(GroupProteins, GeneLevelParsimony, FindMinimalProteinList,
                RemoveSubsetProteins, SelectedSharedPeptides, MinPeptidesPerProtein);
            set
            {
                value ??= ProteinAssociation.ParsimonySettings.DEFAULT;
                GroupProteins = value.GroupProteins;
                GeneLevelParsimony = value.GeneLevelParsimony;
                FindMinimalProteinList = value.FindMinimalProteinList;
                RemoveSubsetProteins = value.RemoveSubsetProteins;
                SelectedSharedPeptides = value.SharedPeptides;
                MinPeptidesPerProtein = value.MinPeptidesPerProtein;
            }
        }

        /// <summary>
        /// Hides the minimum peptides per protein option and returns how much shorter the control got.
        /// </summary>
        public int HideMinPeptides()
        {
            numMinPeptides.Visible = lblMinPeptides.Visible = false;
            int minPeptidesHeight = numMinPeptides.Height + lblMinPeptides.Height;
            Height -= minPeptidesHeight;
            return minPeptidesHeight;
        }

        private void OnOptionsChanged()
        {
            OptionsChanged?.Invoke(this, EventArgs.Empty);
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

            OnOptionsChanged();
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
                for (int i = 0; i < SHARED_PEPTIDE_OPTION_NAMES.Length; ++i)
                    comboSharedPeptides.Items[i] = EnumNames.ResourceManager.GetString(
                                                       (GroupProteins ? @"SharedPeptidesGroup_" : @"SharedPeptides_") +
                                                       SHARED_PEPTIDE_OPTION_NAMES[i]) ??
                                                   throw new InvalidOperationException(SHARED_PEPTIDE_OPTION_NAMES[i]);
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

            OnOptionsChanged();
        }

        private void cbGeneLevel_CheckedChanged(object sender, EventArgs e)
        {
            IfNotUpdatingLabels(() =>
            {
                // adjust labels to reflect whether genes or protein groups are used
                for (int i = 0; i < SHARED_PEPTIDE_OPTION_NAMES.Length; ++i)
                comboSharedPeptides.Items[i] = EnumNames.ResourceManager.GetString(
                                                   (GeneLevelParsimony ? @"SharedPeptidesGene_" : @"SharedPeptidesGroup_") +
                                                   SHARED_PEPTIDE_OPTION_NAMES[i]) ??
                                               throw new InvalidOperationException(SHARED_PEPTIDE_OPTION_NAMES[i]);
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

            OnOptionsChanged();
        }

        private void comboParsimony_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_updatingLabels)
                return;

            OnOptionsChanged();
        }

        private void numMinPeptides_ValueChanged(object sender, EventArgs e)
        {
            OnOptionsChanged();
        }

        private void lnkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string anchor = null;
            if (sender == lnkHelpFindMinimalProteinList)
                anchor = @"#minimal-protein-list";
            else if (sender == lnkHelpProteinGroups)
                anchor = @"#protein-grouping";
            else if (sender == lnkHelpRemoveSubsetProteins)
                anchor = @"#remove-subset-proteins";
            else if (sender == lnkHelpSharedPeptides)
                anchor = @"#shared-peptides";
            WebHelpers.OpenSkylineLink(this, PROTEIN_ASSOCIATION_WIKI_PATH + anchor);
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

        private void numMinPeptides_Enter(object sender, EventArgs e)
        {
            // prevent Enter from closing the form or leaving the wizard page when confirming the value of UpDown box
            var form = FindForm();
            if (form != null)
            {
                _acceptButton = form.AcceptButton;
                form.AcceptButton = null;
            }
        }

        private void numMinPeptides_Leave(object sender, EventArgs e)
        {
            var form = FindForm();
            if (form != null && _acceptButton != null)
                form.AcceptButton = _acceptButton;
            _acceptButton = null;
        }
    }
}
