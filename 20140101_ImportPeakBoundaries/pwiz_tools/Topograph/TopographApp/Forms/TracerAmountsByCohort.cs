using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Util;
using pwiz.Topograph.Util;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.ui.Forms
{
    public partial class TracerAmountsByCohort : WorkspaceForm
    {
        private readonly Dictionary<CohortKey, Columns> _columnsDict = new Dictionary<CohortKey, Columns>();
        public TracerAmountsByCohort(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
        }



        struct CohortKey
        {
            public CohortKey(String cohort, double? timePoint, string sample) : this()
            {
                Cohort = cohort;
                TimePoint = timePoint;
                Sample = sample;
            }
            public String Cohort { get; private set; }
            public double? TimePoint { get; private set; }
            public string Sample { get; private set; }
        }

        class Columns
        {
            public DataColumns TracerAmountsByAreaColumns { get; set; }
            public DataColumns TracerAmountsBySlopeColumns { get; set; }
            public DataColumns PrecursorEnrichmentColumns { get; set; }
            public DataColumns TurnoverColumns { get; set; }
            public DataColumns AreaUnderCurveColumns { get; set; }
            public DataGridViewColumn ReplicateCountColumn { get; set; }
        }

        class DataColumns
        {
            public DataGridViewColumn ValueColumn { get; set; }
            public DataGridViewColumn StdErrColumn { get; set; }
            public DataGridViewColumn StdDevColumn { get; set; }
        }

        class RawRow
        {
            public long PeptideFileAnalysisId { get; set; }
            public long PeptideId { get; set; }
            public CohortKey CohortKey { get; set; }
            public double TracerPercent { get; set; }
            public double TotalArea { get; set; }
            public double RatioToBase { get; set; }
            public double? Turnover { get; set; }
            public double? PrecursorEnrichment { get; set; }
        }
        
        class RowData
        {
            public long PeptideId { get; set; }
            public CohortKey CohortKey { get; set; }
            public double TracerPercentByArea { get; set; }
            public double TracerPercentBySlope { get; set; }
            public double? Turnover { get; set; }
            public double? PrecursorEnrichment { get; set; }
            public double AreaUnderCurve { get; set; }
        }

        class ResultData
        {
            public Statistics TracerPercentByArea { get; set; }
            public Statistics TracerPercentBySlope { get; set; }
            public Statistics Turnover { get; set; }
            public Statistics PrecursorEnrichment { get; set; }
            public Statistics AreaUnderCurve { get; set; }
        }

        class ResultRow
        {
            public ResultRow()
            {
                ResultDatas = new Dictionary<CohortKey, ResultData>();
            }
            public string PeptideSequence { get; set; }
            public string ProteinName { get; set; }
            public string ProteinKey { get; set; }
            public string ProteinDescription { get; set; }
            public Dictionary<CohortKey, ResultData> ResultDatas { get; private set; }
        }

        private void btnRequery_Click(object sender, EventArgs e)
        {
            var lstResultData = new List<ResultData>();
            var activeCohortKeys = new HashSet<CohortKey>();
            using (var session = Workspace.OpenSession())
            {
                var query = session.CreateQuery("SELECT d.PeptideFileAnalysis.Id," 
                                                + "\nd.PeptideFileAnalysis.PeptideAnalysis.Peptide.Id,"
                                                + "\nd.PeptideFileAnalysis.MsDataFile.TimePoint,"
                                                + "\nd.PeptideFileAnalysis.MsDataFile.Cohort,"
                                                + "\nd.PeptideFileAnalysis.MsDataFile.Sample,"
                                                + "\nd.TracerPercent,"
                                                + "\nd.TotalArea,"
                                                + "\nd.RatioToBase,"
                                                + "\nd.PeptideFileAnalysis.Turnover * 100,"
                                                + "\nd.PeptideFileAnalysis.PrecursorEnrichment * 100"
                                                + "\nFROM DbPeak d"
                                                + "\nWHERE d.PeptideFileAnalysis.DeconvolutionScore > :minScore "
                                                + "\nAND d.PeptideFileAnalysis.ValidationStatus <> " + (int) ValidationStatus.reject);
                if (!string.IsNullOrEmpty(tbxMinScore.Text))
                {
                    query.SetParameter("minScore", double.Parse(tbxMinScore.Text));
                }
                else
                {
                    query.SetParameter("minScore", 0.0);
                }
                var rowDataArrays = new List<object[]>();
                using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Executing query"))
                {
                    var broker = new LongOperationBroker(b => query.List(rowDataArrays), 
                        longWaitDialog, session);
                    if (!broker.LaunchJob())
                    {
                        return;
                    }
                    
                }
                bool groupByCohort = cbxGroupByCohort.Checked;
                bool groupByTimePoint = cbxGroupByTimePoint.Checked;
                bool groupBySample = cbxGroupBySample.Checked;
                var rawRows = rowDataArrays.Select(r => new RawRow()
                                                             {
                                                                 PeptideFileAnalysisId =  (long) r[0],
                                                                 PeptideId = (long) r[1],
                                                                 CohortKey = new CohortKey(
                                                                     groupByCohort ? (string) r[3] : null, 
                                                                     groupByTimePoint ? (double?) r[2] : null, 
                                                                     groupBySample ? (string) r[4] : null),
                                                                 TracerPercent = (double) r[5],
                                                                 TotalArea = (double) r[6],
                                                                 RatioToBase = (double) r[7],
                                                                 Turnover = (double?) r[8],
                                                                 PrecursorEnrichment = (double?) r[9],
                                                             });
                var rowDatas = rawRows.GroupBy(r => r.PeptideFileAnalysisId, (id,peaks) => AggregatePeaks(id, peaks));
                var byProtein = cbxByProtein.Checked;
                var groupedRowDatas = new Dictionary<string, IList<RowData>>();
                foreach (var rowData in rowDatas)
                {
                    var peptide = Workspace.Peptides.GetChild(rowData.PeptideId);
                    if (peptide == null)
                    {
                        continue;
                    }
                    var key = byProtein ? peptide.ProteinName : peptide.Sequence;
                    IList<RowData> list;
                    if (!groupedRowDatas.TryGetValue(key, out list))
                    {
                        list = new List<RowData>();
                        groupedRowDatas.Add(key, list);
                    }
                    list.Add(rowData);
                }
                var resultRows = new List<ResultRow>();
                foreach (var entry in groupedRowDatas)
                {
                    resultRows.Add(GetResultRow(entry.Value, byProtein));
                }
                dataGridView1.Rows.Clear();
                if (resultRows.Count > 0)
                {
                    dataGridView1.Rows.Add(resultRows.Count());
                    for (int iRow = 0; iRow < resultRows.Count; iRow ++)
                    {
                        var resultRow = resultRows[iRow];
                        var row = dataGridView1.Rows[iRow];
                        row.Cells[colPeptide.Index].Value = resultRow.PeptideSequence;
                        row.Cells[colProteinName.Index].Value = resultRow.ProteinName;
                        row.Cells[colProteinKey.Index].Value = resultRow.ProteinKey;
                        row.Cells[colProteinDescription.Index].Value 
                            = row.Cells[colProteinDescription.Index].ToolTipText = resultRow.ProteinDescription;
                        foreach (var entry in resultRow.ResultDatas)
                        {
                            Columns columns;
                            if (!_columnsDict.TryGetValue(entry.Key, out columns))
                            {
                                string cohortName = entry.Key.Cohort ?? "";
                                if (entry.Key.TimePoint != null)
                                {
                                    cohortName += " " + entry.Key.TimePoint;
                                }
                                columns = AddColumns(string.IsNullOrEmpty(cohortName) ? "" : cohortName + " ");
                                _columnsDict.Add(entry.Key, columns);
                            }
                            row.Cells[columns.ReplicateCountColumn.Index].Value = entry.Value.TracerPercentByArea.Length;
                            SetColumnValues(columns.TracerAmountsByAreaColumns, row, entry.Value.TracerPercentByArea);
                            SetColumnValues(columns.TracerAmountsBySlopeColumns, row, entry.Value.TracerPercentBySlope);
                            SetColumnValues(columns.PrecursorEnrichmentColumns, row, entry.Value.PrecursorEnrichment);
                            SetColumnValues(columns.TurnoverColumns, row, entry.Value.Turnover);
                            SetColumnValues(columns.AreaUnderCurveColumns, row, entry.Value.AreaUnderCurve);
                            lstResultData.Add(entry.Value);
                            activeCohortKeys.Add(entry.Key);
                        }
                    }
                }
            }
            dataGridViewSummary.Rows.Clear();
            SetSummary("Tracer % (area)", lstResultData.Select(r=>r.TracerPercentByArea));
            SetSummary("Tracer % (slope)", lstResultData.Select(r => r.TracerPercentBySlope));
            SetSummary("Precursor Enrichment", lstResultData.Select(r => r.PrecursorEnrichment));
            SetSummary("Turnover", lstResultData.Select(r => r.Turnover));
            SetSummary("Area Under Curve", lstResultData.Select(r => r.AreaUnderCurve));
            foreach (var entry in _columnsDict.ToArray())
            {
                if (!activeCohortKeys.Contains(entry.Key))
                {
                    dataGridView1.Columns.Remove(entry.Value.ReplicateCountColumn);
                    RemoveDataColumns(entry.Value.TracerAmountsByAreaColumns);
                    RemoveDataColumns(entry.Value.TracerAmountsBySlopeColumns);
                    RemoveDataColumns(entry.Value.PrecursorEnrichmentColumns);
                    RemoveDataColumns(entry.Value.TurnoverColumns);
                    RemoveDataColumns(entry.Value.AreaUnderCurveColumns);
                    _columnsDict.Remove(entry.Key);
                }
            }
            UpdateColumnVisibility();
        }

        private void RemoveDataColumns(DataColumns dataColumns)
        {
            dataGridView1.Columns.Remove(dataColumns.ValueColumn);
            dataGridView1.Columns.Remove(dataColumns.StdDevColumn);
            dataGridView1.Columns.Remove(dataColumns.StdErrColumn);
        }

        private void UpdateColumnVisibility()
        {
            foreach (var columns in _columnsDict.Values)
            {
                columns.ReplicateCountColumn.Visible = cbxShowCount.Checked;
                SetDataColumnVisibility(columns.TracerAmountsByAreaColumns, cbxTracerPercentAreas.Checked);
                SetDataColumnVisibility(columns.TracerAmountsBySlopeColumns, cbxTracerPercentSlope.Checked);
                SetDataColumnVisibility(columns.PrecursorEnrichmentColumns, cbxShowPrecursorEnrichment.Checked);
                SetDataColumnVisibility(columns.TurnoverColumns, cbxShowTurnover.Checked);
                SetDataColumnVisibility(columns.AreaUnderCurveColumns, cbxAreaUnderCurve.Checked);
            }
        }

        private void SetDataColumnVisibility(DataColumns dataColumns, bool baseVisible)
        {
            dataColumns.ValueColumn.Visible = baseVisible;
            dataColumns.StdDevColumn.Visible = baseVisible && cbxShowStdDev.Checked;
            dataColumns.StdErrColumn.Visible = baseVisible && cbxShowStdErr.Checked;
        }

        private Columns AddColumns(String prefix)
        {
            var columns = new Columns();
            columns.TracerAmountsByAreaColumns = AddDataColumns(prefix, "Tracer % (area)");
            columns.TracerAmountsBySlopeColumns = AddDataColumns(prefix, "Tracer % (slope)");
            columns.PrecursorEnrichmentColumns = AddDataColumns(prefix, "Precursor Enrichment");
            columns.TurnoverColumns = AddDataColumns(prefix, "Turnover");
            columns.AreaUnderCurveColumns = AddDataColumns(prefix, "Area Under Curve");
            dataGridView1.Columns.Add(columns.ReplicateCountColumn = new DataGridViewTextBoxColumn
                                               {
                                                   HeaderText = prefix + "Count",
                                               });
            return columns;
        }

        private DataColumns AddDataColumns(String prefix, String valueName)
        {
            var dataColumns = new DataColumns()
                                  {
                                      ValueColumn = new DataGridViewTextBoxColumn
                                                        {
                                                            HeaderText =
                                                                prefix + valueName,
                                                            SortMode =
                                                                DataGridViewColumnSortMode.
                                                                Automatic,
                                                            DefaultCellStyle = {Format = "0.####"},
                                                        },
                                      StdDevColumn= new DataGridViewTextBoxColumn
                                      {
                                          HeaderText = prefix + "StdDev " + valueName,
                                          SortMode =
                                              DataGridViewColumnSortMode.Automatic,
                                          DefaultCellStyle = { Format = "0.####" },
                                      },
                                      StdErrColumn = new DataGridViewTextBoxColumn
                                      {
                                          HeaderText = prefix + "StdErr " + valueName,
                                          SortMode =
                                              DataGridViewColumnSortMode.Automatic,
                                          DefaultCellStyle = { Format = "0.####" },
                                      }, 
                                  };
            dataGridView1.Columns.Add(dataColumns.ValueColumn);
            dataGridView1.Columns.Add(dataColumns.StdDevColumn);
            dataGridView1.Columns.Add(dataColumns.StdErrColumn);
            return dataColumns;
        }

        private DataGridViewRow SetSummary(String quantity, IEnumerable<Statistics> lstStats)
        {
            var row = dataGridViewSummary.Rows[dataGridViewSummary.Rows.Add()];
            row.Cells[colSummaryQuantity.Index].Value = quantity;
            var lstValues = new List<double>();
            var lstStdErr = new List<double>();
            var lstStdDev = new List<double>();
            var totalCount = 0;
            foreach (var stats in lstStats)
            {
                totalCount += stats.Length;
                if (stats.Length > 0)
                {
                    lstValues.Add(stats.Mean());
                }
                if (stats.Length > 1)
                {
                    if (double.IsNaN(stats.StdDev()))
                    {
                        Console.Out.WriteLine(stats);
                    }
                    lstStdErr.Add(stats.StdErr());
                    lstStdDev.Add(stats.StdDev());
                }
            }
            row.Cells[colSummaryValueCount.Index].Value = totalCount;
            row.Cells[colSummaryStdDevStdErrCount.Index].Value = lstStdErr.Count;
            row.Cells[colSummaryAvgValue.Index].Value = new Statistics(lstValues.ToArray()).Mean();
            row.Cells[colSummaryMedianValue.Index].Value = new Statistics(lstValues.ToArray()).Median();
            row.Cells[colSummaryMeanStdDev.Index].Value = new Statistics(lstStdDev.ToArray()).Mean();
            row.Cells[colSummaryMeanStdErr.Index].Value = new Statistics(lstStdErr.ToArray()).Mean();
            return row;
        }

        private RowData AggregatePeaks(long id, IEnumerable<RawRow> rawRows)
        {
            double totalArea = 0;
            double totalTracerPercentArea = 0;
            double totalRatio = 0;
            double totalTracerPercentRatio = 0;
            RawRow first = null;
            foreach (var rawRow in rawRows)
            {
                first = first ?? rawRow;
                totalArea += rawRow.TotalArea;
                totalRatio += rawRow.RatioToBase;
                totalTracerPercentArea = rawRow.TotalArea*rawRow.TracerPercent;
                totalTracerPercentRatio = rawRow.RatioToBase*rawRow.TracerPercent;
            }
            if (first == null)
            {
                return null;
            }
            return new RowData
                       {
                           CohortKey = first.CohortKey,
                           PeptideId = first.PeptideId,
                           TracerPercentByArea = totalTracerPercentArea/totalArea,
                           TracerPercentBySlope = totalTracerPercentRatio/totalRatio,
                           Turnover = first.Turnover,
                           PrecursorEnrichment = first.PrecursorEnrichment,
                           AreaUnderCurve = totalArea,
                       };
        }

        private ResultRow GetResultRow(IEnumerable<RowData> rowDatas, bool byProtein)
        {
            ResultRow resultRow = null;
            var rowDatasByCohort = new Dictionary<CohortKey, IList<RowData>>();
            foreach (var rowData in rowDatas)
            {
                if (resultRow == null)
                {
                    var peptide = Workspace.Peptides.GetChild(rowData.PeptideId);
                    resultRow = new ResultRow
                                    {
                                        PeptideSequence = byProtein ? null : peptide.FullSequence,
                                        ProteinDescription = peptide.ProteinDescription,
                                        ProteinName = peptide.ProteinName,
                                        ProteinKey = peptide.GetProteinKey(),
                                    };
                }
                IList<RowData> list;
                if (!rowDatasByCohort.TryGetValue(rowData.CohortKey, out list))
                {
                    list = new List<RowData>();
                    rowDatasByCohort.Add(rowData.CohortKey, list);
                }
                list.Add(rowData);
            }
            foreach (var entry in rowDatasByCohort)
            {
                resultRow.ResultDatas.Add(entry.Key, GetResultData(entry.Value));
            }
            return resultRow;
        }

        private ResultData GetResultData(IList<RowData> rowDatas)
        {
            var statsArea = new Statistics(rowDatas.Select(r => r.TracerPercentByArea).ToArray());
            var statsSlope = new Statistics(rowDatas.Select(r => r.TracerPercentBySlope).ToArray());
            var statsTurnover =
                new Statistics(rowDatas.Where(r => r.Turnover.HasValue).Select(r => r.Turnover.Value).ToArray());
            var statsPrecursorEnrichment =
                new Statistics(rowDatas.Where(r => r.PrecursorEnrichment.HasValue).Select(r => r.PrecursorEnrichment.Value).ToArray());
            var statsAreaUnderCurve = new Statistics(rowDatas.Select(r => r.AreaUnderCurve).ToArray());

                

            return new ResultData
                       {
                           TracerPercentByArea = statsArea,
                           TracerPercentBySlope = statsSlope,
                           Turnover = statsTurnover,
                           PrecursorEnrichment = statsPrecursorEnrichment,
                           AreaUnderCurve = statsAreaUnderCurve,
                       };
        }

        private void SetColumnValues(DataColumns dataColumns, DataGridViewRow row, Statistics stats)
        {
            row.Cells[dataColumns.ValueColumn.Index].Value = stats.Length > 0 ? stats.Mean() : (double?) null;
            row.Cells[dataColumns.StdDevColumn.Index].Value = stats.Length > 1 ? stats.StdDev() : (double?) null;
            row.Cells[dataColumns.StdErrColumn.Index].Value = stats.Length > 1 ? stats.StdErr() : (double?) null;
        }



        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            var row = dataGridView1.Rows[e.RowIndex];
            if (e.ColumnIndex == colPeptide.Index)
            {
                var peptideSequence = row.Cells[e.ColumnIndex].Value as string;
                if (string.IsNullOrEmpty(peptideSequence))
                {
                    return;
                }
                using (var session = Workspace.OpenSession())
                {
                    var peptideAnalysisIds =
                        session.CreateQuery(
                            "SELECT pa.Id FROM DbPeptideAnalysis pa WHERE pa.Peptide.Sequence = :sequence")
                            .SetParameter("sequence", Peptide.TrimSequence(peptideSequence))
                            .List();
                    if (peptideAnalysisIds.Count >= 0)
                    {
                        PeptideAnalysisFrame.ShowPeptideAnalysis(
                            TurnoverForm.Instance.LoadPeptideAnalysis((long) peptideAnalysisIds[0]));
                    }
                }
                return;
            }
            if (e.ColumnIndex == colProteinKey.Index)
            {
                var halfLifeForm = new HalfLifeForm(Workspace)
                {
                    Peptide = Peptide.TrimSequence(Convert.ToString(row.Cells[colPeptide.Index].Value) ?? ""),
                    ProteinName = Convert.ToString(row.Cells[colProteinName.Index].Value),
                    MinScore = double.Parse(tbxMinScore.Text),
                };
                halfLifeForm.Show(DockPanel, DockState);
            }
        }

        private void cbx_ColumnVisibilityChanged(object sender, EventArgs e)
        {
            UpdateColumnVisibility();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            GridUtil.ExportResults(dataGridView1, "TracerAmounts");
        }
    }
}
