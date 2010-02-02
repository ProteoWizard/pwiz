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
using pwiz.Topograph.Util;
using pwiz.Topograph.ui.Controls;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PeptideAnalysesForm : WorkspaceForm
    {
        private const string Title = "Peptide Analyses";
        private bool _initialQueryCompleted;
        private readonly Dictionary<long, DataGridViewRow> _peptideAnalysisRows = new Dictionary<long, DataGridViewRow>();
        private readonly object _requeryLock = new object();

        private HashSet<long> _peptideAnalysisIdsToRequery;

        private WorkspaceVersion _workspaceVersion;

        public PeptideAnalysesForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            TabText = Name = Title + " (loading)";
            deleteMenuItem.Click += _deleteAnalysesMenuItem_Click;
            
            colMinScoreTracerCount.DefaultCellStyle.Format = "0.####";
            colMaxScoreTracerCount.DefaultCellStyle.Format = "0.####";
            colMinScorePrecursorEnrichment.DefaultCellStyle.Format = "0.####";
            colMaxScorePrecursorEnrichment.DefaultCellStyle.Format = "0.####";
        }

        void _deleteAnalysesMenuItem_Click(object sender, EventArgs e)
        {
            var peptideAnalysisIds = new List<long>();
            foreach (DataGridViewRow row in dataGridView.SelectedRows)
            {
                peptideAnalysisIds.Add((long)row.Tag);
            }
            if (peptideAnalysisIds.Count == 0)
            {
                if (dataGridView.CurrentRow != null)
                {
                    peptideAnalysisIds.Add((long)dataGridView.CurrentRow.Tag);
                }
            }
            if (peptideAnalysisIds.Count == 0)
            {
                MessageBox.Show("No peptide analyses are selected", Program.AppName, MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                return;
            }
            String message;
            if (peptideAnalysisIds.Count == 1)
            {
                using (var session = Workspace.OpenSession())
                {
                    var peptideAnalysis = session.Get<DbPeptideAnalysis>(peptideAnalysisIds[0]);
                    message = "Are you sure you want to delete the analysis of the peptide '" +
                              peptideAnalysis.Peptide.Sequence + "'?";
                }
            }
            else
            {
                message = "Are you sure you want to delete these " + peptideAnalysisIds.Count + " peptide analyses?";
            }
            if (MessageBox.Show(message, Program.AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }
            using (var session = Workspace.OpenWriteSession())
            {
                session.BeginTransaction();
                foreach (var id in peptideAnalysisIds)
                {
                    var peptideAnalysis = session.Get<DbPeptideAnalysis>(id);
                    if (peptideAnalysis == null)
                    {
                        continue;
                    }
                    session.Delete(peptideAnalysis);
                    session.Save(new DbChangeLog(Workspace, peptideAnalysis));
                }
                session.Transaction.Commit();
            }
            foreach (var id in peptideAnalysisIds)
            {
                var peptideAnalysis = Workspace.PeptideAnalyses.GetChild(id);
                if (peptideAnalysis != null)
                {
                    var frame = Program.FindOpenEntityForm<PeptideAnalysisFrame>(peptideAnalysis);
                    if (frame != null)
                    {
                        frame.Close();
                    }
                }
            }
            foreach (var id in peptideAnalysisIds)
            {
                Workspace.PeptideAnalyses.RemoveChild(id);
                dataGridView.Rows.Remove(_peptideAnalysisRows[id]);
                _peptideAnalysisRows.Remove(id);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            new Action(RequeryPeptideAnalyses).BeginInvoke(null, null);
        }

        private void AddPeptideAnalysesToRequery(ICollection<long> ids)
        {
            if (ids.Count == 0)
            {
                return;
            }
            lock(this)
            {
                if (_peptideAnalysisIdsToRequery == null)
                {
                    _peptideAnalysisIdsToRequery = new HashSet<long>();
                    new Action(RequeryPeptideAnalyses).BeginInvoke(null, null);
                }
                _peptideAnalysisIdsToRequery.UnionWith(ids);
            }
        }

        private void RequeryPeptideAnalyses()
        {
            lock(_requeryLock)
            {
                ICollection<long> idsToRequery;
                String idList = null;
                lock (this)
                {
                    if (_initialQueryCompleted)
                    {
                        idsToRequery = _peptideAnalysisIdsToRequery;
                        _peptideAnalysisIdsToRequery = null;
                        if (idsToRequery == null || idsToRequery.Count == 0)
                        {
                            return;
                        }
                        idList = "(" + Lists.Join(idsToRequery, ",") + ")";
                    }
                    else
                    {
                        idsToRequery = null;
                    }
                    _initialQueryCompleted = true;
                }

                var peptideAnalysisRows = new Dictionary<long, PeptideAnalysisRow>();
                using (var session = Workspace.OpenSession())
                {
                    String hql = "SELECT pa.Id, pa.Peptide.Protein, pa.Peptide.FullSequence, pa.Peptide.ValidationStatus, pa.Note, pa.Peptide.ProteinDescription "
                                 + "\nFROM " + typeof(DbPeptideAnalysis) + " pa";

                    if (idsToRequery != null)
                    {
                        hql += "\nWHERE pa.Id IN " + idList;
                    }
                    var query = session.CreateQuery(hql);
                    foreach (object[] rowData in query.List())
                    {
                        PeptideAnalysisRow peptideAnalysisRow;
                        var id = (long)rowData[0];
                        if (!peptideAnalysisRows.TryGetValue(id, out peptideAnalysisRow))
                        {
                            peptideAnalysisRow = new PeptideAnalysisRow { Id = id };
                            peptideAnalysisRows.Add(id, peptideAnalysisRow);
                        }
                        peptideAnalysisRow.Protein = (string)rowData[1];
                        peptideAnalysisRow.Peptide = (string)rowData[2];
                        peptideAnalysisRow.ValidationStatus = (ValidationStatus)rowData[3];
                        peptideAnalysisRow.Note = (string)rowData[4];
                        peptideAnalysisRow.ProteinDescription = (string)rowData[5];
                        peptideAnalysisRow.MaxTracers =
                            Workspace.GetMaxTracerCount(Peptide.TrimSequence(peptideAnalysisRow.Peptide));
                    }
                    var hql2 = "SELECT pd.PeptideFileAnalysis.PeptideAnalysis.Id, pd.PeptideQuantity, Min(pd.Score), Max(pd.Score) "
                               + "\nfrom " + typeof(DbPeptideDistribution) +
                               " pd ";
                    if (idsToRequery != null)
                    {
                        hql2 += "\nWHERE pd.PeptideFileAnalysis.PeptideAnalysis.Id IN " + idList;
                    }
                    hql2 += "\nGROUP BY pd.PeptideFileAnalysis.PeptideAnalysis.Id, pd.PeptideQuantity";
                    var query2 = session.CreateQuery(hql2);
                    foreach (object[] rowData in query2.List())
                    {
                        PeptideAnalysisRow peptideAnalysisRow;
                        var id = (long)rowData[0];
                        if (!peptideAnalysisRows.TryGetValue(id, out peptideAnalysisRow))
                        {
                            continue;
                        }
                        var peptideQuantity = (PeptideQuantity)rowData[1];
                        if (peptideQuantity == PeptideQuantity.tracer_count)
                        {
                            peptideAnalysisRow.MinScoreTracerAmounts = (double?)rowData[2];
                            peptideAnalysisRow.MaxScoreTracerAmounts = (double?)rowData[3];
                        }
                        else
                        {
                            peptideAnalysisRow.MinScorePrecursorEnrichments = (double?)rowData[2];
                            peptideAnalysisRow.MaxScorePrecursorEnrichments = (double?)rowData[3];
                        }
                    }
                }
                if (idsToRequery != null)
                {
                    foreach (var id in idsToRequery)
                    {
                        if (!peptideAnalysisRows.ContainsKey(id))
                        {
                            peptideAnalysisRows.Add(id, null);
                        }
                    }
                }
                BeginInvoke(new Action<Dictionary<long, PeptideAnalysisRow>>(UpdateRows), peptideAnalysisRows);
            }
        }

        private void UpdateRows(Dictionary<long, PeptideAnalysisRow> rows)
        {
            Text = TabText = Title;
            if (rows.Count == 0)
            {
                return;
            }
            try
            {
                dataGridView.SuspendLayout();
                foreach (var entry in rows)
                {
                    DataGridViewRow row;
                    _peptideAnalysisRows.TryGetValue(entry.Key, out row);
                    if (entry.Value == null)
                    {
                        if (row != null)
                        {
                            dataGridView.Rows.Remove(row);
                            _peptideAnalysisRows.Remove(entry.Key);
                        }
                        continue;
                    }
                    if (row == null)
                    {
                        row = dataGridView.Rows[dataGridView.Rows.Add()];
                        _peptideAnalysisRows.Add(entry.Key, row);
                        row.Tag = entry.Value.Id;
                    }
                    row.Cells[colProtein.Name].Value = entry.Value.Protein;
                    row.Cells[colPeptide.Name].Value = entry.Value.Peptide;
                    row.Cells[colStatus.Name].Value = entry.Value.ValidationStatus;
                    row.Cells[colNote.Name].Value = entry.Value.Note;
                    row.Cells[colProteinDescription.Name].Value = entry.Value.ProteinDescription;
                    row.Cells[colMaxTracers.Index].Value = entry.Value.MaxTracers;
                    row.Cells[colMinScoreTracerCount.Index].Value = entry.Value.MinScoreTracerAmounts;
                    row.Cells[colMaxScoreTracerCount.Index].Value = entry.Value.MaxScoreTracerAmounts;
                    row.Cells[colMinScorePrecursorEnrichment.Index].Value = entry.Value.MinScorePrecursorEnrichments;
                    row.Cells[colMaxScorePrecursorEnrichment.Index].Value = entry.Value.MaxScorePrecursorEnrichments;
                }
            }
            finally
            {
                dataGridView.ResumeLayout();
            }
        }

        private void UpdateColumnVisibility()
        {
            var defTracerCount = Workspace.GetDefaultPeptideQuantity() == PeptideQuantity.tracer_count;
            colMinScoreTracerCount.Visible = defTracerCount;
            colMaxScoreTracerCount.Visible = defTracerCount;
            colMinScorePrecursorEnrichment.Visible = !defTracerCount;
            colMaxScorePrecursorEnrichment.Visible = !defTracerCount;
        }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            base.OnWorkspaceEntitiesChanged(args);
            if (!Workspace.WorkspaceVersion.Equals(_workspaceVersion))
            {
                UpdateColumnVisibility();
            }
            AddPeptideAnalysesToRequery(args.GetChangedPeptideAnalyses().Keys);
        }

        private void dataGridView_RowHeaderMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var peptideAnalysis = TurnoverForm.Instance.LoadPeptideAnalysis((long) dataGridView.Rows[e.RowIndex].Tag);
            if (peptideAnalysis == null)
            {
                return;
            }
            OpenPeptideAnalysis(peptideAnalysis);
        }

        private PeptideAnalysisFrame OpenPeptideAnalysis(PeptideAnalysis peptideAnalysis)
        {
            return PeptideAnalysisFrame.ShowPeptideAnalysis(peptideAnalysis);
        }

        private void dataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var column = dataGridView.Columns[e.ColumnIndex];
            var row = dataGridView.Rows[e.RowIndex];
            var cell = row.Cells[e.ColumnIndex];
            var peptideAnalysisId = (long)row.Tag;
            var peptideAnalysis = TurnoverForm.Instance.LoadPeptideAnalysis(peptideAnalysisId);
            if (peptideAnalysis == null)
            {
                return;
            }
            using (Workspace.GetWriteLock())
            {
                if (column == colNote)
                {
                    peptideAnalysis.Note = Convert.ToString(cell.Value);
                }
                else if (column == colStatus)
                {
                    peptideAnalysis.ValidationStatus = (ValidationStatus)cell.Value;
                }
            }
        }

        private void btnAnalyzePeptides_Click(object sender, EventArgs e)
        {
            new AnalyzePeptidesForm(Workspace).Show(this);
        }

        private void dataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }
            var column = dataGridView.Columns[e.ColumnIndex];
            var row = dataGridView.Rows[e.RowIndex];
            var peptideAnalysis = TurnoverForm.Instance.LoadPeptideAnalysis((long) row.Tag);
            if (peptideAnalysis == null)
            {
                return;
            }

            PeptideQuantity? peptideQuantity = null;
            if (column == colMinScoreTracerCount || column == colMaxScoreTracerCount)
            {
                peptideQuantity = PeptideQuantity.tracer_count;
            }
            if (column == colMinScorePrecursorEnrichment || column == colMaxScorePrecursorEnrichment)
            {
                peptideQuantity = PeptideQuantity.precursor_enrichment;
            }
            if (column != colPeptide && peptideQuantity == null)
            {
                return;
            }
            var form = OpenPeptideAnalysis(peptideAnalysis);
            if (column == colMinScoreTracerCount || column == colMaxScoreTracerCount 
                || column == colMinScorePrecursorEnrichment || column == colMaxScorePrecursorEnrichment)
            {
                bool max = column == colMaxScoreTracerCount || column == colMaxScorePrecursorEnrichment;
                PeptideDistribution peptideDistribution = null;
                foreach (var peptideFileAnalysis in peptideAnalysis.FileAnalyses.ListPeptideFileAnalyses(true))
                {
                    foreach (var pd in peptideFileAnalysis.PeptideDistributions.ListChildren())
                    {
                        if (pd.PeptideQuantity != peptideQuantity)
                        {
                            continue;
                        }
                        if (peptideDistribution == null)
                        {
                            peptideDistribution = pd;
                        }
                        else if (max)
                        {
                            if (pd.Score > peptideDistribution.Score)
                            {
                                peptideDistribution = pd;
                            }
                        }
                        else
                        {
                            if (pd.Score < peptideDistribution.Score)
                            {
                                peptideDistribution = pd;
                            }
                        }
                    }
                }
                if (peptideDistribution == null)
                {
                    return;
                }
                if (peptideDistribution.PeptideQuantity == PeptideQuantity.tracer_count)
                {
                    PeptideFileAnalysisFrame.ActivatePeptideDataForm<TracerAmountsForm>(form.PeptideAnalysisSummary, peptideDistribution.PeptideFileAnalysis);
                }
                else
                {
                    PeptideFileAnalysisFrame.ActivatePeptideDataForm<PrecursorEnrichmentsForm>(form.PeptideAnalysisSummary, peptideDistribution.PeptideFileAnalysis);
                }
                return;
            }
        }

        class PeptideAnalysisRow
        {
            public long Id;
            public String Peptide;
            public ValidationStatus ValidationStatus;
            public String Note;
            public String Protein;
            public String ProteinDescription;
            public int MaxTracers;
            public double? MinScoreTracerAmounts;
            public double? MaxScoreTracerAmounts;
            public double? MinScorePrecursorEnrichments;
            public double? MaxScorePrecursorEnrichments;
        }
    }
}
