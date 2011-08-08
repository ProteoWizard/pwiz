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
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Common.DataBinding;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PeptidesForm : WorkspaceForm
    {
        private TopographViewContext _viewContext;
        private BindingList<Peptide> _peptides;
        public PeptidesForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            TabText = Name = "Peptides";
            btnAnalyzePeptides.Enabled = Workspace.Peptides.GetChildCount() > 0;
            var defaultColumns = new[]
                                     {
                                         new ColumnSpec().SetName("Sequence"),
                                         new ColumnSpec().SetName("ProteinName").SetCaption("Protein"),
                                         new ColumnSpec().SetName("ProteinDescription"),
                                         new ColumnSpec().SetName("MaxTracerCount").SetCaption("Max Tracers"),
                                         new ColumnSpec().SetName("SearchResultCount").SetCaption("# Data Files"),
                                     };
            var defaultViewSpec = new ViewSpec()
                .SetName("default")
                .SetColumns(defaultColumns);
            navBar1.ViewContext = _viewContext = new TopographViewContext(dataGridView, workspace, typeof(Peptide), new[] { defaultViewSpec });
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Requery();
        }

        private void Requery()
        {
            _peptides = new BindingList<Peptide>(Workspace.Peptides.ListChildren());
            peptidesBindingSource.DataSource = new BindingListView(new ViewInfo(_viewContext.ParentColumn, _viewContext.BuiltInViewSpecs.First()), _peptides);
        }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            foreach (var peptide in args.GetNewEntities().OfType<Peptide>())
            {
                _peptides.Add(peptide);
            }
        }

        private void dataGridView_RowHeaderMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var peptide = (Peptide) _peptides[e.RowIndex];
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

        private void btnAnalyzePeptides_Click(object sender, EventArgs e)
        {
            var oldCount = Workspace.PeptideAnalyses.ChildCount;
            new AnalyzePeptidesForm(Workspace).Show(TopLevelControl);
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
            new AddSearchResultsForm(Workspace).Show(TopLevelControl);
        }
    }
}
