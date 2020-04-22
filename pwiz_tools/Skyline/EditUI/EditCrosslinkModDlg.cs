using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.EditUI
{
    public partial class EditCrosslinkModDlg : Form
    {
        private ExplicitMods _explicitMods;

        public EditCrosslinkModDlg(LinkedPeptide linkedPeptide)
        {
            InitializeComponent();
            if (linkedPeptide != null)
            {
                tbxPeptideSequence.Text = linkedPeptide.Peptide.Sequence;
                tbxAttachmentOrdinal.Text = (linkedPeptide.IndexAa + 1).ToString();
            }
        }

        public LinkedPeptide LinkedPeptide { get; private set; }

        public void OkDialog()
        {
            LinkedPeptide linkedPeptide = null;
            if (!TryMakeLinkedPeptide(out linkedPeptide))
            {
                return;
            }

            LinkedPeptide = linkedPeptide;
            DialogResult = DialogResult.OK;
        }

        public bool TryMakeLinkedPeptide(out LinkedPeptide linkedPeptide)
        {
            linkedPeptide = null;
            var messageBoxHelper = new MessageBoxHelper(this);
            var peptideSequence = tbxPeptideSequence.Text.Trim();
            if (string.IsNullOrEmpty(peptideSequence))
            {
                linkedPeptide = null;
                return true;
            }
            if (!FastaSequence.IsExSequence(peptideSequence))
            {
                messageBoxHelper.ShowTextBoxError(tbxPeptideSequence, Resources.PasteDlg_ListPeptideSequences_This_peptide_sequence_contains_invalid_characters);
                return false;
            }

            int aaOrdinal;
            if (!messageBoxHelper.ValidateNumberTextBox(tbxAttachmentOrdinal, 1, peptideSequence.Length, out aaOrdinal))
            {
                return false;
            }
            var peptide = new Peptide(peptideSequence);
            linkedPeptide = new LinkedPeptide(peptide, aaOrdinal - 1, MakeExplicitMods(peptide, _explicitMods));
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

        private void btnOk_Click(object sender, System.EventArgs e)
        {
            OkDialog();
        }

        private void btnEditModifications_Click(object sender, System.EventArgs e)
        {
            MessageDlg.Show(this, "Not yet implemented");
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
                return Int32.Parse(tbxAttachmentOrdinal.Text);
            }
            set { tbxAttachmentOrdinal.Text = value.HasValue ? value.ToString() : string.Empty; }
        }
    }
}
