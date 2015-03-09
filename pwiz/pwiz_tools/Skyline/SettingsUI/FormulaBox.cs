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
using System.Globalization;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class FormulaBox : UserControl
    {
        private int? _charge;        // If non-null, mono and average values are displayed as m/z instead of mass
        private double? _averageMass; // Our internal value for mass, regardless of whether displaying mass or mz
        private double? _monoMass;    // Our internal value for mass, regardless of whether displaying mass or mz

        /// <summary>
        /// Reusable control for dealing with chemical formulas and their masses
        /// </summary>
        /// <param name="labelFormulaText">Label text for the formula textedit control</param>
        /// <param name="labelAverageText">Label text for the average mass or m/z textedit control</param>
        /// <param name="labelMonoText">Label text for the monoisotopic mass or m/z textedit control</param>
        /// <param name="charge">If non-null, treat the average and monoisotopic textedits as describing m/z instead of mass</param>
        public FormulaBox(string labelFormulaText, string labelAverageText, string labelMonoText, int? charge = null)
        {
            InitializeComponent();
            _charge = charge;
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
                UpdateAverageAndMonoTextsForFormula();
            }
        }

        public int? Charge
        {
            get
            {
                return _charge;
            }
            set
            {
                _charge = value;
                if (_charge.HasValue)
                {
                    if (string.IsNullOrEmpty(textFormula.Text))
                    {
                        // If we have no formula, then mass is defined by charge and declared mz
                        _monoMass = GetMassFromText(textMono.Text);
                        _averageMass = GetMassFromText(textAverage.Text);
                    }
                    else
                    {
                        // If we have a formula, display m/z values are defined by formula and charge
                        UpdateMonoTextForMass();
                        UpdateAverageTextForMass();
                    }
                }
            }
        }

        public double? MonoMass
        {
            get
            {
                return _monoMass;
            }
            set
            {
                if (textMono.Enabled && !String.IsNullOrEmpty(Formula)) // Avoid side effects of repeated setting
                    Formula = string.Empty;  // Direct edit of mass means formula is obsolete
                _monoMass = value;
                UpdateMonoTextForMass();
            }
        }

        public string MonoText
        {
            get { return textMono.Text;  }
        }

        public double? AverageMass
        {
            get
            {
                return _averageMass;
            }
            set
            {
                if (textAverage.Enabled && !String.IsNullOrEmpty(Formula)) // Avoid side effects of repeated setting
                    Formula = string.Empty;   // Direct edit of mass means formula is obsolete
                _averageMass = value;
                UpdateAverageTextForMass();
            }
        }

        public string AverageText
        {
            get { return textAverage.Text; }
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

        public bool ValidateMonoText(MessageBoxHelper helper, double min, double max, out double val)
        {
            return helper.ValidateDecimalTextBox(textMono, min, max, out val);
        }

        public bool ValidateAverageText(MessageBoxHelper helper, double min, double max, out double val)
        {
            return helper.ValidateDecimalTextBox(textAverage, min, max, out val);
        }

        public bool ValidateMonoText(MessageBoxHelper helper)
        {
            double val;
            return helper.ValidateDecimalTextBox(textMono, out val);
        }

        public bool ValidateAverageText(MessageBoxHelper helper)
        {
            double val;
            return helper.ValidateDecimalTextBox(textAverage, out val);
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

        /// <summary>
        /// Get a mass value from the text string, treating the string as m/z info if we have a charge state
        /// </summary>
        private double? GetMassFromText(string text)
        {
            try
            {
                if (String.IsNullOrEmpty(text))
                {
                    return null;
                }
                else
                {
                    double parsed = double.Parse(text);
                    if (Charge.HasValue)
                    {
                        // Convert from m/z to mass
                        return BioMassCalc.CalculateIonMassFromMz(parsed, Charge.Value);
                    }
                    return parsed;
                }

            }
            catch (Exception)
            {
                return null;
            }
        }

        private string GetTextFromMass(double? mass)
        {
            if (!mass.HasValue)
                return string.Empty;
            double result = mass.Value;
            if (Charge.HasValue)
            {
                // We want to show this as an m/z value, rounded to a reasonable length
                result = SequenceMassCalc.PersistentMZ(BioMassCalc.CalculateIonMz(result, Charge.Value));
            }
            return result.ToString(CultureInfo.CurrentCulture);
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
            UpdateAverageAndMonoTextsForFormula();
        }

        private void textMono_TextChanged(object sender, EventArgs e)
        {
            // Did text change because user edited, or because we set it on mass change?
            var text = GetTextFromMass(MonoMass);
            if (string.IsNullOrEmpty(Formula) && // Can't be a user edit if formula box is populated
                !Equals(text, textMono.Text))
            {
                var value = GetMassFromText(textMono.Text);
                if (!value.Equals(MonoMass)) // This check lets the user type the "." on the way to "123.4"
                    MonoMass = value;
            }
        }

        private void textAverage_TextChanged(object sender, EventArgs e)
        {
            // Did text change because user edited, or because we set it on mass change?
            var text = GetTextFromMass(AverageMass);
            if (string.IsNullOrEmpty(Formula) &&  // Can't be a user edit if formula box is populated
                !Equals(text, textAverage.Text))
            {
               var value = GetMassFromText(textAverage.Text);
                if (!value.Equals(AverageMass)) // This check lets the user type the "." on the way to "123.4"
                    AverageMass = value;
            }
        }

        private void UpdateMonoTextForMass()
        {
            // Avoid a casecade of text-changed events
            var text = GetTextFromMass(_monoMass);
            if (!Equals(GetMassFromText(text), GetMassFromText(textMono.Text)))
                textMono.Text = text;
        }

        private void UpdateAverageTextForMass()
        {
            // Avoid a casecade of text-changed events
            var text = GetTextFromMass(_averageMass);
            if (!Equals(GetMassFromText(text), GetMassFromText(textAverage.Text)))
                textAverage.Text = text;
        }

        private void UpdateAverageAndMonoTextsForFormula()
        {
            string formula = textFormula.Text;
            if (string.IsNullOrEmpty(formula))
            {
                // Leave any precalculated masses in place for user convenience
                textMono.Enabled = textAverage.Enabled = true;
            }
            else
            {
                // Formula drives mass, no direct edit allowed
                textMono.Enabled = textAverage.Enabled = false;
                try
                {
                    var monoMass = SequenceMassCalc.ParseModMass(BioMassCalc.MONOISOTOPIC, formula);
                    var averageMass = SequenceMassCalc.ParseModMass(BioMassCalc.AVERAGE, formula);
                    GetTextFromMass(monoMass);     // Just to see if it throws or not
                    GetTextFromMass(averageMass);  // Just to see if it throws or not
                    MonoMass = monoMass;
                    AverageMass = averageMass;
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
