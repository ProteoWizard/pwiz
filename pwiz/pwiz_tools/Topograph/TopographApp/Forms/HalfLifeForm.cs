/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;
using pwiz.Topograph.ui.Controls;
using pwiz.Topograph.Util;
using ZedGraph;
using pwiz.Topograph.ui.Properties;

namespace pwiz.Topograph.ui.Forms
{
    public partial class HalfLifeForm : WorkspaceForm
    {
        private readonly List<PeptideAnalysis> _peptideAnalyses = new List<PeptideAnalysis>();
        private HalfLifeSettings? _lastCalcedHalfLifeSettings;
        private ZedGraphControl _zedGraphControl;
        private LineItem _pointsCurve;
        private IList<PeptideFileAnalysis> _peptideFileAnalysisPoints;
        private ICollection<double> _excludedTimePoints = new double[0];
        public HalfLifeForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            _zedGraphControl = new ZedGraphControlEx
                                   {
                                       Dock = DockStyle.Fill,
                                       GraphPane = { Title = {Text = null}}
                                   };
            _zedGraphControl.GraphPane.XAxis.Title.Text = "Time";
            _zedGraphControl.MouseDownEvent += ZedGraphControlOnMouseDownEvent;
            splitContainer1.Panel2.Controls.Add(_zedGraphControl);
            colTurnover.DefaultCellStyle.Format = "#.##%";
            colPrecursorPool.DefaultCellStyle.Format = "#.##%";
            SetHalfLifeSettings(Workspace.GetHalfLifeSettings(Settings.Default.HalfLifeSettings));
        }

        bool ZedGraphControlOnMouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (_pointsCurve == null)
            {
                return false;
            }
            CurveItem nearestCurve;
            int nearestIndex;
            if (!sender.GraphPane.FindNearestPoint(e.Location, _pointsCurve, out nearestCurve, out nearestIndex))
            {
                return false;
            }
            var nearestPeptideFileAnalysis = _peptideFileAnalysisPoints[nearestIndex];
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                var row = dataGridView1.Rows[i];
                if (Equals(nearestPeptideFileAnalysis, row.Tag))
                {
                    dataGridView1.CurrentCell = row.Cells[0];
                    return true;
                }
            }
            return false;
        }

        public String Peptide { 
            get
            {
                return tbxPeptide.Text;
            }
            set
            {
                tbxPeptide.Text = value;
                Requery();
            } 
        }
        public String ProteinName
        {
            get
            {
                return tbxProtein.Text;
            }
            set
            {
                tbxProtein.Text = value;
                Requery();
            }
        }
        public String Cohort
        {
            get
            {
                return Convert.ToString(comboCohort.SelectedItem);
            }
            set
            {
                comboCohort.SelectedItem = value;
                Requery();
            }
        }

        public HalfLifeSettings GetHalfLifeSettings()
        {
            var result = halfLifeSettingsControl.HalfLifeSettings;
            result.BySample = cbxBySample.Checked;
            return result;
        }

        public void SetHalfLifeSettings(HalfLifeSettings halfLifeSettings)
        {
            cbxBySample.Checked = halfLifeSettings.BySample;
            halfLifeSettingsControl.HalfLifeSettings = halfLifeSettings;
            UpdateRows(true);
        }

        public bool LogPlot
        {
            get
            {
                return cbxLogPlot.Checked;
            }
            set
            {
                cbxLogPlot.Checked = value;
                UpdateRows(true);
            }
        }
        public TracerDef TracerDef
        {
            get
            {
                return Workspace.GetTracerDefs()[0];
            }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Requery();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            ClearPeptideAnalyses();
        }

        private void ClearPeptideAnalyses()
        {
            foreach (var peptideAnalysis in _peptideAnalyses)
            {
                peptideAnalysis.DecChromatogramRefCount();
            }
            _peptideAnalyses.Clear();
        }

        private void RequeryPeptideAnalyses()
        {
            PeptideAnalysis[] peptideAnalysesToLoad;
            if (string.IsNullOrEmpty(Peptide))
            {
                peptideAnalysesToLoad = Workspace.PeptideAnalyses.Where(
                    peptideAnalysis => peptideAnalysis.Peptide.ProteinName == ProteinName).ToArray();
            }
            else
            {
                peptideAnalysesToLoad =
                    Workspace.PeptideAnalyses.Where(peptideAnalysis => peptideAnalysis.Peptide.Sequence == Peptide)
                        .ToArray();
            }
            foreach (var peptideAnalysis in peptideAnalysesToLoad)
            {
                peptideAnalysis.IncChromatogramRefCount();
            }
            var peptideAnalysisIds = new HashSet<long>(peptideAnalysesToLoad.Select(pa=>pa.Id));
            var peptideAnalyses = TopographForm.Instance.LoadPeptideAnalyses(peptideAnalysisIds);
            ClearPeptideAnalyses();
            _peptideAnalyses.AddRange(peptideAnalyses.Values);
        }

        private void UpdateCohortCombo()
        {
            var cohortSet = new HashSet<String> {""};
            foreach (var msDataFile in Workspace.MsDataFiles)
            {
                cohortSet.Add(HalfLifeCalculator.GetCohort(msDataFile, GetHalfLifeSettings().BySample));
            }
            var selectedCohort = (string) comboCohort.SelectedItem;
            var cohorts = new List<String>(cohortSet);
            cohorts.Sort();
            if (Lists.EqualsDeep(cohorts, comboCohort.Items))
            {
                return;
            }
            comboCohort.Items.Clear();
            foreach (var cohort in cohorts)
            {
                comboCohort.Items.Add(cohort);
            }
            comboCohort.SelectedIndex = Math.Max(0, cohorts.IndexOf(selectedCohort));
        }

        private void Requery()
        {
            UpdateCohortCombo();
            if (string.IsNullOrEmpty(Peptide))
            {
                Text = TabText = "Half Life: " + ProteinName.Substring(0, Math.Min(20, ProteinName.Length));
            }
            else
            {
                Text = TabText = "Half Life: " + Peptide;
            }
            if (!IsHandleCreated)
            {
                return;
            }
            dataGridView1.Rows.Clear();
            RequeryPeptideAnalyses();
            foreach (var peptideAnalysis in _peptideAnalyses)
            {
                peptideAnalysis.EnsurePeaksCalculated();
                tbxProteinDescription.Text = peptideAnalysis.Peptide.ProteinDescription;
                foreach (var peptideFileAnalysis in peptideAnalysis.GetFileAnalyses(false))
                {
                    var row = dataGridView1.Rows[dataGridView1.Rows.Add()];
                    row.Tag = peptideFileAnalysis;
                }
            }
            UpdateRows(true);
        }

        private void UpdateRows(bool force)
        {
            if (!IsHandleCreated)
            {
                return;
            }
            var halfLifeSettings = GetHalfLifeSettings();
            if (!force && null != _lastCalcedHalfLifeSettings && Equals(_lastCalcedHalfLifeSettings.Value, halfLifeSettings))
            {
                return;
            }
            colSample.Visible = halfLifeSettings.BySample;
            colEvviesFilter.Visible = halfLifeSettings.EvviesFilter != EvviesFilterEnum.None;
            var peptideFileAnalyses = new List<PeptideFileAnalysis>();
            for (int iRow = 0; iRow < dataGridView1.Rows.Count; iRow++)
            {
                var row = dataGridView1.Rows[iRow];
                var peptideFileAnalysis = (PeptideFileAnalysis) row.Tag;
                peptideFileAnalysis.PeptideAnalysis.EnsurePeaksCalculated();
                bool included = IsIncluded(halfLifeSettings, peptideFileAnalysis);
                SetIncluded(row, included);
                if (included)
                {
                    peptideFileAnalyses.Add(peptideFileAnalysis);
                }
                row.Cells[colPeptide.Index].Value = peptideFileAnalysis.Peptide.Sequence;
                row.Cells[colFile.Index].Value = peptideFileAnalysis.MsDataFile.Label;
                row.Cells[colStatus.Index].Value = peptideFileAnalysis.ValidationStatus;
                row.Cells[colTimePoint.Index].Value = peptideFileAnalysis.MsDataFile.TimePoint;
                row.Cells[colCohort.Index].Value = peptideFileAnalysis.MsDataFile.Cohort;
                if (peptideFileAnalysis.CalculatedPeaks == null)
                {
                    row.Cells[colTracerPercent.Index].Value = null;
                    row.Cells[colScore.Index].Value = null;
                    row.Cells[colTurnover.Index].Value = null;
                    row.Cells[colPrecursorPool.Index].Value = null;
                    row.Cells[colTurnoverScore.Index].Value = null;
                    row.Cells[colAuc.Index].Value = null;
                }
                else
                {
                    row.Cells[colTracerPercent.Index].Value = peptideFileAnalysis.CalculatedPeaks.TracerPercent;
                    row.Cells[colScore.Index].Value = peptideFileAnalysis.CalculatedPeaks.DeconvolutionScore;
                    row.Cells[colTurnover.Index].Value = peptideFileAnalysis.CalculatedPeaks.Turnover;
                    row.Cells[colPrecursorPool.Index].Value = peptideFileAnalysis.CalculatedPeaks.PrecursorEnrichment;
                    row.Cells[colTurnoverScore.Index].Value = peptideFileAnalysis.CalculatedPeaks.TurnoverScore;
                    row.Cells[colAuc.Index].Value = peptideFileAnalysis.CalculatedPeaks.AreaUnderCurve;
                }
                row.Cells[colSample.Index].Value = peptideFileAnalysis.MsDataFile.Sample;
            }
            HalfLifeCalculator.ResultData resultData;
            var halfLifeCalculator = UpdateGraph(peptideFileAnalyses, halfLifeSettings, out resultData);
            var allRowDatas = resultData.RowDatas.ToDictionary(rowData=>rowData.RawRowData.PeptideFileAnalysisId, rowData=>rowData);
            colTurnoverAvg.Visible = true;
            colPrecursorPoolAvg.Visible = true;
            colTurnoverScoreAvg.Visible = true;

            for (int iRow = 0; iRow < dataGridView1.Rows.Count; iRow++)
            {
                var row = dataGridView1.Rows[iRow];
                var peptideFileAnalysis = (PeptideFileAnalysis) row.Tag;
                HalfLifeCalculator.ProcessedRowData rowData;
                if (allRowDatas.TryGetValue(peptideFileAnalysis.Id, out rowData))
                {
                    SetBackColor(row, rowData.RejectReason == null ? Color.White : Color.LightGray);
                }
                else
                {
                    rowData = halfLifeCalculator.ToRowData(peptideFileAnalysis);
                }
                if (rowData != null)
                {
                    row.Cells[colTurnoverAvg.Index].Value = rowData.Turnover;
                    row.Cells[colPrecursorPoolAvg.Index].Value =
                        rowData.CurrentPrecursorPool;
                    row.Cells[colTurnoverScoreAvg.Index].Value = rowData.TurnoverScore;
                    row.Cells[colRejectReason.Index].Value = rowData.RejectReason;
                    if (rowData.EvviesFilterMin.HasValue || rowData.EvviesFilterMax.HasValue)
                    {
                        row.Cells[colEvviesFilter.Index].Value = "[" + rowData.EvviesFilterMin + "," + rowData.EvviesFilterMax + "]";
                    }
                }
                else
                {
                    row.Cells[colTurnoverAvg.Index].Value = null;
                    row.Cells[colPrecursorPoolAvg.Index].Value = null;
                    row.Cells[colTurnoverScoreAvg.Index].Value = null;
                    row.Cells[colRejectReason.Index].Value = null;
                    row.Cells[colEvviesFilter.Index].Value = null;
                }
            }
            _lastCalcedHalfLifeSettings = halfLifeSettings;
        }
        private HalfLifeCalculator UpdateGraph(List<PeptideFileAnalysis> peptideFileAnalyses, HalfLifeSettings halfLifeSettings, out HalfLifeCalculator.ResultData resultData)
        {
            var halfLifeCalculator = new HalfLifeCalculator(Workspace, halfLifeSettings);
            var halfLife = resultData = halfLifeCalculator.CalculateHalfLife(peptideFileAnalyses);
            _zedGraphControl.GraphPane.CurveList.Clear();
            _zedGraphControl.GraphPane.GraphObjList.Clear();
            _pointsCurve = null;
            _peptideFileAnalysisPoints = null;
            var xValues = new List<double>();
            var yValues = new List<double>();
            var fileAnalysisPoints = new List<PeptideFileAnalysis>();
            var filteredFileAnalysisIds =
                new HashSet<long>(resultData.FilteredRowDatas.Select(rd => rd.RawRowData.PeptideFileAnalysisId));
            foreach (var peptideFileAnalysis in peptideFileAnalyses)
            {
                if (!filteredFileAnalysisIds.Contains(peptideFileAnalysis.Id))
                {
                    continue;
                }
                double? value;
                var processedRowData = halfLifeCalculator.ToRowData(peptideFileAnalysis);
                if (!processedRowData.Turnover.HasValue)
                {
                    continue;
                }
                if (LogPlot)
                {
                    value = 2-Math.Log10(100 - processedRowData.Turnover.Value * 100);
                } 
                else
                {
                    value = processedRowData.Turnover.Value * 100;
                }
                if (double.IsInfinity(value.Value) || double.IsNaN(value.Value))
                {
                    continue;
                }
                Debug.Assert(peptideFileAnalysis.MsDataFile.TimePoint != null);
                xValues.Add(peptideFileAnalysis.MsDataFile.TimePoint.Value);
                yValues.Add(value.Value);
                fileAnalysisPoints.Add(peptideFileAnalysis);
            }
            UpdateStatsGrid(xValues, yValues);
            var pointsCurve = _zedGraphControl.GraphPane.AddCurve("Data Points", xValues.ToArray(), yValues.ToArray(), Color.Black);
            pointsCurve.Line.IsVisible = false;
            pointsCurve.Label.IsVisible = false;
            Func<double, double> funcMiddle = x => halfLife.YIntercept + halfLife.RateConstant * x;
            Func<double, double> funcMin = x => halfLife.YIntercept + (halfLife.RateConstant - halfLife.RateConstantError) * x;
            Func<double, double> funcMax = x => halfLife.YIntercept + (halfLife.RateConstant + halfLife.RateConstantError) * x;
            Func<double, double> funcConvertToDisplayedValue;
            if (LogPlot)
            {
                _zedGraphControl.GraphPane.YAxis.Title.Text = "-Log(100% - % Newly Synthesized)";
                funcConvertToDisplayedValue = x => -x / Math.Log(10);
            }
            else
            {
                _zedGraphControl.GraphPane.YAxis.Title.Text = "% Newly Synthesized";
                funcConvertToDisplayedValue = x => (1 - Math.Exp(x)) * 100;
            }
            // ReSharper disable ImplicitlyCapturedClosure
            AddFunction("Best Fit", x=>funcConvertToDisplayedValue(funcMiddle(x)), Color.Black);
            AddFunction("Minimum Bound", x=>funcConvertToDisplayedValue(funcMin(x)), Color.LightBlue);
            AddFunction("Maximum Bound", x=>funcConvertToDisplayedValue(funcMax(x)), Color.LightGreen);
            // ReSharper restore ImplicitlyCapturedClosure
            _zedGraphControl.GraphPane.AxisChange();
            _zedGraphControl.Invalidate();
            _pointsCurve = pointsCurve;
            _peptideFileAnalysisPoints = fileAnalysisPoints;
            tbxRateConstant.Text = resultData.RateConstant.ToString("0.##E0") + "+/-" +
                                   resultData.RateConstantError.ToString("0.##E0");
            tbxHalfLife.Text = resultData.HalfLife.ToString("0.##") + "(" + resultData.MinHalfLife.ToString("0.##") + "-" +
                               resultData.MaxHalfLife.ToString("0.##") + ")";
            if (resultData.RSquared.HasValue)
            {
                tbxCorrelationCoefficient.Text = Math.Sqrt(resultData.RSquared.Value).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                tbxCorrelationCoefficient.Text = "";
            }
            return halfLifeCalculator;
        }

        public static double InvertLogValue(double x)
        {
            return Math.Pow(10, -x);
        }

        public static double GetLogValue(double x)
        {
            return -Math.Log10(x);
        }

        private void UpdateStatsGrid(IList<double> xValues, IList<double> yValues)
        {
            var timeToValuesDict = new Dictionary<double, IList<double>>();
            for (var i = 0; i < xValues.Count(); i ++)
            {
                var time = xValues[i];
                var value = yValues[i];
                IList<double> values;
                if (!timeToValuesDict.TryGetValue(time, out values))
                {
                    values = new List<double>();
                    timeToValuesDict.Add(time, values);
                }
                values.Add(value);
            }
            var allTimePoints = new HashSet<double>(Workspace.MsDataFiles
                                            .Where(d => d.TimePoint.HasValue)
// ReSharper disable PossibleInvalidOperationException
                                            .Select(d => d.TimePoint.Value))
// ReSharper restore PossibleInvalidOperationException
                                            .ToArray();
            Array.Sort(allTimePoints);
            gridViewStats.Rows.Clear();
            if (allTimePoints.Length > 0)
            {
                gridViewStats.Rows.Add(allTimePoints.Length);
                for (int i = 0; i < allTimePoints.Length; i++)
                {
                    var row = gridViewStats.Rows[i];
                    var time = allTimePoints[i];
                    row.Cells[colStatsTime.Index].Value = time;
                    row.Cells[colStatsInclude.Index].Value = !IsTimePointExcluded(time);
                    IList<double> values;
                    if (timeToValuesDict.TryGetValue(time, out values))
                    {
                        var stats = new Statistics(values.ToArray());
                        row.Cells[colStatsMean.Index].Value = stats.Mean();
                        row.Cells[colStatsMedian.Index].Value = stats.Median();
                        row.Cells[colStatsStdDev.Index].Value = stats.StdDev();
                        row.Cells[colStatsPointCount.Index].Value = stats.Length;

                    }
                }
            }
        }

// ReSharper disable UnusedMethodReturnValue.Local
        private CurveItem AddFunction(string name, Func<double,double> func, Color color)
// ReSharper restore UnusedMethodReturnValue.Local
        {
            double minTime = 0;
            double maxTime = 0;
            foreach (var msDataFile in Workspace.MsDataFiles)
            {
                if (!msDataFile.TimePoint.HasValue)
                {
                    continue;
                }
                var timePoint = msDataFile.TimePoint.Value;
                minTime = Math.Min(minTime, timePoint);
                maxTime = Math.Max(maxTime, timePoint);
            }
            var xValues = new List<double>();
            var yValues = new List<double>();
            for (var time = minTime; time < maxTime + 1; time++)
            {
                xValues.Add(time);
                yValues.Add(func.Invoke(time));
            }
            var curve = _zedGraphControl.GraphPane.AddCurve(name, xValues.ToArray(), yValues.ToArray(), color, SymbolType.None);
            curve.Label.IsVisible = false;
            return curve;
        }

        private bool IsIncluded(HalfLifeSettings halfLifeSettings, PeptideFileAnalysis peptideFileAnalysis)
        {
            if (!string.IsNullOrEmpty(Cohort))
            {
                if (Cohort != HalfLifeCalculator.GetCohort(peptideFileAnalysis.MsDataFile, GetHalfLifeSettings().BySample))
                {
                    return false;
                }
            }
            if (peptideFileAnalysis.MsDataFile.TimePoint == null)
            {
                return false;
            }
            if (IsTimePointExcluded(peptideFileAnalysis.MsDataFile.TimePoint.Value))
            {
                return false;
            }
            if (halfLifeSettings.PrecursorPoolCalculation == PrecursorPoolCalculation.Individual)
            {
                if (null == peptideFileAnalysis.CalculatedPeaks || !peptideFileAnalysis.CalculatedPeaks.Turnover.HasValue)
                {
                    return false;
                }
            }
            return true;
        }

        private void SetIncluded(DataGridViewRow row, bool included)
        {
            SetBackColor(row, included ? Color.White : Color.Gray);
        }

        private void SetBackColor(DataGridViewRow row, Color color)
        {
            for (int i = 0; i < row.Cells.Count; i++)
            {
                row.Cells[i].Style.BackColor = color;
            }
            
        }

        private void CbxLogPlotOnCheckedChanged(object sender, EventArgs e)
        {
            UpdateRows(true);
        }

        private void ComboCohortOnSelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateRows(true);
        }

        private void DataGridView1OnCellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }
            var row = dataGridView1.Rows[e.RowIndex];
            if (e.ColumnIndex == colPeptide.Index)
            {
                var peptideFileAnalysis = (PeptideFileAnalysis) row.Tag;
                PeptideFileAnalysisFrame.ShowFileAnalysisForm<TracerChromatogramForm>(peptideFileAnalysis);
            }
        }

        protected override void WorkspaceOnChange(object sender, WorkspaceChangeArgs args)
        {
            UpdateRows(true);
        }

        private void DataGridView1OnCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0)
            {
                return;
            }
            var row = dataGridView1.Rows[e.RowIndex];
            var peptideFileAnalysis = (PeptideFileAnalysis) row.Tag;
            var cell = row.Cells[e.ColumnIndex];
            if (e.ColumnIndex == colStatus.Index)
            {
                peptideFileAnalysis.ValidationStatus = (ValidationStatus) cell.Value;
            }
        }

        private bool IsTimePointExcluded(double timePoint)
        {
            return _excludedTimePoints.Contains(timePoint);
        }
        public void SetTimePointExcluded(double time, bool excluded)
        {
            if (_excludedTimePoints.Contains(time) == excluded)
            {
                return;
            }
            var excludedTimePoints = new HashSet<double>(_excludedTimePoints);
            if (excluded)
            {
                excludedTimePoints.Add(time);
            }
            else
            {
                excludedTimePoints.Remove(time);
            }
            _excludedTimePoints = excludedTimePoints;
            if (IsHandleCreated)
            {
                BeginInvoke(new Action<bool>(UpdateRows), true);
            }
        }

        private void GridViewStatsOnCellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            gridViewStats.EndEdit();
        }

        private void GridViewStatsOnCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            gridViewStats.EndEdit();
        }
        private void GridViewStatsOnCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var row = gridViewStats.Rows[e.RowIndex];
            var time = (double)row.Cells[colStatsTime.Index].Value;
            var excluded = !(bool)row.Cells[colStatsInclude.Index].Value;
            SetTimePointExcluded(time, excluded);
        }

        private void CbxBySampleOnCheckedChanged(object sender, EventArgs e)
        {
            Requery();
        }

        private void HalfLifeSettingsControlOnSettingsChange(object sender, EventArgs e)
        {
            UpdateRows(false);
        }
    }
}
