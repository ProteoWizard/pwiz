using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NHibernate;
using turnover.Data;
using turnover.Model;

namespace turnover.ui.Forms
{
    public partial class PeptideInfoForm : PeptideFileAnalysisForm
    {
        public PeptideInfoForm(PeptideFileAnalysis peptideFileAnalysis) : base (peptideFileAnalysis)
        {
            InitializeComponent();
            TabText = "General Info";
            PeptideFileAnalysisChanged();
        }

        protected override void PeptideFileAnalysisChanged()
        {
            var res = Workspace.GetResidueComposition();
            tbxFormula.Text =
                res.DictionaryToFormula(
                    res.FormulaToDictionary(res.MolecularFormula(PeptideFileAnalysis.Sequence)));
            tbxMass.Text = res.GetMonoisotopicMz(PeptideFileAnalysis.Peptide.GetChargedPeptide(1)).ToString();
            tbxMaxTracers.Text = PeptideFileAnalysis.TracerCount.ToString();
        }
    }
}
