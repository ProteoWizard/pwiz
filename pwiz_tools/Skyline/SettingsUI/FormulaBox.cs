/*
 * Original author: Max Horowitz-Gelb <maxhg .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class FormulaBox : UserControl
    {
        public FormulaBox(string labelFormulaText, string labelAverageText, string labelMonoText)
        {
            InitializeComponent();
            
            labelFormula.Text = labelFormulaText;
            labelAverage.Text = labelAverageText;
            labelMono.Text = labelMonoText;

            Bitmap bm = Resources.PopupBtn;
            bm.MakeTransparent(Color.Fuchsia);
            btnFormula.Image = bm;
        }

        public string Formula
        {
            get { return textFormula.Text; }
            set
            {
                textFormula.Text = value;
                UpdateMasses();
            }
        }

        public double? MonoMass
        {
            get
            {
                try
                {
                    return String.IsNullOrEmpty(textMono.Text) ? (double?) null : double.Parse(textMono.Text);
                }
                catch (Exception)
                {
                    return null;
                }
            }
            set
            {
                if (!String.IsNullOrEmpty(Formula)) // Avoid side effects of repeated setting
                    Formula = string.Empty;
                textMono.Text = value != null
                    ? value.Value.ToString(LocalizationHelper.CurrentCulture)
                    : string.Empty;
            }
        }

        public double? AverageMass
        {
            get
            {
                try
                {
                    return String.IsNullOrEmpty(textAverage.Text) ? (double?) null : double.Parse(textAverage.Text);
                }
                catch (Exception)
                {
                    return null;
                }
            }
            set
            {
                if (!String.IsNullOrEmpty(Formula)) // Avoid side effects of repeated setting
                    Formula = string.Empty;
                textAverage.Text = value != null
                    ? value.Value.ToString(LocalizationHelper.CurrentCulture)
                    : string.Empty;
            }
        }

        public bool FormulaVisible
        {
            get { return textFormula.Visible; }
            set
            {
                textFormula.Visible = value;
                btnFormula.Visible = value;
                labelFormula.Visible = value;
            }
        }

        public bool MassEnabled
        {
            get { return textMono.Enabled; }
            set
            {
                textMono.Enabled = value;
                textAverage.Enabled = value;
                labelMono.Enabled = value;
                labelAverage.Enabled = value;
            }
        }

        public bool ValidateMonoMass(MessageBoxHelper helper, double min, double max, out double val)
        {
            return helper.ValidateDecimalTextBox(textMono, min, max, out val);
        }

        public bool ValidateAverageMass(MessageBoxHelper helper, double min, double max, out double val)
        {
            return helper.ValidateDecimalTextBox(textAverage, min, max, out val);
        }

        public void ShowTextBoxErrorAverageMass(MessageBoxHelper helper, string message)
        {
            helper.ShowTextBoxError(textAverage,message);
        }
        public void ShowTextBoxErrorMonoMass(MessageBoxHelper helper, string message)
        {
            helper.ShowTextBoxError(textMono, message);
        }
        public void ShowTextBoxErrorFormula(MessageBoxHelper helper, string message)
        {
            helper.ShowTextBoxError(textFormula, message);
        }

        private void btnFormula_Click(object sender, EventArgs e)
        {
            contextFormula.Show(this, btnFormula.Right + 1,
                btnFormula.Top);
        }

        private void AddFormulaSymbol(string symbol)
        {
            textFormula.Text += symbol;
            textFormula.Focus();
            textFormula.SelectionLength = 0;
            textFormula.SelectionStart = textFormula.Text.Length;
        }

        private void hToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.H);
        }

        private void h2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.H2);
        }

        private void cToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.C);
        }

        private void c13ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.C13);
        }

        private void nToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.N);
        }

        private void n15ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.N15);
        }

        private void oToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.O);
        }

        private void pToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.P);
        }

        private void sToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.S);
        }

        private void o18ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.O18);
        }

        private void textFormula_TextChanged(object sender, EventArgs e)
        {
            UpdateMasses();
        }

        private void UpdateMasses()
        {
            string formula = textFormula.Text;
            if (string.IsNullOrEmpty(formula))
            {
                textMono.Text = textAverage.Text = string.Empty;
                textMono.Enabled = textAverage.Enabled = true;
            }
            else
            {
                textMono.Enabled = textAverage.Enabled = false;
                try
                {
                    textMono.Text = SequenceMassCalc.ParseModMass(BioMassCalc.MONOISOTOPIC,
                        formula).ToString(LocalizationHelper.CurrentCulture);
                    textAverage.Text = SequenceMassCalc.ParseModMass(BioMassCalc.AVERAGE,
                        formula).ToString(LocalizationHelper.CurrentCulture);
                    textFormula.ForeColor = Color.Black;
                }
                catch (ArgumentException)
                {
                    textFormula.ForeColor = Color.Red;
                    textMono.Text = textAverage.Text = string.Empty;
                }
            }
        }
    }
}
