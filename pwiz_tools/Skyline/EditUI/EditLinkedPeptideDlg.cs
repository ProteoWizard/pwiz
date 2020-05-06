/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.EditUI
{
    public partial class EditLinkedPeptideDlg : Form
    {
        private SrmSettings _settings;
        private ExplicitMods _explicitMods;
        private StaticMod _crosslinkMod;

        public EditLinkedPeptideDlg(SrmSettings settings, LinkedPeptide linkedPeptide, StaticMod crosslinkMod)
        {
            InitializeComponent();
            _settings = settings;
            _crosslinkMod = crosslinkMod;
            if (linkedPeptide != null)
            {
                tbxPeptideSequence.Text = linkedPeptide.Peptide.Sequence;
                tbxAttachmentOrdinal.Text = (linkedPeptide.IndexAa + 1).ToString();
            }
        }

        public LinkedPeptide LinkedPeptide { get; private set; }

        public void OkDialog()
        {
            LinkedPeptide linkedPeptide;
            if (!TryMakeLinkedPeptide(out linkedPeptide))
            {
                return;
            }

            LinkedPeptide = linkedPeptide;
            DialogResult = DialogResult.OK;
        }

        private bool TryMakeLinkedPeptide(out LinkedPeptide linkedPeptide)
        {
            linkedPeptide = null;
            Peptide peptide;
            if (!TryMakePeptide(out peptide))
            {
                return false;
            }

            string peptideSequence = peptide.Sequence;
            var messageBoxHelper = new MessageBoxHelper(this);
            int aaOrdinal;
            if (!messageBoxHelper.ValidateNumberTextBox(tbxAttachmentOrdinal, 1, peptideSequence.Length, out aaOrdinal))
            {
                return false;
            }

            string validAminoAcids = _crosslinkMod?.AAs;
            if (!string.IsNullOrEmpty(validAminoAcids))
            {
                char aa = peptideSequence[aaOrdinal - 1];
                if (!validAminoAcids.Contains(aa))
                {
                    string message = string.Format(Resources.EditLinkedPeptideDlg_TryMakeLinkedPeptide_The_crosslinker___0___cannot_attach_to_the_amino_acid___1___,
                        _crosslinkMod.Name, aa);
                    messageBoxHelper.ShowTextBoxError(tbxAttachmentOrdinal, message);
                    return false;
                }
            }
            linkedPeptide = new LinkedPeptide(peptide, aaOrdinal - 1, MakeExplicitMods(peptide, _explicitMods));
            return true;
        }

        private bool TryMakePeptide(out Peptide peptide)
        {
            peptide = null;
            var messageBoxHelper = new MessageBoxHelper(this);
            var peptideSequence = tbxPeptideSequence.Text.Trim();
            if (string.IsNullOrEmpty(peptideSequence))
            {
                messageBoxHelper.ShowTextBoxError(tbxPeptideSequence, Resources.PasteDlg_ListPeptideSequences_The_peptide_sequence_cannot_be_blank);
                return false;
            }
            if (!FastaSequence.IsExSequence(peptideSequence))
            {
                messageBoxHelper.ShowTextBoxError(tbxPeptideSequence, Resources.PasteDlg_ListPeptideSequences_This_peptide_sequence_contains_invalid_characters);
                return false;
            }

            peptide = new Peptide(peptideSequence);
            return true;
        }

        private ExplicitMods MakeExplicitMods(Peptide peptide, ExplicitMods oldExplicitMods)
        {
            if (oldExplicitMods == null)
            {
                return null;
            }

            var newStaticMods = oldExplicitMods.StaticModifications.Where(mod => mod.IndexAA < peptide.Sequence.Length)
                .ToList();
            var newHeavyMods = new List<TypedExplicitModifications>();
            foreach (var heavyMods in oldExplicitMods.GetHeavyModifications())
            {
                var newMods = heavyMods.Modifications.Where(mod => mod.IndexAA < peptide.Sequence.Length).ToList();
                if (newMods.Count != 0)
                {
                    newHeavyMods.Add(new TypedExplicitModifications(peptide, heavyMods.LabelType, newMods));
                }
            }
            return new ExplicitMods(peptide, newStaticMods, newHeavyMods);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnEditModifications_Click(object sender, EventArgs e)
        {
            ShowEditModifications();
        }

        public void ShowEditModifications()
        {
            Peptide peptide;
            if (!TryMakePeptide(out peptide))
            {
                return;
            }

            var explicitMods = MakeExplicitMods(peptide, _explicitMods);
            var peptideDocNode = new PeptideDocNode(peptide, _settings, explicitMods, null, ExplicitRetentionTimeInfo.EMPTY, new TransitionGroupDocNode[0], false);
            using (var pepModsDlg = new EditPepModsDlg(_settings, peptideDocNode, false))
            {
                if (pepModsDlg.ShowDialog(this) == DialogResult.OK)
                {
                    _explicitMods = pepModsDlg.ExplicitMods;
                }
            }
        }

        public string PeptideSequence
        {
            get { return tbxPeptideSequence.Text; }
            set
            {
                tbxPeptideSequence.Text = value;
            }
        }

        public int? AttachmentOrdinal
        {
            get
            {
                if (string.IsNullOrEmpty(tbxAttachmentOrdinal.Text))
                {
                    return null;
                }
                return int.Parse(tbxAttachmentOrdinal.Text);
            }
            set { tbxAttachmentOrdinal.Text = value.HasValue ? value.ToString() : string.Empty; }
        }
    }
}
