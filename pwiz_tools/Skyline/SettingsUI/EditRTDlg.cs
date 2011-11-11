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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditRTDlg : Form
    {
        private readonly SettingsListComboDriver<RetentionScoreCalculatorSpec> _driverCalculators;

        private RetentionTimeRegression _regression;
        private readonly IEnumerable<RetentionTimeRegression> _existing;
        private RetentionTimeStatistics _statistics;

        public EditRTDlg(IEnumerable<RetentionTimeRegression> existing)
        {
            _existing = existing;

            InitializeComponent();

            Icon = Resources.Skyline;

            _driverCalculators = new SettingsListComboDriver<RetentionScoreCalculatorSpec>(
                comboCalculator, Settings.Default.RTScoreCalculatorList, true);
            _driverCalculators.LoadList(null);

            ShowPeptides(Settings.Default.EditRTVisible);
        }

        public RetentionTimeRegression Regression
        {
            get { return _regression; }

            set
            {
                _regression = value;
                if (_regression == null)
                {
                    textName.Text = "";
                }
                else
                {
                    textName.Text = _regression.Name;

                    _activePeptides = _regression.PeptideTimes.ToList();
                    var pepTimes = _regression.PeptideTimes;
                    if (pepTimes.Count > 0)
                    {
                        ShowPeptides(true);
                        foreach (var pepTime in pepTimes)
                        {
                            gridPeptides.Rows.Add(pepTime.PeptideSequence,
                                string.Format("{0:F02}", pepTime.RetentionTime));                            
                        }

                        // Get statistics
                        RecalcRegression(_driverCalculators.SelectedItem, _regression.PeptideTimes.ToList());
                        //UpdateCalculator();
                    }

                    comboCalculator.SelectedItem = _regression.Calculator.Name;
                    /*
                    textSlope.Text = string.Format("{0:F04}", _regression.Conversion.Slope);
                    textIntercept.Text = string.Format("{0:F04}", _regression.Conversion.Intercept);
                    textTimeWindow.Text = string.Format("{0:F04}", _regression.TimeWindow);
                     */
                    textSlope.Text = _regression.Conversion.Slope.ToString();
                    textIntercept.Text = _regression.Conversion.Intercept.ToString();
                    textTimeWindow.Text = _regression.TimeWindow.ToString();
                }
            }
        }

        private List<MeasuredRetentionTime> _activePeptides;

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(e, textName, out name))
                return;

            if (_existing.Contains(r => !ReferenceEquals(_regression, r) && Equals(name, r.Name)))
            {
                helper.ShowTextBoxError(textName, "The retention time regression '{0}' already exists.", name);
                e.Cancel = true;
                return;
            }

            double slope;
            if (!helper.ValidateDecimalTextBox(e, textSlope, out slope))
                return;

            double intercept;
            if (!helper.ValidateDecimalTextBox(e, textIntercept, out intercept))
                return;

            double window;
            if (!helper.ValidateDecimalTextBox(e, textTimeWindow, out window))
                return;

            if (window <= 0)
            {
                helper.ShowTextBoxError(textTimeWindow, "{0} must be greater than 0.");
                return;
            }

            if (comboCalculator.SelectedIndex == -1)
            {
                MessageBox.Show(this, "Retention time prediction requires a calculator algorithm.", Program.Name);
                comboCalculator.Focus();
                return;
            }
            var calculator = _driverCalculators.SelectedItem;

            // Todo: replace this with code that gets the active calculator, then chooses regression peptides
            // Todo: from _activePeptides using that calculator
            var listPeptides = new List<MeasuredRetentionTime>();
            if (!ValidatePeptides(e, listPeptides))
                return;
            RetentionTimeRegression regression =
                new RetentionTimeRegression(name, calculator, slope, intercept, window, listPeptides);

            _regression = regression;

            DialogResult = DialogResult.OK;
        }

        private bool ValidatePeptides(CancelEventArgs e, ICollection<MeasuredRetentionTime> listPeptides)
        {
            string[] fields = new string[2];
            foreach (DataGridViewRow row in gridPeptides.Rows)
            {
                if (row.IsNewRow)
                    continue;

                object val = row.Cells[0].Value;
                fields[0] = (val == null ? "" : val.ToString());
                val = row.Cells[1].Value;
                fields[1] = (val == null ? "" : val.ToString());
                int colError;
                if (!ValidatePeptideCellValues(fields, out colError))
                {
                    gridPeptides.ClearSelection();
                    gridPeptides.CurrentCell = row.Cells[colError];
                    gridPeptides.BeginEdit(true);
                    e.Cancel = true;
                    return false;                    
                }
                listPeptides.Add(new MeasuredRetentionTime(fields[0], double.Parse(fields[1])));
            }
            return true;
        }

        public void ShowPeptides(bool visible)
        {
            bool hasResults = Program.ActiveDocumentUI.Settings.HasResults;

            Control ctlCorner;
            int yExtra = 0;
            if (visible)
            {
                btnCalculate.Text = "&Calculate <<";
                ctlCorner = gridPeptides;
                yExtra = btnUseCurrent.Height + 10;
            }
            else
            {
                btnCalculate.Text = "&Calculate >>";
                ctlCorner = btnCalculate;
            }

            Point ptCorner = new Point(ctlCorner.Right, ctlCorner.Bottom + yExtra);
            ptCorner = PointToScreen(ptCorner);
            ptCorner.Offset(20, 20);

            if (!visible)
            {
                gridPeptides.Anchor &= ~AnchorStyles.Bottom;
                btnUseCurrent.Anchor |= ~AnchorStyles.Top;
                btnUseCurrent.Anchor &= ~AnchorStyles.Bottom;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                Height = ptCorner.Y - Top;
            }

            labelPeptides.Visible = visible;
            labelRValue.Visible = visible;
            btnUseCurrent.Visible = visible;
            btnUseCurrent.Enabled = hasResults;
            gridPeptides.Visible = visible;

            if (visible)
            {
                Height = ptCorner.Y - Top;
                FormBorderStyle = FormBorderStyle.Sizable;
                btnUseCurrent.Anchor |= AnchorStyles.Bottom;
                btnUseCurrent.Anchor &= ~AnchorStyles.Top;
                gridPeptides.Anchor |= AnchorStyles.Bottom;
            }
        }

        private void comboCalculator_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_driverCalculators.SelectedIndexChangedEvent(sender, e))
                CalculatorChanged();
        }

        private void CalculatorChanged()
        {
            if (comboCalculator.SelectedItem != null)
            {
                var calc = _driverCalculators.SelectedItem;

                try
                {
                    var regressionPeps = UpdateCalculator(calc);
                    if (regressionPeps != null)
                        SetTablePeptides(regressionPeps);
                }
                catch (CalculatorException e)
                {
                    MessageDlg.Show(this, e.Message);
                }
            }
        }

        private void btnCalculate_Click(object sender, EventArgs e)
        {
            ShowPeptides(!gridPeptides.Visible);
        }

        private List<MeasuredRetentionTime> GetTablePeptides()
        {
            var peptides = new List<MeasuredRetentionTime>();
            ValidatePeptides(new CancelEventArgs(), peptides);

            return peptides;
        }

        private void btnUseCurrent_Click(object sender, EventArgs e)
        {
            AddResults();
        }

        public List<MeasuredRetentionTime> GetDocumentPeptides()
        {
            var document = Program.ActiveDocumentUI;
            if (!document.Settings.HasResults)
                return null; // This shouldn't be possible, but just to be safe.
            if (!document.Settings.MeasuredResults.IsLoaded)
                return null;

            var peps = new List<MeasuredRetentionTime>();
            foreach(var nodePep in document.Peptides)
            {
                if (nodePep.AveragePeakCountRatio < 0.5)
                    continue;

                double? retentionTime = nodePep.SchedulingTime;
                if (!retentionTime.HasValue)
                    continue;

                string modSeq = document.Settings.GetModifiedSequence(nodePep);
                peps.Add(new MeasuredRetentionTime(modSeq, retentionTime.Value));
            }

            return peps;
        }

        /// <summary>
        /// This function will update the calculator to the one given, or to the one with the best score for the document 
        /// peptides. It will then return the peptides chosen by that calculator for regression.
		/// Todo: split this function into one that chooses and returns the calculator and one that returns the peptides
		/// todo: chosen by that calculator
        /// </summary>
        private List<MeasuredRetentionTime> UpdateCalculator(RetentionScoreCalculatorSpec calculator)
        {
            bool calcInitiallyNull = calculator == null;

            if(_activePeptides == null)
                return null;

            //Try connecting all the calculators
            Settings.Default.RTScoreCalculatorList.Initialize(null);

            if (calculator == null)
            {
                //this will not update the calculator
                calculator = RecalcRegression(_activePeptides);
            }
            else
            {
                var calcSettings = Settings.Default.GetCalculatorByName(calculator.Name);
                if (calcSettings != null)
                    calculator = calcSettings;

                if (!calculator.IsUsable)
                {
                    MessageDlg.Show(this, "The calculator cannot be used to score peptides. Please check its settings.");
                    return _activePeptides;
                }

                RecalcRegression(calculator, _activePeptides);
            }

            var usePeptides = new HashSet<string>(calculator.ChooseRegressionPeptides(
                _activePeptides.Select(pep => pep.PeptideSequence)));
            //now go back and get the MeasuredPeptides corresponding to the strings chosen by the calculator
            var tablePeptides = _activePeptides.Where(measuredRT =>
                usePeptides.Contains(measuredRT.PeptideSequence)).ToList();

            if (tablePeptides.Count == 0 && _activePeptides.Count != 0)
            {
                MessageDlg.Show(this, String.Format("The {0} calculator cannot score any of the peptides.", calculator.Name));
                comboCalculator.SelectedIndex = 0;
                return null;
            }
            
			//This "if" is to keep from getting into infinite loops
            if (calcInitiallyNull)
                comboCalculator.SelectedItem = calculator.Name;

            return tablePeptides;
        }

        public void SetTablePeptides(List<MeasuredRetentionTime> tablePeps)
        {
            if (ArrayUtil.EqualsDeep(_activePeptides, tablePeps))
                return;

            var rowsNew = new List<string[]>();

            foreach (var measuredRT in tablePeps)
                rowsNew.Add(new[] { measuredRT.PeptideSequence, measuredRT.RetentionTime.ToString() });
                //rowsNew.Add(new[] { pep.Sequence, string.Format("{0:F02}", pep.RetentionTimeOrIrt) });

            gridPeptides.SuspendLayout();

            gridPeptides.Rows.Clear();
            foreach (var columns in rowsNew)
                gridPeptides.Rows.Add(columns);

            gridPeptides.ResumeLayout();
        }

        private void gridPeptides_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl + V for paste
            if (e.KeyCode == Keys.V && e.Control)
            {
                if (gridPeptides.DoPaste(this, ValidatePeptideCellValues))
                    RecalcRegression(GetTablePeptides());
            }
            else if (e.KeyCode == Keys.Delete)
            {
                if (gridPeptides.DoDelete())
                    RecalcRegression(GetTablePeptides());
            }
        }

        private bool ValidatePeptideCellValues(string[] values)
        {
            int colError;
            return ValidatePeptideCellValues(values, out colError);
        }

        private bool ValidatePeptideCellValues(string[] values, out int colError)
        {
            string seq = values[0].Trim();
            if (!FastaSequence.IsSequence(seq))
            {
                string message = string.Format("The sequence {0} is not a valid peptide.", seq);
                MessageBox.Show(this, message, Program.Name);
                colError = 0;
                return false;
            }
//            else
//            {
//                char cTerm = seq[seq.Length - 1];
//                if (cTerm != 'R' && cTerm != 'K')
//                {
//                    string message = string.Format("The sequence {0} does not end with R or K.  " +
//                                                   "The hydrophobicity calculator is only calibrated for tryptic cleavage.",
//                                                   seq);
//                    MessageBox.Show(message, Program.Name);
//                    return false;
//                }
//            }

            string rt = (values.Length < 2 ? "" : values[1].Trim());
            try
            {
                double.Parse(rt);
            }
            catch (FormatException)
            {
                string message = string.Format("The value {0} is not a valid retention time.", rt);
                MessageBox.Show(this, message, Program.Name);
                colError = 1;
                return false;
            }
            colError = -1;
            return true;
        }


        private void gridPeptides_RowValidating(object sender, DataGridViewCellCancelEventArgs e)
        {
            var newPeps = new List<MeasuredRetentionTime>();
            if (ValidatePeptides(e, newPeps))
                _activePeptides = newPeps;
        }

        private void gridPeptides_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_activePeptides == null || _activePeptides.Count < 1)
            {
                return;
            }

            try
            {
                var regressionPeps = UpdateCalculator(_driverCalculators.SelectedItem);
                if (regressionPeps != null)
                    SetTablePeptides(regressionPeps);
                RecalcRegression(_activePeptides);
            }
            catch (Exception)
            {
                comboCalculator.SelectedIndex = 0;
            }
        }

        private void gridPeptides_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            /*
            var newPeps = new List<MeasuredRetentionTime>();
            if (!ValidatePeptides(new CancelEventArgs(), newPeps))
                return;

            _activePeptides = newPeps;

            if (_activePeptides.Count < 1)
                return;
            try
            {
                UpdateCalculator(_driverCalculators.SelectedItem);
            }
            catch (IncompleteStandardException f)
            {
                MessageDlg.Show(this,
                    string.Format(
                        "The table no longer lists the complete standard. The {0} calculator cannot be used.",
                        f.Calculator));
                comboCalculator.SelectedIndex = 0;
            }
             */
        }

        private RetentionScoreCalculatorSpec RecalcRegression(List<MeasuredRetentionTime> peptides)
        {
            var tryCalcs = Settings.Default.RTScoreCalculatorList.ToList();
            RetentionScoreCalculatorSpec calculator = null;
            while (calculator == null && tryCalcs.Count > 0)
            {
                try
                {
                    calculator = RecalcRegression(tryCalcs, peptides);
                    break;
                }
                catch (IncompleteStandardException e)
                {
                    tryCalcs.Remove(e.Calculator);
                }
            }
            return calculator;
        }

        private void RecalcRegression(RetentionScoreCalculatorSpec calculator, List<MeasuredRetentionTime> peptides)
        {
            RecalcRegression(new[] { calculator }, peptides);
        }

        private RetentionScoreCalculatorSpec RecalcRegression(IEnumerable<RetentionScoreCalculatorSpec> calculators, List<MeasuredRetentionTime> peptidesTimes)
        {
            RetentionScoreCalculatorSpec calculatorSpec;
            RetentionTimeStatistics statistics;

            RetentionTimeRegression regression = RetentionTimeRegression.CalcRegression("Recalc", calculators,
                                                                                        peptidesTimes, out statistics);

            double r;
            if (regression == null)
            {
                textSlope.Text = "";
                textIntercept.Text = "";
                textTimeWindow.Text = "";
                comboCalculator.SelectedIndex = -1;

                return null;
            }
            else
            {
                textSlope.Text = string.Format("{0}", regression.Conversion.Slope);
                textIntercept.Text = string.Format("{0}", regression.Conversion.Intercept);
                textTimeWindow.Text = string.Format("{0:F01}", regression.TimeWindow);

                // Select best calculator match.
                calculatorSpec = regression.Calculator;

                // Save statistics to show in RTDetails form.
                _statistics = statistics;
                r = statistics.R;
            }

            var pepCount = calculatorSpec.ChooseRegressionPeptides(peptidesTimes.Select(mrt => mrt.PeptideSequence)).Count();

            labelRValue.Text = string.Format("({0} peptides, R = {1:F02})",
                pepCount, r);
            // Right align with the peptide grid.
            labelRValue.Left = gridPeptides.Right - labelRValue.Width;

            return calculatorSpec;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void labelRValue_Click(object sender, EventArgs e)
        {
/*
            var calc = _driverCalculators.SelectedItem;
            if (!calc.IsUsable)
            {
                try
                {
                    calc = calc.Initialize();
                }
                catch (CalculatorException f)
                {
                    MessageDlg.Show(this, f.Message);
                    return;
                }
            }
            var peptides = new List<string>();
            var scores = new List<double>();
            var predicts = new List<double>();
            var times = new List<double>();

            double slope = double.Parse(textSlope.Text);
            double intercept = double.Parse(textIntercept.Text);
            foreach (var measuredPeptide in GetDocumentPeptides())
            {
                peptides.Add(measuredPeptide.PeptideSequence);
                times.Add(measuredPeptide.RetentionTime);
                double score = calc.ScoreSequence(measuredPeptide.PeptideSequence);
                scores.Add(score);
                predicts.Add(slope*score + intercept);
            }
            var statistics = new RetentionTimeStatistics(1.0, peptides, scores, predicts, times);
 */
            var statistics = _statistics;
            if (statistics != null)
            {
                using (RTDetails dlg = new RTDetails(statistics))
                {
                    dlg.ShowDialog(this);
                }
            }
        }

        #region Functional test support

        public void SetTimeWindow(double time)
        {
            textTimeWindow.Text = time.ToString();
        }

        public void SetRegressionName(string name)
        {
            textName.Text = name;
        }

        public void AddCalculator()
        {
            _driverCalculators.AddItem();
        }

        public void EditCalculatorList()
        {
            _driverCalculators.EditList();
        }

        public void ChooseCalculator(string name)
        {
            comboCalculator.SelectedItem = name;
        }

        public void AddResults()
        {
            _activePeptides = GetDocumentPeptides();
            var regressionPeps = UpdateCalculator(null);
            if (regressionPeps != null)
                SetTablePeptides(regressionPeps);
        }

        public void SetSlope(string s)
        {
            textSlope.Text = s;
        }

        public void SetIntercept(string s)
        {
            textIntercept.Text = s;
        }

        #endregion
    }
}
