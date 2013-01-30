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
            _timer.Tick += TimerOnTick;
            if (workspace.GetTracerDefs().Count == 0)
            {
                tbxMinTracers.Text = 0.ToString(CultureInfo.CurrentCulture);
            }
            else
            {
                tbxMinTracers.Text = 1.ToString(CultureInfo.CurrentCulture);
            }
            tbxChromTimeAroundMs2.Text = workspace.GetChromTimeAroundMs2Id().ToString(CultureInfo.CurrentCulture);
            tbxExtraChromTimeWithoutMs2Id.Text = workspace.GetExtraChromTimeWithoutMs2Id().ToString(CultureInfo.CurrentCulture);
        }

        void TimerOnTick(object sender, EventArgs e)
        {
            lock (this)
            {
                tbxStatus.Text = _statusMessage;
                progressBar.Value = _progress;
            }
        }

        private void BtnCreateAnalysesOnClick(object sender, EventArgs e)
        {
            Workspace.SetChromTimeAroundMs2Id(double.Parse(tbxChromTimeAroundMs2.Text));
            Workspace.SetExtraChromTimeWithoutMs2Id(double.Parse(tbxExtraChromTimeWithoutMs2Id.Text));
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
            var peptides = new List<DbPeptide>();
            bool includeMissingMs2 = cbxIncludeMissingMS2.Checked;
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
                    var idList = new List<long>();
                    if (dbPeptideAnalysis.Id.HasValue)
                    {
                        var dataFileIdQuery = session.CreateQuery("SELECT A.MsDataFile.Id FROM " + typeof(DbPeptideFileAnalysis) +
                                                                  " A WHERE A.PeptideAnalysis.Id = :peptideAnalysisId")
                            .SetParameter("peptideAnalysisId", dbPeptideAnalysis.Id);
                        dataFileIdQuery.List(idList);
                    }
                    var existingDataFileIds = new HashSet<long>(idList);
                    var psmTimesByDataFileId = peptide.PsmTimesByDataFileId(session);
                    if (psmTimesByDataFileId.Count == 0)
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
                    IList<MsDataFile> dataFiles;
                    if (includeMissingMs2)
                    {
                        dataFiles = Workspace.MsDataFiles
                            .Where(msDataFile => !existingDataFileIds.Contains(msDataFile.Id))
                            .ToArray();
                    }
                    else
                    {
                        dataFiles = new List<MsDataFile>();
                        foreach (var grouping in psmTimesByDataFileId)
                        {
                            MsDataFile dataFile;
                            if (existingDataFileIds.Contains(grouping.Key) || !Workspace.MsDataFiles.TryGetValue(grouping.Key, out dataFile))
                            {
                                continue;
                            }
                            dataFiles.Add(dataFile);
                        }
                    }
                    foreach (var msDataFile in dataFiles)
                    {
                        var dbPeptideFileAnalysis = PeptideFileAnalysis.CreatePeptideFileAnalysis(session, msDataFile, dbPeptideAnalysis, psmTimesByDataFileId);
                        insertStatements.Add(sqlStatementBuilder.GetInsertStatement("DbPeptideFileAnalysis",
                            new Dictionary<string, object>
                                {
                                    {"ChromatogramEndTime", dbPeptideFileAnalysis.ChromatogramEndTime},
                                    {"ChromatogramStartTime", dbPeptideFileAnalysis.ChromatogramStartTime},
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
                        existingDataFileIds.Add(msDataFile.Id);
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
                        session.Update(dbWorkspace);
                    }
                    session.Transaction.Commit();
                }
            }
        }

        private void BtnCancelOnClick(object sender, EventArgs e)
        {
            Close();
        }
    }
}
