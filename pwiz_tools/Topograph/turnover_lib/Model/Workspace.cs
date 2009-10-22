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
        private readonly Modifications _modifications;
        private readonly WorkspaceSettings _settings;
        private AminoAcidFormulas _aminoAcidFormulas;
        private EntitiesChangedEventArgs _entitiesChangedEventArgs;
        private readonly HashSet<PeptideAnalysis> _dirtyPeptideAnalyses = new HashSet<PeptideAnalysis>();
        private readonly HashSet<Peptide> _dirtyPeptides = new HashSet<Peptide>();
        private TracerDefs _tracerDefs;
        private IList<TracerDef> _tracerDefList;
        private ActionInvoker _actionInvoker;
        public Workspace(String path)
        {
            DatabasePath = path;
            DatabaseLock = new ReaderWriterLock();
            WorkspaceVersion = new WorkspaceVersion();
            SavedWorkspaceVersion = new WorkspaceVersion();
            SessionFactory = SessionFactoryFactory.CreateSessionFactory(path, false);
            _chromatogramGenerator = new ChromatogramGenerator(this);
            _resultCalculator = new ResultCalculator(this);
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
        public ISession OpenSessionWithoutLock()
        {
            return new SessionWithLock(SessionFactory.OpenSession(), DatabaseLock, SessionLockType.unlocked);
        }
        public ISession OpenSession()
        {
            return new SessionWithLock(SessionFactory.OpenSession(), DatabaseLock, SessionLockType.normal);
        }
        public ISession OpenWriteSession()
        {
            return new SessionWithLock(SessionFactory.OpenSession(), DatabaseLock, SessionLockType.write);
        }
        public ISessionFactory SessionFactory
        {
            get; private set;
        }

        public ReaderWriterLock DatabaseLock
        {
            get; private set;
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
            lock(Lock)
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
                lock(Lock)
                {
                    _aminoAcidFormulas = null;
                    _modifications.Clear();
                    foreach (var modification in value)
                    {
                        _modifications.AddChild(modification.Key, new DbModification
                        {
                            Symbol = modification.Key,
                            DeltaMass = modification.Value
                        });
                    }
                    SetWorkspaceVersion(WorkspaceVersion.IncMassVersion());
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

        public static String ResolveMsDataFilePath(String directory, String filename)
        {
            while(true)
            {
                String path = Path.Combine(directory, filename + ".RAW");
                _log.Debug("Checking existence of:" + path);
                if (File.Exists(path))
                {
                    return path;
                }
                var parent = Path.GetDirectoryName(directory);
                if (String.IsNullOrEmpty(parent) || parent == directory)
                {
                    return null;
                }
                directory = parent;
            }
        }

        public void SetTracerDefs(IList<DbTracerDef> tracerDefs)
        {
            lock (Lock)
            {
                var oldTracers = new Dictionary<KeyValuePair<String, double>, TracerDef>();
                foreach (var tracerDef in GetTracerDefs())
                {
                    oldTracers[new KeyValuePair<string, double>(tracerDef.TraceeSymbol, tracerDef.DeltaMass)] =
                        tracerDef;
                }
                var newWorkspaceVersion = WorkspaceVersion;
                if (tracerDefs.Count != oldTracers.Count)
                {
                    newWorkspaceVersion = newWorkspaceVersion.IncMassVersion();
                }
                else
                {
                    foreach (var dbTracerDef in tracerDefs)
                    {
                        TracerDef oldTracerDef;
                        if (!oldTracers.TryGetValue(
                                 new KeyValuePair<string, double>(dbTracerDef.TracerSymbol, dbTracerDef.DeltaMass),
                                 out oldTracerDef))
                        {
                            newWorkspaceVersion = newWorkspaceVersion.IncMassVersion();
                            break;
                        }
                        if (oldTracerDef.IsotopesEluteEarlier != dbTracerDef.IsotopesEluteEarlier
                            || oldTracerDef.IsotopesEluteLater != dbTracerDef.IsotopesEluteLater)
                        {
                            newWorkspaceVersion = newWorkspaceVersion.IncChromatogramPeakVersion();
                        }
                        if (oldTracerDef.Name != dbTracerDef.Name
                            || oldTracerDef.InitialApe != dbTracerDef.InitialEnrichment
                            || oldTracerDef.FinalApe != dbTracerDef.FinalEnrichment
                            || oldTracerDef.AtomCount != dbTracerDef.AtomCount
                            || oldTracerDef.AtomPercentEnrichment != dbTracerDef.AtomPercentEnrichment)
                        {
                            newWorkspaceVersion = newWorkspaceVersion.IncEnrichmentVersion();
                        }
                    }
                }
                _tracerDefs.Clear();
                _tracerDefList = null;
                foreach (var dbTracerDef in tracerDefs)
                {
                    _tracerDefs.AddChild(dbTracerDef.Name, dbTracerDef);
                }
                SetWorkspaceVersion(newWorkspaceVersion);
            }
        }
        public IList<TracerDef> GetTracerDefs()
        {
            lock(Lock)
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
        private EntitiesChangedEventArgs EnsureEntitiesChangedEventArgs()
        {
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
            lock (Lock)
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
            foreach (var msDataFile in entitiesChangedEventArgs.GetEntities<MsDataFile>())
            {
                if (entitiesChangedEventArgs.IsChanged(msDataFile))
                {
                    SetWorkspaceVersion(WorkspaceVersion.IncCohortVersion());
                    break;
                }
            }
        }
        public void RemoveEntityModel(EntityModel key)
        {
            lock(Lock)
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
            lock(Lock)
            {
                EnsureEntitiesChangedEventArgs().AddNewEntity(key);
            }
        }
        public void EntityChanged(EntityModel key)
        {
            lock(Lock)
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
                else if (key is Peptide)
                {
                    _dirtyPeptides.Add((Peptide) key);
                }
                if (!IsDirty)
                {
                    if (key is MsDataFile)
                    {
                        IsDirty = true;
                    }
                    else if (_dirtyPeptideAnalyses.Count > 0 || _dirtyPeptides.Count > 0)
                    {
                        IsDirty = true;
                    }
                }
            }
        }
        public void GetChromatogramProgress(out String dataFileName, out int progress)
        {
            _chromatogramGenerator.GetProgress(out dataFileName, out progress);
        }
        public void Save()
        {
            using (var session = OpenWriteSession())
            {
                lock (this)
                {
                    session.BeginTransaction();
                    if (!SavedWorkspaceVersion.ChromatogramsValid(WorkspaceVersion))
                    {
                        session.CreateSQLQuery("DELETE FROM DbChromatogram")
                            .ExecuteUpdate();
                        session.CreateSQLQuery("UPDATE DbPeptideFileAnalysis SET ChromatogramCount = 0, PeakCount = 0, PeptideDistributionCount = 0")
                            .ExecuteUpdate();
                    }
                    if (!SavedWorkspaceVersion.PeaksValid(WorkspaceVersion))
                    {
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
                        session.CreateSQLQuery("DELETE FROM DbPeptideAmount")
                            .ExecuteUpdate();
                        session.CreateSQLQuery("DELETE FROM DbPeptideDistribution")
                            .ExecuteUpdate();
                        session.CreateSQLQuery("DELETE FROM DbPeptideRate")
                            .ExecuteUpdate();
                        if (SavedWorkspaceVersion.PeaksValid(WorkspaceVersion))
                        {
                            session.CreateSQLQuery("UPDATE DbPeptideFileAnalysis SET PeptideDistributionCount = 0")
                                .ExecuteUpdate();
                        }
                        session.CreateSQLQuery("UPDATE DbPeptideAnalysis SET PeptideRateCount = 0");
                    }
                    _tracerDefs.Save(session);
                    _modifications.Save(session);
                    _settings.Save(session);
                    MsDataFiles.Save(session);
                    foreach (var peptide in _dirtyPeptides)
                    {
                        peptide.Save(session);
                    }
                    foreach (var peptideAnalysis in PeptideAnalyses.ListChildren())
                    {
                        peptideAnalysis.SaveDeep(session);
                    }
                    session.Transaction.Commit();
                    SavedWorkspaceVersion = WorkspaceVersion;
                    _dirtyPeptideAnalyses.Clear();
                    _dirtyPeptides.Clear();
                    IsDirty = false;
                }
            }
            var saveEventListener = WorkspaceDirty;
            if (saveEventListener != null)
            {
                saveEventListener.Invoke(this);
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
                _resultCalculator.Start();
                _chromatogramGenerator.Start();
            }
            else
            {
                _chromatogramGenerator.Stop();
                _resultCalculator.Stop();
            }
        }

        public event EntitiesChangeListener EntitiesChange;
        public event WorkspaceDirtyListener WorkspaceDirty;

        public WorkspaceVersion WorkspaceVersion { get; private set; }
        public WorkspaceVersion SavedWorkspaceVersion { get; private set; }
        private void SetWorkspaceVersion(WorkspaceVersion newWorkspaceVersion)
        {
            if (newWorkspaceVersion.Equals(WorkspaceVersion))
            {
                return;
            }
            WorkspaceVersion = newWorkspaceVersion;
            foreach (var peptideAnalysis in PeptideAnalyses.ListChildren())
            {
                peptideAnalysis.SetWorkspaceVersion(newWorkspaceVersion);
            }
            IsDirty = true;
        }
        public object Lock {get { return DatabaseLock;}}
        public bool SaveIfNotDirty(PeptideAnalysis peptideAnalysis)
        {
            lock(this)
            {
                if (_dirtyPeptideAnalyses.Contains(peptideAnalysis) || !Equals(SavedWorkspaceVersion, WorkspaceVersion))
                {
                    return false;
                }
            }
            using (var session = OpenWriteSession())
            {
                lock(this) 
                {
                    if (_dirtyPeptideAnalyses.Contains(peptideAnalysis) || !Equals(SavedWorkspaceVersion, WorkspaceVersion))
                    {
                        return false;
                    }
                    session.BeginTransaction();
                    peptideAnalysis.SaveDeep(session);
                    session.Transaction.Commit();
                }
                return true;
            }
        }

        private void DispatchDirtyEvent()
        {
            var action = WorkspaceDirty;
            if (action != null)
            {
                action.Invoke(this);
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
                lock(Lock)
                {
                    if (_isDirty == value)
                    {
                        return;
                    }
                    _isDirty = value;
                    if (_actionInvoker != null)
                    {
                        _actionInvoker.Invoke(DispatchDirtyEvent);
                    }
                }
            } 
        }

        public ResultCalculator ResultCalculator { get { return _resultCalculator; } }
        public ChromatogramGenerator ChromatogramGenerator { get { return _chromatogramGenerator; } }
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
    }

    public delegate void EntitiesChangeListener(EntitiesChangedEventArgs entitiesChangedEventArgs);

    public delegate void WorkspaceDirtyListener(Workspace workspace);

    public delegate void ActionInvoker(Action action);

}