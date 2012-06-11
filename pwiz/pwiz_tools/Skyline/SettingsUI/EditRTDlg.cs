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
using pwiz.Common.DataBinding;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditRTDlg : FormEx
    {
        private readonly SettingsListComboDriver<RetentionScoreCalculatorSpec> _driverCalculators;
        private readonly RetentionTimeGridViewDriver _gridViewDriver;

        private RetentionTimeRegression _regression;
        private readonly IEnumerable<RetentionTimeRegression> _existing;
        private RetentionTimeStatistics _statistics;

        public EditRTDlg(IEnumerable<RetentionTimeRegression> existing)
        {
            _existing = existing;

            InitializeComponent();

            Icon = Resources.Skyline;

            _gridViewDriver = new RetentionTimeGridViewDriver(gridPeptides, bindingPeptides,
                                                            new SortableBindingList<MeasuredPeptide>());

            _driverCalculators = new SettingsListComboDriver<RetentionScoreCalculatorSpec>(
                comboCalculator, Settings.Default.RTScoreCalculatorList);
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

                    var pepTimes = _regression.PeptideTimes;
                    if (pepTimes.Count > 0)
                    {
                        ShowPeptides(true);
                        foreach (var pepTime in pepTimes)
                            Peptides.Add(new MeasuredPeptide(pepTime.PeptideSequence, pepTime.RetentionTime));

                        // Get statistics
                        RecalcRegression(_driverCalculators.SelectedItem, _regression.PeptideTimes.ToList());
                        //UpdateCalculator();
                    }

                    comboCalculator.SelectedItem = _regression.Calculator.Name;
                    /*
                    textSlope.Text = string.Format("{0:F04}", _regression.Conversion.Slope);
                    textIntercept.Text = string.Format("{0:F04}", _regression.Conversion.Intercept);
                     */
                    if (_regression.Conversion == null)
                        cbAutoCalc.Checked = true;
                    else
                    {
                        cbAutoCalc.Checked = false;
                        textSlope.Text = _regression.Conversion.Slope.ToString(CultureInfo.CurrentCulture);
                        textIntercept.Text = _regression.Conversion.Intercept.ToString(CultureInfo.CurrentCulture);
                    }
                    textTimeWindow.Text = string.Format("{0:F04}", _regression.TimeWindow);
                }
            }
        }

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

            double? slope = null;
            double? intercept = null;

            if (!cbAutoCalc.Checked)
            {
                double slopeTmp;
                if (!helper.ValidateDecimalTextBox(e, textSlope, out slopeTmp))
                    return;
                slope = slopeTmp;

                double interceptTmp;
                if (!helper.ValidateDecimalTextBox(e, textIntercept, out interceptTmp))
                    return;
                intercept = interceptTmp;
            }

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

            RetentionTimeRegression regression =
                new RetentionTimeRegression(name, calculator, slope, intercept, window, GetTablePeptides());

            _regression = regression;

            DialogResult = DialogResult.OK;
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
                btnUseCurrent.Anchor |= AnchorStyles.Top;
                btnUseCurrent.Anchor &= ~AnchorStyles.Bottom;
                btnShowGraph.Anchor |= AnchorStyles.Top;
                btnShowGraph.Anchor &= ~AnchorStyles.Bottom;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                Height = ptCorner.Y - Top;
            }

            labelPeptides.Visible = visible;
            labelRValue.Visible = visible;
            btnUseCurrent.Visible = visible;
            btnUseCurrent.Enabled = hasResults;
            btnShowGraph.Visible = visible;
            gridPeptides.Visible = visible;

            if (visible)
            {
                Height = ptCorner.Y - Top;
                FormBorderStyle = FormBorderStyle.Sizable;
                btnUseCurrent.Anchor |= AnchorStyles.Bottom;
                btnUseCurrent.Anchor &= ~AnchorStyles.Top;
                btnShowGraph.Anchor |= AnchorStyles.Bottom;
                btnShowGraph.Anchor &= ~AnchorStyles.Top;
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
            var calc = _driverCalculators.SelectedItem;
            btnShowGraph.Enabled = (calc != null);
            cbAutoCalc.Enabled = (calc is RCalcIrt);
            if (!cbAutoCalc.Enabled)
                cbAutoCalc.Checked = false;
            if (calc != null)
            {
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

        private class RetentionTimeGridViewDriver : PeptideGridViewDriver<MeasuredPeptide>
        {
            public RetentionTimeGridViewDriver(DataGridViewEx gridView,
                                             BindingSource bindingSource,
                                             SortableBindingList<MeasuredPeptide> items)
                : base(gridView, bindingSource, items)
            {
            }

            protected override void DoPaste()
            {
                var measuredPeptidesNew = new List<MeasuredPeptide>();
                GridView.DoPaste(MessageParent, ValidateRow,
                                          values =>
                                          measuredPeptidesNew.Add(new MeasuredPeptide
                                          {
                                              Sequence = values[0],
                                              RetentionTime = double.Parse(values[1])
                                          }));

                SetTablePeptides(measuredPeptidesNew);
            }

            public void SetTablePeptides(IEnumerable<MeasuredRetentionTime> tablePeps)
            {
                SetTablePeptides(tablePeps.Select(p => new MeasuredPeptide(p.PeptideSequence, p.RetentionTime)).ToArray());
            }

            private void SetTablePeptides(IList<MeasuredPeptide> tablePeps)
            {
                string message = ValidateUniquePeptides(tablePeps.Select(p => p.Sequence), null, null);
                if (message != null)
                {
                    MessageDlg.Show(MessageParent, message);
                    return;
                }

                Items.Clear();
                foreach (var measuredRT in tablePeps)
                    Items.Add(measuredRT);
            }
        }

        private SortableBindingList<MeasuredPeptide> Peptides { get { return _gridViewDriver.Items; } }

        private IList<MeasuredRetentionTime> GetTablePeptides()
        {
            return (from p in Peptides
                    where p.Sequence != null
                    select new MeasuredRetentionTime(p.Sequence, p.RetentionTime)).ToArray();
        }

        private void btnUseCurrent_Click(object sender, EventArgs e)
        {
            AddResults();
        }

        public void AddResults()
        {
            SetTablePeptides(GetDocumentPeptides().ToArray());
            var regressionPeps = UpdateCalculator(null);
            if (regressionPeps != null)
                SetTablePeptides(regressionPeps);
        }

        public void SetTablePeptides(IList<MeasuredRetentionTime> tablePeps)
        {
            if (ArrayUtil.EqualsDeep(GetTablePeptides(), tablePeps))
                return;

            _gridViewDriver.SetTablePeptides(tablePeps);
        }

        private void btnShowGraph_Click(object sender, EventArgs e)
        {
            ShowGraph();
        }

        public void ShowGraph()
        {
            var calc = _driverCalculators.SelectedItem;
            if (calc == null)
                return;

            if (!calc.IsUsable)
            {
                using (var longWait = new LongWaitDlg
                {
                    Text = "Initializing",
                    Message = string.Format("Initializing {0} calculator", calc.Name)
                })
                {
                    try
                    {
                        var status = longWait.PerformWork(this, 800, monitor =>
                        {
                            calc = Settings.Default.RTScoreCalculatorList.Initialize(monitor, calc);
                        });
                        if (status.IsError)
                        {
                            MessageBox.Show(this, status.ErrorException.Message, Program.Name);
                            return;
                        }
                    }
                    catch (Exception x)
                    {
                        MessageDlg.Show(this, string.Format("An error occurred attempting to initialize the calculator {0}.\n{1}", calc.Name, x.Message));
                        return;
                    }
                }
            }

            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            double slope;
            if (!helper.ValidateDecimalTextBox(e, textSlope, out slope))
                return;

            double intercept;
            if (!helper.ValidateDecimalTextBox(e, textIntercept, out intercept))
                return;

            var scores = new List<double>();
            var times = new List<double>();

            foreach (var measuredPeptide in Peptides)
            {
                times.Add(measuredPeptide.RetentionTime);
                double? score = calc.ScoreSequence(measuredPeptide.Sequence);
                scores.Add(score ?? calc.UnknownScore);
            }

            var statScores = new Statistics(scores);
            var statTimes = new Statistics(times);
            double slopeRegress = statTimes.Slope(statScores);
            double interceptRegress = statTimes.Intercept(statScores);

            var regressionGraphData = new RegressionGraphData
            {
                Title = "Retention Times by Score",
                LabelX = calc.Name,
                LabelY = "Measured Time",
                XValues = scores.ToArray(),
                YValues = times.ToArray(),
                RegressionLine = new RegressionLine(slopeRegress, interceptRegress),
                RegressionLineCurrent = new RegressionLine(slope, intercept)
            };

            using (var dlg = new GraphRegression(new[] { regressionGraphData }))
            {
                dlg.ShowDialog(this);
            }
        }

        public IEnumerable<MeasuredRetentionTime> GetDocumentPeptides()
        {
            var document = Program.ActiveDocumentUI;
            if (!document.Settings.HasResults)
                yield break; // This shouldn't be possible, but just to be safe.
            if (!document.Settings.MeasuredResults.IsLoaded)
                yield break;

            var setPeps = new HashSet<string>();
            foreach(var nodePep in document.Peptides)
            {
                string modSeq = document.Settings.GetModifiedSequence(nodePep);
                // If a document contains the same peptide twice, make sure it
                // only gets added once.
                if (setPeps.Contains(modSeq))
                    continue;
                setPeps.Add(modSeq);

                if (nodePep.AveragePeakCountRatio < 0.5)
                    continue;

                double? retentionTime = nodePep.SchedulingTime;
                if (!retentionTime.HasValue)
                    continue;

                yield return new MeasuredRetentionTime(modSeq, retentionTime.Value);
            }
        }

        /// <summary>
        /// This function will update the calculator to the one given, or to the one with the best score for the document 
        /// peptides. It will then return the peptides chosen by that calculator for regression.
		/// Todo: split this function into one that chooses and returns the calculator and one that returns the peptides
		/// todo: chosen by that calculator
        /// </summary>
        private IList<MeasuredRetentionTime> UpdateCalculator(RetentionScoreCalculatorSpec calculator)
        {
            bool calcInitiallyNull = calculator == null;

            var activePeptides = GetTablePeptides();
            if (activePeptides.Count == 0)
                return null;

            //Try connecting all the calculators
            Settings.Default.RTScoreCalculatorList.Initialize(null);

            if (calculator == null)
            {
                //this will not update the calculator
                calculator = RecalcRegression(activePeptides);
            }
            else
            {
                var calcSettings = Settings.Default.GetCalculatorByName(calculator.Name);
                if (calcSettings != null)
                    calculator = calcSettings;

                if (!calculator.IsUsable)
                {
                    MessageDlg.Show(this, "The calculator cannot be used to score peptides. Please check its settings.");
                    return activePeptides;
                }

                RecalcRegression(calculator, activePeptides);
            }

            var usePeptides = new HashSet<string>(calculator.ChooseRegressionPeptides(
                activePeptides.Select(pep => pep.PeptideSequence)));
            //now go back and get the MeasuredPeptides corresponding to the strings chosen by the calculator
            var tablePeptides = activePeptides.Where(measuredRT =>
                usePeptides.Contains(measuredRT.PeptideSequence)).ToList();

            if (tablePeptides.Count == 0 && activePeptides.Count != 0)
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

        private RetentionScoreCalculatorSpec RecalcRegression(IList<MeasuredRetentionTime> peptides)
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

        private void RecalcRegression(RetentionScoreCalculatorSpec calculator, IList<MeasuredRetentionTime> peptides)
        {
            RecalcRegression(new[] { calculator }, peptides);
        }

        private RetentionScoreCalculatorSpec RecalcRegression(IList<RetentionScoreCalculatorSpec> calculators, IList<MeasuredRetentionTime> peptidesTimes)
        {
            RetentionScoreCalculatorSpec calculatorSpec;
            RetentionTimeStatistics statistics;

            RetentionTimeRegression regression = RetentionTimeRegression.CalcRegression("Recalc", calculators,
                                                                                        peptidesTimes, out statistics);

            double r = 0;
            if (regression == null)
            {
                if (calculators.Count() > 1)
                {
                    textSlope.Text = "";
                    textIntercept.Text = "";
                    textTimeWindow.Text = "";
                    comboCalculator.SelectedIndex = -1;

                    return null;
                }
                calculatorSpec = calculators.First();
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

            labelRValue.Text = string.Format("({0} peptides, R = {1})", pepCount, Math.Round(r, RetentionTimeRegression.ThresholdPrecision));
            // Right align with the peptide grid.
            labelRValue.Left = gridPeptides.Right - labelRValue.Width;

            return calculatorSpec;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void cbAutoCalc_CheckedChanged(object sender, EventArgs e)
        {
            textSlope.Enabled = textIntercept.Enabled = btnCalculate.Enabled = !cbAutoCalc.Checked;
            if (cbAutoCalc.Checked)
            {
                if (gridPeptides.Visible)
                {
                    ShowPeptides(false);
                }
                textSlope.Text = textIntercept.Text = "";
                Peptides.Clear();
            }
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
            textTimeWindow.Text = time.ToString(CultureInfo.CurrentCulture);
        }

        public void SetRegressionName(string name)
        {
            textName.Text = name;
        }

        public void AddCalculator()
        {
            CheckDisposed();
            _driverCalculators.AddItem();
        }

        public void EditCalculatorList()
        {
            CheckDisposed();
            _driverCalculators.EditList();
        }

        public void ChooseCalculator(string name)
        {
            comboCalculator.SelectedItem = name;
        }

        public void SetSlope(string s)
        {
            textSlope.Text = s;
        }

        public void SetIntercept(string s)
        {
            textIntercept.Text = s;
        }

        public void SetAutoCalcRegression(bool autoCalc)
        {
            cbAutoCalc.Checked = autoCalc;
        }

        #endregion
    }

    public class MeasuredPeptide : IPeptideData
    {
        public MeasuredPeptide()
        {
        }

        public MeasuredPeptide(string seq, double rt)
        {
            Sequence = seq;
            RetentionTime = rt;
        }

        public string Sequence { get; set; }
        public double RetentionTime { get; set; }

        public static string ValidateSequence(string sequence)
        {
            if (sequence == null)
                return "A modified peptide sequence is required for each entry.";
            if (!FastaSequence.IsExSequence(sequence))
                return string.Format("The sequence '{0}' is not a valid modified peptide sequence.", sequence);
            return null;
        }

        public static string ValidateRetentionTime(string rtText, bool allowNegative)
        {
            double rtValue;
            if (rtText == null || !double.TryParse(rtText, out rtValue))
                return "Measured retention times must be valid decimal numbers.";
            if (!allowNegative && rtValue <= 0)
                return "Measured retention times must be greater than zero.";
            return null;
        }
    }
}
