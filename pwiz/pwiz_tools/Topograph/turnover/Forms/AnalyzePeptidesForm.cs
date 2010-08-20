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
using System.Threading;
using NHibernate.Criterion;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using Timer=System.Windows.Forms.Timer;

namespace pwiz.Topograph.ui.Forms
{
    public partial class AnalyzePeptidesForm : WorkspaceForm
    {
        private String _statusMessage;
        private int _progress;
        private bool _running;
        private bool _cancelled;
        private Timer _timer = new Timer
                                   {
                                       Interval = 1000
                                    };
        public AnalyzePeptidesForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            _timer.Tick += _timer_Tick;
        }

        void _timer_Tick(object sender, EventArgs e)
        {
            lock (this)
            {
                tbxStatus.Text = _statusMessage;
                progressBar.Value = _progress;
            }
        }

        private void btnCreateAnalyses_Click(object sender, EventArgs e)
        {
            btnCreateAnalyses.Enabled = false;
            tbxStatus.Visible = true;
            _statusMessage = "Creating Analyses";
            _timer.Start();
            _running = true;
            new Action(CreateAnalyses).BeginInvoke(null, null);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _cancelled = true;
            while (_running)
            {
                Thread.Sleep(100);
            }
            base.OnClosing(e);
        }

        private void CreateAnalyses()
        {
            var excludeAas = tbxExcludeAas.Text.ToCharArray();
            var minTracerCount = int.Parse(tbxMinTracers.Text);
            try
            {
                DoCreateAnalyses(excludeAas, minTracerCount);
            }
            finally
            {
                _running = false;
            }
            BeginInvoke(new Action(Close));
        }

        private void DoCreateAnalyses(char[] excludeAas, int minTracerCount)
        {
            _statusMessage = "Listing peptides";
            var peptides = new List<DbPeptide>();
            using (var session = Workspace.OpenWriteSession())
            {
                session.CreateCriteria(typeof (DbPeptide)).List(peptides);
                for (int i = 0; i < peptides.Count(); i++)
                {
                    _statusMessage = "Peptide " + (i + 1) + "/" + peptides.Count;
                    _progress = 100 * i / peptides.Count;
                    if (_cancelled)
                    {
                        break;
                    }
                    var peptide = peptides[i];
                    if (peptide.Sequence.IndexOfAny(excludeAas) >= 0)
                    {
                        continue;
                    }
                    if (Workspace.GetMaxTracerCount(peptide.Sequence) < minTracerCount)
                    {
                        continue;
                    }
                    var searchResults = new List<DbPeptideSearchResult>();
                    var dbPeptideAnalysis = (DbPeptideAnalysis) session.CreateCriteria(typeof (DbPeptideAnalysis))
                        .Add(Restrictions.Eq("Peptide", peptide)).UniqueResult();
                    if (dbPeptideAnalysis == null)
                    {
                        dbPeptideAnalysis = Peptide.CreateDbPeptideAnalysis(session, peptide);
                    }
                    var idList = new List<long>();
                    if (dbPeptideAnalysis.Id.HasValue)
                    {
                        var dataFileIdQuery = session.CreateQuery("SELECT A.MsDataFile.Id FROM " + typeof(DbPeptideFileAnalysis) +
                                                                  " A WHERE A.PeptideAnalysis.Id = :peptideAnalysisId")
                            .SetParameter("peptideAnalysisId", dbPeptideAnalysis.Id);
                        dataFileIdQuery.List(idList);
                    }
                    var dataFileIds = new HashSet<long>(idList);
                    foreach (var peptideSearchResult in peptide.SearchResults)
                    {
                        if (dataFileIds.Contains(peptideSearchResult.MsDataFile.Id.Value))
                        {
                            continue;
                        }
                        searchResults.Add(peptideSearchResult);
                    }
                    foreach (var peptideSearchResult in searchResults)
                    {
                        if (_cancelled)
                        {
                            return;
                        }
                        var msDataFile = Workspace.MsDataFiles.GetMsDataFile(peptideSearchResult.MsDataFile);
                        if (!msDataFile.HasTimes())
                        {
                            var fileInited = (bool)Invoke(new Func<MsDataFile, bool>(
                                                               TurnoverForm.Instance.EnsureMsDataFile), msDataFile);
                            if (!fileInited)
                            {
                                continue;
                            }
                        }
                    }
                    if (searchResults.Count == 0)
                    {
                        continue;
                    }
                    session.BeginTransaction();
                    bool newAnalysis;
                    if (dbPeptideAnalysis.Id.HasValue)
                    {
                        newAnalysis = false;
                    }
                    else
                    {
                        session.Save(dbPeptideAnalysis);
                        newAnalysis = true;
                    }
                    var sqlStatementBuilder = new SqlStatementBuilder(session.GetSessionImplementation().Factory.Dialect);
                    var insertStatements = new List<string>();
                    foreach (var peptideSearchResult in searchResults)
                    {
                        var msDataFile = Workspace.MsDataFiles.GetMsDataFile(peptideSearchResult.MsDataFile);
                        if (!msDataFile.HasTimes())
                        {
                            continue;
                        }
                        var dbPeptideFileAnalysis = PeptideFileAnalysis.CreatePeptideFileAnalysis(session, msDataFile,
                                                                                                  dbPeptideAnalysis,
                                                                                                  peptideSearchResult);
                        insertStatements.Add(sqlStatementBuilder.GetInsertStatement("DbPeptideFileAnalysis",
                            new Dictionary<string, object>
                                {
                                    {"ChromatogramEndTime", dbPeptideFileAnalysis.ChromatogramEndTime},
                                    {"ChromatogramStartTime", dbPeptideFileAnalysis.ChromatogramStartTime},
                                    {"FirstDetectedScan", dbPeptideFileAnalysis.FirstDetectedScan},
                                    {"LastDetectedScan", dbPeptideFileAnalysis.LastDetectedScan},
                                    {"MsDataFile", msDataFile.Id},
                                    {"PeptideAnalysis", dbPeptideAnalysis.Id},
                                    {"AutoFindPeak", 1},
                                    {"Version",1},
                                    {"ChromatogramCount", 0},
                                    {"PeakCount",0},
                                    {"PeptideDistributionCount",0},
                                    {"ValidationStatus",0},
                                }
                            ));
                        dbPeptideAnalysis.FileAnalysisCount++;
                    }
                    sqlStatementBuilder.ExecuteStatements(session, insertStatements);
                    session.Update(dbPeptideAnalysis);
                    if (!newAnalysis)
                    {
                        session.Save(new DbChangeLog(Workspace, dbPeptideAnalysis));
                    }
                    else
                    {
                        var dbWorkspace = Workspace.LoadDbWorkspace(session);
                        dbWorkspace.PeptideAnalysisCount++;
                        session.Update(dbWorkspace);
                    }
                    session.Transaction.Commit();
                }
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
