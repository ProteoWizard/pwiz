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
using pwiz.Topograph.Util;
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
            if (workspace.GetTracerDefs().Count == 0)
            {
                tbxMinTracers.Text = 0.ToString();
            }
            else
            {
                tbxMinTracers.Text = 1.ToString();
            }
            tbxChromTimeAroundMs2.Text = workspace.GetChromTimeAroundMs2Id().ToString();
            tbxExtraChromTimeWithoutMs2Id.Text = workspace.GetExtraChromTimeWithoutMs2Id().ToString();
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
            using (Workspace.GetWriteLock())
            {
                Workspace.SetChromTimeAroundMs2Id(double.Parse(tbxChromTimeAroundMs2.Text));
                Workspace.SetExtraChromTimeWithoutMs2Id(double.Parse(tbxExtraChromTimeWithoutMs2Id.Text));
            }
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
            catch (Exception e)
            {
                ErrorHandler.LogException("Create Analyses", null, e);
            }
            finally
            {
                _running = false;
            }
            BeginInvoke(new Action(Close));
        }

        private void DoCreateAnalyses(char[] excludeAas, int minTracerCount)
        {
            double chromTimeAroundMs2Id = Workspace.GetChromTimeAroundMs2Id();
            double extraChromTimeWithoutMs2Id = Workspace.GetExtraChromTimeWithoutMs2Id();
            var peptides = new List<DbPeptide>();
            bool includeMissingMs2 = cbxIncludeMissingMS2.Checked;
            if (includeMissingMs2)
            {
                _statusMessage = "Initializing Data Files";
                foreach (var msDataFile in Workspace.MsDataFiles.ListChildren())
                {
                    if (_cancelled)
                    {
                        return;
                    }
                    if (!msDataFile.HasTimes())
                    {
                        Invoke(new Func<MsDataFile, bool>(TurnoverForm.Instance.EnsureMsDataFile), msDataFile);
                    }
                }
            }
            _statusMessage = "Listing peptides";
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
                    double minStartTime = double.MaxValue;
                    double maxEndTime = -double.MaxValue;
                    var peptide = peptides[i];
                    if (peptide.Sequence.IndexOfAny(excludeAas) >= 0)
                    {
                        continue;
                    }
                    if (Workspace.GetMaxTracerCount(peptide.Sequence) < minTracerCount)
                    {
                        continue;
                    }
                    var dbPeptideAnalysis = (DbPeptideAnalysis) session.CreateCriteria(typeof (DbPeptideAnalysis))
                        .Add(Restrictions.Eq("Peptide", peptide)).UniqueResult();
                    if (dbPeptideAnalysis == null)
                    {
                        dbPeptideAnalysis = Peptide.CreateDbPeptideAnalysis(session, peptide);
                    }
                    else
                    {
                        if (includeMissingMs2)
                        {
                            var query = session.CreateQuery("SELECT MIN(T.ChromatogramStartTime),MAX(T.ChromatogramEndTime) FROM " +
                                typeof(DbPeptideFileAnalysis) + " T WHERE T.PeptideAnalysis = :peptideAnalysis AND T.FirstDetectedScan IS NOT NULL AND T.LastDetectedScan IS NOT NULL")
                                .SetParameter("peptideAnalysis", dbPeptideAnalysis);
                            var result = (object[])query.UniqueResult();
                            if (result != null)
                            {
                                minStartTime = Convert.ToDouble(result[0]);
                                maxEndTime = Convert.ToDouble(result[1]);
                            }
                        }
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
                    var searchResults = peptide.SearchResults
                        .Where(peptideSearchResult => !dataFileIds.Contains(peptideSearchResult.MsDataFile.Id.Value))
                        .ToDictionary(peptideSearchResult => Workspace.MsDataFiles.GetChild(peptideSearchResult.MsDataFile.Id.Value));
                    if (includeMissingMs2)
                    {
                        foreach (var msDataFile in Workspace.MsDataFiles.ListChildren())
                        {
                            if (searchResults.ContainsKey(msDataFile)
                                || dataFileIds.Contains(msDataFile.Id.Value))
                            {
                                continue;
                            }
                            searchResults.Add(msDataFile, null);
                        }
                    }
                    foreach (var entry in searchResults)
                    {
                        if (_cancelled)
                        {
                            return;
                        }
                        var msDataFile = entry.Key;
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
                    var entries = searchResults.ToArray();
                    Array.Sort(entries, (e1,e2)=>(e1.Value == null).CompareTo(e2.Value == null));
                    // If no entries at all have a PeptideSearchResult, skip this peptide.
                    if (entries.Length == 0 || (entries[0].Value == null && minStartTime > maxEndTime))
                    {
                        continue;
                    }
                    foreach (var entry in entries)
                    {
                        var msDataFile = entry.Key;
                        if (!msDataFile.HasTimes())
                        {
                            continue;
                        }
                        var dbPeptideFileAnalysis = PeptideFileAnalysis.CreatePeptideFileAnalysis(
                            session, msDataFile, dbPeptideAnalysis, entry.Value, false);
                        if (entry.Value == null)
                        {
                            dbPeptideFileAnalysis.ChromatogramStartTime = minStartTime - extraChromTimeWithoutMs2Id;
                            dbPeptideFileAnalysis.ChromatogramEndTime = maxEndTime + extraChromTimeWithoutMs2Id;
                        }
                        else
                        {
                            minStartTime = Math.Min(minStartTime, dbPeptideFileAnalysis.ChromatogramStartTime);
                            maxEndTime = Math.Max(maxEndTime, dbPeptideFileAnalysis.ChromatogramEndTime);
                        }
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
                                    {"PeakCount",0},
                                    {"PsmCount", dbPeptideFileAnalysis.PsmCount},
                                    {"ValidationStatus",0},
                                }
                            ));
                        dbPeptideAnalysis.FileAnalysisCount++;
                        dataFileIds.Add(msDataFile.Id.Value);
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
