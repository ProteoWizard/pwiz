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
using pwiz.Topograph.ui.Controls;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PeptideAnalysesForm : WorkspaceForm
    {
        private readonly Dictionary<long, DataGridViewRow> _peptideAnalysisRows
            = new Dictionary<long, DataGridViewRow>();

        public PeptideAnalysesForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            TabText = Name = "Peptide Analyses";
            colHalfLifePrecursorEnrichment.Tag = PeptideQuantity.precursor_enrichment;
            colHalfLifeTracerCount.Tag = PeptideQuantity.tracer_count;
            deleteMenuItem.Click += _deleteAnalysesMenuItem_Click;
        }

        void _deleteAnalysesMenuItem_Click(object sender, EventArgs e)
        {
            var peptideAnalyses = new List<PeptideAnalysis>();
            foreach (DataGridViewRow row in dataGridView.SelectedRows)
            {
                peptideAnalyses.Add((PeptideAnalysis)row.Tag);
            }
            if (peptideAnalyses.Count == 0)
            {
                if (dataGridView.CurrentRow != null)
                {
                    peptideAnalyses.Add((PeptideAnalysis)dataGridView.CurrentRow.Tag);
                }
            }
            if (peptideAnalyses.Count == 0)
            {
                MessageBox.Show("No peptide analyses are selected", Program.AppName, MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                return;
            }
            String message;
            if (peptideAnalyses.Count == 1)
            {
                message = "Are you sure you want to delete the analysis of the peptide '" +
                          peptideAnalyses[0].Peptide.Sequence + "'?";
            }
            else
            {
                message = "Are you sure you want to delete these " + peptideAnalyses.Count + " peptide analyses?";
            }
            if (MessageBox.Show(message, Program.AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }
            foreach (var peptideAnalysis in peptideAnalyses)
            {
                Workspace.PeptideAnalyses.RemoveChild(peptideAnalysis.Id.Value);
            }
        }


        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Requery();
        }


        private void AddAndUpdateRows(ICollection<PeptideAnalysis> peptideAnalyses)
        {
            foreach (var entry in AddRows(peptideAnalyses))
            {
                UpdateRow(entry.Value, entry.Key);
            }
        }

        protected void Requery()
        {
            dataGridView.Rows.Clear();
            _peptideAnalysisRows.Clear();
            using (var session = Workspace.OpenSession())
            {
                String hql = "SELECT pa.Id, pa.Peptide.Protein, pa.Peptide.FullSequence, pa.Peptide.ValidationStatus, pa.Note, pa.Peptide.ProteinDescription, pa.Peptide.MaxTracerCount "
                             + "\nFROM " + typeof (DbPeptideAnalysis) + " pa";
                var query = session.CreateQuery(hql);
                var rowDatas = query.List();
                if (rowDatas.Count == 0)
                {
                    return;
                }
                dataGridView.Rows.Add(rowDatas.Count);
                for (int i = 0; i < rowDatas.Count; i ++)
                {
                    var rowData = (object[]) rowDatas[i];
                    var row = dataGridView.Rows[i];
                    row.Tag = rowData[0];
                    _peptideAnalysisRows.Add((long) rowData[0], row);
                    row.Cells[colProtein.Index].Value = rowData[1];
                    row.Cells[colPeptide.Index].Value = rowData[2];
                    row.Cells[colStatus.Index].Value = Convert.ChangeType(rowData[3], typeof(ValidationStatus));
                    row.Cells[colNote.Index].Value = rowData[4];
                    row.Cells[colProteinDescription.Index].Value = rowData[5];
                    row.Cells[colMaxTracers.Index].Value = rowData[6];
                }
                var query2 =
                    session.CreateQuery("SELECT pr.PeptideAnalysis.Id, pr.PeptideQuantity, pr.HalfLife, pr.IsComplete FROM " +
                                        typeof (DbPeptideRate)
                                        + " pr WHERE pr.Cohort = ''");
                foreach (object[] rowData in query2.List())
                {
                    DataGridViewRow row;
                    if (!_peptideAnalysisRows.TryGetValue((long) rowData[0], out row))
                    {
                        continue;
                    }
                    DisplayHalfLife(row, (PeptideQuantity) rowData[1], (double?) rowData[2], (bool) rowData[3]);
                }
            }
        }

        private void UpdateRow(DataGridViewRow row, PeptideAnalysis peptideAnalysis)
        {
            row.Cells[colProtein.Name].Value = peptideAnalysis.Peptide.ProteinName;
            row.Cells[colPeptide.Name].Value = peptideAnalysis.Peptide.FullSequence;
            row.Cells[colStatus.Name].Value = peptideAnalysis.ValidationStatus;
            row.Cells[colNote.Name].Value = peptideAnalysis.Note;
            row.Cells[colProteinDescription.Name].Value = peptideAnalysis.Peptide.ProteinDescription;
            row.Cells[colMaxTracers.Index].Value = peptideAnalysis.Peptide.MaxTracerCount;
            DisplayHalfLife(row, PeptideQuantity.precursor_enrichment, peptideAnalysis);
            DisplayHalfLife(row, PeptideQuantity.tracer_count, peptideAnalysis);
        }

        private void DisplayHalfLife(DataGridViewRow row, PeptideQuantity peptideQuantity, PeptideAnalysis peptideAnalysis)
        {
            var rate = peptideAnalysis.PeptideRates.GetChild(new RateKey(peptideQuantity, null));
            if (rate == null)
            {
                DisplayHalfLife(row, peptideQuantity, null, true);
            }
            else
            {
                DisplayHalfLife(row, peptideQuantity, rate.HalfLife, rate.IsComplete);
            }
        }

        private void DisplayHalfLife(DataGridViewRow row, PeptideQuantity peptideQuantity, double? halfLife, bool isComplete)
        {
            var cell = row.Cells[peptideQuantity == PeptideQuantity.tracer_count ? colHalfLifeTracerCount.Index : colHalfLifePrecursorEnrichment.Index];
            cell.Value = halfLife;
            cell.Style.ForeColor = isComplete ? Color.Black : Color.Gray;
        }

        private IDictionary<PeptideAnalysis, DataGridViewRow> AddRows(ICollection<PeptideAnalysis> peptideAnalyses)
        {
            var result = new Dictionary<PeptideAnalysis, DataGridViewRow>();
            foreach (var peptideAnalysis in peptideAnalyses)
            {
                var row = new DataGridViewRow {Tag = peptideAnalysis.Id};
                row.Tag = peptideAnalysis.Id;
                _peptideAnalysisRows.Add(peptideAnalysis.Id.Value, row);
                result.Add(peptideAnalysis, row);
            }
            dataGridView.Rows.AddRange(result.Values.ToArray());
            return result;
        }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            base.OnWorkspaceEntitiesChanged(args);
            var peptideAnalyses = new HashSet<PeptideAnalysis>();
            foreach (var peptideAnalysis in args.GetEntities<PeptideAnalysis>())
            {
                DataGridViewRow row;
                _peptideAnalysisRows.TryGetValue(peptideAnalysis.Id.Value, out row);
                if (args.IsRemoved(peptideAnalysis))
                {
                    if (row != null)
                    {
                        dataGridView.Rows.Remove(row);
                        _peptideAnalysisRows.Remove(peptideAnalysis.Id.Value);
                    }
                }
                else
                {
                    if (row == null)
                    {
                        AddRows(new[] {peptideAnalysis});
                    }
                    peptideAnalyses.Add(peptideAnalysis);
                }
            }
            foreach (var entity in args.GetEntities<PeptideRates>())
            {
                peptideAnalyses.Add(entity.PeptideAnalysis);
            }
            foreach (var peptideAnalysis in peptideAnalyses)
            {
                DataGridViewRow row;
                if (_peptideAnalysisRows.TryGetValue(peptideAnalysis.Id.Value, out row))
                {
                    UpdateRow(row, peptideAnalysis);
                }
            }
        }

        private void dataGridView_RowHeaderMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            using (var session = Workspace.OpenSession())
            {
                var id = (long) dataGridView.Rows[e.RowIndex].Tag;
                OpenPeptideAnalysis(
                    Workspace.PeptideAnalyses.GetChild(id, session));
            }
        }

        private void OpenPeptideAnalysis(PeptideAnalysis peptideAnalysis)
        {
            var form = Program.FindOpenEntityForm<PeptideAnalysisFrame>(peptideAnalysis);
            if (form != null)
            {
                form.Activate();
                return;
            }
            new PeptideAnalysisFrame(peptideAnalysis).Show(DockPanel, DockState);
        }

        private void dataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var column = dataGridView.Columns[e.ColumnIndex];
            var row = dataGridView.Rows[e.RowIndex];
            var peptideAnalysis = (PeptideAnalysis) row.Tag;
            var cell = row.Cells[e.ColumnIndex];
            if (column == colNote)
            {
                peptideAnalysis.Note = Convert.ToString(cell.Value);
            }
            else if (column == colStatus)
            {
                peptideAnalysis.ValidationStatus = (ValidationStatus) cell.Value;
            }
        }

        private void btnAnalyzePeptides_Click(object sender, EventArgs e)
        {
            new AnalyzePeptidesForm(Workspace).ShowDialog(this);
        }
    }
}
