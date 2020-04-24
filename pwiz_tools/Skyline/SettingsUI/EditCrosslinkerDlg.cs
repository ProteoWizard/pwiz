using System;
using System.Windows.Forms;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditCrosslinkerDlg : FormEx
    {
        private FormulaBox formulaBox;
        public EditCrosslinkerDlg(bool isCrosslinker, CrosslinkerSettings crosslinkerSettings)
        {
            InitializeComponent();
            formulaBox = new FormulaBox(Resources.EditStaticModDlg_EditStaticModDlg_Chemical_formula_,
                Resources.EditMeasuredIonDlg_EditMeasuredIonDlg_A_verage_mass_,
                Resources.EditMeasuredIonDlg_EditMeasuredIonDlg__Monoisotopic_mass_);
            cbxIsCrosslinker.Checked = isCrosslinker;
            panelFormula.Controls.Add(formulaBox);
            if (crosslinkerSettings != null)
            {
                formulaBox.Formula = crosslinkerSettings.Formula;
                formulaBox.MonoMass = crosslinkerSettings.MonoisotopicMass;
                formulaBox.AverageMass = crosslinkerSettings.AverageMass;
            }
        }

        public CrosslinkerSettings CrosslinkerSettings { get; private set; }

        public void OkDialog()
        {
            if (cbxIsCrosslinker.Checked)
            {
                CrosslinkerSettings = (CrosslinkerSettings ?? CrosslinkerSettings.EMPTY)
                    .ChangeFormula(formulaBox.Formula, formulaBox.MonoMass, formulaBox.AverageMass);
            }
            else
            {
                CrosslinkerSettings = null;
            }
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public string Formula
        {
            get { return formulaBox.Formula; }
            set { formulaBox.Formula = value; }
        }
        public double? MonoMass
        {
            get { return formulaBox.MonoMass; }
            set { formulaBox.MonoMass = value; }
        }

        public double? AverageMass
        {
            get { return formulaBox.AverageMass; }
            set { formulaBox.AverageMass = value; }
        }

        private void cbxIsCrosslinker_CheckedChanged(object sender, EventArgs e)
        {
            panelFormula.Enabled = cbxIsCrosslinker.Checked;
        }

        public bool IsCrosslinker
        {
            get { return cbxIsCrosslinker.Checked; }
            set { cbxIsCrosslinker.Checked = value; }
        }
    }
}
