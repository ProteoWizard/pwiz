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
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

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
                    textName.Text = string.Empty;
                }
                else
                {
                    textName.Text = _regression.Name;

                    var pepTimes = _regression.PeptideTimes;
                    if (pepTimes.Count > 0)
                    {
                        ShowPeptides(true);

                        var previous = Peptides.RaiseListChangedEvents;
                        Peptides.RaiseListChangedEvents = false;
                        Peptides.AddRange(pepTimes.Select(pepTime => new MeasuredPeptide(pepTime.PeptideSequence, pepTime.RetentionTime)));
                        Peptides.RaiseListChangedEvents = previous;
                        if (previous)
                            Peptides.ResetBindings();

                        // Get statistics
                        RecalcRegression(_driverCalculators.SelectedItem, _regression.PeptideTimes.ToList());
                        //UpdateCalculator();
                    }

                    comboCalculator.SelectedItem = _regression.Calculator.Name;
                    /*
                    textSlope.Text = string.Format("{0:F04}", _regression.Conversion.Slope);
                    textIntercept.Text = string.Format("{0:F04}", _regression.Conversion.Intercept);
                     */
                    if (_regression.IsAutoCalculated || _regression.Conversion == null)
                        cbAutoCalc.Checked = true;
                    else
                    {
                        cbAutoCalc.Checked = false;
                        var regressionLine = _regression.Conversion as RegressionLineElement;
                        if (regressionLine != null)
                        {
                            textSlope.Text = regressionLine.Slope.ToString(LocalizationHelper.CurrentCulture);
                            textIntercept.Text =
                                regressionLine.Intercept.ToString(LocalizationHelper.CurrentCulture);
                        }
                    }
                    textTimeWindow.Text = string.Format(@"{0:F04}", _regression.TimeWindow);
                }
            }
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(textName, out name))
                return;

            if (_existing.Contains(r => !ReferenceEquals(_regression, r) && Equals(name, r.Name)))
            {
                helper.ShowTextBoxError(textName, Resources.EditRTDlg_OkDialog_The_retention_time_regression__0__already_exists, name);
                return;
            }

            double? slope = null;
            double? intercept = null;

            if (!cbAutoCalc.Checked)
            {
                double slopeTmp;
                if (!helper.ValidateDecimalTextBox(textSlope, out slopeTmp))
                    return;
                slope = slopeTmp;

                double interceptTmp;
                if (!helper.ValidateDecimalTextBox(textIntercept, out interceptTmp))
                    return;
                intercept = interceptTmp;
            }

            double window;
            if (!helper.ValidateDecimalTextBox(textTimeWindow, out window))
                return;

            if (window <= 0)
            {
                helper.ShowTextBoxError(textTimeWindow, Resources.EditRTDlg_OkDialog__0__must_be_greater_than_0);
                return;
            }

            if (comboCalculator.SelectedIndex == -1)
            {
                MessageDlg.Show(this, Resources.EditRTDlg_OkDialog_Retention_time_prediction_requires_a_calculator_algorithm);
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
                btnCalculate.Text = Resources.EditRTDlg_ShowPeptides_Calculate_Left;
                ctlCorner = gridPeptides;
                yExtra = btnUseCurrent.Height + 10;
            }
            else
            {
                btnCalculate.Text = Resources.EditRTDlg_ShowPeptides_Calculate_Right;
                ctlCorner = btnCalculate;
            }

            Point ptCorner = new Point(ctlCorner.Right, ctlCorner.Bottom + yExtra);
            // A Windows 10 update caused using PointToScreen to leak GDI handles
//            ptCorner = PointToScreen(ptCorner);
            ptCorner.Offset(20, 20);

            if (!visible)
            {
                gridPeptides.Anchor &= ~AnchorStyles.Bottom;
                btnUseCurrent.Anchor |= AnchorStyles.Top;
                btnUseCurrent.Anchor &= ~AnchorStyles.Bottom;
                btnShowGraph.Anchor |= AnchorStyles.Top;
                btnShowGraph.Anchor &= ~AnchorStyles.Bottom;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                // Adjust form size without knowing the screen rectangle to avoid leaking
//                Height = ptCorner.Y - Top;
                Height -= ClientRectangle.Bottom - ptCorner.Y;
            }

            labelPeptides.Visible = visible;
            labelRValue.Visible = visible;
            btnUseCurrent.Visible = visible;
            btnUseCurrent.Enabled = hasResults;
            btnShowGraph.Visible = visible;
            gridPeptides.Visible = visible;

            if (visible)
            {
                // Adjust form size without knowing the screen rectangle to avoid leaking
//                Height = ptCorner.Y - Top;
                Height -= ClientRectangle.Bottom - ptCorner.Y;
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
                // Automatically set up if all fields are empty
                if (calc is RCalcIrt)
                {
                    if (string.IsNullOrEmpty(textName.Text))
                        textName.Text = Helpers.GetUniqueName(calc.Name, name => !_existing.Contains(r => Equals(r.Name, name)));
                    if (string.IsNullOrEmpty(textSlope.Text) && string.IsNullOrEmpty(textIntercept.Text))
                        cbAutoCalc.Checked = true;
                    if (string.IsNullOrEmpty(textTimeWindow.Text))
                        textTimeWindow.Text = ImportPeptideSearch.DEFAULT_RT_WINDOW.ToString(LocalizationHelper.CurrentCulture);
                }

                try
                {
                    var regressionPeps = UpdateCalculator(calc);
                    if (regressionPeps != null)
                        SetTablePeptides(regressionPeps);
                }
                catch (CalculatorException e)
                {
                    MessageDlg.ShowException(this, e);
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
                GridView.DoPaste(MessageParent, ValidateRowWithTime,
                                          values =>
                                          measuredPeptidesNew.Add(new MeasuredPeptide
                                          {
                                              Target = new Target(values[0]),  // CONSIDER(bspratt) small molecule equivalent?
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
                string message = ValidateUniquePeptides(tablePeps.Select(p => p.Target), null, null);
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

        public int PeptideCount { get { return Peptides.Count; } }

        private SortableBindingList<MeasuredPeptide> Peptides { get { return _gridViewDriver.Items; } }

        private IList<MeasuredRetentionTime> GetTablePeptides()
        {
            return (from p in Peptides
                    where p.Sequence != null
                    select new MeasuredRetentionTime(p.Target, p.RetentionTime)).ToArray();
        }

        private void btnUseCurrent_Click(object sender, EventArgs e)
        {
            AddResults();
        }

        public void AddResults()
        {
            var peps = GetDocumentPeptides().ToArray();
            var regressionPeps = UpdateCalculator(null, peps);
            SetTablePeptides(regressionPeps ?? peps);
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
                    Text = Resources.EditRTDlg_ShowGraph_Initializing,
                    Message = string.Format(Resources.EditRTDlg_ShowGraph_Initializing__0__calculator, calc.Name)
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
                            MessageDlg.Show(this, status.ErrorException.Message);
                            return;
                        }
                    }
                    catch (Exception x)
                    {
                        var message = TextUtil.LineSeparate(string.Format(Resources.EditRTDlg_ShowGraph_An_error_occurred_attempting_to_initialize_the_calculator__0__,
                                                                          calc.Name),
                                                            x.Message);
                        MessageDlg.ShowWithException(this,message, x);
                        return;
                    }
                }
            }

            var helper = new MessageBoxHelper(this);

            double slope;
            if (!helper.ValidateDecimalTextBox(textSlope, out slope))
                return;

            double intercept;
            if (!helper.ValidateDecimalTextBox(textIntercept, out intercept))
                return;

            var scores = new List<double>();
            var times = new List<double>();

            foreach (var measuredPeptide in Peptides)
            {
                
                times.Add(measuredPeptide.RetentionTime);
                double? score = calc.ScoreSequence(measuredPeptide.Target);
                scores.Add(score ?? calc.UnknownScore);
            }

            var statScores = new Statistics(scores);
            var statTimes = new Statistics(times);
            double slopeRegress = statTimes.Slope(statScores);
            double interceptRegress = statTimes.Intercept(statScores);

            var regressionGraphData = new RegressionGraphData
            {
                Title = Resources.EditRTDlg_ShowGraph_Retention_Times_by_Score,
                LabelX = calc.Name,
                LabelY = Resources.EditRTDlg_ShowGraph_Measured_Time,
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

            var setPeps = new HashSet<Target>();
            foreach(var nodePep in document.Molecules)
            {
                var modSeq = document.Settings.GetModifiedSequence(nodePep);
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
        private IList<MeasuredRetentionTime> UpdateCalculator(RetentionScoreCalculatorSpec calculator, IList<MeasuredRetentionTime> activePeptides = null)
        {
            bool calcInitiallyNull = calculator == null;

            activePeptides = activePeptides ?? GetTablePeptides();
            if (activePeptides.Count == 0)
                return null;

            //Try connecting all the calculators
            Settings.Default.RTScoreCalculatorList.Initialize(null);

            if (calculator == null)
            {
                //this will not update the calculator
                calculator = RecalcRegression(activePeptides);
                if (calculator == null)
                    return null;
            }
            else
            {
                var calcSettings = Settings.Default.GetCalculatorByName(calculator.Name);
                if (calcSettings != null)
                    calculator = calcSettings;

                if (!calculator.IsUsable)
                {
                    MessageDlg.Show(this,
                                    Resources.
                                        EditRTDlg_UpdateCalculator_The_calculator_cannot_be_used_to_score_peptides_Please_check_its_settings);
                    return activePeptides;
                }

                RecalcRegression(calculator, activePeptides);
            }

            int minCount;
            var usePeptides = new HashSet<Target>(calculator.ChooseRegressionPeptides(
                activePeptides.Select(pep => pep.PeptideSequence), out minCount));
            //now go back and get the MeasuredPeptides corresponding to the strings chosen by the calculator
            var tablePeptides = activePeptides.Where(measuredRT =>
                usePeptides.Contains(measuredRT.PeptideSequence)).ToList();

            if (tablePeptides.Count == 0 && activePeptides.Count != 0)
            {
                MessageDlg.Show(this,
                                String.Format(
                                    Resources.
                                        EditRTDlg_UpdateCalculator_The__0__calculator_cannot_score_any_of_the_peptides,
                                                    calculator.Name));
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
            var summary = RetentionTimeRegression.CalcBestRegressionLongOperationRunner(XmlNamedElement.NAME_INTERNAL, calculators, peptidesTimes,
                null, false, RegressionMethodRT.linear, CancellationToken.None);
            var regression = summary.Best.Regression;
            var statistics = summary.Best.Statistics;
            var calculatorSpec = summary.Best.Calculator;

            double r = 0;
            if (regression == null)
            {
                if (calculators.Count > 1)
                {
                    textSlope.Text = string.Empty;
                    textIntercept.Text = string.Empty;
                    textTimeWindow.Text = string.Empty;
                    comboCalculator.SelectedIndex = -1;

                    return null;
                }
                calculatorSpec = calculators.First();
            }
            else
            {
                var regressionLine = regression.Conversion as RegressionLineElement;
                if (regressionLine != null)
                {
                    textSlope.Text = string.Format(@"{0}", regressionLine.Slope);
                    textIntercept.Text = string.Format(@"{0}", regressionLine.Intercept);
                }
                textTimeWindow.Text = string.Format(@"{0:F01}", regression.TimeWindow);

                // Select best calculator match.
                calculatorSpec = regression.Calculator;

                // Save statistics to show in RTDetails form.
                _statistics = statistics;
                r = statistics.R;
            }

            int minCount;
            var pepCount = calculatorSpec.ChooseRegressionPeptides(peptidesTimes.Select(mrt => mrt.PeptideSequence), out minCount).Count();

            labelRValue.Text = string.Format(Resources.EditRTDlg_RecalcRegression__0__peptides_R__1__, pepCount,
                                             Math.Round(r, RetentionTimeRegression.ThresholdPrecision));
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
                textSlope.Text = textIntercept.Text = string.Empty;
                Peptides.Clear();
            }
        }

        private void labelRValue_Click(object sender, EventArgs e)
        {
            ShowDetails();
        }

        public void ShowDetails()
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
            textTimeWindow.Text = time.ToString(LocalizationHelper.CurrentCulture);
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

        public void EditCurrentCalculator()
        {
            CheckDisposed();
            _driverCalculators.EditCurrent();
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
}
