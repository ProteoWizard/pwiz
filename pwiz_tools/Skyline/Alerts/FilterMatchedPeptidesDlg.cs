using System;
using System.Windows.Forms;
using pwiz.Skyline.SettingsUI;

namespace pwiz.Skyline.Alerts
{
    public partial class FilterMatchedPeptidesDlg : Form
    {
        public FilterMatchedPeptidesDlg(int numWithDuplicates)
        {
            InitializeComponent();
            msg.Text = string.Format("{0} peptide{1} found in multiple protein sequences."
                                + "\nHow would you like to handle {2}?", numWithDuplicates,
                                numWithDuplicates > 1 ? "s were" : " was", 
                                numWithDuplicates > 1 ? "these peptides" : "this peptide");
        }

        public ViewLibraryPepMatching.DuplicateProteinsFilter PeptidesDuplicateProteins
        {
            get; set;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (btnNoDuplicates.Checked)
                PeptidesDuplicateProteins = ViewLibraryPepMatching.DuplicateProteinsFilter.NoDuplicates;
            else if (btnFirstOccurence.Checked)
                PeptidesDuplicateProteins = ViewLibraryPepMatching.DuplicateProteinsFilter.FirstOccurence;
            else
                PeptidesDuplicateProteins = ViewLibraryPepMatching.DuplicateProteinsFilter.AddToAll;

            DialogResult = DialogResult.OK;
        }
    }
}
