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
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using log4net;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Common.Chemistry;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Search;
using pwiz.Topograph.Util;
using pwiz.Topograph.MsData;

namespace pwiz.Topograph.Model
{
    public class Workspace
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof (Workspace));
        private readonly ChromatogramGenerator _chromatogramGenerator;
        private readonly ResultCalculator _resultCalculator;
        private readonly Reconciler _reconciler;
        private Modifications _modifications;
        private WorkspaceSettings _settings;
        private AminoAcidFormulas _aminoAcidFormulas;
        private EntitiesChangedEventArgs _entitiesChangedEventArgs;
        private readonly HashSet<PeptideAnalysis> _dirtyPeptideAnalyses = new HashSet<PeptideAnalysis>();
        private TracerDefs _tracerDefs;
        private IList<TracerDef> _tracerDefList;
        private ActionInvoker _actionInvoker;
        public Workspace(String path)
        {
            InstanceId = Guid.NewGuid();
            DatabasePath = path;
            DatabaseLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
            WorkspaceLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
            WorkspaceVersion = new WorkspaceVersion();
            SavedWorkspaceVersion = new WorkspaceVersion();
            if (Path.GetExtension(path) == TpgLinkDef.Extension)
            {
                TpgLinkDef = TpgLinkDef.Load(path);
                SessionFactory = SessionFactoryFactory.CreateSessionFactory(TpgLinkDef, 0);
                QuerySessionFactory = SessionFactoryFactory.CreateSessionFactory(
                    TpgLinkDef, SessionFactoryFlags.remove_binary_columns);
            }
            else
            {
                SessionFactory = SessionFactoryFactory.CreateSessionFactory(path, 0);
                QuerySessionFactory = SessionFactoryFactory.CreateSessionFactory(
                    path, SessionFactoryFlags.remove_binary_columns);
            }
            _chromatogramGenerator = new ChromatogramGenerator(this);
            _resultCalculator = new ResultCalculator(this);
            _reconciler = new Reconciler(this);
            using (var session = OpenSession())
            {
                DbWorkspaceId = ((DbWorkspace)session.CreateCriteria(typeof(DbWorkspace)).UniqueResult()).Id.Value;
                var dbWorkspace = LoadDbWorkspace(session);
                _modifications = new Modifications(this, dbWorkspace);
                _settings = new WorkspaceSettings(this, dbWorkspace);
                _tracerDefs = new TracerDefs(this, dbWorkspace);
                PeptideAnalyses = new PeptideAnalyses(this, dbWorkspace);
                MsDataFiles = new MsDataFiles(this, dbWorkspace);
                Peptides = new Peptides(this, dbWorkspace);
            }
        }

        public long DbWorkspaceId { get; private set; }
        public DbWorkspace LoadDbWorkspace(ISession session)
        {
            return session.Load<DbWorkspace>(DbWorkspaceId);
        }
        public PeptideAnalyses PeptideAnalyses { get; private set; }
        public MsDataFiles MsDataFiles { get; private set; }
        public Peptides Peptides { get; private set; }
        public String DatabasePath { get; private set; }
        public Guid InstanceId { get; private set; }
        public TpgLinkDef TpgLinkDef { get; private set; }
        public ISession OpenSession()
        {
            return SessionFactory.OpenSession();
        }
        /// <summary>
        /// Returns an ISession which has some binary columns removed so that some queries
        /// run faster.
        /// </summary>
        public ISession OpenQuerySession()
        {
            return QuerySessionFactory.OpenSession();
        }
        public ISession OpenWriteSession()
        {
            return SessionFactory.OpenSession();
        }
        public ISessionFactory SessionFactory
        {
            get; private set;
        }
        public ISessionFactory QuerySessionFactory
        {
            get; private set;
        }

        public ReaderWriterLockSlim DatabaseLock
        {
            get; private set;
        }
        public ReaderWriterLockSlim WorkspaceLock { get; private set; }
        public bool IsLoaded { get; private set; }
        public void Load(DbWorkspace dbWorkspace, ICollection<DbSetting> settings, ICollection<DbModification> modifications, ICollection<DbTracerDef> tracerDefs)
        {
            var savedWorkspaceVersion = WorkspaceVersion;
            savedWorkspaceVersion = _settings.MergeChildren(dbWorkspace, savedWorkspaceVersion, settings.ToDictionary(s => s.Name));
            savedWorkspaceVersion = _modifications.MergeChildren(dbWorkspace, savedWorkspaceVersion,
                                                                 modifications.ToDictionary(m => m.Symbol));
            savedWorkspaceVersion = _tracerDefs.MergeChildren(dbWorkspace, savedWorkspaceVersion,
                                                              tracerDefs.ToDictionary(t => t.Name));
            var currentWorkspaceVersion = savedWorkspaceVersion;
            currentWorkspaceVersion = _settings.CurrentWorkspaceVersion(currentWorkspaceVersion);
            currentWorkspaceVersion = _modifications.CurrentWorkspaceVersion(currentWorkspaceVersion);
            currentWorkspaceVersion = _tracerDefs.CurrentWorkspaceVersion(currentWorkspaceVersion);
            
            SavedWorkspaceVersion = savedWorkspaceVersion;
            SetWorkspaceVersion(currentWorkspaceVersion);
            if (!IsLoaded)
            {
                IsLoaded = true;
                var listener = WorkspaceLoaded;
                if (listener != null)
                {
                    listener.Invoke(this);
                }
            }
            DispatchDirtyEvent();
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
            using(GetReadLock())
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

        public IDictionary<String, double> Modifications
        {
            get
            {
                var result = new SortedDictionary<String, double>();
                foreach (var modification in _modifications.ListChildren())
                {
                    result.Add(modification.Symbol, modification.DeltaMass);
                }
                return result;
            }
            set
            {
                using(GetWriteLock())
                {
                    _aminoAcidFormulas = null;
                    var modificationsDict = new Dictionary<String, DbModification>();
                    foreach (var entry in value)
                    {
                        modificationsDict.Add(entry.Key, new DbModification {Symbol = entry.Key, DeltaMass = entry.Value});
                    }

                    var workspaceVersion = _modifications.UpdateFromUi(WorkspaceVersion, modificationsDict);
                    SetWorkspaceVersion(workspaceVersion);
                }
            }
        }

        public int GetMinTracerCount()
        {
            return _settings.GetSetting(SettingEnum.min_tracer_count, 0);
        }

        public void SetMinTracerCount(int count)
        {
            if (count == GetMinTracerCount())
            {
                return;
            }
            _settings.SetSetting(SettingEnum.min_tracer_count, count);
        }

        public String GetExcludeAas()
        {
            return _settings.GetSetting(SettingEnum.exclude_aas, "");
        }

        public void SetExcludeAas(String excludeAas)
        {
            if (excludeAas == GetExcludeAas())
            {
                return;
            }
            _settings.SetSetting(SettingEnum.exclude_aas, excludeAas);
        }

        public double GetMassAccuracy()
        {
            return _settings.GetSetting(SettingEnum.mass_accuracy, 200000.0);
        }

        public void SetMassAccuracy(double massAccuracy)
        {
            if (massAccuracy == GetMassAccuracy())
            {
                return;
            }
            _settings.SetSetting(SettingEnum.mass_accuracy, massAccuracy);
            SetWorkspaceVersion(WorkspaceVersion.IncChromatogramPeakVersion());
        }

        public bool GetErrOnSideOfLowerAbundance()
        {
            return _settings.GetSetting(SettingEnum.err_on_side_of_lower_abundance, false);
        }

        public void SetErrOnSideOfLowerAbundance(bool b)
        {
            if (b == GetErrOnSideOfLowerAbundance())
            {
                return;
            }
            _settings.SetSetting(SettingEnum.err_on_side_of_lower_abundance, b);
            SetWorkspaceVersion(WorkspaceVersion.IncEnrichmentVersion());
        }

        public PeptideQuantity GetDefaultPeptideQuantity()
        {
            return _settings.GetSetting(SettingEnum.default_peptide_quantity, PeptideQuantity.tracer_count);
        }

        public void SetDefaultPeptideQuantity(PeptideQuantity peptideQuantity)
        {
            if (peptideQuantity == GetDefaultPeptideQuantity())
            {
                return;
            }
            _settings.SetSetting(SettingEnum.default_peptide_quantity, peptideQuantity);
            SetWorkspaceVersion(WorkspaceVersion.IncEnrichmentVersion());
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

        public List<DbTracerDef> GetDbTracerDefs()
        {
            var result = new List<DbTracerDef>();
            foreach (var tracerDefModel in TracerDefs.ListChildren())
            {
                result.Add(tracerDefModel.ToDbTracerDef());
            }
            return result;
        }
        
        public void SetDbTracerDefs(IList<DbTracerDef> tracerDefs)
        {
            using(GetWriteLock())
            {
                var newWorkspaceVersion = _tracerDefs.UpdateFromUi(WorkspaceVersion, tracerDefs.ToDictionary(t => t.Name));
                SetWorkspaceVersion(newWorkspaceVersion);
            }
        }
        public IList<TracerDef> GetTracerDefs()
        {
            using(GetReadLock())
            {
                if (_tracerDefList != null)
                {
                    return _tracerDefList;
                }
                var tracerDefs = new List<TracerDef>();
                foreach (var dbTracerDef in _tracerDefs.ListChildren())
                {
                    tracerDefs.Add(new TracerDef(this, dbTracerDef));
                }
                _tracerDefList = new ReadOnlyCollection<TracerDef>(tracerDefs);
                return _tracerDefList;
            }
        }
        internal TracerDefs TracerDefs { get { return _tracerDefs; } }
        internal Modifications ModificationsList { get { return _modifications; } }

        private EntitiesChangedEventArgs EnsureEntitiesChangedEventArgs()
        {
            if (!WorkspaceLock.IsWriteLockHeld)
            {
                throw new InvalidOperationException("Must have workspace writer lock");
            }
            if (_entitiesChangedEventArgs == null)
            {
                _entitiesChangedEventArgs = new EntitiesChangedEventArgs();
                if (_actionInvoker != null)
                {
                    _actionInvoker.Invoke(DispatchChangeEvent);
                }
            }
            return _entitiesChangedEventArgs;
        }

        private void DispatchChangeEvent()
        {
            EntitiesChangeListener entitiesChangedEvent;
            EntitiesChangedEventArgs entitiesChangedEventArgs;
            lock(this)
            {
                entitiesChangedEvent = EntitiesChange;
                entitiesChangedEventArgs = _entitiesChangedEventArgs;
                _entitiesChangedEventArgs = null;
            }
            if (entitiesChangedEvent == null || entitiesChangedEventArgs == null)
            {
                return;
            }
            entitiesChangedEventArgs.SetReadOnly();
            entitiesChangedEvent.Invoke(entitiesChangedEventArgs);
            if (entitiesChangedEventArgs.GetChangedEntities().Count == 0)
            {
                return;
            }
        }
        public void RemoveEntityModel(EntityModel key)
        {
            lock(this)
            {
                EnsureEntitiesChangedEventArgs().RemoveEntity(key);
            }
        }
        public ICollection<MsDataFile> GetMsDataFiles()
        {
            return MsDataFiles.ListChildren();
        }
        public void AddEntityModel(EntityModel key)
        {
            lock(this)
            {
                EnsureEntitiesChangedEventArgs().AddNewEntity(key);
            }
        }
        public void EntityChanged(EntityModel key)
        {
            lock(this)
            {
                EnsureEntitiesChangedEventArgs().AddChangedEntity(key);
                if (key is PeptideAnalysis)
                {
                    _dirtyPeptideAnalyses.Add((PeptideAnalysis) key);
                }
                else if (key is PeptideFileAnalysis)
                {
                    _dirtyPeptideAnalyses.Add(((PeptideFileAnalysis) key).PeptideAnalysis);
                }
                if (!IsDirty)
                {
                    if (key is WorkspaceSetting)
                    {
                        IsDirty = IsDirty || ((WorkspaceSetting) key).IsDirty();
                    }
                    if (key is MsDataFile)
                    {
                        IsDirty = IsDirty || ((MsDataFile) key).IsDirty();
                    }
                    else if (key is Peptide)
                    {
                        IsDirty = IsDirty || ((Peptide) key).IsDirty();
                    }
                    else if (_dirtyPeptideAnalyses.Count > 0)
                    {
                        IsDirty = true;
                    }
                }
            }
        }
        public void AddChangedPeptideAnalyses(IEnumerable<KeyValuePair<long,DbPeptideAnalysis>> peptideAnalyses)
        {
            lock(this)
            {
                EnsureEntitiesChangedEventArgs().AddChangedPeptideAnalyses(peptideAnalyses);
                foreach (var entry in peptideAnalyses)
                {
                    var peptideAnalysis = PeptideAnalyses.GetChild(entry.Key);
                    if (entry.Value == null)
                    {
                        PeptideAnalyses.RemoveChild(entry.Key);
                        if (peptideAnalysis != null)
                        {
                            EnsureEntitiesChangedEventArgs().RemoveEntity(peptideAnalysis);
                        }
                    }
                    else
                    {
                        PeptideAnalyses.AddChildId(entry.Key);
                        if (peptideAnalysis != null)
                        {
                            EnsureEntitiesChangedEventArgs().AddChangedEntity(peptideAnalysis);
                        }
                    }
                }
            }
        }
        public void GetChromatogramProgress(out String dataFileName, out int progress)
        {
            _chromatogramGenerator.GetProgress(out dataFileName, out progress);
        }
        public bool Save(ILongOperationUi longOperationUi)
        {
            var broker = new LongOperationBroker(Save, longOperationUi);
            broker.LaunchJob();
            if (broker.WasCancelled)
            {
                return false;
            }
            _reconciler.Wake();
            return true;
        }

        public void Save(LongOperationBroker longOperationBroker)
        {
            longOperationBroker.UpdateStatusMessage("Synchronizing with database changes");
            int reconcileAttempt = 1;
            while (true)
            {
                if (Reconciler.ReconcileNow())
                {
                    break;
                }
                reconcileAttempt++;
                longOperationBroker.UpdateStatusMessage("Synchronizing with database changes attempt #" + reconcileAttempt);
            }
            using (GetReadLock())
            {
                using (var session = OpenWriteSession())
                {
                    session.BeginTransaction();
                    if (!SavedWorkspaceVersion.ChromatogramsValid(WorkspaceVersion))
                    {
                        longOperationBroker.UpdateStatusMessage("Deleting chromatograms");
                        session.CreateSQLQuery("DELETE FROM DbChromatogram")
                            .ExecuteUpdate();
                        session.CreateSQLQuery("UPDATE DbPeptideFileAnalysis SET ChromatogramCount = 0, PeakCount = 0, PeptideDistributionCount = 0")
                            .ExecuteUpdate();
                        session.CreateSQLQuery("DELETE FROM DbLock").ExecuteUpdate();
                    }
                    if (!SavedWorkspaceVersion.PeaksValid(WorkspaceVersion))
                    {
                        longOperationBroker.UpdateStatusMessage("Deleting peaks");
                        session.CreateSQLQuery("DELETE FROM DbPeak").ExecuteUpdate();
                        if (SavedWorkspaceVersion.ChromatogramsValid(WorkspaceVersion))
                        {
                            session.CreateSQLQuery("UPDATE DbPeptideFileAnalysis SET PeakCount = 0, PeptideDistributionCount = 0")
                                .ExecuteUpdate();
                        }
                        session.CreateSQLQuery(
                            "UPDATE DbPeptideFileAnalysis SET PeakStart = NULL, PeakEnd = NULL WHERE DbPeptideFileAnalysis.AutoFindPeak")
                            .ExecuteUpdate();
                    }
                    if (!SavedWorkspaceVersion.DistributionsValid(WorkspaceVersion))
                    {
                        longOperationBroker.UpdateStatusMessage("Deleting results");
                        session.CreateSQLQuery("DELETE FROM DbPeptideAmount")
                            .ExecuteUpdate();
                        session.CreateSQLQuery("DELETE FROM DbPeptideDistribution")
                            .ExecuteUpdate();
                        if (SavedWorkspaceVersion.PeaksValid(WorkspaceVersion))
                        {
                            session.CreateSQLQuery("UPDATE DbPeptideFileAnalysis SET PeptideDistributionCount = 0")
                                .ExecuteUpdate();
                        }
                        if (SavedWorkspaceVersion.ChromatogramsValid(WorkspaceVersion))
                        {
                            session.CreateSQLQuery("DELETE FROM DbLock WHERE LockType = " + (int)LockType.results).ExecuteUpdate();
                        }
                    }
                    bool workspaceChanged = false;
                    longOperationBroker.UpdateStatusMessage("Saving tracer definitions");
                    workspaceChanged = _tracerDefs.SaveChildren(session) || workspaceChanged;
                    longOperationBroker.UpdateStatusMessage("Saving modifications");
                    workspaceChanged = _modifications.SaveChildren(session) || workspaceChanged;
                    longOperationBroker.UpdateStatusMessage("Saving settings");
                    workspaceChanged = _settings.SaveChildren(session) || workspaceChanged;
                    longOperationBroker.UpdateStatusMessage("Saving data files");
                    MsDataFiles.SaveChildren(session);
                    longOperationBroker.UpdateStatusMessage("Saving peptides");
                    Peptides.SaveChildren(session);
                    longOperationBroker.UpdateStatusMessage("Saving peptide analyses");
                    foreach (var peptideAnalysis in _dirtyPeptideAnalyses)
                    {
                        if (longOperationBroker.WasCancelled)
                        {
                            return;
                        }
                        peptideAnalysis.SaveDeep(session);
                    }
                    if (workspaceChanged)
                    {
                        session.Save(new DbChangeLog(this));
                    }
                    longOperationBroker.SetIsCancelleable(false);
                    session.Transaction.Commit();
                    CheckDirty();
                }
            }
        }
        public void SetActionInvoker(ActionInvoker actionInvoker)
        {
            lock(this)
            {
                _actionInvoker = actionInvoker;
                if (actionInvoker != null)
                {
                    actionInvoker.Invoke(DispatchChangeEvent);
                }
            }
            if (actionInvoker != null) 
            {
                _reconciler.Start();
                _resultCalculator.Start();
                _chromatogramGenerator.Start();
            }
            else
            {
                _chromatogramGenerator.Stop();
                _resultCalculator.Stop();
                _reconciler.Stop();
            }
        }

        public event EntitiesChangeListener EntitiesChange;
        public event WorkspaceDirtyListener WorkspaceDirty;
        public event WorkspaceLoadedListener WorkspaceLoaded; 
        public WorkspaceVersion WorkspaceVersion { get; private set; }
        public WorkspaceVersion SavedWorkspaceVersion { get; private set; }
        private void SetWorkspaceVersion(WorkspaceVersion newWorkspaceVersion)
        {
            if (newWorkspaceVersion.Equals(WorkspaceVersion))
            {
                return;
            }
            _tracerDefList = null;
            WorkspaceVersion = newWorkspaceVersion;
            foreach (var peptideAnalysis in PeptideAnalyses.ListChildren())
            {
                peptideAnalysis.SetWorkspaceVersion(newWorkspaceVersion);
            }
            EnsureEntitiesChangedEventArgs();
            IsDirty = true;
        }
        public bool SaveIfNotDirty(PeptideAnalysis peptideAnalysis)
        {
            try {
                using (GetReadLock())
                {
                    using (var session = OpenWriteSession())
                    {
                        lock (this)
                        {
                            if (_dirtyPeptideAnalyses.Contains(peptideAnalysis) || !Equals(SavedWorkspaceVersion, WorkspaceVersion))
                            {
                                return false;
                            }
                            if (_dirtyPeptideAnalyses.Contains(peptideAnalysis) || !Equals(SavedWorkspaceVersion, WorkspaceVersion))
                            {
                                return false;
                            }
                        }
                        session.BeginTransaction();
                        foreach (var peptideFileAnalysis in peptideAnalysis.GetFileAnalyses(false))
                        {
                            peptideFileAnalysis.Peaks.Save(session);
                            peptideFileAnalysis.PeptideDistributions.Save(session);
                        }
                        session.Transaction.Commit();
                        return true;
                    }
                }
            }
            finally
            {
                using (GetWriteLock())
                {
                    lock(this)
                    {
                        EnsureEntitiesChangedEventArgs().AddChangedEntity(peptideAnalysis);
                    }
                }
            }
        }

        public string GetDataDirectory()
        {
            if (TpgLinkDef != null)
            {
                return TpgLinkDef.DataDirectory;
            }
            return _settings.GetSetting(SettingEnum.data_directory, (string) null);
        }

        public void SetDataDirectory(string directory)
        {
            using (GetWriteLock())
            {
                if (directory == GetDataDirectory())
                {
                    return;
                }
                if (TpgLinkDef != null)
                {
                    TpgLinkDef.DataDirectory = directory;
                    TpgLinkDef.Save(DatabasePath);
                    return;
                }
                _settings.SetSetting(SettingEnum.data_directory, directory);
            }
        }

        private void DispatchDirtyEvent()
        {
            var action = WorkspaceDirty;
            if (action != null && _actionInvoker != null)
            {
                _actionInvoker.Invoke(()=>action.Invoke(this));
            }
        }

        private bool _isDirty;
        public bool IsDirty 
        { 
            get
            {
                return _isDirty;
            } 
            private set
            {
                lock(this)
                {
                    if (_isDirty == value)
                    {
                        return;
                    }
                    _isDirty = value;
                    DispatchDirtyEvent();
                }
            } 
        }

        public ResultCalculator ResultCalculator { get { return _resultCalculator; } }
        public ChromatogramGenerator ChromatogramGenerator { get { return _chromatogramGenerator; } }
        public Reconciler Reconciler { get { return _reconciler; } }
        public WorkspaceSettings Settings 
        { 
            get
            {
                return _settings;
            } 
        }

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

        public AutoLock GetReadLock()
        {
            return new AutoLock(WorkspaceLock, false);
        }
        public AutoLock GetWriteLock()
        {
            return new AutoLock(WorkspaceLock, true);
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
        public void CheckDirty()
        {
            foreach (var peptideAnalysis in _dirtyPeptideAnalyses.ToArray())
            {
                if (!peptideAnalysis.IsDirty())
                {
                    _dirtyPeptideAnalyses.Remove(peptideAnalysis);
                }
            }
            bool isDirty = _dirtyPeptideAnalyses.Count > 0
                           || _settings.IsDirty()
                           || _tracerDefs.IsDirty()
                           || _modifications.IsDirty()
                           || Peptides.IsDirty()
                           || MsDataFiles.IsDirty();
            IsDirty = isDirty;
        }
        public string GetDataFilePath(String msDataFileName)
        {
            return GetDataFilePath(msDataFileName, GetDataDirectory());
        }
        private string GetDataFilePath(String msDataFileName, String dataDirectory)
        {
            if (string.IsNullOrEmpty(dataDirectory))
            {
                return null;
            }
            foreach (var ext in new[] { ".RAW", ".mzML", ".mzXML" })
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
            foreach (var msDataFile in MsDataFiles.ListChildren())
            {
                if (GetDataFilePath(msDataFile.Name, path) != null)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public delegate void EntitiesChangeListener(EntitiesChangedEventArgs entitiesChangedEventArgs);

    public delegate void WorkspaceDirtyListener(Workspace workspace);

    public delegate void WorkspaceLoadedListener(Workspace workspace);

    public delegate void ActionInvoker(Action action);

}