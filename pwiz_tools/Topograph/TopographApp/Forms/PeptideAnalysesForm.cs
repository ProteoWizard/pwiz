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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using NHibernate;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.DataBinding.RowSources;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;
using pwiz.Topograph.ui.Controls;
using pwiz.Topograph.ui.DataBinding;
using pwiz.Topograph.ui.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PeptideAnalysesForm : BasePeptideAnalysesForm
    {
        private const string Title = "Peptide Analyses";
        private readonly PeptideAnalysisRows _peptideAnalyses;

        public PeptideAnalysesForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            TabText = Name = Title;
            _peptideAnalyses = new PeptideAnalysisRows(Workspace.PeptideAnalyses);
            var viewContext = new TopographViewContext(Workspace, typeof(PeptideAnalysisRow), _peptideAnalyses)
                                  {
                                      DeleteHandler = new PeptideAnalysisDeleteHandler(this),
                                  };
            var idPathPeptideAnalysis = PropertyPath.Root.Property("PeptideAnalysis");
            var idPathPeptide = idPathPeptideAnalysis.Property("Peptide");
            var viewSpec = new ViewSpec()
                .SetName(AbstractViewContext.DefaultViewName)
                .SetColumns(
                new[]
                    {
                        new ColumnSpec(PropertyPath.Root.Property("Peptide")),
                        new ColumnSpec(PropertyPath.Root.Property("ValidationStatus")),
                        new ColumnSpec(idPathPeptideAnalysis.Property("Note")),
                        new ColumnSpec(idPathPeptide.Property("ProteinName")), 
                        new ColumnSpec(idPathPeptide.Property("ProteinDescription")), 
                        new ColumnSpec(idPathPeptide.Property("MaxTracerCount")), 
                        new ColumnSpec(PropertyPath.Root.Property("FileAnalysisCount")), 
                        new ColumnSpec(PropertyPath.Root.Property("MinScore")), 
                        new ColumnSpec(PropertyPath.Root.Property("MaxScore")),
                    });
            bindingListSource1.SetViewContext(viewContext);
            bindingListSource1.RowSource = _peptideAnalyses;
        }

        private void btnAnalyzePeptides_Click(object sender, EventArgs e)
        {
            new AnalyzePeptidesForm(Workspace).Show(TopLevelControl);
        }

        public class PeptideAnalysisRow : PropertyChangedSupport
        {
            public PeptideAnalysisRow(PeptideAnalysis peptideAnalysis)
            {
                PeptideAnalysis = peptideAnalysis;
            }

            protected override IEnumerable<INotifyPropertyChanged> GetProperyChangersToPropagate()
            {
                return new INotifyPropertyChanged[] {PeptideAnalysis}.Concat(PeptideAnalysis.FileAnalyses);
            }

            public LinkValue<string> Peptide
            {
                get
                {
                    return new LinkValue<string>(PeptideAnalysis.Peptide.FullSequence,
                                                 (sender, args) => PeptideAnalysisFrame.ShowPeptideAnalysis(PeptideAnalysis));
                }
            }

            public PeptideAnalysis PeptideAnalysis { get; private set; }

            [DataGridViewColumnType(typeof (ValidationStatusColumn))]
            public ValidationStatus? ValidationStatus
            {
                get { return PeptideAnalysis.GetValidationStatus(); }
                set { PeptideAnalysis.SetValidationStatus(value); }
            }

            public LinkValue<double?> MinScore
            {
                get
                {
                    KeyValuePair<PeptideFileAnalysis, double>? minEntry = null;
                    foreach (var kvp in ListScores())
                    {
                        if (!minEntry.HasValue || minEntry.Value.Value > kvp.Value)
                        {
                            minEntry = kvp;
                        }
                    }
                    return new LinkValue<double?>(minEntry == null ? null : (double?) minEntry.Value.Value,
                                                  (sender, args) =>
                                                      {
                                                          if (minEntry != null)
                                                          {
                                                              PeptideFileAnalysisFrame.ShowPeptideFileAnalysis(
                                                                  PeptideAnalysis.Workspace, minEntry.Value.Key.Id);
                                                          }
                                                      });
                }
            }

            public LinkValue<double?> MaxScore
            {
                get
                {
                    KeyValuePair<PeptideFileAnalysis, double>? maxEntry = null;
                    foreach (var kvp in ListScores())
                    {
                        if (!maxEntry.HasValue || maxEntry.Value.Value <= kvp.Value)
                        {
                            maxEntry = kvp;
                        }
                    }
                    return new LinkValue<double?>(maxEntry == null ? null : (double?) maxEntry.Value.Value,
                                                  (sender, args) =>
                                                      {
                                                          if (maxEntry != null)
                                                          {
                                                              PeptideFileAnalysisFrame.ShowPeptideFileAnalysis(
                                                                  PeptideAnalysis.Workspace, maxEntry.Value.Key.Id);
                                                          }

                                                      });
                }
            }
            [DisplayName("# Data Files")]
            public int FileAnalysisCount { get { return PeptideAnalysis.FileAnalyses.Count; } }


            private IList<KeyValuePair<PeptideFileAnalysis, double>> ListScores()
            {
                return PeptideAnalysis.GetFileAnalyses(true)
                    .Select(peptideFileAnalysis => new KeyValuePair<PeptideFileAnalysis, double?>(peptideFileAnalysis, peptideFileAnalysis.PeakData.DeconvolutionScore))
                    .Where(kvp => kvp.Value.HasValue)
                    .Select(kvp =>new KeyValuePair<PeptideFileAnalysis, double>(kvp.Key, kvp.Value.GetValueOrDefault()))
                    .ToArray();
            }
        }

        class PeptideAnalysisDeleteHandler : DeleteHandler
        {
            private readonly PeptideAnalysesForm _form;
            public PeptideAnalysisDeleteHandler(PeptideAnalysesForm form)
            {
                _form = form;
            }

            public override void Delete()
            {
                IList<PeptideAnalysis> peptideAnalyses = GetSelectedRows<PeptideAnalysisRow>(_form.boundDataGridView1).Select(row=>row.PeptideAnalysis).ToArray();
                if (peptideAnalyses.Count == 0)
                {
                    return;
                }
                string message;
                if (peptideAnalyses.Count == 1)
                {
                    message = string.Format("Are you sure you want to delete the analysis of the peptide '{0}'?",
                                            peptideAnalyses[0].Peptide.FullSequence);
                }
                else
                {
                    message = string.Format("Are you sure you want to delete these {0} peptide analyses?",
                                            peptideAnalyses.Count);
                }
                if (MessageBox.Show(_form, message, Program.AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                {
                    return;
                }
                using (var longWaitDlg = new LongWaitDialog(_form, "Deleting Peptide Analyses"))
                {
                    using (var session = _form.Workspace.OpenSession())
                    {
                        new LongOperationBroker(
                            longOpBroker => DeletePeptideAnalyses(longOpBroker, session, peptideAnalyses),
                            longWaitDlg).LaunchJob();
                    }
                }
                _form.Workspace.DatabasePoller.MergeChangesNow();
            }

            private static void DeletePeptideAnalyses(LongOperationBroker broker, ISession session, IList<PeptideAnalysis> peptideAnalyses)
            {
                var changeLogs = peptideAnalyses.Select(peptideAnalysis => new DbChangeLog(peptideAnalysis)).ToArray();
                var analysisIds = peptideAnalyses.Select(peptideAnalysis => peptideAnalysis.Id).ToArray();
                var strAnalysisIds = string.Join(",", analysisIds.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToArray());
                session.BeginTransaction();
                broker.UpdateStatusMessage("Deleting chromatograms");
                session.CreateSQLQuery(
                    string.Format(
                        "UPDATE DbPeptideFileAnalysis SET ChromatogramSet = NULL WHERE PeptideAnalysis IN ({0})",
                        strAnalysisIds))
                    .ExecuteUpdate();
                session.CreateSQLQuery(
                    string.Format("DELETE C FROM DbChromatogram C"
                                    + "\nJOIN DbChromatogramSet S ON C.ChromatogramSet + S.Id"
                                    + "\nJOIN DbPeptideFileAnalysis F ON S.PeptideFileAnalysis = F.Id"
                                    + "\nWHERE F.PeptideAnalysis IN ({0})",
                                    strAnalysisIds))
                    .ExecuteUpdate();
                session.CreateSQLQuery(
                    string.Format("DELETE S FROM DbChromatogramSet S"
                                    + "\nJOIN DbPeptideFileAnalysis F ON S.PeptideFileAnalysis = F.Id"
                                    + "\nWHERE F.PeptideAnalysis IN ({0})",
                                    strAnalysisIds))
                    .ExecuteUpdate();
                broker.UpdateStatusMessage("Deleting results");
                session.CreateSQLQuery(
                    string.Format("DELETE P FROM DbPeak P"
                                    + "\nJOIN DbPeptideFileAnalysis F ON P.PeptideFileAnalysis = F.Id"
                                    + "\nWHERE F.PeptideAnalysis IN ({0})",
                                    strAnalysisIds))
                    .ExecuteUpdate();
                broker.UpdateStatusMessage("Deleting analyses");
                session.CreateSQLQuery(
                    string.Format("DELETE FROM DbPeptideFileAnalysis WHERE PeptideAnalysis IN ({0})",
                                    strAnalysisIds))
                    .ExecuteUpdate();
                session.CreateSQLQuery(
                    string.Format("DELETE FROM DbPeptideAnalysis WHERE Id IN ({0})",
                                    strAnalysisIds))
                    .ExecuteUpdate();
                foreach (var changeLog in changeLogs)
                {
                    session.Save(changeLog);
                }
                session.Transaction.Commit();
            }
        }

        class PeptideAnalysisRows : ConvertedCloneableBindingList<long, PeptideAnalysis, PeptideAnalysisRow>
        {
            public PeptideAnalysisRows(PeptideAnalyses peptideAnalyses) : base(peptideAnalyses)
            {
            }

            public override long GetKey(PeptideAnalysisRow value)
            {
                return value.PeptideAnalysis.Id;
            }

            protected override PeptideAnalysisRow Convert(PeptideAnalysis source)
            {
                return new PeptideAnalysisRow(source);
            }
        }
    }
}
