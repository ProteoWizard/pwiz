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
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Common.Chemistry;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model.Data;
using pwiz.Topograph.Util;
using pwiz.Topograph.MsData;

namespace pwiz.Topograph.Model
{
    public class Workspace : IDisposable
    {
        private readonly Workspace _owner;
        private ChromatogramGenerator _chromatogramGenerator;
        private ResultCalculator _resultCalculator;
        private DatabasePoller _databasePoller;
        private AminoAcidFormulas _aminoAcidFormulas;
        private IList<TracerDef> _tracerDefList;
        private TaskScheduler _taskScheduler;
        private ICollection<long> _rejectedMsDataFileIds = new HashSet<long>();
        private readonly IDictionary<Guid, DateTime> _lastChangeLogs = new Dictionary<Guid, DateTime>();
        private TpgLinkDef _tpgLinkDef;
        private ISessionFactory _sessionFactory;
        private WorkspaceData _savedWorkspaceData;
        private WorkspaceData _data;
        private Workspace()
        {
            _data = _savedWorkspaceData = new WorkspaceData();
            Modifications = new Modifications(this);
            Settings = new WorkspaceSettings(this);
            TracerDefs = new TracerDefs(this);
            PeptideAnalyses = new PeptideAnalyses(this);
            Peptides = new Peptides(this);
            MsDataFiles = new MsDataFiles(this);
            RetentionTimeAlignments = new RetentionTimeAlignments(Data);
        }
        public Workspace(String path) : this()
        {
            _owner = this;
            InstanceId = Guid.NewGuid();
            DatabasePath = path;
            DatabaseLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
            _savedWorkspaceData = _data = new WorkspaceData();
        }

        public Workspace(Workspace workspace) : this()
        {
            _owner = workspace._owner;
            InstanceId = workspace.InstanceId;
            DatabasePath = workspace.DatabasePath;
            DatabaseLock = workspace.DatabaseLock;
            _tpgLinkDef = workspace.TpgLinkDef;
            _data = workspace._data;
            _savedWorkspaceData = workspace._savedWorkspaceData;
            RetentionTimeAlignments = new RetentionTimeAlignments(Data);
            RetentionTimeAlignments.MergeFrom(workspace.RetentionTimeAlignments);
        }

        public WorkspaceData Data
        {
            get { return _data; }
            set
            {
                SetData(value, _savedWorkspaceData);
            }
        }
        public WorkspaceData SavedData
        {
            get { return _savedWorkspaceData; }
        }

        public void SetData(WorkspaceData data, WorkspaceData savedData)
        {
            if (Equals(Data, data) && Equals(SavedData, savedData))
            {
                return;
            }
            var workspaceChange = new WorkspaceChangeArgs(Data, SavedData);
            _data = data;
            _savedWorkspaceData = savedData;
            _tracerDefList = null;
            RetentionTimeAlignments.SetData(Data);
            Settings.Update(workspaceChange);
            Modifications.Update(workspaceChange);
            TracerDefs.Update(workspaceChange);
            Peptides.Update(workspaceChange);
            MsDataFiles.Update(workspaceChange);
            PeptideAnalyses.Update(workspaceChange);
            var changeHandlers = Change;
            if (null != changeHandlers)
            {
                changeHandlers(this, workspaceChange);
            }
        }

        public DbWorkspace LoadDbWorkspace(ISession session)
        {
            return session.Load<DbWorkspace>(Data.DbWorkspaceId);
        }
        public PeptideAnalyses PeptideAnalyses { get; private set; }

        public Workspace Clone()
        {
            if (null == _taskScheduler)
            {
                return new Workspace(this);
            }
            var task = new Task<Workspace>(()=>new Workspace(this));
            task.Start(_taskScheduler);
            return task.Result;
        }

        public MsDataFiles MsDataFiles { get; private set; }
        public Peptides Peptides { get; private set; }
        public String DatabasePath { get; private set; }
        public Guid InstanceId { get; private set; }
        public TpgLinkDef TpgLinkDef { get { return _tpgLinkDef; } }
        public RetentionTimeAlignments RetentionTimeAlignments { get; set; }
        public ISession OpenSession()
        {
            return SessionFactory.OpenSession();
        }
        public WorkspaceChangeArgs CompareSettings(Workspace workspace)
        {
            WorkspaceChangeArgs workspaceChange = new WorkspaceChangeArgs(workspace);
            DiffSettings(workspaceChange);
            return workspaceChange;
        }

        public ISession OpenWriteSession()
        {
            return SessionFactory.OpenSession();
        }
        public ISessionFactory SessionFactory
        {
            get { return _owner._sessionFactory; }
        }
        private void OpenSessionFactory()
        {
            if (!ReferenceEquals(this, _owner))
            {
                return;
            }
            if (SessionFactory != null)
            {
                throw new InvalidOperationException("SessionFactory is already open");
            }
            if (Path.GetExtension(DatabasePath) == TpgLinkDef.Extension)
            {
                _tpgLinkDef = TpgLinkDef.Load(DatabasePath);
                _sessionFactory = SessionFactoryFactory.CreateSessionFactory(TpgLinkDef, 0);
            }
            else
            {
                _sessionFactory = SessionFactoryFactory.CreateSessionFactory(DatabasePath, 0);
            }
        }
        private void CloseSessionFactory()
        {
            if (!ReferenceEquals(this, _owner))
            {
                return;
            }
            if (SessionFactory != null)
            {
                using (var session = SessionFactory.OpenSession())
                {
                    session.BeginTransaction();
                    var criteria = session.CreateCriteria(typeof (DbLock))
                        .Add(Restrictions.Eq("InstanceIdBytes", InstanceId.ToByteArray()));
                    foreach (DbLock dbLock in criteria.List())
                    {
                        session.Delete(dbLock);
                    }
                    session.Transaction.Commit();
                }
                SessionFactory.Close();
                _sessionFactory = null;
            }
        }

        public ReaderWriterLockSlim DatabaseLock
        {
            get; private set;
        }
        public bool IsLoaded { get { return null != Data.PeptideAnalyses; }
        }
        public void Merge(WorkspaceData newData)
        {
            var newSavedData = newData;
            newData = Settings.Merge(newData);
            newData = Modifications.Merge(newData);
            newData = TracerDefs.Merge(newData);
            newData = Peptides.Merge(newData);
            newData = MsDataFiles.Merge(newData);
            newData = PeptideAnalyses.Merge(newData);
            SetData(newData, newSavedData);
        }

        public String ConnectionString
        {
            get
            {
                return new SQLiteConnectionStringBuilder
                           {
                               DataSource = DatabasePath
                           }.ToString();   
            }
        }

        public AminoAcidFormulas GetAminoAcidFormulas()
        {
            if (_aminoAcidFormulas != null)
            {
                return _aminoAcidFormulas;
            }
            var massShifts = new Dictionary<char, double>();
            foreach (var modification in Modifications)
            {
                massShifts[modification.Key[0]] = modification.Value;
            }
            return _aminoAcidFormulas = AminoAcidFormulas.Default.SetMassShifts(massShifts);
        }

        /// <summary>
        /// Returns the AminoAcidFormulas with elements added for each of the tracers.
        /// </summary>
        public AminoAcidFormulas GetAminoAcidFormulasWithTracers()
        {
            var aminoAcidFormulas = GetAminoAcidFormulas();
            foreach (var tracerDef in GetTracerDefs())
            {
                aminoAcidFormulas = tracerDef.AddTracerToAminoAcidFormulas(aminoAcidFormulas);
            }
            return aminoAcidFormulas;
        }

        public Modifications Modifications
        {
            get; private set;
        }

        public int GetMinTracerCount()
        {
            return Settings.GetSetting(SettingEnum.min_tracer_count, 0);
        }

        public void SetMinTracerCount(int count)
        {
            if (count == GetMinTracerCount())
            {
                return;
            }
            Settings.SetSetting(SettingEnum.min_tracer_count, count);
        }

        public String GetExcludeAas()
        {
            return Settings.GetSetting(SettingEnum.exclude_aas, "");
        }

        public void SetExcludeAas(String excludeAas)
        {
            if (excludeAas == GetExcludeAas())
            {
                return;
            }
            Settings.SetSetting(SettingEnum.exclude_aas, excludeAas);
        }

        public String GetProteinDescriptionKey()
        {
            return Settings.GetSetting(SettingEnum.protein_description_key, "");
        }

        public double GetMaxIsotopeRetentionTimeShift()
        {
            return Settings.GetSetting(SettingEnum.max_isotope_retention_time_shift, 1.0);
        }

        public void SetMaxIsotopeRetentionTimeShift(double value)
        {
            Settings.SetSetting(SettingEnum.max_isotope_retention_time_shift, value);
        }

        public double GetMinCorrelationCoefficient()
        {
            return Settings.GetSetting(SettingEnum.min_correlation_coeff, .9);
        }
        public void SetMinCorrelationCoefficient(double value)
        {
            Settings.SetSetting(SettingEnum.min_correlation_coeff, value);
        }
        public double GetMinDeconvolutionScoreForAvgPrecursorPool()
        {
            return Settings.GetSetting(SettingEnum.min_deconvolution_score_for_avg_precursor_pool, 0.0);
        }
        public void SetMinDeconvolutionScoreForAvgPrecursorPool(double value)
        {
            Settings.SetSetting(SettingEnum.min_deconvolution_score_for_avg_precursor_pool, value);
        }

        public bool GetAcceptSamplesWithoutMs2Id()
        {
            return Settings.GetSetting(SettingEnum.accept_samples_without_ms2_id, true);
        }
        public void SetAcceptSamplesWithoutMs2Id(bool value)
        {
            Settings.SetSetting(SettingEnum.accept_samples_without_ms2_id, value);
        }
        public double GetAcceptMinDeconvolutionScore()
        {
            return Settings.GetSetting(SettingEnum.accept_min_deconvolution_score, 0.0);
        }
        public void SetAcceptMinDeconvolutionScore(double value)
        {
            Settings.SetSetting(SettingEnum.accept_min_deconvolution_score, value);
        }
        public double GetAcceptMinAreaUnderChromatogramCurve()
        {
            return Settings.GetSetting(SettingEnum.accept_min_auc, 0.0);
        }
        public void SetAcceptMinAreaUnderChromatogramCurve(double value)
        {
            Settings.SetSetting(SettingEnum.accept_min_auc, value);
        }
        public IEnumerable<IntegrationNote> GetAcceptIntegrationNotes()
        {
            return IntegrationNote.ParseCollection(Settings.GetSetting(SettingEnum.accept_integration_notes, 
                IntegrationNote.ToString(new[]{IntegrationNote.Manual, IntegrationNote.Success,})));
        }
        public void SetAcceptIntegrationNotes(IEnumerable<IntegrationNote> integrationNotes)
        {
            Settings.SetSetting(SettingEnum.accept_integration_notes, IntegrationNote.ToString(integrationNotes));
        }
        public double GetAcceptMinTurnoverScore()
        {
            return Settings.GetSetting(SettingEnum.accept_min_turnover_score, 0.0);
        }

        public void SetAcceptMinTurnoverScore(double value)
        {
            Settings.SetSetting(SettingEnum.accept_min_turnover_score, value);
        }

        public void SetProteinDescriptionKey(String proteinDescriptionKey)
        {
            if (proteinDescriptionKey == GetProteinDescriptionKey())
            {
                return;
            }
            Settings.SetSetting(SettingEnum.protein_description_key, proteinDescriptionKey);
        }

        public string GetProteinKey(string proteinName, string proteinDescription)
        {
            String strRegex = GetProteinDescriptionKey();
            if (string.IsNullOrEmpty(strRegex))
            {
                return proteinName;
            }
            proteinDescription = proteinDescription ?? "";
            var regex = new Regex(strRegex);
            var parts = new List<String>();
            for (Match match = regex.Match(proteinDescription); match.Success; match = match.NextMatch())
            {
                string part = match.Groups.Count == 0 ? match.ToString() : match.Groups[0].ToString();
                if (!parts.Contains(part))
                {
                    parts.Add(part);
                }
            }
            if (parts.Count == 0)
            {
                return proteinName;
            }
            return string.Join(" ", parts.ToArray());
        }

        public double GetMassAccuracy()
        {
            return Settings.GetSetting(SettingEnum.mass_accuracy, 200000.0);
        }

        public void SetMassAccuracy(double massAccuracy)
        {
            Settings.SetSetting(SettingEnum.mass_accuracy, massAccuracy);
        }

        public double GetChromTimeAroundMs2Id()
        {
            return Settings.GetSetting(SettingEnum.chrom_time_around_ms2_id, 5.0);
        }

        public void SetChromTimeAroundMs2Id(double value)
        {
            Settings.SetSetting(SettingEnum.chrom_time_around_ms2_id, value);
        }

        public double GetExtraChromTimeWithoutMs2Id()
        {
            return Settings.GetSetting(SettingEnum.extra_chrom_time_without_ms2_id, 0.0);
        }

        public void SetExtraChromTimeWithoutMs2Id(double value)
        {
            Settings.SetSetting(SettingEnum.extra_chrom_time_without_ms2_id, value);
        }

        public bool GetErrOnSideOfLowerAbundance()
        {
            return Settings.GetSetting(SettingEnum.err_on_side_of_lower_abundance, false);
        }

        public void SetErrOnSideOfLowerAbundance(bool b)
        {
            Settings.SetSetting(SettingEnum.err_on_side_of_lower_abundance, b);
        }
        public string GetSetting(string name)
        {
            string value;
            Settings.Data.TryGetValue(name, out value);
            return value;
        }

        public bool IsExcluded(Peptide peptide)
        {
            return !FilterPeptides(new[] {peptide})
                .GetEnumerator()
                .MoveNext();
        }
        
        public IEnumerable<Peptide> FilterPeptides(IEnumerable<Peptide> peptides)
        {
            char[] excludeAas = GetExcludeAas().ToCharArray();
            int minTracerCount = GetMinTracerCount();
            foreach (var peptide in peptides)
            {
                if (peptide.MaxTracerCount < minTracerCount
                    || peptide.Sequence.IndexOfAny(excludeAas) >= 0)
                {
                    continue;
                }
                yield return peptide;
            }
        }

        public void UpdateDataDirectoryFromSearchResultDirectory(String searchResultDirectory, String filename)
        {
            if (GetDataDirectory() != null)
            {
                return;
            }
            var directory = searchResultDirectory;
            while(true)
            {
                if (GetDataFilePath(filename, directory) != null)
                {
                    SetDataDirectory(directory);
                    return;
                }
                var parent = Path.GetDirectoryName(directory);
                if (String.IsNullOrEmpty(parent) || parent == directory)
                {
                    return;
                }
                directory = parent;
            }
        }

        public TracerDefs TracerDefs { get; private set; }
        
        public IList<TracerDef> GetTracerDefs()
        {
            if (_tracerDefList != null)
            {
                return _tracerDefList;
            }
            var tracerDefs = new List<TracerDef>();
            foreach (var dbTracerDef in TracerDefs)
            {
                tracerDefs.Add(new TracerDef(this, dbTracerDef.Value));
            }
            _tracerDefList = new ReadOnlyCollection<TracerDef>(tracerDefs);
            return _tracerDefList;
        }

        public ICollection<MsDataFile> GetMsDataFiles()
        {
            return MsDataFiles;
        }
        public void GetChromatogramProgress(out String dataFileName, out int progress)
        {
            _chromatogramGenerator.GetProgress(out dataFileName, out progress);
        }
        public bool HasAnyChromatograms()
        {
            return PeptideAnalyses.Any(
                peptideAnalysis => peptideAnalysis.FileAnalyses.Any(
                    fileAnalysis => fileAnalysis.ChromatogramSetId.HasValue));
        }
        public bool Save(ILongOperationUi longOperationUi)
        {
            var broker = new LongOperationBroker(Save, longOperationUi);
            broker.LaunchJob();
            if (broker.WasCancelled)
            {
                return false;
            }
            _databasePoller.Wake();
            return true;
        }

        public void UpdateWorkspaceVersion(LongOperationBroker longOperationBroker, ISession session, WorkspaceChangeArgs savedWorkspaceChange)
        {
            if (savedWorkspaceChange.HasChromatogramMassChange)
            {
                longOperationBroker.UpdateStatusMessage("Deleting chromatograms");
                session.CreateSQLQuery("DELETE FROM DbChromatogram")
                    .ExecuteUpdate();
                session.CreateSQLQuery("UPDATE DbPeptideFileAnalysis SET ChromatogramSet = NULL, PeakCount = 0")
                    .ExecuteUpdate();
                session.CreateSQLQuery("DELETE FROM DbChromatogramSet").ExecuteUpdate();
                session.CreateSQLQuery("DELETE FROM DbLock").ExecuteUpdate();
            }
            if (savedWorkspaceChange.HasTurnoverChange)
            {
                session.CreateSQLQuery("UPDATE DbPeptideFileAnalysis SET TracerPercent = NULL")
                    .ExecuteUpdate();
                if (savedWorkspaceChange.HasPeakPickingChange)
                {
                    if (!savedWorkspaceChange.HasChromatogramMassChange)
                    {
                        session.CreateSQLQuery("UPDATE DbPeptideFileAnalysis SET PeakCount = 0")
                            .ExecuteUpdate();
                    }
                    longOperationBroker.UpdateStatusMessage("Deleting peaks");
                    session.CreateSQLQuery("DELETE FROM DbPeak").ExecuteUpdate();
                }
                if (!savedWorkspaceChange.HasChromatogramMassChange)
                {
                    session.CreateSQLQuery("DELETE FROM DbLock WHERE LockType = " + (int)LockType.results).ExecuteUpdate();
                }
            }
        }

        public void Save(LongOperationBroker longOperationBroker)
        {
            longOperationBroker.UpdateStatusMessage("Synchronizing with database changes");
            int reconcileAttempt = 1;
            while (true)
            {
                var dirtyPeptideAnalyses = PeptideAnalyses.ListDirty()
                    .ToDictionary(pa => pa.Id, pa => pa.ChromatogramsWereLoaded);
                if (DatabasePoller.TryLoadAndMergeChanges(dirtyPeptideAnalyses))
                {
                    break;
                }
                reconcileAttempt++;
                longOperationBroker.UpdateStatusMessage("Synchronizing with database changes attempt #" + reconcileAttempt);
            }
            using (var session = OpenWriteSession())
            {
                session.BeginTransaction();
                UpdateWorkspaceVersion(longOperationBroker, session, SavedWorkspaceChange);
                bool workspaceChanged = false;
                var dbWorkspace = LoadDbWorkspace(session);
                longOperationBroker.UpdateStatusMessage("Saving tracer definitions");
                workspaceChanged = TracerDefs.Save(session, dbWorkspace) || workspaceChanged;
                longOperationBroker.UpdateStatusMessage("Saving modifications");
                workspaceChanged = Modifications.Save(session, dbWorkspace) || workspaceChanged;
                longOperationBroker.UpdateStatusMessage("Saving settings");
                workspaceChanged = Settings.Save(session, dbWorkspace) || workspaceChanged;
                longOperationBroker.UpdateStatusMessage("Saving data files");
                foreach (var msDataFile in MsDataFiles.ListDirty())
                {
                    msDataFile.Save(session);
                }
                longOperationBroker.UpdateStatusMessage("Saving peptides");
                foreach (var peptide in Peptides.ListDirty())
                {
                    peptide.Save(session);
                }
                longOperationBroker.UpdateStatusMessage("Saving peptide analyses");
                foreach (var peptideAnalysis in PeptideAnalyses.ListDirty())
                {
                    if (longOperationBroker.WasCancelled)
                    {
                        return;
                    }
                    peptideAnalysis.Save(session);
                }
                if (workspaceChanged)
                {
                    session.Save(new DbChangeLog(this));
                }
                longOperationBroker.SetIsCancelleable(false);
                session.Transaction.Commit();
            }
        }
        public void SetTaskScheduler(TaskScheduler taskScheduler)
        {
            _taskScheduler = taskScheduler;
            if (_taskScheduler != null) 
            {
                if (SessionFactory == null)
                {
                    OpenSessionFactory();
                }
                _databasePoller = _databasePoller ?? new DatabasePoller(this);
                _databasePoller.Start();
                _resultCalculator = _resultCalculator ?? new ResultCalculator(this);
                _resultCalculator.Start();
                _chromatogramGenerator = _chromatogramGenerator ?? new ChromatogramGenerator(this);
                _chromatogramGenerator.Start();
            }
            else
            {
                if (null != _chromatogramGenerator)
                {
                    _chromatogramGenerator.Stop();
                }
                if (null != _resultCalculator)
                {
                    _resultCalculator.Stop();
                }
                if (null != _databasePoller)
                {
                    _databasePoller.Stop();
                }
                CloseSessionFactory();
            }
        }

        public TaskScheduler EventTaskScheduler { get { return _taskScheduler; } }
        public void RunOnEventQueue(Action action)
        {
            var task = new Task(action);
            task.Start(_taskScheduler ?? TaskScheduler.Default);
            task.Wait();
        }
        public T RunOnEventQueue<T>(Func<T> function)
        {
            var task = new Task<T>(function);
            task.Start(_taskScheduler ?? TaskScheduler.Default);
            return task.Result;
        }

        public WorkspaceChangeArgs SavedWorkspaceChange
        {
            get
            {
                var workspaceChange = new WorkspaceChangeArgs(SavedData, SavedData);
                DiffSettings(workspaceChange);
                return workspaceChange;
            }
        }
        public void DiffSettings(WorkspaceChangeArgs workspaceChange)
        {
            Settings.Diff(workspaceChange);
            Modifications.Diff(workspaceChange);
            TracerDefs.Diff(workspaceChange);
        }
        public string GetDataDirectory()
        {
            if (Data.Settings == null)
            {
                return null;
            }
            if (TpgLinkDef != null)
            {
                return TpgLinkDef.DataDirectory;
            }
            return Settings.GetSetting(SettingEnum.data_directory, (string) null);
        }

        public void SetDataDirectory(string directory)
        {
            if (directory == GetDataDirectory())
            {
                return;
            }
            ClearRejectedMsDataFiles();
            if (TpgLinkDef != null)
            {
                TpgLinkDef.DataDirectory = directory;
                TpgLinkDef.Save(DatabasePath);
                return;
            }
            Settings.SetSetting(SettingEnum.data_directory, directory);
        }

        public bool IsDirty 
        { 
            get
            {
                return Settings.IsDirty
                    || Modifications.IsDirty
                    || TracerDefs.IsDirty
                    || Peptides.IsDirty
                    || MsDataFiles.IsDirty
                    || PeptideAnalyses.IsDirty;
            } 
        }

        public ResultCalculator ResultCalculator { get { return _resultCalculator; } }
        public ChromatogramGenerator ChromatogramGenerator { get { return _chromatogramGenerator; } }
        public DatabasePoller DatabasePoller { get { return _databasePoller; } }
        public WorkspaceSettings Settings { get; private set; }

        public int GetMaxTracerCount(String sequence)
        {
            int result = 0;
            var elements = new HashSet<String>();
            foreach (var tracerDef in GetTracerDefs())
            {
                if (tracerDef.AminoAcidSymbol.HasValue)
                {
                    var newSequence = sequence.Replace("" + tracerDef.AminoAcidSymbol, "");
                    result += sequence.Length - newSequence.Length;
                    sequence = newSequence;
                }
                else
                {
                    elements.Add(tracerDef.TraceeSymbol);
                }
            }
            if (elements.Count == 0)
            {
                return result;
            }
            var formula = GetAminoAcidFormulas().GetFormula(sequence);
            foreach (var element in elements)
            {
                result += formula.GetElementCount(element);
            }
            return result;
        }

        public DatabaseTypeEnum DatabaseTypeEnum
        {
            get
            {
                if (TpgLinkDef == null)
                {
                    return DatabaseTypeEnum.sqlite;
                }
                return TpgLinkDef.DatabaseTypeEnum;
            }
        }
        public bool UseLongTransactions
        {
            get
            {
                return DatabaseTypeEnum == DatabaseTypeEnum.sqlite;   
            }
        }
        public bool UseDatabaseLock
        {
            get
            {
                return DatabaseTypeEnum == DatabaseTypeEnum.sqlite;
            }
        }

        public string GetDataFilePath(String msDataFileName)
        {
            return GetDataFilePath(msDataFileName, GetDataDirectory());
        }
        public static string GetDataFilePath(String msDataFileName, String dataDirectory)
        {
            if (string.IsNullOrEmpty(dataDirectory))
            {
                return null;
            }
            foreach (var ext in new[] { ".RAW", ".raw", ".wiff", ".mzML", ".mzXML" })
            {
                var path = Path.Combine(dataDirectory, msDataFileName + ext);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return null;
        }

        public bool IsValidDataDirectory(String path)
        {
            foreach (var msDataFile in MsDataFiles)
            {
                if (GetDataFilePath(msDataFile.Name, path) != null)
                {
                    return true;
                }
            }
            return false;
        }
        
        public void RejectMsDataFile(MsDataFile msDataFile)
        {
            _rejectedMsDataFileIds.Add(msDataFile.Id);
        }

        public bool IsRejected(MsDataFile msDataFile)
        {
            return _rejectedMsDataFileIds.Contains(msDataFile.Id);
        }
        public bool IsRejected(DbMsDataFile msDataFile)
        {
            return _rejectedMsDataFileIds.Contains(msDataFile.Id.Value);
        }

        public void ClearRejectedMsDataFiles()
        {
            _rejectedMsDataFileIds.Clear();
        }

        public bool IsShared
        {
            get { return DatabasePath.EndsWith("tpglnk"); }
        }

        public HalfLifeSettings GetHalfLifeSettings(HalfLifeSettings halfLifeSettings)
        {
            var tracerDef = GetTracerDefs().FirstOrDefault();
            if (tracerDef != null)
            {
                halfLifeSettings.InitialPrecursorPool = tracerDef.InitialApe;
                halfLifeSettings.CurrentPrecursorPool = tracerDef.FinalApe;
            }
            return halfLifeSettings;
        }

        public IList<PeptideAnalysis> ListOpenPeptideAnalyses()
        {
            return PeptideAnalyses.Where(peptideAnalysis => peptideAnalysis.GetChromatogramRefCount() > 0).ToArray();
        }

        public DateTime? GetLastChangeTime(Guid instanceId)
        {
            lock (_lastChangeLogs)
            {
                DateTime dateTime;
                if (_lastChangeLogs.TryGetValue(instanceId, out dateTime))
                {
                    return dateTime;
                }
                return null;
            }
        }

        public void UpdateLastChangeTime(Guid instanceId)
        {
            lock (_lastChangeLogs)
            {
                _lastChangeLogs[instanceId] = DateTime.Now;
            }
        }

        public void LockTables(IDbConnection connection)
        {
            if (DatabaseTypeEnum != DatabaseTypeEnum.mysql)
            {
                return;
            }
            string[] tables =
            {
                "DbChromatogram", "DbMsDataFile", "DbPeptide", "DbTracerDef", "DbWorkspace", "DbModification",
                "DbSetting", "DbPeak", "DbPeptideFileAnalysis", "DbChromatogramSet", "DbPeptideAnalysis", "DbChangeLog",
                "DbLock", "DbPeptideSpectrumMatch"
            };
            string sql = "LOCK TABLES " + string.Join(",", tables.Select(name => name + " WRITE"));
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        public void UnlockTables(IDbConnection connection)
        {
            if (DatabaseTypeEnum != DatabaseTypeEnum.mysql)
            {
                return;
            }
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "UNLOCK TABLES";
                cmd.ExecuteNonQuery();
            }
        }


        public void Dispose()
        {
            if (null != _chromatogramGenerator)
            {
                _chromatogramGenerator.Dispose();
                _chromatogramGenerator = null;
            }
            if (null != _databasePoller)
            {
                _databasePoller.Dispose();
                _databasePoller = null;
            }
            if (null != _resultCalculator)
            {
                _resultCalculator.Dispose();
                _resultCalculator = null;
            }
        }

        public event EventHandler<WorkspaceChangeArgs> Change;
    }

    public delegate void WorkspaceDirtyListener(Workspace workspace);

    public delegate void WorkspaceLoadedListener(Workspace workspace);

    public delegate void ActionInvoker(Action action);


}