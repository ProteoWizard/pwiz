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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using log4net;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.Search;

namespace pwiz.Topograph.ui.Forms
{
    public partial class AddSearchResultsForm : WorkspaceForm
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof (AddSearchResultsForm));
        private readonly IList<String> filenames;
        private bool _isRunning;
        private bool _isCancelled;
        private String _message;
        private int _progress;
        public AddSearchResultsForm(Workspace workspace, IList<String> filenames)
            : base(workspace)
        {
            InitializeComponent();
            this.filenames = filenames;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            new Action(WorkBackground).BeginInvoke(null, null);
        }

        private void WorkBackground()
        {
            try
            {
                IsRunning = true;
                if (IsCancelled)
                {
                    return;
                }
                for (int i = 0; i < filenames.Count; i++)
                {
                    String message = "Processing file ";
                    if (filenames.Count > 1)
                    {
                        message += (i + 1) + "/" + filenames.Count + " ";
                    }
                    message += Path.GetFileName(filenames[i]);
                    UpdateProgress(message, 0);
                    AddSearchResults(filenames[i]);
                }
                if (!IsCancelled)
                {
                    SafeBeginInvoke(Close);
                }
            }
            finally
            {
                IsRunning = false;
            }
        }

        private bool ProgressMonitor(int progress)
        {
            if (IsCancelled)
            {
                return false;
            }
            progress = progress/2;
            progress = Math.Max(0, Math.Min(progress, 100));
            UpdateProgress(_message, progress);
            return true;
        }
        private void UpdateProgress(String message, int progress)
        {
            lock(this)
            {
                if (message == _message && progress == _progress)
                {
                    return;
                }
                _message = message;
                _progress = progress;
            }
            SafeBeginInvoke(delegate
                                {
                                    lock(this)
                                    {
                                        tbxStatus.Text = _message;
                                        progressBar.Value = _progress;
                                    }
                                });
        }

        private void AddSearchResults(String file)
        {
            String extension = Path.GetExtension(file);
            List<SearchResult> searchResults;
            bool requirePeptideAlreadyBeThere = false;
            String msDataFileName;
            using (var stream = File.OpenRead(file))
            {
                if (extension == ".sqt")
                {
                    msDataFileName = Path.GetFileNameWithoutExtension(file);
                    searchResults = SearchResults.ReadSQT(msDataFileName, stream, ProgressMonitor);
                    requirePeptideAlreadyBeThere = true;
                }
                else
                {
                    msDataFileName = null;
                    searchResults = SearchResults.ReadDTASelect(stream, ProgressMonitor);
                }
            }
            if (searchResults == null)
            {
                return;
            }
            var dbPeptideSearchResults = GetSearchResults(msDataFileName);
            var peptides = new Dictionary<String, DbPeptide>();
            var modifiedPeptides = new HashSet<DbPeptide>();
            var dataFiles = new Dictionary<String, DbMsDataFile>();
            using (var session = Workspace.OpenSession())
            {
                foreach (DbPeptide peptide in session.CreateCriteria(typeof(DbPeptide)).List())
                {
                    peptides.Add(peptide.Sequence, peptide);
                }
                foreach (DbMsDataFile msDataFile in session.CreateCriteria(typeof(DbMsDataFile)).List())
                {
                    dataFiles.Add(msDataFile.Name, msDataFile);
                }
            }

            var enrichment = Workspace.GetEnrichmentDef();
            using (var session = Workspace.OpenWriteSession())
            {
                var dbWorkspace = Workspace.LoadDbWorkspace(session);
                session.BeginTransaction();
                for (int i = 0; i < searchResults.Count; i++)
                {
                    if (!ProgressMonitor(100 + 100 * i / searchResults.Count))
                    {
                        return;
                    }
                    var searchResult = searchResults[i];
                    String trimmedSequence = Peptide.TrimSequence(searchResult.Sequence);
                    DbPeptide dbPeptide;
                    if (!peptides.TryGetValue(trimmedSequence, out dbPeptide))
                    {
                        if (requirePeptideAlreadyBeThere)
                        {
                            continue;
                        }
                        dbPeptide = new DbPeptide
                        {
                            Workspace = dbWorkspace,
                            Protein = searchResult.Protein,
                            ProteinDescription = searchResult.ProteinDescription,
                            Sequence = trimmedSequence,
                            FullSequence = searchResult.Sequence,
                            MaxTracerCount =
                                enrichment.GetMaximumTracerCount(
                                new ChargedPeptide(searchResult.Sequence, 1)),
                        };
                        session.Save(dbPeptide);
                        peptides.Add(trimmedSequence, dbPeptide);
                    }
                    DbMsDataFile dbMsDataFile;
                    if (!dataFiles.TryGetValue(searchResult.Filename, out dbMsDataFile))
                    {
                        var msDataFilePath = Workspace.ResolveMsDataFilePath(Path.GetDirectoryName(file), searchResult.Filename);
                        dbMsDataFile = new DbMsDataFile
                                           {
                                               Name = searchResult.Filename,
                                               Label = searchResult.Filename,
                                               Workspace = dbWorkspace,
                                               Path = msDataFilePath
                                           };
                        session.Save(dbMsDataFile);
                        dataFiles.Add(dbMsDataFile.Name, dbMsDataFile);
                    }
                    var key = new SearchResultKey(dbMsDataFile, dbPeptide);
                    DbPeptideSearchResult dbPeptideSearchResult;
                    if (!dbPeptideSearchResults.TryGetValue(key, out dbPeptideSearchResult))
                    {
                        dbPeptideSearchResult = new DbPeptideSearchResult
                                                    {
                                                        MsDataFile = dbMsDataFile,
                                                        Peptide = dbPeptide,
                                                        MinCharge = searchResult.Charge,
                                                        MaxCharge = searchResult.Charge,
                                                        FirstDetectedScan = searchResult.ScanIndex,
                                                        LastDetectedScan = searchResult.ScanIndex,
                                                        FirstTracerCount = searchResult.TracerCount,
                                                        LastTracerCount = searchResult.TracerCount,
                                                    };
                        session.Save(dbPeptideSearchResult);
                        dbPeptide.SearchResultCount++;
                        session.Update(dbPeptide);
                        modifiedPeptides.Add(dbPeptide);
                        dbPeptideSearchResults.Add(key, dbPeptideSearchResult);
                    }
                    else
                    {
                        if (dbPeptideSearchResult.FirstDetectedScan <= searchResult.ScanIndex &&
                            dbPeptideSearchResult.LastDetectedScan >= searchResult.ScanIndex &&
                            dbPeptideSearchResult.MinCharge <= searchResult.Charge &&
                            dbPeptideSearchResult.MaxCharge >= searchResult.Charge)
                        {
                            continue;
                        }
                        if (dbPeptideSearchResult.FirstDetectedScan > searchResult.ScanIndex)
                        {
                            dbPeptideSearchResult.FirstDetectedScan = searchResult.ScanIndex;
                            dbPeptideSearchResult.FirstTracerCount = searchResult.TracerCount;
                        }
                        if (dbPeptideSearchResult.LastDetectedScan < searchResult.ScanIndex)
                        {
                            dbPeptideSearchResult.LastDetectedScan = searchResult.ScanIndex;
                            dbPeptideSearchResult.LastTracerCount = searchResult.TracerCount;
                        }
                        dbPeptideSearchResult.MinCharge = Math.Min(dbPeptideSearchResult.MinCharge, searchResult.Charge);
                        dbPeptideSearchResult.MaxCharge = Math.Max(dbPeptideSearchResult.MaxCharge, searchResult.Charge);
                        session.Update(dbPeptideSearchResult);
                    }
                }
                dbWorkspace.PeptideCount = peptides.Count;
                dbWorkspace.MsDataFileCount = dataFiles.Count;
                session.Update(dbWorkspace);
                session.Transaction.Commit();
                foreach (var dbPeptide in modifiedPeptides)
                {
                    var peptide = Workspace.Peptides.GetPeptide(dbPeptide);
                    if (peptide == null)
                    {
                        peptide = new Peptide(Workspace, dbPeptide);
                        Workspace.Peptides.AddChild(dbPeptide.Id.Value, peptide);
                    }
                    peptide.SearchResultCount = dbPeptide.SearchResultCount;
                }
                foreach (var dbMsDataFile in dataFiles.Values)
                {
                    var msDataFile = Workspace.MsDataFiles.GetMsDataFile(dbMsDataFile);
                    if (msDataFile == null)
                    {
                        msDataFile = new MsDataFile(Workspace, dbMsDataFile);
                        Workspace.MsDataFiles.AddChild(msDataFile.Id.Value, msDataFile);
                    }
                }
            }
        }
        class SearchResultKey
        {
            public SearchResultKey(DbMsDataFile msDataFile, DbPeptide peptide)
            {
                MsDataFile = msDataFile;
                Peptide = peptide;
            }
            public DbMsDataFile MsDataFile { get; private set; }
            public DbPeptide Peptide { get; private set; }
            public override int GetHashCode()
            {
                int hashCode = MsDataFile.GetHashCode();
                hashCode = hashCode * 31 + Peptide.GetHashCode();
                return hashCode;
            }
            public override bool Equals(object obj)
            {
                if (obj == this)
                {
                    return true;
                }
                var that = obj as SearchResultKey;
                if (that == null)
                {
                    return false;
                }
                return MsDataFile.Equals(that.MsDataFile) && Peptide.Equals(that.Peptide);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            IsCancelled = true;
            for (;;)
            {
                if (!IsRunning)
                {
                    break;
                }
                Thread.Sleep(100);
            }
        }
        private bool IsCancelled
        {
            get
            {
                lock(this)
                {
                    return _isCancelled;
                }
            }
            set
            {
                lock(this)
                {
                    _isCancelled = value;
                }
            }
        }
        private bool IsRunning
        {
            get
            {
                lock(this)
                {
                    return _isRunning;
                }
            }
            set
            {
                lock(this)
                {
                    _isRunning = value;
                }
            }
        }
        private Dictionary<SearchResultKey, DbPeptideSearchResult> GetSearchResults(String msDataFileName)
        {
            var result = new Dictionary<SearchResultKey, DbPeptideSearchResult>();
            using (var session = Workspace.OpenSession())
            {
                var criteria = session.CreateCriteria(typeof (DbPeptideSearchResult));
                if (msDataFileName != null)
                {
                    var dataFileCriteria = session.CreateCriteria(typeof (DbMsDataFile))
                        .Add(Restrictions.Eq("Name", msDataFileName));
                    var dbMsDataFile = (DbMsDataFile) dataFileCriteria.UniqueResult();
                    if (dbMsDataFile == null)
                    {
                        return result;
                    }
                    criteria.Add(Restrictions.Eq("MsDataFile", dbMsDataFile));
                }
                foreach (DbPeptideSearchResult searchResult in criteria.List())
                {
                    result.Add(new SearchResultKey(searchResult.MsDataFile, searchResult.Peptide), searchResult);
                }
            }
            return result;
        }
    }
}
