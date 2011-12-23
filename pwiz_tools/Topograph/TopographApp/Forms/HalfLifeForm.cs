using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;
using pwiz.Topograph.ui.Controls;
using pwiz.Topograph.Util;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class HalfLifeForm : WorkspaceForm
    {
        private readonly List<PeptideAnalysis> _peptideAnalyses = new List<PeptideAnalysis>();
        private WorkspaceVersion _workspaceVersion;
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
            _zedGraphControl.MouseDownEvent += new ZedGraphControl.ZedMouseEventHandler(_zedGraphControl_MouseDownEvent);
            splitContainer1.Panel2.Controls.Add(_zedGraphControl);
            var tracerDef = Workspace.GetTracerDefs()[0];
            tbxInitialPercent.Text = tracerDef.InitialApe.ToString();
            tbxFinalPercent.Text = tracerDef.FinalApe.ToString();
            tbxMinScore.Text = workspace.GetAcceptMinDeconvolutionScore().ToString();
            tbxMinAuc.Text = workspace.GetAcceptMinAreaUnderChromatogramCurve().ToString();
            colTurnover.DefaultCellStyle.Format = "#.##%";
            colPrecursorPool.DefaultCellStyle.Format = "#.##%";
            comboCalculationType.SelectedIndex = 0;
            foreach (var evviesFilter in Enum.GetValues(typeof(EvviesFilterEnum)))
            {
                comboEvviesFilter.Items.Add(evviesFilter);
            }
            comboEvviesFilter.SelectedIndex = 0;
        }

        bool _zedGraphControl_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
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

        public bool BySample
        {
            get
            {
                return cbxBySample.Checked;
            }
            set
            {
                cbxBySample.Checked = value;
                UpdateRows();
            }
        }
        public double MinScore
        {
            get
            {
                return ParseDouble(tbxMinScore.Text);
            }
            set
            {
                tbxMinScore.Text = value.ToString();
                Requery();
            }
        }
        public double MinAuc
        {
            get { return ParseDouble(tbxMinAuc.Text); }
            set 
            { 
                tbxMinAuc.Text = value.ToString();
                Requery();
            }
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
                UpdateRows();
            }
        }
        public TracerDef TracerDef
        {
            get
            {
                return Workspace.GetTracerDefs()[0];
            }
        }
        public double InitialPercent
        {
            get
            {
                return ParseDouble(tbxInitialPercent.Text);
            }
            set
            {
                tbxInitialPercent.Text = value.ToString();
            }
        }
        public double FinalPercent
        {
            get
            {
                return ParseDouble(tbxFinalPercent.Text);
            }
            set
            {
                tbxFinalPercent.Text = value.ToString();
            }
        }
        public bool FixedInitialPercent
        {
            get
            {
                return cbxFixedInitialPercent.Checked;
            }
            set
            {
                cbxFixedInitialPercent.Checked = value;
            }
        }

        private static double ParseDouble(String value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }
            try
            {
                return double.Parse(value);
            }
            catch
            {
                return 0;
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
            var peptideAnalysisIds = new HashSet<long>();
            using (var session = Workspace.OpenSession())
            {
                IQuery query;
                if (string.IsNullOrEmpty(Peptide))
                {
                    query = session.CreateQuery("FROM " + typeof(DbPeptideAnalysis) +
                                                " T WHERE T.Peptide.Protein = :protein")
                        .SetParameter("protein", ProteinName);
                }
                else
                {
                    query = session.CreateQuery("FROM " + typeof(DbPeptideAnalysis) +
                                                " T WHERE T.Peptide.Sequence = :sequence")
                        .SetParameter("sequence", Peptide);
                }
                foreach (DbPeptideAnalysis dbPeptideAnalysis in query.List())
                {
                    peptideAnalysisIds.Add(dbPeptideAnalysis.Id.Value);
                }
            }
            var peptideAnalyses = TurnoverForm.Instance.LoadPeptideAnalyses(peptideAnalysisIds);
            ClearPeptideAnalyses();
            foreach (var peptideAnalysis in peptideAnalyses.Values)
            {
                peptideAnalysis.IncChromatogramRefCount();
            }
            _peptideAnalyses.AddRange(peptideAnalyses.Values);
        }

        private void UpdateCohortCombo()
        {
            var cohortSet = new HashSet<String> {""};
            foreach (var msDataFile in Workspace.MsDataFiles.ListChildren())
            {
                cohortSet.Add(HalfLifeCalculator.GetCohort(msDataFile, BySample));
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
            _workspaceVersion = Workspace.WorkspaceVersion;
            dataGridView1.Rows.Clear();
            RequeryPeptideAnalyses();
            using (Workspace.GetReadLock())
            {
                foreach (var peptideAnalysis in _peptideAnalyses)
                {
                    tbxProteinDescription.Text = peptideAnalysis.Peptide.ProteinDescription;
                    foreach (var peptideFileAnalysis in peptideAnalysis.GetFileAnalyses(false))
                    {
                        var row = dataGridView1.Rows[dataGridView1.Rows.Add()];
                        row.Tag = peptideFileAnalysis;
                    }
                }
                UpdateRows();
            }
        }

        private void UpdateRows()
        {
            if (!IsHandleCreated)
            {
                return;
            }
            colSample.Visible = BySample;
            using (Workspace.GetReadLock())
            {
                var peptideFileAnalyses = new List<PeptideFileAnalysis>();
                for (int iRow = 0; iRow < dataGridView1.Rows.Count; iRow++)
                {
                    var row = dataGridView1.Rows[iRow];
                    var peptideFileAnalysis = (PeptideFileAnalysis) row.Tag;
                    bool included = IsIncluded(peptideFileAnalysis);
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
                    row.Cells[colTracerPercent.Index].Value = peptideFileAnalysis.Peaks.TracerPercent;
                    row.Cells[colScore.Index].Value = peptideFileAnalysis.Peaks.DeconvolutionScore;
                    row.Cells[colTurnover.Index].Value = peptideFileAnalysis.Peaks.Turnover;
                    row.Cells[colPrecursorPool.Index].Value = peptideFileAnalysis.Peaks.PrecursorEnrichment;
                    row.Cells[colTurnoverScore.Index].Value = peptideFileAnalysis.Peaks.TurnoverScore;
                    row.Cells[colSample.Index].Value = peptideFileAnalysis.MsDataFile.Sample;
                    row.Cells[colAuc.Index].Value = peptideFileAnalysis.Peaks.AreaUnderCurve;
                }
                HalfLifeCalculator.ResultData resultData;
                var halfLifeCalculator = UpdateGraph(peptideFileAnalyses, out resultData);
                var allFileAnalysisIds = new HashSet<long>(resultData.RowDatas.Select(rd => rd.PeptideFileAnalysisId));
                var filteredFileAnalysisIds =
                    new HashSet<long>(resultData.FilteredRowDatas.Select(rd => rd.PeptideFileAnalysisId));
                if (HalfLifeCalculationType == HalfLifeCalculationType.GroupPrecursorPool || HalfLifeCalculationType == HalfLifeCalculationType.OldGroupPrecursorPool)
                {
                    colTurnoverAvg.Visible = true;
                    colPrecursorPoolAvg.Visible = true;
                    colTurnoverScoreAvg.Visible = HalfLifeCalculationType != HalfLifeCalculationType.OldGroupPrecursorPool;
                    for (int iRow = 0; iRow < dataGridView1.Rows.Count; iRow++)
                    {
                        var row = dataGridView1.Rows[iRow];
                        var peptideFileAnalysis = (PeptideFileAnalysis) row.Tag;
                        var rowData = halfLifeCalculator.ToRowData(peptideFileAnalysis);
                        if (rowData != null)
                        {
                            row.Cells[colTurnoverAvg.Index].Value = rowData.AvgTurnover;
                            row.Cells[colPrecursorPoolAvg.Index].Value =
                                rowData.AvgPrecursorEnrichment;
                            row.Cells[colTurnoverScoreAvg.Index].Value = rowData.AvgTurnoverScore;
                        }
                        else
                        {
                            row.Cells[colTurnoverAvg.Index].Value = null;
                            row.Cells[colPrecursorPoolAvg.Index].Value = null;
                            row.Cells[colTurnoverScoreAvg.Index].Value = null;
                        }
                        if (allFileAnalysisIds.Contains(peptideFileAnalysis.Id.Value) && !filteredFileAnalysisIds.Contains(peptideFileAnalysis.Id.Value))
                        {
                            SetBackColor(row, Color.LightBlue);
                        }
                    }
                }
                else
                {
                    colTurnoverAvg.Visible = false;
                    colPrecursorPoolAvg.Visible = false;
                    colTurnoverScoreAvg.Visible = false;
                }
            }
        }
        private HalfLifeCalculator UpdateGraph(List<PeptideFileAnalysis> peptideFileAnalyses, out HalfLifeCalculator.ResultData resultData)
        {
            var halfLifeCalculator = new HalfLifeCalculator(Workspace, HalfLifeCalculationType)
                                         {
                                                 InitialPercent = InitialPercent,
                                                 FinalPercent = FinalPercent,
                                                 FixedInitialPercent = FixedInitialPercent,
                                                 EvviesFilter = EvviesFilter,
                                                 BySample = BySample,
                                            };
            var halfLife = resultData = halfLifeCalculator.CalculateHalfLife(peptideFileAnalyses);
            _zedGraphControl.GraphPane.CurveList.Clear();
            _zedGraphControl.GraphPane.GraphObjList.Clear();
            _pointsCurve = null;
            _peptideFileAnalysisPoints = null;
            var xValues = new List<double>();
            var yValues = new List<double>();
            var fileAnalysisPoints = new List<PeptideFileAnalysis>();
            var filteredFileAnalysisIds =
                new HashSet<long>(resultData.FilteredRowDatas.Select(rd => rd.PeptideFileAnalysisId));
            foreach (var peptideFileAnalysis in peptideFileAnalyses)
            {
                if (!filteredFileAnalysisIds.Contains(peptideFileAnalysis.Id.Value))
                {
                    continue;
                }
                double? value;
                if (LogPlot)
                {
                    value = halfLifeCalculator.GetLogValue(peptideFileAnalysis);
                } 
                else
                {
                    value = halfLifeCalculator.GetValue(peptideFileAnalysis);
                }
                if (!value.HasValue)
                {
                    continue;
                }
                if (double.IsInfinity(value.Value) || double.IsNaN(value.Value))
                {
                    continue;
                }
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
            if (LogPlot)
            {
                AddFunction("Best Fit", funcMiddle, Color.Black);
                AddFunction("Minimum Bound", funcMin, Color.LightBlue);
                AddFunction("Maximum Bound", funcMax, Color.LightGreen);
            }
            else
            {
                AddFunction("Best Fit", x => halfLifeCalculator.InvertLogValue(funcMiddle(x)), Color.Black);
                AddFunction("Minimum Bound", x => halfLifeCalculator.InvertLogValue(funcMin(x)), Color.LightBlue);
                AddFunction("Maximum Bound", x => halfLifeCalculator.InvertLogValue(funcMax(x)), Color.LightGreen);
            }
            if (LogPlot)
            {
                if (HalfLifeCalculationType == HalfLifeCalculationType.TracerPercent)
                {
                    _zedGraphControl.GraphPane.YAxis.Title.Text = "Log ((Tracer % - Final %)/(Initial % - Final %))";
                }
                else
                {
                    _zedGraphControl.GraphPane.YAxis.Title.Text = "Log (100% - % newly synthesized)";
                }
            }
            else
            {
                if (HalfLifeCalculationType == HalfLifeCalculationType.TracerPercent)
                {
                    _zedGraphControl.GraphPane.YAxis.Title.Text = "Tracer %";
                }
                else
                {
                    _zedGraphControl.GraphPane.YAxis.Title.Text = "% newly synthesized";
                }
            }
            _zedGraphControl.GraphPane.XAxis.IsAxisSegmentVisible = !LogPlot;
            _zedGraphControl.GraphPane.AxisChange();
            _zedGraphControl.Invalidate();
            _pointsCurve = pointsCurve;
            _peptideFileAnalysisPoints = fileAnalysisPoints;
            tbxRateConstant.Text = resultData.RateConstant.ToString("0.##E0") + "+/-" +
                                   resultData.RateConstantError.ToString("0.##E0");
            tbxHalfLife.Text = resultData.HalfLife.ToString("0.##") + "(" + resultData.MinHalfLife.ToString("0.##") + "-" +
                               resultData.MaxHalfLife.ToString("0.##") + ")";
            return halfLifeCalculator;
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
            var allTimePoints = new HashSet<double>(Workspace.MsDataFiles.ListChildren()
                                            .Where(d => d.TimePoint.HasValue)
                                            .Select(d => d.TimePoint.Value))
                                            .ToArray();
            Array.Sort(allTimePoints);
            gridViewStats.Rows.Clear();
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
                }
            }
        }

        private CurveItem AddFunction(string name, Func<double,double> func, Color color)
        {
            double minTime = 0;
            double maxTime = 0;
            foreach (var msDataFile in Workspace.MsDataFiles.ListChildren())
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

        private bool IsIncluded(PeptideFileAnalysis peptideFileAnalysis)
        {
            if (!string.IsNullOrEmpty(Cohort))
            {
                if (Cohort != HalfLifeCalculator.GetCohort(peptideFileAnalysis.MsDataFile, BySample))
                {
                    return false;
                }
            }
            if (peptideFileAnalysis.ValidationStatus == ValidationStatus.reject)
            {
                return false;
            }
            if (peptideFileAnalysis.MsDataFile.TimePoint == null)
            {
                return false;
            }
            if (IsTimePointExcluded(peptideFileAnalysis.MsDataFile.TimePoint.Value))
            {
                return false;
            }
            if (!peptideFileAnalysis.Peaks.DeconvolutionScore.HasValue || peptideFileAnalysis.Peaks.DeconvolutionScore < MinScore)
            {
                return false;
            }
            if (MinAuc > 0 && peptideFileAnalysis.Peaks.AreaUnderCurve < MinAuc)
            {
                return false;
            }
            if (HalfLifeCalculationType == HalfLifeCalculationType.IndividualPrecursorPool)
            {
                if (!peptideFileAnalysis.Peaks.Turnover.HasValue)
                {
                    return false;
                }
            }
            return true;
        }

        private void SetIncluded(DataGridViewRow row, bool included)
        {
            SetBackColor(row, included ? Color.White : Color.LightGray);
        }

        private void SetBackColor(DataGridViewRow row, Color color)
        {
            for (int i = 0; i < row.Cells.Count; i++)
            {
                row.Cells[i].Style.BackColor = color;
            }
            
        }

        private void cbxLogPlot_CheckedChanged(object sender, EventArgs e)
        {
            UpdateRows();
        }

        private void tbxMinScore_TextChanged(object sender, EventArgs e)
        {
            UpdateRows();
        }

        private void comboCohort_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateRows();
        }

        private void tbxInitialPercent_TextChanged(object sender, EventArgs e)
        {
            UpdateRows();
        }

        private void tbxFinalPercent_TextChanged(object sender, EventArgs e)
        {
            UpdateRows();
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
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

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            if (Workspace.WorkspaceVersion != _workspaceVersion)
            {
                Requery();
                return;
            }
            UpdateRows();
        }

        private void dataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e)
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
                using (Workspace.GetWriteLock())
                {
                    peptideFileAnalysis.ValidationStatus = (ValidationStatus) cell.Value;
                }
            }
        }

        private void cbxFixedInitialPercent_CheckedChanged(object sender, EventArgs e)
        {
            UpdateRows();
        }

        private void comboCalculationType_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (HalfLifeCalculationType)
            {
                default:
                    tbxInitialPercent.Enabled = false;
                    tbxFinalPercent.Enabled = false;
                    break;
                case HalfLifeCalculationType.TracerPercent:
                    tbxInitialPercent.Enabled = true;
                    tbxFinalPercent.Enabled = true;
                    break;
            }
            UpdateRows();
        }

        public HalfLifeCalculationType HalfLifeCalculationType
        {
            get
            {
                return (HalfLifeCalculationType) comboCalculationType.SelectedIndex;
            }
            set
            {
                comboCalculationType.SelectedIndex = (int) value;
            }
        }

        public EvviesFilterEnum EvviesFilter
        {
            get
            {
                return (EvviesFilterEnum) comboEvviesFilter.SelectedIndex;
            }
            set
            {
                comboEvviesFilter.SelectedIndex = (int) value;
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
                BeginInvoke(new Action(UpdateRows));
            }
        }

        private void gridViewStats_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            gridViewStats.EndEdit();
        }

        private void gridViewStats_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            gridViewStats.EndEdit();
        }
        private void gridViewStats_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var row = gridViewStats.Rows[e.RowIndex];
            var time = (double)row.Cells[colStatsTime.Index].Value;
            var excluded = !(bool)row.Cells[colStatsInclude.Index].Value;
            SetTimePointExcluded(time, excluded);
        }

        private void cbxBySample_CheckedChanged(object sender, EventArgs e)
        {
            Requery();
        }

        private void comboEvviesFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateRows();
        }

        private void tbxMinAuc_TextChanged(object sender, EventArgs e)
        {
            UpdateRows();
        }
    }
}
