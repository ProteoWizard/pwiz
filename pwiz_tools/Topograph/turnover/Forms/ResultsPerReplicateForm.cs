using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;
using pwiz.Topograph.ui.Util;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class ResultsPerReplicateForm : WorkspaceForm
    {
        public ResultsPerReplicateForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            if (Workspace.MsDataFiles.ListChildren().Where(f=>f.TimePoint != null).Count() == 0)
            {
                colTimePoint.Visible = false;
            }
            if (Workspace.MsDataFiles.ListChildren().Where(f=>f.Cohort != null).Count() == 0)
            {
                colCohort.Visible = false;
            }
            if (Workspace.MsDataFiles.ListChildren().Where(f=>f.Sample != null).Count() == 0)
            {
                colSample.Visible = false;
            }
            if (Workspace.GetTracerDefs().Count == 0)
            {
                colIndPrecursorEnrichment.Visible = false;
                colIndTurnover.Visible = false;
                colIndTurnoverScore.Visible = false;
                colTracerPercent.Visible = false;
            }
            if (Workspace.GetTracerDefs().Count != 1)
            {
                colAvgPrecursorEnrichment.Visible = false;
                colAvgTurnover.Visible = false;
                colAvgTurnoverScore.Visible = false;
            }
            colTotalIonCurrent.Visible = false;
            for (int i = 0; i < dataGridView1.Columns.Count; i++)
            {
                var column = dataGridView1.Columns[i];
                checkedListBoxColumns.Items.Add(new ColumnListItem(column));
                checkedListBoxColumns.SetItemChecked(i, column.Visible);
            }
            checkedListBoxColumns.ItemCheck += checkedListBoxColumns_ItemCheck;
        }

        void checkedListBoxColumns_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            var item = checkedListBoxColumns.Items[e.Index] as ColumnListItem;
            if (item == null)
            {
                return;
            }
            item.Column.Visible = e.NewValue == CheckState.Checked;
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0)
            {
                return;
            }
            var row = dataGridView1.Rows[e.RowIndex];
            if (e.ColumnIndex == colPeptide.Index)
            {
                ShowPeptideFileAnalysis(row.Tag as long?);
            }
        }

        private PeptideFileAnalysisFrame ShowPeptideFileAnalysis(long? peptideFileAnalysisId)
        {
            return PeptideFileAnalysisFrame.ShowPeptideFileAnalysis(Workspace, peptideFileAnalysisId);
        }

        private class ColumnListItem
        {
            public ColumnListItem(DataGridViewColumn column)
            {
                Column = column;
            }
            public DataGridViewColumn Column { get; private set; }
            public override string ToString()
            {
                return Column.HeaderText;
            }
        }

        private void btnRequery_Click(object sender, EventArgs e)
        {
            var calculator = new HalfLifeCalculator(Workspace, HalfLifeCalculationType.GroupPrecursorPool)
            {
            };
            using (var longWaitDialog = new LongWaitDialog(this, "Calculating Half Lives"))
            {
                var longOperationBroker = new LongOperationBroker(calculator, longWaitDialog);
                if (!longOperationBroker.LaunchJob())
                {
                    return;
                }
            }
            UpdateRows(calculator);
        }

        private void UpdateRows(HalfLifeCalculator halfLifeCalculator)
        {
            dataGridView1.Rows.Clear();
            btnSave.Enabled = true;
            var rowDatas = halfLifeCalculator.RowDatas;
            if (rowDatas.Count == 0)
            {
                return;
            }
            dataGridView1.Rows.Add(rowDatas.Count);
            for (int iRow = 0; iRow < rowDatas.Count; iRow++)
            {
                var row = dataGridView1.Rows[iRow];
                var rowData = rowDatas[iRow];
                row.Tag = rowData.PeptideFileAnalysisId;
                row.Cells[colPeptide.Index].Value = rowData.Peptide.FullSequence;
                row.Cells[colProteinName.Index].Value = Workspace.GetProteinKey(rowData.Peptide.ProteinName,
                                                                          rowData.Peptide.ProteinDescription);
                row.Cells[colProteinDescription.Index].Value = rowData.Peptide.ProteinDescription;
                row.Cells[colSample.Index].Value = rowData.MsDataFile.Sample;
                row.Cells[colTimePoint.Index].Value = rowData.MsDataFile.TimePoint;
                row.Cells[colCohort.Index].Value = rowData.MsDataFile.Cohort;
                row.Cells[colDataFile.Index].Value = rowData.MsDataFile.Label;

                row.Cells[colAvgPrecursorEnrichment.Index].Value = rowData.AvgPrecursorEnrichment;
                row.Cells[colAvgTurnover.Index].Value = rowData.AvgTurnover;
                row.Cells[colAvgTurnoverScore.Index].Value = rowData.AvgTurnoverScore;
                row.Cells[colArea.Index].Value = rowData.AreaUnderCurve;
                row.Cells[colDeconvolutionScore.Index].Value = rowData.DeconvolutionScore;
                row.Cells[colIndPrecursorEnrichment.Index].Value = rowData.IndPrecursorEnrichment;
                row.Cells[colIndTurnover.Index].Value = rowData.IndTurnover;
                row.Cells[colIndTurnoverScore.Index].Value = rowData.IndTurnoverScore;
                row.Cells[colTracerPercent.Index].Value = rowData.TracerPercent;
                row.Cells[colStatus.Index].Value = rowData.ValidationStatus;
                row.Cells[colTotalIonCurrent.Index].Value =
                    rowData.MsDataFile.MsDataFileData.GetTotalIonCurrent(rowData.StartTime.Value, rowData.EndTime.Value);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            GridUtil.ExportResults(dataGridView1, Path.GetFileNameWithoutExtension(Workspace.DatabasePath) + "ResultsPerReplicate");
        }

        private void dataGridView1_RowHeaderMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            var row = dataGridView1.Rows[e.RowIndex];
            ShowPeptideFileAnalysis(row.Tag as long?);
        }
    }
}
