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
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using log4net;
using NHibernate;
using NHibernate.Criterion;
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
        private EntitiesChangedEventArgs _entitiesChangedEventArgs;
        private readonly HashSet<PeptideAnalysis> _dirtyPeptideAnalyses = new HashSet<PeptideAnalysis>();
        private EnrichmentDef _enrichmentDef;
        private ActionInvoker actionInvoker;
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

        public ResidueComposition GetResidueComposition()
        {
            ResidueComposition residueComposition = new ResidueComposition();
            foreach (var modification in Modifications)
            {
                residueComposition.SetMassDelta(modification.Key, modification.Value);
            }
            return residueComposition;
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

        public void SetEnrichment(DbEnrichment enrichment)
        {
            var newWorkspaceVersion = WorkspaceVersion;
            var oldEnrichment = GetEnrichmentDef();
            if (oldEnrichment.TracerSymbol != enrichment.TracerSymbol || oldEnrichment.DeltaMass != enrichment.DeltaMass)
            {
                newWorkspaceVersion = newWorkspaceVersion.IncMassVersion();
            }
            else if (oldEnrichment.IsotopesEluteEarlier != enrichment.IsotopesEluteEarlier
                || oldEnrichment.IsotopesEluteLater != enrichment.IsotopesEluteLater)
            {
                newWorkspaceVersion = newWorkspaceVersion.IncChromatogramPeakVersion();
            }
            else if (oldEnrichment.InitialApe != enrichment.InitialEnrichment
                || oldEnrichment.FinalApe != enrichment.FinalEnrichment)
            {
                newWorkspaceVersion = newWorkspaceVersion.IncEnrichmentVersion();
            }
            else if (oldEnrichment.AtomCount != enrichment.AtomCount
                || oldEnrichment.AtomPercentEnrichment != enrichment.AtomPercentEnrichment)
            {
                newWorkspaceVersion = newWorkspaceVersion.IncEnrichmentVersion();
            }
            _enrichmentDef = new EnrichmentDef(this, enrichment);
            SetWorkspaceVersion(newWorkspaceVersion);
        }
        public EnrichmentDef GetEnrichmentDef()
        {
            if (_enrichmentDef != null)
            {
                return _enrichmentDef;
            }
            using (ISession session = OpenSession())
            {
                DbWorkspace workspace = (DbWorkspace)session.CreateCriteria(typeof(DbWorkspace)).UniqueResult();
                _enrichmentDef = new EnrichmentDef(this, workspace.Enrichment);
                return _enrichmentDef;
            }
        }
        private EntitiesChangedEventArgs EnsureEntitiesChangedEventArgs()
        {
            if (_entitiesChangedEventArgs == null)
            {
                _entitiesChangedEventArgs = new EntitiesChangedEventArgs();
                if (actionInvoker != null)
                {
                    actionInvoker.Invoke(DispatchChangeEvent);
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
                if (!IsDirty)
                {
                    if (key is MsDataFile || key is Peptide)
                    {
                        IsDirty = true;
                    }
                    else if (_dirtyPeptideAnalyses.Count > 0)
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
                        session.CreateSQLQuery("UPDATE DbPeptideFileAnalysis SET ChromatogramCount = 0")
                            .ExecuteUpdate();
                    }
                    if (!SavedWorkspaceVersion.PeaksValid(WorkspaceVersion))
                    {
                        session.CreateSQLQuery(
                            "UPDATE DbPeptideFileAnalysis SET PeakCount = 0 WHERE DbPeptideFileAnalysis.AutoFindPeak")
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
                        session.CreateSQLQuery("UPDATE DbPeptideFileAnalysis SET PeptideDistributionCount = 0")
                            .ExecuteUpdate();
                        session.CreateSQLQuery("UPDATE DbPeptideAnalysis SET PeptideRateCount = 0");
                    }
                    var dbWorkspace = LoadDbWorkspace(session);
                    if (_enrichmentDef != null)
                    {
                        _enrichmentDef.Update(dbWorkspace.Enrichment);
                        session.Update(dbWorkspace.Enrichment);
                    }
                    _modifications.Save(session);
                    MsDataFiles.Save(session);
                    Peptides.Save(session);
                    foreach (var peptideAnalysis in PeptideAnalyses.ListChildren())
                    {
                        peptideAnalysis.SaveDeep(session);
                    }
                    session.Transaction.Commit();
                    SavedWorkspaceVersion = WorkspaceVersion;
                    _dirtyPeptideAnalyses.Clear();
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
                this.actionInvoker = actionInvoker;
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

        private void SetDirty(bool dirty)
        {
            lock(Lock)
            {
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
                    if (actionInvoker != null)
                    {
                        actionInvoker.Invoke(DispatchDirtyEvent);
                    }
                }
            } 
        }

        public ResultCalculator ResultCalculator { get { return _resultCalculator; } }
        public ChromatogramGenerator ChromatogramGenerator { get { return _chromatogramGenerator; } }
        public WorkspaceSettings Settings { get
        {
            return _settings;
        } }
    }

    public delegate void EntitiesChangeListener(EntitiesChangedEventArgs entitiesChangedEventArgs);

    public delegate void WorkspaceDirtyListener(Workspace workspace);

    public delegate void ActionInvoker(Action action);

}