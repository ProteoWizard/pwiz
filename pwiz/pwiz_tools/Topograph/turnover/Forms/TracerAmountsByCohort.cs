using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Model;
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
            public CohortKey(String cohort, double? timePoint) : this()
            {
                Cohort = cohort;
                TimePoint = timePoint;
            }
            public String Cohort { get; private set; }
            public double? TimePoint { get; private set; }
        }

        class Columns
        {
            public DataGridViewColumn TracerAmountsColumn { get; set;}
            public DataGridViewColumn ErrorColumn { get; set;}
        }

        class RowData
        {
            public long PeptideId { get; set; }
            public double? TimePoint { get; set; }
            public String Cohort { get; set; }
            public double TracerPercent { get; set; }
        }

        class ResultData
        {
            public double TracerPercent { get; set; }
            public double TracerPercentError { get; set; }
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
            using (var session = Workspace.OpenSession())
            {
                var query = session.CreateQuery("SELECT d.PeptideFileAnalysis.PeptideAnalysis.Peptide.Id,"
                                                + "\nd.PeptideFileAnalysis.MsDataFile.TimePoint,"
                                                + "\nd.PeptideFileAnalysis.MsDataFile.Cohort,"
                                                + "\nd.TracerPercent"
                                                + "\nFROM DbPeptideDistribution d"
                                                + "\nWHERE d.Score > :minScore AND d.PeptideQuantity = 0 "
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
                var broker = new LongOperationBroker(b => query.List(rowDataArrays), 
                    new LongWaitDialog(this, "Executing query"), session);
                if (!broker.LaunchJob())
                {
                    return;
                }
                var rowDatas = rowDataArrays.Select(r => new RowData
                                                             {
                                                                 PeptideId = (long) r[0],
                                                                 TimePoint = (double?) r[1],
                                                                 Cohort = (string) r[2],
                                                                 TracerPercent = (double) r[3],
                                                             });
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
                            columns = new Columns()
                                          {
                                              TracerAmountsColumn = new DataGridViewTextBoxColumn
                                                                        {
                                                                            HeaderText = cohortName + " tracer %",
                                                                            SortMode =
                                                                                DataGridViewColumnSortMode.Automatic,
                                                                            DefaultCellStyle = {Format = "0.####"},
                                                                        },
                                              ErrorColumn = new DataGridViewTextBoxColumn
                                                                {
                                                                    HeaderText = cohortName + " error",
                                                                    SortMode = DataGridViewColumnSortMode.Automatic,
                                                                    DefaultCellStyle = {Format = "0.####"},
                                                                }
                                          };
                            dataGridView1.Columns.Add(columns.TracerAmountsColumn);
                            dataGridView1.Columns.Add(columns.ErrorColumn);
                            _columnsDict.Add(entry.Key, columns);
                        }
                        row.Cells[columns.TracerAmountsColumn.Index].Value = entry.Value.TracerPercent;
                        row.Cells[columns.ErrorColumn.Index].Value = entry.Value.TracerPercentError;
                    }
                }
            }
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
                var cohortKey = new CohortKey(rowData.Cohort, rowData.TimePoint);
                IList<RowData> list;
                if (!rowDatasByCohort.TryGetValue(cohortKey, out list))
                {
                    list = new List<RowData>();
                    rowDatasByCohort.Add(cohortKey, list);
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
            var statistics = new Statistics(rowDatas.Select(r => r.TracerPercent).ToArray());
            return new ResultData
                       {
                           TracerPercent = statistics.Mean(),
                           TracerPercentError = statistics.StdErr()
                       };
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
    }
}
