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
using NHibernate.Criterion;
using pwiz.Common.DataBinding;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PeptidesForm : WorkspaceForm
    {
        private TopographViewContext _viewContext;
        private BindingList<LinkValue<Peptide>> _peptides;
        public PeptidesForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            TabText = Name = "Peptides";
            btnAnalyzePeptides.Enabled = Workspace.Peptides.GetChildCount() > 0;
            var defaultColumns = new[]
                                     {
                                         new ColumnSpec().SetIdentifierPath(IdentifierPath.Root),
                                         new ColumnSpec().SetName("ProteinName").SetCaption("Protein"),
                                         new ColumnSpec().SetName("ProteinDescription"),
                                         new ColumnSpec().SetName("MaxTracerCount"),
                                         new ColumnSpec().SetName("SearchResultCount"),
                                     };
            var defaultViewSpec = new ViewSpec()
                .SetName("default")
                .SetColumns(defaultColumns);
            navBar1.ViewContext = _viewContext = new TopographViewContext(workspace, typeof(LinkValue<Peptide>), new[] { defaultViewSpec });
            dataGridView.BindingListView.ViewInfo = new ViewInfo(_viewContext.ParentColumn, _viewContext.BuiltInViewSpecs.First());
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Requery();
        }

        private void Requery()
        {
            _peptides = new BindingList<LinkValue<Peptide>>(Workspace.Peptides.ListChildren().Select(p=>MakeLinkValue(p)).ToList());
            dataGridView.BindingListView.RowSource = _peptides;
        }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            foreach (var peptide in args.GetNewEntities().OfType<Peptide>())
            {
                _peptides.Add(MakeLinkValue(peptide));
            }
        }

        private LinkValue<Peptide> MakeLinkValue(Peptide peptide)
        {
            return new LinkValue<Peptide>(peptide, PeptideClickHandler(peptide));
        }

        private EventHandler PeptideClickHandler(Peptide peptide)
        {
            return (sender, args) => ShowPeptideAnalysis(peptide);
        }

        private void ShowPeptideAnalysis(Peptide peptide)
        {
            using (var session = Workspace.OpenSession())
            {
                var dbPeptide = session.Load<DbPeptide>(peptide.Id);
                var dbPeptideAnalysis = (DbPeptideAnalysis)session
                    .CreateCriteria(typeof(DbPeptideAnalysis))
                    .Add(Restrictions.Eq("Peptide", dbPeptide))
                    .UniqueResult();
                if (dbPeptideAnalysis == null)
                {
                    var searchResults = session
                         .CreateCriteria(typeof(DbPeptideSearchResult))
                         .Add(Restrictions.Eq("Peptide", dbPeptide))
                         .List();
                    if (searchResults.Count == 0)
                    {
                        MessageBox.Show(
                            "This peptide cannot be analyzed because it has no search results.",
                            Program.AppName);
                        return;
                    }
                    if (MessageBox.Show("This peptide has not yet been analyzed.  Do you want to analyze it now?", Program.AppName, MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                    {
                        return;
                    }
                    session.BeginTransaction();
                    session.Save(dbPeptideAnalysis = Peptide.CreateDbPeptideAnalysis(session, dbPeptide));
                    foreach (DbPeptideSearchResult dbPeptideSearchResult in searchResults)
                    {
                        var msDataFile = Workspace.MsDataFiles.GetMsDataFile(dbPeptideSearchResult.MsDataFile);
                        if (!TurnoverForm.Instance.EnsureMsDataFile(msDataFile, true))
                        {
                            continue;
                        }
                        session.Save(PeptideFileAnalysis.CreatePeptideFileAnalysis(
                            session, msDataFile, dbPeptideAnalysis, dbPeptideSearchResult, false));
                        dbPeptideAnalysis.FileAnalysisCount++;
                    }
                    session.Update(dbPeptideAnalysis);
                    session.Transaction.Commit();
                }
                var peptideAnalysis = TurnoverForm.Instance.LoadPeptideAnalysis(dbPeptideAnalysis.Id.Value);
                if (peptideAnalysis == null)
                {
                    return;
                }
                PeptideAnalysisFrame.ShowPeptideAnalysis(peptideAnalysis);
            }
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
