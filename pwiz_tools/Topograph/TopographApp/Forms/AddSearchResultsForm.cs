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
using pwiz.Topograph.Model;
using pwiz.Topograph.Search;
using pwiz.Topograph.ui.Properties;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class AddSearchResultsForm : WorkspaceForm
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof (AddSearchResultsForm));
        private IList<String> filenames;
        private bool _isRunning;
        private bool _isCancelled;
        private String _message;
        private int _progress;
        private double _minXCorr1 = 1.8;
        private double _minXCorr2 = 2.0;
        private double _minXCorr3 = 2.4;
        private double _maxQValue = 0.01;
        private bool _useMinXCorr = false;
        private bool _onlyExistingPeptides = true;
        private SearchResultFileType searchResultFileType;
        public AddSearchResultsForm(Workspace workspace)
            : base(workspace)
        {
            InitializeComponent();
            tbxMinXCorr1.Text = _minXCorr1.ToString();
            tbxMinXCorr2.Text = _minXCorr2.ToString();
            tbxMinXCorr3.Text = _minXCorr3.ToString();
            tbxMaxQValue.Text = _maxQValue.ToString();
            cbxMinimumXCorr.Checked = _useMinXCorr;
            cbxMinimumXCorr_CheckedChanged(cbxMinimumXCorr, new EventArgs());
            cbxOnlyExistingPeptides.Checked = _onlyExistingPeptides;
        }

        private void WorkBackground()
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
                    AddSearchResults(filenames[i], peptides, dataFiles);
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

        private IList<SearchResult> FilterSearchResults(Dictionary<string, DbPeptide> peptides, IList<SearchResult> searchResults)
        {
            if (searchResults == null)
            {
                return null;
            }
            bool useMinXCorr = _useMinXCorr;
            bool onlyExistingPeptides = _onlyExistingPeptides;
            if (!useMinXCorr && !onlyExistingPeptides)
            {
                return searchResults;
            }
            var result = new List<SearchResult>();
            foreach (var searchResult in searchResults)
            {
                if (onlyExistingPeptides && !peptides.ContainsKey(Peptide.TrimSequence(searchResult.Sequence)))
                {
                    continue;
                }
                if (useMinXCorr)
                {
                    double minXCorr;
                    switch (searchResult.Charge)
                    {
                        case 1:
                            minXCorr = _minXCorr1;
                            break;
                        case 2:
                            minXCorr = _minXCorr2;
                            break;
                        default:
                            minXCorr = _minXCorr3;
                            break;
                    }
                    if (searchResult.XCorr < minXCorr)
                    {
                        continue;
                    }
                }
                result.Add(searchResult);
            }
            return result;
        }

        private Dictionary<string, int> SearchResultCountsBySequence(IEnumerable<SearchResult> searchResults)
        {
            var searchResultFilenames = new Dictionary<string, HashSet<string>>();
            foreach (var searchResult in searchResults)
            {
                HashSet<string> filenames;
                var trimmedSequence = Peptide.TrimSequence(searchResult.Sequence);
                if (!searchResultFilenames.TryGetValue(trimmedSequence, out filenames))
                {
                    filenames = new HashSet<string>();
                    searchResultFilenames.Add(trimmedSequence, filenames);
                }
                if (searchResult.Filename != null)
                {
                    filenames.Add(searchResult.Filename);
                }
            }
            var result = new Dictionary<string, int>();
            foreach (var entry in searchResultFilenames)
            {
                result.Add(entry.Key, entry.Value.Count);
            }
            return result;
        }

        private void AddSearchResults(String file, Dictionary<string,DbPeptide> peptides, Dictionary<string,DbMsDataFile> dataFiles)
        {
            String baseMessage = _message;
            String extension = Path.GetExtension(file);
            IList<SearchResult> searchResults = null;
            String msDataFileName = null;
            if (searchResultFileType == SearchResultFileType.BlibBuild)
            {
                searchResults = SearchResults.ReadBlibBuild(file, ProgressMonitor);
            }
            else
            {
                using (var stream = File.OpenRead(file))
                {
                    switch (searchResultFileType)
                    {
                        default:
                            if (extension == ".sqt")
                            {
                                msDataFileName = Path.GetFileNameWithoutExtension(file);
                                searchResults = SearchResults.ReadSQT(msDataFileName, stream, ProgressMonitor);
                                searchResults = FilterSearchResults(peptides, searchResults);
                            }
                            else
                            {
                                if (file.ToLower().EndsWith(".pep.xml"))
                                {
                                    msDataFileName = Path.GetFileName(file);
                                    msDataFileName = msDataFileName.Substring(0, msDataFileName.Length - 8);
                                }
                                else
                                {
                                    msDataFileName = Path.GetFileNameWithoutExtension(file);
                                }
                                searchResults = SearchResults.ReadPepXml(msDataFileName, stream, ProgressMonitor);
                                searchResults = FilterSearchResults(peptides, searchResults);
                            }
                            break;
                        case SearchResultFileType.Dtaselect:
                            msDataFileName = null;
                            searchResults = SearchResults.ReadDTASelect(stream, ProgressMonitor);
                            break;
                        case SearchResultFileType.Percolator:
                            msDataFileName = null;
                            searchResults = SearchResults.ReadPercolatorOutput(stream, _maxQValue, ProgressMonitor);
                            break;
                        case SearchResultFileType.ListOfPeptides:
                            msDataFileName = null;
                            searchResults = SearchResults.ReadListOfPeptides(stream, ProgressMonitor);
                            break;
                    }
                }
            }
            if (searchResults == null)
            {
                return;
            }
            var dbPeptideSearchResults = GetSearchResults(msDataFileName);
            var modifiedPeptides = new HashSet<DbPeptide>();
            var searchResultsToInsert = new Dictionary<SearchResultKey, DbPeptideSearchResult>();
            var searchResultsToUpdate = new Dictionary<SearchResultKey, DbPeptideSearchResult>();
            using (var session = Workspace.OpenWriteSession())
            {
                var statementBuilder = new SqlStatementBuilder(session.GetSessionImplementation().Factory.Dialect);
                var dbWorkspace = Workspace.LoadDbWorkspace(session);
                session.BeginTransaction();
                var searchResultCountsBySequence = SearchResultCountsBySequence(searchResults);
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
                                                             {"Workspace", dbWorkspace.Id},
                                                             {"Protein", searchResult.Protein},
                                                             {"ProteinDescription", searchResult.ProteinDescription},
                                                             {"Sequence", trimmedSequence},
                                                             {"FullSequence", searchResult.Sequence},
                                                             {"SearchResultCount", searchResultCountsBySequence[trimmedSequence]},
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
                        Workspace.UpdateDataDirectoryFromSearchResultDirectory(Path.GetDirectoryName(file),
                                                                             searchResult.Filename);
                        dbMsDataFile = new DbMsDataFile
                        {
                            Name = searchResult.Filename,
                            Label = searchResult.Filename,
                            Workspace = dbWorkspace,
                        };
                        session.Save(dbMsDataFile);
                        dataFiles.Add(dbMsDataFile.Name, dbMsDataFile);
                    }
                    
                    var key = new SearchResultKey(dbMsDataFile, dbPeptide);
                    DbPeptideSearchResult dbPeptideSearchResult;
                    if (dbPeptideSearchResults.TryGetValue(key, out dbPeptideSearchResult))
                    {
                        if (dbPeptideSearchResult.FirstDetectedScan <= searchResult.ScanIndex &&
                            dbPeptideSearchResult.LastDetectedScan >= searchResult.ScanIndex &&
                            dbPeptideSearchResult.MinCharge <= searchResult.Charge &&
                            dbPeptideSearchResult.MaxCharge >= searchResult.Charge)
                        {
                            continue;
                        }
                        searchResultsToUpdate[key] = dbPeptideSearchResult;
                    }
                    else
                    {
                        if (!searchResultsToInsert.TryGetValue(key, out dbPeptideSearchResult))
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
                                                        PsmCount = 0,
                                                    };
                            searchResultsToInsert.Add(key, dbPeptideSearchResult);
                            if (!newPeptides.ContainsKey(trimmedSequence))
                            {
                                dbPeptide.SearchResultCount++;
                                modifiedPeptides.Add(dbPeptide);
                            }
                        }
                    }
                    dbPeptideSearchResult.FirstDetectedScan = Math.Min(dbPeptideSearchResult.FirstDetectedScan,
                                                                       searchResult.ScanIndex);
                    dbPeptideSearchResult.LastDetectedScan = Math.Max(dbPeptideSearchResult.LastDetectedScan,
                                                                      searchResult.ScanIndex);
                    dbPeptideSearchResult.MinCharge = Math.Min(dbPeptideSearchResult.MinCharge, searchResult.Charge);
                    dbPeptideSearchResult.MaxCharge = Math.Max(dbPeptideSearchResult.MaxCharge, searchResult.Charge);
                    dbPeptideSearchResult.PsmCount++;
                }
                var statements = new List<string>();
                foreach (var dbPeptide in modifiedPeptides)
                {
                    statements.Add(statementBuilder.GetUpdateStatement("DbPeptide", 
                        new Dictionary<string, object>{{"SearchResultCount", dbPeptide.SearchResultCount}}, 
                        new Dictionary<string, object>{{"Id", dbPeptide.Id}}));
                    statements.Add(statementBuilder.GetInsertStatement("DbChangeLog",
                        new Dictionary<string, object>{
                            {"InstanceIdBytes", Workspace.InstanceId.ToByteArray()},
                            {"PeptideId", dbPeptide.Id}
                        }));
                }
                foreach (var dbPeptideSearchResult in searchResultsToInsert.Values)
                {
                    statements.Add(statementBuilder.GetInsertStatement("DbPeptideSearchResult",
                        new Dictionary<string, object> {
                            {"MsDataFile", dbPeptideSearchResult.MsDataFile.Id},
                            {"Peptide", dbPeptideSearchResult.Peptide.Id},
                            {"MinCharge", dbPeptideSearchResult.MinCharge},
                            {"MaxCharge", dbPeptideSearchResult.MaxCharge},
                            {"FirstDetectedScan", dbPeptideSearchResult.FirstDetectedScan},
                            {"LastDetectedScan", dbPeptideSearchResult.LastDetectedScan},
                            {"FirstTracerCount", dbPeptideSearchResult.FirstTracerCount},
                            {"LastTracerCount", dbPeptideSearchResult.LastTracerCount},
                            {"Version",1},
                            {"ValidationStatus", 0},
                            {"PsmCount", dbPeptideSearchResult.PsmCount}
                        }));
                }
                foreach (var dbPeptideSearchResult in searchResultsToUpdate.Values)
                {
                    statements.Add(statementBuilder.GetUpdateStatement("DbPeptideSearchResult",
                        new Dictionary<string, object> {
                            {"MinCharge", dbPeptideSearchResult.MinCharge},
                            {"MaxCharge", dbPeptideSearchResult.MaxCharge},
                            {"FirstDetectedScan", dbPeptideSearchResult.FirstDetectedScan},
                            {"LastDetectedScan", dbPeptideSearchResult.LastDetectedScan},
                            {"FirstTracerCount", dbPeptideSearchResult.FirstTracerCount},
                            {"LastTracerCount", dbPeptideSearchResult.LastTracerCount},
                            {"PsmCount", dbPeptideSearchResult.PsmCount},
                        },
                            new Dictionary<string, object> { { "Id", dbPeptideSearchResult.Id.Value } }
                        ));
                }
                statementBuilder.ExecuteStatements(session, statements);
                dbWorkspace.PeptideCount = peptides.Count;
                dbWorkspace.MsDataFileCount = dataFiles.Count;
                session.Update(dbWorkspace);
                UpdateProgress(baseMessage + "(Committing transaction", 99);
                session.Transaction.Commit();
            }
        }

        class SearchResultKey
        {
            public SearchResultKey(DbMsDataFile msDataFile, DbPeptide peptide) : this(msDataFile.Id.Value, peptide.Id.Value)
            {
            }
            public SearchResultKey(long msDataFileId, long peptideId)
            {
                MsDataFileId = msDataFileId;
                PeptideId = peptideId;
            }
            public long MsDataFileId { get; private set; }
            public long PeptideId { get; private set; }
            public override int GetHashCode()
            {
                int hashCode = MsDataFileId.GetHashCode();
                hashCode = hashCode * 31 + PeptideId.GetHashCode();
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
                return MsDataFileId.Equals(that.MsDataFileId) && PeptideId.Equals(that.PeptideId);
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
                    Workspace.Reconciler.Wake();
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

        private void cbxMinimumXCorr_CheckedChanged(object sender, EventArgs e)
        {
            _useMinXCorr = cbxMinimumXCorr.Checked;
            
            tbxMinXCorr1.Enabled = _useMinXCorr;
            tbxMinXCorr2.Enabled = _useMinXCorr;
            tbxMinXCorr3.Enabled = _useMinXCorr;
        }

        private void cbxOnlyExistingPeptides_CheckedChanged(object sender, EventArgs e)
        {
            _onlyExistingPeptides = cbxOnlyExistingPeptides.Checked;
        }

        private void AddSearchResults(IList<String> filenames, SearchResultFileType searchResultFileType)
        {
            _minXCorr1 = double.Parse(tbxMinXCorr1.Text);
            _minXCorr2 = double.Parse(tbxMinXCorr2.Text);
            _minXCorr3 = double.Parse(tbxMinXCorr3.Text);
            _maxQValue = double.Parse(tbxMaxQValue.Text);
            this.filenames = filenames;
            btnBiblioSpec.Enabled = false;
            btnChooseSqtFiles.Enabled = false;
            btnChooseDTASelect.Enabled = false;
            btnChoosePercolatorResults.Enabled = false;
            btnChoosePeptideList.Enabled = false;
            IsRunning = true;
            this.searchResultFileType = searchResultFileType;
            new Action(WorkBackground).BeginInvoke(null, null);
        }

        private void btnChooseFiles_Click(object sender, EventArgs e)
        {
            if (cbxOnlyExistingPeptides.Checked)
            {
                if (Workspace.Peptides.ChildCount == 0)
                {
                    MessageBox.Show(this, "This workspace does not contain any peptides.  Either uncheck the 'Only existing peptides' checkbox, or add DTASelect search results first.", Program.AppName);
                    return;
                }
            }
            Settings.Default.Reload();
            using (var openFileDialog = new OpenFileDialog
                {
                    Filter =
                        "Search results(*.sqt;*.pep.xml)|*.sqt;*.pep.xml",

                    Multiselect = true,
                    InitialDirectory = Settings.Default.SearchResultsDirectory
                })
            {
                if (openFileDialog.ShowDialog(this) == DialogResult.Cancel)
                {
                    return;
                }
                Settings.Default.SearchResultsDirectory = Path.GetDirectoryName(openFileDialog.FileName);
                Settings.Default.Save();
                AddSearchResults(openFileDialog.FileNames, SearchResultFileType.Sequest);
            }
        }

        private void btnChooseDTASelect_Click(object sender, EventArgs e)
        {
            Settings.Default.Reload();
            using (var openFileDialog = new OpenFileDialog
                                     {
                                         Filter = "DTASelect Filter Files (*filter*.txt)|*filter*.txt|All Files|*.*",
                                         Multiselect = true,
                                         InitialDirectory = Settings.Default.SearchResultsDirectory
                                     })
            {
                if (openFileDialog.ShowDialog(this) == DialogResult.Cancel)
                {
                    return;
                }
                Settings.Default.SearchResultsDirectory = Path.GetDirectoryName(openFileDialog.FileName);
                Settings.Default.Save();
                AddSearchResults(openFileDialog.FileNames, SearchResultFileType.Dtaselect);
            }
        }

        private void btnChoosePercolatorResults_Click(object sender, EventArgs e)
        {
            Settings.Default.Reload();
            using (var openFileDialog = new OpenFileDialog
                                     {
                                         Filter =
                                             "Percolator Combined Results|combined-results.xml;combined-results.perc.xml|All Files|*.*",
                                         Multiselect = true,
                                         InitialDirectory = Settings.Default.SearchResultsDirectory
                                     })
            {
                if (openFileDialog.ShowDialog(this) == DialogResult.Cancel)
                {
                    return;
                }
                Settings.Default.SearchResultsDirectory = Path.GetDirectoryName(openFileDialog.FileName);
                Settings.Default.Save();
                AddSearchResults(openFileDialog.FileNames, SearchResultFileType.Percolator);
            }
        }

        private void btnChoosePeptideList_Click(object sender, EventArgs e)
        {
            Settings.Default.Reload();
            using (var openFileDialog = new OpenFileDialog
            {
                Filter =
                    "All Files|*.*",
                Multiselect = true,
                InitialDirectory = Settings.Default.SearchResultsDirectory
            })
            {
                if (openFileDialog.ShowDialog(this) == DialogResult.Cancel)
                {
                    return;
                }
                Settings.Default.SearchResultsDirectory = Path.GetDirectoryName(openFileDialog.FileName);
                Settings.Default.Save();
                AddSearchResults(openFileDialog.FileNames, SearchResultFileType.ListOfPeptides);
            }
        }

        private void btnBiblioSpec_Click(object sender, EventArgs e)
        {
            Settings.Default.Reload();
            using (var openFileDialog = new OpenFileDialog
                                     {
                                         Filter =
                                             "Supported Files|*.sqt;*.pep.xml;*.pepXML;*.blib;*.idpXML;*.dat;*.ssl;*.mzid;*.perc.xml;*final_fragment.csv",
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
                AddSearchResults(openFileDialog.FileNames, SearchResultFileType.BlibBuild);
            }
        }
    }

    enum SearchResultFileType
    {
        Dtaselect,
        Percolator,
        Sequest,
        ListOfPeptides,
        BlibBuild,
    }
}
