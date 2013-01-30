/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Windows.Forms;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class DataFileSummary : BasePeptideAnalysesForm
    {
        private Dictionary<long, DataGridViewRow> _rows 
            = new Dictionary<long, DataGridViewRow>();

        public DataFileSummary(MsDataFile msDataFile) : base(msDataFile.Workspace)
        {
            MsDataFile = msDataFile;
            InitializeComponent();
        }

        public MsDataFile MsDataFile
        {
            get; private set;
        }

        protected override void Requery(ISession session, ICollection<long> peptideAnalysisIds)
        {
            var query = session.CreateQuery("SELECT F.Id"
                + "\n,F.PeptideAnalysis.Id" 
                + "\n,F.PeptideAnalysis.Peptide.FullSequence"
                + "\n,F.TracerPercent"
                + "\n,F.DeconvolutionScore"
                + "\n,F.ValidationStatus"
                + "\nFROM " + typeof(DbPeptideFileAnalysis) + " F"
                + "\nWHERE F.MsDataFile.Id = " + MsDataFile.Id
                );
            var rowDatas = new Dictionary<long, RowData>();
            foreach (object[] row in query.List()) 
            {
                var rowData = new RowData
                {
                    PeptideFileAnalysisId = Convert.ToInt32(row[0]),
                    PeptideAnalysisId = Convert.ToInt32(row[1]),
                    Sequence = Convert.ToString(row[2]),
                    TracerPercent = row[3] == null ? (double?) null : Convert.ToDouble(row[3]),
                    DeconvolutionScore = row[4] == null ? (double?) null : Convert.ToDouble(row[4]),
                    Status = (ValidationStatus)row[5],
                };
                rowDatas.Add(rowData.PeptideFileAnalysisId, rowData);
            }
            BeginInvoke(new Action<ICollection<RowData>>(AddRows), rowDatas.Values);
            var query2 = session.CreateQuery("SELECT P.PeptideFileAnalysis.Id"
                                             + "\n,MIN(P.StartTime)"
                                             + "\n,MAX(P.EndTime)"
                                             + "\nFROM " + typeof (DbPeak) + " P"
                                             + "\nWHERE P.PeptideFileAnalysis.MsDataFile.Id = " + MsDataFile.Id
                                             + "\nGROUP BY P.PeptideFileAnalysis.Id");
            foreach (object[] row in query2.List())
            {
                RowData rowData;
                if (!rowDatas.TryGetValue(Convert.ToInt32(row[0]), out rowData))
                {
                    continue;
                }
                rowData.PeakStart = Convert.ToDouble(row[1]);
                rowData.PeakEnd = Convert.ToDouble(row[2]);
            }
            BeginInvoke(new Action<ICollection<RowData>>(AddRows), rowDatas.Values);
        }

        private void AddRows(ICollection<RowData> rowDatas)
        {
            var newRows = new List<DataGridViewRow>();
            foreach (var rowData in rowDatas)
            {
                if (_rows.ContainsKey(rowData.PeptideFileAnalysisId))
                {
                    continue;
                }
                var row = new DataGridViewRow
                              {
                                  Tag = rowData,
                              };
                _rows.Add(rowData.PeptideFileAnalysisId, row);
                newRows.Add(row);
            }
            if (newRows.Count > 0)
            {
                dataGridView.Rows.AddRange(newRows.ToArray());
            }
            foreach (var rowData in rowDatas)
            {
                DataGridViewRow row;
                if (!_rows.TryGetValue(rowData.PeptideFileAnalysisId, out row))
                {
                    continue;
                }
                PeptideFileAnalysis peptideFileAnalysis = null;
                var peptideAnalysis = Workspace.PeptideAnalyses.FindByKey(rowData.PeptideAnalysisId);
                if (peptideAnalysis != null)
                {
                    peptideFileAnalysis = peptideAnalysis.GetFileAnalysis(rowData.PeptideFileAnalysisId);
                }
                if (peptideFileAnalysis != null)
                {
                    row.Cells[colApe.Index].Value = peptideFileAnalysis.CalculatedPeaks.TracerPercent;
                    row.Cells[colPeakStart.Index].Value = peptideFileAnalysis.PeakStartTime;
                    row.Cells[colPeakEnd.Index].Value = peptideFileAnalysis.PeakEndTime;
                    row.Cells[colSequence.Index].Value = peptideFileAnalysis.Peptide.FullSequence;
                    row.Cells[colStatus.Index].Value = peptideFileAnalysis.ValidationStatus;
                    row.Cells[colTurnover.Index].Value = peptideFileAnalysis.CalculatedPeaks.Turnover;
                }
                else
                {
                    row.Cells[colApe.Index].Value = rowData.TracerPercent;
                    row.Cells[colPeakStart.Index].Value = rowData.PeakStart;
                    row.Cells[colPeakEnd.Index].Value = rowData.PeakEnd;
                    row.Cells[colSequence.Index].Value = rowData.Sequence;
                    row.Cells[colStatus.Index].Value = rowData.Status;
                    //row.Cells[colScore.Index].Value = rowData.DeconvolutionScore;
                }
            }
        }

        private void BtnCreateFileAnalysesOnClick(object sender, EventArgs e)
        {
            // TODO(nicksh)
        }

        private void ShowPeptideFileAnalysisForm<T>(long peptideFileAnalysisId) where T:PeptideFileAnalysisForm
        {
            var frame = PeptideFileAnalysisFrame.ShowPeptideFileAnalysis(Workspace, peptideFileAnalysisId);
            if (frame != null)
            {
                frame.ShowForm<T>();
            }
        }

        private void DataGridViewOnCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var row = dataGridView.Rows[e.RowIndex];
            var rowData = (RowData) row.Tag;
            var column = dataGridView.Columns[e.ColumnIndex];
            var cell = row.Cells[e.ColumnIndex];
            if (column == colStatus)
            {
                var peptideAnalysis = TopographForm.Instance.LoadPeptideAnalysis(rowData.PeptideAnalysisId);
                if (peptideAnalysis == null)
                {
                    return;
                }
                var peptideFileAnalysis = peptideAnalysis.GetFileAnalysis(rowData.PeptideFileAnalysisId);
                if (peptideFileAnalysis != null)
                {
                    peptideFileAnalysis.ValidationStatus = (ValidationStatus) cell.Value;
                }
            }
        }
        class RowData
        {
            public long PeptideFileAnalysisId { get; set;}
            public long PeptideAnalysisId { get; set; }
            public string Sequence { get; set;}
            public double? PeakStart { get; set; }
            public double? PeakEnd { get; set; }
            public double? TracerPercent { get; set; }
            public double? DeconvolutionScore { get; set; }
            public ValidationStatus Status { get; set; }
        }

        private void DataGridViewOnCellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            RowData rowData = (RowData)dataGridView.Rows[e.RowIndex].Tag;
            if (e.ColumnIndex == colSequence.Index)
            {
                ShowPeptideFileAnalysisForm<TracerChromatogramForm>(rowData.PeptideFileAnalysisId);
            }
        }
    }
}
