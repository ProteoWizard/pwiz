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
using System.IO;
using System.Threading;
using System.Windows.Forms;
using NHibernate.Criterion;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Search;
using pwiz.Topograph.ui.Properties;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class AddSearchResultsForm : WorkspaceForm
    {
        private bool _isRunning;
        private bool _isCancelled;
        private String _message;
        private int _progress;
        public AddSearchResultsForm(Workspace workspace)
            : base(workspace)
        {
            InitializeComponent();
        }

        private void WorkBackground(IList<string> filenames, Func<string, Func<int, bool>, IList<SearchResult>> fnReadSearchResults)
        {
            try
            {
                if (IsCancelled)
                {
                    return;
                }
                var peptides = new Dictionary<String, DbPeptide>();
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

                for (int i = 0; i < filenames.Count; i++)
                {
                    String message = "Processing file ";
                    if (filenames.Count > 1)
                    {
                        message += (i + 1) + "/" + filenames.Count + " ";
                    }
                    message += Path.GetFileName(filenames[i]);
                    UpdateProgress(message, 0);
                    var searchResults = fnReadSearchResults(filenames[i], ProgressMonitor);
                    AddSearchResults(searchResults, peptides, dataFiles);
                }
                if (!IsCancelled)
                {
                    SafeBeginInvoke(Close);
                }
            }
            catch (Exception e)
            {
                ErrorHandler.LogException("Add Search Results", "Exception occurred while processing search results", e);
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

        private void AddSearchResults(IList<SearchResult> searchResults, Dictionary<string,DbPeptide> peptides, Dictionary<string,DbMsDataFile> dataFiles)
        {
            String baseMessage = _message;
            if (searchResults == null)
            {
                return;
            }
            var dbPeptideSearchResults = GetSearchResults();
            var modifiedPeptides = new HashSet<DbPeptide>();
            var searchResultsToInsert = new Dictionary<SearchResultKey, DbPeptideSpectrumMatch>();
            var searchResultsToUpdate = new Dictionary<SearchResultKey, DbPeptideSpectrumMatch>();
            using (var session = Workspace.OpenWriteSession())
            {
                var statementBuilder = new SqlStatementBuilder(session.GetSessionImplementation().Factory.Dialect);
                var dbWorkspace = Workspace.LoadDbWorkspace(session);
                session.BeginTransaction();
                var newPeptides = new Dictionary<String, Dictionary<String, object>>();

                foreach (var searchResult in searchResults)
                {
                    var trimmedSequence = Peptide.TrimSequence(searchResult.Sequence);
                    if (peptides.ContainsKey(trimmedSequence) || newPeptides.ContainsKey(trimmedSequence))
                    {
                        continue;
                    }
                    newPeptides.Add(trimmedSequence, new Dictionary<string, object>
                                                         {
                                                             {"Sequence", trimmedSequence},
                                                             {"FullSequence", searchResult.Sequence},
                                                             {"Version",1},
                                                             {"ValidationStatus",0}
                                                         });
                }
                if (newPeptides.Count > 0)
                {
                    long maxPeptideId = (long?) session.CreateQuery("SELECT Max(T.Id) FROM DbPeptide T").UniqueResult() ?? 0;
                    var insertStatements = new List<String>();
                    foreach (var dict in newPeptides.Values)
                    {
                        insertStatements.Add(statementBuilder.GetInsertStatement("DbPeptide", dict));
                    }
                    statementBuilder.ExecuteStatements(session, insertStatements);
                    var criteria = session.CreateCriteria(typeof (DbPeptide))
                        .Add(Restrictions.Gt("Id", maxPeptideId));
                    foreach (DbPeptide dbPeptide in criteria.List())
                    {
                        peptides.Add(dbPeptide.Sequence, dbPeptide);
                    }
                }

                foreach (var searchResult in searchResults)
                {
                    var trimmedSequence = Peptide.TrimSequence(searchResult.Sequence);
                    DbPeptide dbPeptide;
                    if (!peptides.TryGetValue(trimmedSequence, out dbPeptide))
                    {
                        // should not happen
                        continue;
                    }
                    if (searchResult.Filename == null)
                    {
                        continue;
                    }
                    DbMsDataFile dbMsDataFile;
                    if (!dataFiles.TryGetValue(searchResult.Filename, out dbMsDataFile))
                    {
                        dbMsDataFile = new DbMsDataFile
                        {
                            Name = searchResult.Filename,
                            Label = searchResult.Filename,
                        };
                        session.Save(dbMsDataFile);
                        dataFiles.Add(dbMsDataFile.Name, dbMsDataFile);
                    }
                    
                    var key = new SearchResultKey(dbMsDataFile, dbPeptide, searchResult.RetentionTime);
                    DbPeptideSpectrumMatch dbPeptideSpectrumMatch;
                    if (dbPeptideSearchResults.TryGetValue(key, out dbPeptideSpectrumMatch))
                    {
                        bool changed = false;
                        if (dbPeptideSpectrumMatch.ModifiedSequence == null && searchResult.ModifiedSequence != null)
                        {
                            dbPeptideSpectrumMatch.ModifiedSequence = searchResult.ModifiedSequence;
                            changed = true;
                        }
                        if (!dbPeptideSpectrumMatch.PrecursorMz.HasValue && searchResult.PrecursorMz.HasValue)
                        {
                            dbPeptideSpectrumMatch.PrecursorMz = searchResult.PrecursorMz;
                            dbPeptideSpectrumMatch.PrecursorCharge = searchResult.PrecursorCharge;
                            changed = true;
                        }
                        if (!changed)
                        {
                            continue;
                        }
                        searchResultsToUpdate[key] = dbPeptideSpectrumMatch;
                    }
                    else
                    {
                        if (!searchResultsToInsert.TryGetValue(key, out dbPeptideSpectrumMatch))
                        {
                            dbPeptideSpectrumMatch = new DbPeptideSpectrumMatch
                                                    {
                                                        MsDataFile = dbMsDataFile,
                                                        Peptide = dbPeptide,
                                                        RetentionTime = searchResult.RetentionTime,
                                                        PrecursorCharge = searchResult.PrecursorCharge,
                                                        PrecursorMz = searchResult.PrecursorMz,
                                                        ModifiedSequence = searchResult.ModifiedSequence,
                                                    };
                            searchResultsToInsert.Add(key, dbPeptideSpectrumMatch);
                            if (!newPeptides.ContainsKey(trimmedSequence))
                            {
                                modifiedPeptides.Add(dbPeptide);
                            }
                        }
                    }
                }
                var statements = new List<string>();
                foreach (var dbPeptideSearchResult in searchResultsToInsert.Values)
                {
                    statements.Add(statementBuilder.GetInsertStatement("DbPeptideSpectrumMatch",
                        new Dictionary<string, object> {
                            {"MsDataFile", dbPeptideSearchResult.MsDataFile.Id},
                            {"Peptide", dbPeptideSearchResult.Peptide.Id},
                            {"RetentionTime", dbPeptideSearchResult.RetentionTime},
                            {"ModifiedSequence", dbPeptideSearchResult.ModifiedSequence},
                            {"PrecursorMz", dbPeptideSearchResult.PrecursorMz},
                            {"PrecursorCharge", dbPeptideSearchResult.PrecursorCharge},
                            {"Version", 1},
                        }));
                }
                foreach (var dbPeptideSearchResult in searchResultsToUpdate.Values)
                {
                    statements.Add(statementBuilder.GetUpdateStatement("DbPeptideSearchResult",
                        new Dictionary<string, object> {
                            {"RetentionTime", dbPeptideSearchResult.RetentionTime},
                            {"ModifiedSequence", dbPeptideSearchResult.ModifiedSequence},
                            {"PrecursorMz", dbPeptideSearchResult.PrecursorMz},
                            {"PrecursorCharge", dbPeptideSearchResult.PrecursorCharge},
                            {"Version", dbPeptideSearchResult.Version + 1},
                        },
                            new Dictionary<string, object> { { "Id", dbPeptideSearchResult.Id.GetValueOrDefault() } }
                        ));
                }
                statementBuilder.ExecuteStatements(session, statements);
                UpdateProgress(baseMessage + "(Committing transaction)", 99);
                session.Transaction.Commit();
            }
        }

        class SearchResultKey
        {
            public SearchResultKey(DbMsDataFile msDataFile, DbPeptide peptide, double retentionTime)
                : this(msDataFile.Id.GetValueOrDefault(), peptide.Id.GetValueOrDefault(), retentionTime)
            {
            }
            public SearchResultKey(long msDataFileId, long peptideId, double retentionTime)
            {
                MsDataFileId = msDataFileId;
                PeptideId = peptideId;
                RetentionTime = retentionTime;
            }

            private long MsDataFileId { get; set; }
            private long PeptideId { get; set; }
            private double RetentionTime { get; set; }
            public override int GetHashCode()
            {
                int hashCode = MsDataFileId.GetHashCode();
                hashCode = hashCode * 31 + PeptideId.GetHashCode();
                hashCode = hashCode*31 + RetentionTime.GetHashCode();
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
                return MsDataFileId.Equals(that.MsDataFileId) 
                    && PeptideId.Equals(that.PeptideId) 
                    && RetentionTime.Equals(that.RetentionTime); 
            }
        }

        private void BtnCancelClick(object sender, EventArgs e)
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
                    Workspace.DatabasePoller.Wake();
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
        private Dictionary<SearchResultKey, DbPeptideSpectrumMatch> GetSearchResults()
        {
            var result = new Dictionary<SearchResultKey, DbPeptideSpectrumMatch>();
            using (var session = Workspace.OpenSession())
            {
                var criteria = session.CreateCriteria(typeof (DbPeptideSpectrumMatch));
                foreach (DbPeptideSpectrumMatch searchResult in criteria.List())
                {
                    result.Add(new SearchResultKey(searchResult.MsDataFile, searchResult.Peptide, searchResult.RetentionTime), searchResult);
                }
            }
            return result;
        }

        private void AddSearchResults(IList<String> filenames, Func<string, Func<int,bool>, IList<SearchResult>> fnReadSearchResults)
        {
            IsRunning = true;
            btnChooseSearchResults.Enabled = false;
            btnImportLibrary.Enabled = false;
            new Action<IList<string>, Func<string, Func<int, bool>, IList<SearchResult>>>(WorkBackground)
                .BeginInvoke(filenames, fnReadSearchResults, null, null);
        }

        private void BtnChooseSearchResultsClick(object sender, EventArgs e)
        {
            Settings.Default.Reload();
            using (var openFileDialog = new OpenFileDialog
                                     {
                                         Filter =
                                             "Supported Files|*.sqt;*.pep.xml;*.pepXML;*.idpXML;*.dat;*.ssl;*.mzid;*.perc.xml;*final_fragment.csv|All Files|*.*",
                                         Multiselect = true,
                                         InitialDirectory = Settings.Default.SearchResultsDirectory,
                                     })
            {
                if (openFileDialog.ShowDialog(this) == DialogResult.Cancel)
                {
                    return;
                }
                Settings.Default.SearchResultsDirectory = Path.GetDirectoryName(openFileDialog.FileName);
                Settings.Default.Save();
                AddSearchResults(openFileDialog.FileNames, SearchResults.ReadSearchResultsViaBiblioSpec);
            }
        }

        private void BtnImportLibraryClick(object sender, EventArgs e)
        {
            Settings.Default.Reload();
            using (var openFileDialog = new OpenFileDialog
            {
                Filter =
                    "BiblioSpec Libraries|*.blib|All Files|*.*",
                Multiselect = true,
                InitialDirectory = Settings.Default.SearchResultsDirectory,
            })
            {
                if (openFileDialog.ShowDialog(this) == DialogResult.Cancel)
                {
                    return;
                }
                Settings.Default.SearchResultsDirectory = Path.GetDirectoryName(openFileDialog.FileName);
                Settings.Default.Save();
                AddSearchResults(openFileDialog.FileNames, SearchResults.ReadBiblioSpecDatabase);
            }
        }
    }
}
