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
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    public partial class FormulaBox : UserControl
    {
        public enum EditMode
        {
            formula_only,
            adduct_only,
            formula_and_adduct
        };
        private Adduct _adduct;       // If non-empty, mono and average values are displayed as m/z instead of mass
        private readonly EditMode _editMode;
        private string _neutralFormula;
        private Dictionary<string, string> _isotopeLabelsForMassCalc;
        private TypedMass _neutralMonoMass;
        private TypedMass _neutralAverageMass;
        private double? _averageMass; // Our internal value for mass, regardless of whether displaying mass or mz
        private double? _monoMass;    // Our internal value for mass, regardless of whether displaying mass or mz

        /// <summary>
        /// Reusable control for dealing with chemical formulas and their masses
        /// </summary>
        /// <param name="isProteomic">if true, don't offer Cl, Br, or heavy P or heavy S in elements popup</param>
        /// <param name="labelFormulaText">Label text for the formula textedit control</param>
        /// <param name="labelAverageText">Label text for the average mass or m/z textedit control</param>
        /// <param name="labelMonoText">Label text for the monoisotopic mass or m/z textedit control</param>
        /// <param name="adduct">If non-null, treat the average and monoisotopic textedits as describing m/z instead of mass</param>
        /// <param name="mode">Controls editing of the formula and/or adduct edit</param>
        public FormulaBox(bool isProteomic, string labelFormulaText, string labelAverageText, string labelMonoText, Adduct adduct, EditMode mode = EditMode.formula_only)
        {
            InitializeComponent();
            if (isProteomic)
            {
                // Don't offer exotic atoms or isotopes
                p32ToolStripMenuItem.Visible = 
                    s33ToolStripMenuItem.Visible = 
                       s34ToolStripMenuItem.Visible =
                           h3ToolStripMenuItem.Visible =
                clToolStripMenuItem.Visible =
                    cl37ToolStripMenuItem.Visible =
                        brToolStripMenuItem.Visible =
                            br81ToolStripMenuItem.Visible = false;
            }
            _adduct = adduct;
            _editMode = mode;

            switch (mode)
            {
                case EditMode.adduct_only:
                case EditMode.formula_and_adduct:
                    TransitionSettingsUI.AppendAdductMenus(contextFormula, adductStripMenuItem_Click);
                    break;
            }

            toolTip1.SetToolTip(textFormula, _editMode==EditMode.adduct_only ? AdductHelpText : FormulaHelpText);  // Explain how formulas work, and ion formula adducts if charge.HasValue

            labelFormula.Text = labelFormulaText;
            labelAverage.Text = labelAverageText;
            labelMono.Text = labelMonoText;

            Bitmap bm = Resources.PopupBtn;
            bm.MakeTransparent(Color.Fuchsia);
            btnFormula.Image = bm;
        }

        public FormulaBox(string labelFormulaText, string labelAverageText, string labelMonoText) :
            this(true, labelFormulaText, labelAverageText, labelMonoText, Adduct.EMPTY)
        {
        }

        public event EventHandler ChargeChange;

        public string DisplayFormula
        {
            get
            {
                switch (_editMode)
                {
                    case EditMode.adduct_only:
                        return _adduct.IsEmpty ? string.Empty : _adduct.AdductFormula;
                    case EditMode.formula_and_adduct:
                        return Formula;
                    case EditMode.formula_only:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                return NeutralFormula;
            }
        }

        public string Formula
        {
            get { return (NeutralFormula ?? string.Empty) + (_adduct.AdductFormula ?? string.Empty); }
            set
            {
                if (string.IsNullOrEmpty(value) && string.IsNullOrEmpty(NeutralFormula))
                {
                    return; // Do nothing - just initializing
                }
                Molecule ion;
                Adduct newAdduct;
                string newNeutralFormula;
                string newTextFormulaText = null;
                var editAdductOnly = _editMode == EditMode.adduct_only;
                if (Adduct.TryParse(value, out newAdduct))
                {
                    // Text describes adduct only
                    if (!editAdductOnly)
                    {
                        NeutralFormula = string.Empty;
                    }
                    Adduct = newAdduct;
                    newTextFormulaText = value;
                }
                else if (IonInfo.IsFormulaWithAdduct(value, out ion, out newAdduct, out newNeutralFormula))
                {
                    // If we're allowing edit of adduct only, set aside the formula portion
                    var displayText = editAdductOnly ? newAdduct.AdductFormula : value;
                    if (!editAdductOnly)
                    {
                        NeutralFormula = newNeutralFormula;
                    }
                    Adduct = newAdduct;
                    if (!Equals(displayText, textFormula.Text))
                    {
                        newTextFormulaText = displayText;
                    }
                }
                else if (!editAdductOnly)
                {
                    NeutralFormula = value;
                    Adduct = Adduct.EMPTY;
                    newTextFormulaText = value;
                }
                if (newTextFormulaText != null && textFormula.Text != newTextFormulaText)
                {
                    SetFormulaText(newTextFormulaText);
                }
                else
                {
                    // No text change, but make sure all displays are consistent
                    UpdateAverageAndMonoTextsForFormula();
                }
            }
        }

        // Isotopes for mass calc - any isotopic description in adduct overrides
        public Dictionary<string, string> IsotopeLabelsForMassCalc
        {
            get { return _isotopeLabelsForMassCalc; }
            set
            {
                _isotopeLabelsForMassCalc = value;
                UpdateAverageAndMonoTextsForFormula();
            }
        }

        public string NeutralFormula
        {
            get { return _neutralFormula; }
            set
            {
                _neutralFormula = value;
                if (string.IsNullOrEmpty(value)) // If formula gets emptied out, leave masses alone
                {
                    MassEnabled = _editMode != EditMode.adduct_only; // Allow editing of masses if formula was editable, but there's no formula to edit
                }
                else
                {
                    MassEnabled = false; // No direct editing of masses when there's a formula
                    // Update masses for this new formula value
                    try
                    {
                        _neutralMonoMass = BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(value);
                        _neutralAverageMass = BioMassCalc.AVERAGE.CalculateMassFromFormula(value);
                    }
                    catch
                    {
                        // Ignored - syntax error highlighting happens elsewhere
                    }
                }
            }
        }

        public Adduct Adduct
        {
            get { return _adduct; }
            set
            {
                var previous = _adduct;
                _adduct = value;
                if (!_adduct.IsEmpty)
                {
                    if (string.IsNullOrEmpty(NeutralFormula))
                    {
                        // If we have no formula, then mass is defined by charge and declared mz
                        _monoMass = GetMassFromText(textMono.Text, MassType.Monoisotopic);
                        _averageMass = GetMassFromText(textAverage.Text, MassType.Average);
                    }
                    else
                    {
                        // If we have a formula, display m/z values are defined by formula and charge, and any isotopic labels we are given
                        UpdateMonoTextForMass();
                        UpdateAverageTextForMass();
                    }
                    if (!Equals(previous, _adduct) && ChargeChange != null)
                    {
                        ChargeChange(this, EventArgs.Empty);
                    }
                    if (!Equals(textFormula.Text, DisplayFormula))
                    {
                        SetFormulaText(DisplayFormula);
                    }
                }
            }
        }

        public double? MonoMass
        {
            get { return _monoMass; }
            set
            {
                if (textMono.Enabled && !Equals(value ?? 0, _neutralMonoMass.Value)) // Avoid side effects of repeated setting
                    NeutralFormula = null; // Direct edit of mass means formula is obsolete
                _monoMass = value;
                UpdateMonoTextForMass();
            }
        }

        public string MonoText
        {
            get { return textMono.Text; }
        }

        public double? AverageMass
        {
            get { return _averageMass; }
            set
            {
                if (textAverage.Enabled && !Equals(value ?? 0, _neutralAverageMass.Value)) // Avoid side effects of repeated setting
                    NeutralFormula = null; // Direct edit of mass means formula is obsolete
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
            helper.ShowTextBoxError(textAverage, message);
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
        private double? GetMassFromText(string text, MassType massType)
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
                    if (!Adduct.IsEmpty)
                    {
                        // Convert from m/z to mass
                        return Adduct.MassFromMz(parsed, massType);
                    }
                    return parsed;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Handler for adduct picker menu
        private void adductStripMenuItem_Click(object sender, EventArgs e)
        {
            var mi = sender as ToolStripMenuItem;
            if (mi != null)
            {
                var adduct =  mi.Text;
                var formulaText = textFormula.Text;
                if (!string.IsNullOrEmpty(textFormula.Text))
                {
                    // Replacing an existing adduct declaration?
                    var start = textFormula.Text.LastIndexOf(@"[", StringComparison.Ordinal);
                    var end = textFormula.Text.IndexOf(@"]", StringComparison.Ordinal);
                    if (start >= 0 && end > start)
                    {
                        formulaText = textFormula.Text.Substring(0, start);
                    }
                }
                formulaText += adduct;
                SetFormulaText(formulaText);
            }
        }

        private string GetTextFromMass(double? mass, MassType massType)
        {
            if (!mass.HasValue)
                return string.Empty;
            var result = mass.Value;
            if (!Adduct.IsEmpty)
            {
                // We want to show this as an m/z value, rounded to a reasonable length
                result = Math.Abs(SequenceMassCalc.PersistentMZ(Adduct.MzFromNeutralMass(result, massType)));
            }
            return result.ToString(CultureInfo.CurrentCulture);
        }

        private void btnFormula_Click(object sender, EventArgs e)
        {
            contextFormula.Show(this, btnFormula.Right + 1, btnFormula.Top);
        }

        private void AddFormulaSymbol(string symbol)
        {
            // Insert at cursor
            var insertAt = textFormula.SelectionStart;
            textFormula.Text = textFormula.Text.Substring(0, insertAt) + symbol + textFormula.Text.Substring(insertAt);
            textFormula.Focus();
            textFormula.SelectionLength = 0;
            textFormula.SelectionStart = insertAt + symbol.Length;
        }

        private void SetFormulaText(string text)
        {
            if (Equals(text, textFormula.Text))
            {
                return;
            }
            // Preserve cursor location
            var insertAt = textFormula.SelectionStart;
            textFormula.Text = text;
            textFormula.SelectionLength = 0;
            textFormula.SelectionStart = Math.Min(insertAt, text?.Length ?? 0);
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

        private void c14ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.C14);
        }

        private void nToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.N);
        }

        private void n15ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.N15);
        }

        private void clToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.Cl);
        }

        private void cl37ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.Cl37);
        }

        private void brToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.Br);
        }

        private void br81ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.Br81);
        }

        private void p32ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.P32);
        }

        private void s33ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.S33);
        }

        private void s34ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.S34);
        }

        private void h3ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.H3);
        }

        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var helpText = FormulaHelpText;

            // CONSIDER(bspratt) use DocumentationViewer instead, this is quite a lot of text
            MessageDlg.Show(this, helpText);
        }

        private string FormulaHelpText
        {
            get
            {
                var helpText = TextUtil.LineSeparate(Resources.FormulaBox_helpToolStripMenuItem_Click_Formula_Help, 
                    string.Empty,
                    Resources.FormulaBox_FormulaHelpText_Formulas_are_written_in_standard_chemical_notation__e_g___C2H6O____Heavy_isotopes_are_indicated_by_a_prime__e_g__C__for_C13__or_double_prime_for_less_abundant_stable_iostopes__e_g__O__for_O17__O__for_O18__);
                if (_editMode != EditMode.formula_only)
                {
                    helpText = TextUtil.LineSeparate(helpText, string.Empty, Adduct.Tips); // Charge implies ion formula, so help with adduct descriptions as well
                }
                return helpText;
            }
        }

        private string AdductHelpText
        {
            get { return Adduct.Tips; }
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

        private bool _inTextChanged;
        private void textFormula_TextChanged(object sender, EventArgs e)
        {
            if (!_inTextChanged)
            {
                try
                {
                    _inTextChanged = true;
                    UpdateAverageAndMonoTextsForFormula();
                }
                finally
                {
                    _inTextChanged = false;
                }
            }
        }

        private void textMono_TextChanged(object sender, EventArgs e)
        {
            // Did text change because user edited, or because we set it on mass change?
            var text = GetTextFromMass(MonoMass, MassType.Monoisotopic);
            if (string.IsNullOrEmpty(NeutralFormula) && // Can't be a user edit if formula box is populated
                !Equals(text, textMono.Text))
            {
                var value = GetMassFromText(textMono.Text, MassType.Monoisotopic);
                if (!value.Equals(MonoMass)) // This check lets the user type the "." on the way to "123.4"
                    MonoMass = value;
            }
        }

        private void textAverage_TextChanged(object sender, EventArgs e)
        {
            // Did text change because user edited, or because we set it on mass change?
            var text = GetTextFromMass(AverageMass, MassType.Average);
            if (string.IsNullOrEmpty(NeutralFormula) && // Can't be a user edit if formula box is populated
                !Equals(text, textAverage.Text))
            {
                var value = GetMassFromText(textAverage.Text, MassType.Average);
                if (!value.Equals(AverageMass)) // This check lets the user type the "." on the way to "123.4"
                    AverageMass = value;
            }
        }

        private void UpdateMonoTextForMass()
        {
            // Avoid a casecade of text-changed events
            var text = GetTextFromMass(_monoMass, MassType.Monoisotopic);
            if (!Equals(GetMassFromText(text, MassType.Monoisotopic), GetMassFromText(textMono.Text, MassType.Monoisotopic)))
                textMono.Text = text;
        }

        private void UpdateAverageTextForMass()
        {
            // Avoid a casecade of text-changed events
            var text = GetTextFromMass(_averageMass, MassType.Average);
            if (!Equals(GetMassFromText(text, MassType.Average), GetMassFromText(textAverage.Text, MassType.Average)))
                textAverage.Text = text;
        }

        private void UpdateAverageAndMonoTextsForFormula()
        {
            bool valid;
            try
            {
                var formula = Formula; // Get current formula and adduct

                var userinput = textFormula.Text.Trim();
                if (_editMode == EditMode.adduct_only)
                {
                    if (!string.IsNullOrEmpty(userinput) && !userinput.StartsWith(@"["))
                    {
                        // Assume they're trying to type an adduct
                        userinput = @"[" + userinput + @"]";
                    }
                    if (string.IsNullOrEmpty(NeutralFormula))
                    {
                        formula = null; // Parent molecule was described as mass only
                    }
                    else
                    {
                        formula = NeutralFormula + userinput; // Try to apply this new adduct to parent molecule
                    }
                }
                else
                {
                    formula = userinput;
                }
                string neutralFormula;
                Molecule ion;
                Adduct adduct;
                if (!IonInfo.IsFormulaWithAdduct(formula, out ion, out adduct, out neutralFormula, true))
                {
                    neutralFormula = formula;
                    if (!Adduct.TryParse(userinput, out adduct, Adduct.ADDUCT_TYPE.non_proteomic, true))
                    {
                        adduct = Adduct.EMPTY;
                    }
                }
                if (_editMode != EditMode.adduct_only)
                {
                    NeutralFormula = neutralFormula;
                }
                if (_editMode != EditMode.formula_only)
                {
                    Adduct = adduct;
                }
                // Update mass/mz displays
                if (string.IsNullOrEmpty(neutralFormula))
                {
                    if (!adduct.IsEmpty)
                    {
                        // No formula, but adduct changed
                        Adduct = adduct;
                        // ReSharper disable once PossibleNullReferenceException
                        GetTextFromMass(_neutralMonoMass, MassType.Monoisotopic); // Just to see if it throws or not
                        GetTextFromMass(_neutralAverageMass, MassType.Average); // Just to see if it throws or not
                    }
                }
                else
                {
                    // Is there an isotopic label we should apply to get the mass?
                    if (IsotopeLabelsForMassCalc != null && (Adduct.IsEmpty || !Adduct.HasIsotopeLabels)) // If adduct declares an isotope, that takes precedence
                    {
                        neutralFormula = IsotopeLabelsForMassCalc.Aggregate(neutralFormula, (current, kvp) => current.Replace(kvp.Key, kvp.Value));
                    }
                    var monoMass = SequenceMassCalc.FormulaMass(BioMassCalc.MONOISOTOPIC, neutralFormula, SequenceMassCalc.MassPrecision);
                    var averageMass = SequenceMassCalc.FormulaMass(BioMassCalc.AVERAGE, neutralFormula, SequenceMassCalc.MassPrecision);
                    GetTextFromMass(monoMass, MassType.Monoisotopic); // Just to see if it throws or not
                    GetTextFromMass(averageMass, MassType.Average); // Just to see if it throws or not
                    MonoMass = monoMass;
                    AverageMass = averageMass;
                }
                valid = true; // If we got here, formula parsed OK, or adduct did
                textFormula.ForeColor = Color.Black;
                if (_editMode == EditMode.adduct_only)
                {
                    SetFormulaText(userinput);
                    if (adduct.IsEmpty)
                    {
                        valid = false; // Adduct did not parse
                    }
                }
                else if (_editMode == EditMode.formula_only)
                {
                    valid &= adduct.IsEmpty; // Should not have anything going on with adduct here
                }
            }
            catch (InvalidOperationException)
            {
                valid = false;
            }
            catch (ArgumentException)
            {
                valid = false;
            }
            if (valid)
            {
                textFormula.ForeColor = Color.Black;
            }
            else
            {
                textFormula.ForeColor = Color.Red;
                textMono.Text = textAverage.Text = string.Empty;
            }

            // Allow direct editing of masses if direct editing of formula is allowed, but formula is empty
            MassEnabled = _editMode != EditMode.adduct_only && string.IsNullOrEmpty(_neutralFormula);
        }
    }
}
