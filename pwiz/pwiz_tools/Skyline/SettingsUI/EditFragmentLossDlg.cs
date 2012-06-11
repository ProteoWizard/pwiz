/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditFragmentLossDlg : FormEx
    {
        private FragmentLoss _loss;
        private readonly IEnumerable<FragmentLoss> _existing;

        public EditFragmentLossDlg(IEnumerable<FragmentLoss> existing)
        {
            InitializeComponent();

            _existing = existing;

            Bitmap bm = Resources.PopupBtn;
            bm.MakeTransparent(Color.Fuchsia);
            btnLossFormulaPopup.Image = bm;
        }

        public FragmentLoss Loss
        {
            get { return _loss; }
            set
            {
                _loss = value;
                if (_loss == null)
                {
                    textLossFormula.Text = "";
                    textLossMonoMass.Text = "";
                    textLossAverageMass.Text = "";                    
                }
                else if (!string.IsNullOrEmpty(_loss.Formula))
                {
                    textLossFormula.Text = _loss.Formula;
                }
                else
                {
                    textLossFormula.Text = "";
                    textLossMonoMass.Text = (_loss.MonoisotopicMass != 0 ?
                        _loss.MonoisotopicMass.ToString(CultureInfo.CurrentCulture) : "");
                    textLossAverageMass.Text = (_loss.AverageMass != 0 ?
                        _loss.AverageMass.ToString(CultureInfo.CurrentCulture) : "");
                }
            }
        }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            string formulaLoss = textLossFormula.Text;
            double? monoLoss = null;
            double? avgLoss = null;
            if (!string.IsNullOrEmpty(formulaLoss))
            {
                try
                {
                    double massMono = SequenceMassCalc.ParseModMass(BioMassCalc.MONOISOTOPIC, formulaLoss);
                    double massAverage = SequenceMassCalc.ParseModMass(BioMassCalc.AVERAGE, formulaLoss);
                    if (FragmentLoss.MIN_LOSS_MASS > massMono || FragmentLoss.MIN_LOSS_MASS > massAverage)
                    {
                        helper.ShowTextBoxError(textLossFormula, string.Format("Neutral loss masses must be greater than or equal to {0}.", FragmentLoss.MIN_LOSS_MASS));
                        return;
                    }
                    if (massMono > FragmentLoss.MAX_LOSS_MASS || massAverage > FragmentLoss.MAX_LOSS_MASS)
                    {
                        helper.ShowTextBoxError(textLossFormula, string.Format("Neutral loss masses must be less than or equal to {0}.", FragmentLoss.MAX_LOSS_MASS));
                        return;
                    }
                }
                catch (ArgumentException x)
                {
                    helper.ShowTextBoxError(textLossFormula, x.Message);
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(textLossMonoMass.Text) ||
                    !string.IsNullOrEmpty(textLossAverageMass.Text))
            {
                formulaLoss = null;
                double mass;
                if (!helper.ValidateDecimalTextBox(e, textLossMonoMass, FragmentLoss.MIN_LOSS_MASS, FragmentLoss.MAX_LOSS_MASS, out mass))
                    return;
                monoLoss = mass;
                if (!helper.ValidateDecimalTextBox(e, textLossAverageMass, FragmentLoss.MIN_LOSS_MASS, FragmentLoss.MAX_LOSS_MASS, out mass))
                    return;
                avgLoss = mass;
            }
            else
            {
                helper.ShowTextBoxError(textLossFormula, "Please specify a formula or constant masses.");
                return;
            }

            // Make sure the new loss does not already exist.
            var loss = new FragmentLoss(formulaLoss, monoLoss, avgLoss);
            if (_existing.Contains(loss))
            {
                MessageDlg.Show(this, string.Format("The loss '{0}' already exists", loss));
                return;
            }

            Loss = loss;

            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void textLossFormula_TextChanged(object sender, EventArgs e)
        {
            UpdateLosses();
        }

        private void UpdateLosses()
        {
            string formula = textLossFormula.Text;
            if (string.IsNullOrEmpty(formula))
            {
                textLossMonoMass.Text = textLossAverageMass.Text = "";
                textLossMonoMass.Enabled = textLossAverageMass.Enabled = true;
            }
            else
            {
                textLossMonoMass.Enabled = textLossAverageMass.Enabled = false;
                try
                {
                    textLossMonoMass.Text = SequenceMassCalc.ParseModMass(
                        BioMassCalc.MONOISOTOPIC, formula).ToString(CultureInfo.CurrentCulture);
                    textLossAverageMass.Text = SequenceMassCalc.ParseModMass(
                        BioMassCalc.AVERAGE, formula).ToString(CultureInfo.CurrentCulture);
                    textLossFormula.ForeColor = Color.Black;
                }
                catch (ArgumentException)
                {
                    textLossFormula.ForeColor = Color.Red;
                    textLossMonoMass.Text = textLossAverageMass.Text = "";
                }
            }
        }

        private static void textLossFormula_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Force uppercase in this control.
            // Atoms have been added containing lower case chars
            // e.KeyChar = char.ToUpper(e.KeyChar);
        }

        private void btnLossFormulaPopup_Click(object sender, EventArgs e)
        {
            contextFormula.Show(this, panelLossFormula.Left + btnLossFormulaPopup.Right + 1,
                panelLossFormula.Top + btnLossFormulaPopup.Top);
        }

        private void AddFormulaSymbol(string symbol)
        {
            textLossFormula.Text += symbol;
            textLossFormula.Focus();
            textLossFormula.SelectionLength = 0;
            textLossFormula.SelectionStart = textLossFormula.Text.Length;
        }

        private void hContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.H);
        }

        private void h2ContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.H2);
        }

        private void cContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.C);
        }

        private void c13ContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.C13);
        }

        private void nContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.N);
        }

        private void n15ContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.N15);
        }

        private void oContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.O);
        }

        private void o18ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.O18);
        }

        private void pContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.P);
        }

        private void sContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.S);
        }
    }
}
