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
using System.Data.Common;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PeptidesForm : WorkspaceForm
    {
        private readonly Dictionary<Peptide, DataGridViewRow> peptideRows 
            = new Dictionary<Peptide, DataGridViewRow>();
        public PeptidesForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            TabText = Name = "Peptides";
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Requery();
        }

        private void Requery()
        {
            tbxMinTracerCount.Text = Workspace.GetMinTracerCount().ToString();
            tbxExcludeAas.Text = Workspace.GetExcludeAas();
            dataGridView.Rows.Clear();
            peptideRows.Clear();
            AddAndUpdateRows(Workspace.Peptides.ListChildren());
        }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            base.OnWorkspaceEntitiesChanged(args);
            btnAnalyzePeptides.Enabled = Workspace.Peptides.GetChildCount() > 0;
            var newPeptides = new List<Peptide>();
            foreach (var peptide in args.GetEntities<Peptide>())
            {
                DataGridViewRow row;
                peptideRows.TryGetValue(peptide, out row);
                if (args.IsRemoved(peptide))
                {
                    if (row != null)
                    {
                        dataGridView.Rows.Remove(row);
                        peptideRows.Remove(peptide);
                    }
                }
                else
                {
                    if (row == null)
                    {
                        newPeptides.Add(peptide);
                        continue;
                    }
                    UpdateRow(row);
                }
            }

            foreach (var row in AddRows(newPeptides))
            {
                UpdateRow(row);
            }
        }

        private IList<DataGridViewRow> AddRows(IEnumerable<Peptide> peptides)
        {
            var rows = new List<DataGridViewRow>();
            foreach (var peptide in Workspace.FilterPeptides(peptides))
            {
                if (peptideRows.ContainsKey(peptide))
                {
                    continue;
                }
                var row = new DataGridViewRow();
                row.Tag = peptide;
                rows.Add(row);
                peptideRows.Add(peptide, row);
            }
            dataGridView.Rows.AddRange(rows.ToArray());
            return rows;
        }

        private void AddAndUpdateRows(IEnumerable<Peptide> peptides)
        {
            foreach(var row in AddRows(peptides))
            {
                UpdateRow(row);
            }
        }

        private void UpdateRow(DataGridViewRow row)
        {
            var peptide = (Peptide) row.Tag;
            row.Cells[colSequence.Name].Value = peptide.FullSequence;
            row.Cells[colProtein.Name].Value = peptide.ProteinName;
            row.Cells[colProteinDescription.Name].Value = peptide.ProteinDescription;
            row.Cells[colMaxTracerCount.Name].Value = peptide.MaxTracerCount;
            row.Cells[colSearchResultCount.Name].Value = peptide.SearchResultCount;
            // TODO
            //row.Cells[colValidationStatus.Name].
        }

        private void dataGridView_RowHeaderMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var row = dataGridView.Rows[e.RowIndex];
            var peptide = (Peptide) row.Tag;
            PeptideAnalysis peptideAnalysis = peptide.EnsurePeptideAnalysis();
            if (peptideAnalysis == null)
            {
                MessageBox.Show(
                    "It is not possible to analyze this peptide because there are no search results for this peptide",
                    Program.AppName);
                return;
            }
            PeptideAnalysisFrame.ShowPeptideAnalysis(peptideAnalysis);
        }

        private void tbxMinTracerCount_Leave(object sender, EventArgs e)
        {
            int minTracerCount = Convert.ToInt32(tbxMinTracerCount.Text);
            if (minTracerCount == Workspace.GetMinTracerCount())
            {
                return;
            }
            Workspace.SetMinTracerCount(minTracerCount);
            Requery();
        }

        private void tbxExcludeAas_Leave(object sender, EventArgs e)
        {
            var excludeAas = tbxExcludeAas.Text;
            if (excludeAas == Workspace.GetExcludeAas())
            {
                return;
            }
            Workspace.SetExcludeAas(excludeAas);
            Requery();
        }

        private void btnAnalyzePeptides_Click(object sender, EventArgs e)
        {
            var oldCount = Workspace.PeptideAnalyses.ChildCount;
            new AnalyzePeptidesForm(Workspace).Show(this);
            if (oldCount == Workspace.PeptideAnalyses.ChildCount)
            {
                return;
            }
            var peptideAnalysesForm = Program.FindOpenForm<PeptideAnalysesForm>();
            if (peptideAnalysesForm != null)
            {
                peptideAnalysesForm.Activate();
                return;
            }
            peptideAnalysesForm = new PeptideAnalysesForm(Workspace);
            peptideAnalysesForm.Show(DockPanel, DockState.Document);
        }

        private void btnAddSearchResults_Click(object sender, EventArgs e)
        {
            new AddSearchResultsForm(Workspace).Show(this);
        }
    }
}
