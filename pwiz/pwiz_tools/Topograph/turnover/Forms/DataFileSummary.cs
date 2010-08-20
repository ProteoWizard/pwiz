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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class DataFileSummary : EntityModelForm
    {
        private Dictionary<PeptideFileAnalysis, DataGridViewRow> _rows 
            = new Dictionary<PeptideFileAnalysis, DataGridViewRow>();
        public DataFileSummary(MsDataFile msDataFile) : base(msDataFile)
        {
            InitializeComponent();
        }

        public MsDataFile MsDataFile
        {
            get
            {
                return (MsDataFile) EntityModel;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Requery();
        }

        private void Requery()
        {
            TabText = MsDataFile.Label;
            _rows.Clear();
            dataGridView.Rows.Clear();
            foreach (var row in AddRows(MsDataFile.GetFileAnalyses()))
            {
                UpdateRow(row);
            }
        }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            base.OnWorkspaceEntitiesChanged(args);
            foreach (var peptideFileAnalysis in args.GetEntities<PeptideFileAnalysis>())
            {
                if (!Equals(peptideFileAnalysis.MsDataFile, MsDataFile))
                {
                    continue;
                }
                DataGridViewRow row;
                _rows.TryGetValue(peptideFileAnalysis, out row);
                if (args.IsRemoved(peptideFileAnalysis))
                {
                    if (row != null)
                    {
                        dataGridView.Rows.Remove(row);
                        _rows.Remove(peptideFileAnalysis);
                    }
                    continue;
                } 
                if (row == null)
                {
                    row = AddRows(new[] {peptideFileAnalysis})[0];
                }
                UpdateRow(row);
            }
        }

        private void UpdateRow(DataGridViewRow row)
        {
            var peptideFileAnalysis = (PeptideFileAnalysis) row.Tag;
            row.Cells[colStatus.Index].Value = peptideFileAnalysis.ValidationStatus;
            row.Cells[colSequence.Index].Value = peptideFileAnalysis.Sequence;
            row.Cells[colPeakStart.Index].Value = peptideFileAnalysis.PeakStart;
            row.Cells[colPeakEnd.Index].Value = peptideFileAnalysis.PeakEnd;
            var precursorEnrichments = peptideFileAnalysis.PeptideDistributions.GetChild(PeptideQuantity.precursor_enrichment);
            var tracerAmounts = peptideFileAnalysis.PeptideDistributions.GetChild(PeptideQuantity.tracer_count);
            if (precursorEnrichments != null)
            {
                row.Cells[colTurnover.Index].Value = 100.0 - precursorEnrichments.GetChild("").PercentAmountValue;
            }
            else
            {
                row.Cells[colTurnover.Index].Value = null;
            }
            if (tracerAmounts != null)
            {
                row.Cells[colApe.Index].Value = tracerAmounts.TracerPercent; 
            }
            else
            {
                row.Cells[colApe.Index].Value = null;
            }
        }

        private IList<DataGridViewRow> AddRows(ICollection<PeptideFileAnalysis> peptideFileAnalyses)
        {
            var result = new List<DataGridViewRow>();
            foreach (var peptideFileAnalysis in peptideFileAnalyses)
            {
                if (_rows.ContainsKey(peptideFileAnalysis))
                {
                    continue;
                }
                var row = new DataGridViewRow
                              {
                                  Tag = peptideFileAnalysis
                              };
                _rows.Add(peptideFileAnalysis, row);
                result.Add(row);
            }
            dataGridView.Rows.AddRange(result.ToArray());
            return result;
        }

        private void btnCreateFileAnalyses_Click(object sender, EventArgs e)
        {
            
        }

        private void ShowPeptideFileAnalysisForm<T>(PeptideFileAnalysis peptideFileAnalysis) where T:PeptideFileAnalysisForm
        {
            PeptideFileAnalysisFrame.ShowFileAnalysisForm<T>(peptideFileAnalysis);
        }

        private void dataGridView_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var peptideFileAnalysis = (PeptideFileAnalysis) dataGridView.Rows[e.RowIndex].Tag;
            if (e.ColumnIndex < 0)
            {
                ShowPeptideFileAnalysisForm<AbstractChromatogramForm>(peptideFileAnalysis);
                return;
            }
            var column = dataGridView.Columns[e.ColumnIndex];
            if (column == colPeakStart || column == colPeakEnd)
            {
                ShowPeptideFileAnalysisForm<AbstractChromatogramForm>(peptideFileAnalysis);
            } 
            else if (column == colTurnover)
            {
                ShowPeptideFileAnalysisForm<PrecursorEnrichmentsForm>(peptideFileAnalysis);
            }
            else if (column == colApe)
            {
                ShowPeptideFileAnalysisForm<TracerAmountsForm>(peptideFileAnalysis);
            }
            
        }

        private void dataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var row = dataGridView.Rows[e.RowIndex];
            var peptideFileAnalysis = (PeptideFileAnalysis) row.Tag;
            var column = dataGridView.Columns[e.ColumnIndex];
            var cell = row.Cells[e.ColumnIndex];
            if (column == colStatus)
            {
                peptideFileAnalysis.ValidationStatus = (ValidationStatus) cell.Value;
            }
        }

    }
}
