using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Common.SystemUtil;
using pwiz.Topograph.Data;
using pwiz.Topograph.Data.Snapshot;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.MsData
{
    public class Reconciler : MustDispose
    {
        private const int DelayMillis = 60000;
        private readonly Workspace _workspace;
        private readonly EventWaitHandle _eventWaitHandle = new EventWaitHandle(true, EventResetMode.AutoReset);
        private WorkspaceData _dataSet;

        private Thread _reconcilerThread;
        private bool _isRunning;
        private long _lastChangeId;
        private long _lastPeptideId;
        private long _lastMsDataFileId;
        private long _lastPeptideAnalysisId;
        public Reconciler(Workspace workspace)
        {
            _workspace = workspace;
        }

        public void Start()
        {
            lock(this)
            {
                if (_isRunning)
                {
                    return;
                }
                _isRunning = true;
                if (_reconcilerThread == null)
                {
                    _reconcilerThread = new Thread(WorkerMethod)
                                            {
                                                Name = "Reconciler",
                                                Priority = ThreadPriority.BelowNormal
                                            };
                    _reconcilerThread.Start();
                    _eventWaitHandle.Set();
                }
            }
        }
        public void Stop()
        {
            lock (this)
            {
                if (!_isRunning)
                {
                    return;
                }
                _isRunning = false;
                _eventWaitHandle.Set();
            }
        }
        private void WorkerMethod()
        {
            while (true)
            {
                _eventWaitHandle.WaitOne(DelayMillis);
                lock (this)
                {
                    CheckDisposed();
                    if (!_isRunning)
                    {
                        return;
                    }
                }
                try
                {
                    if (!ReconcileNow())
                    {
                        _eventWaitHandle.Set();
                    }
                }
                catch (Exception exception)
                {
                    ErrorHandler.LogException("Reconciler", "Exception", exception);
                }
            }
        }
        
        private Dictionary<long, PeptideAnalysis> GetActivePeptideAnalyses()
        {
            return _workspace.PeptideAnalyses.ListChildren().ToDictionary(a => a.Id.Value);
        }

        public bool LoadPeptideAnalyses(Dictionary<long, PeptideAnalysis> peptideAnalyses, bool loadChromatograms)
        {
            lock(this)
            {
                var peptideAnalysisDict = new Dictionary<long, KeyValuePair<PeptideAnalysis, bool>>();
                var activePeptideAnalyses = GetActivePeptideAnalyses();
                var anyMissing = false;

                foreach (var id in peptideAnalyses.Keys.ToArray())
                {
                    PeptideAnalysis peptideAnalysis;
                    if (activePeptideAnalyses.TryGetValue(id, out peptideAnalysis) && (!loadChromatograms || peptideAnalysis.ChromatogramsWereLoaded))
                    {
                        peptideAnalyses[id] = peptideAnalysis;
                    }
                    else
                    {
                        if (peptideAnalysis == null || (loadChromatograms && !peptideAnalysis.ChromatogramsWereLoaded))
                        {
                            peptideAnalysisDict.Add(id, new KeyValuePair<PeptideAnalysis, bool>(
                                peptideAnalysis, 
                                loadChromatograms));
                            anyMissing = true;
                        }
                    }
                }
                if (!anyMissing)
                {
                    return true;
                }
                if (!ReconcileNow(peptideAnalysisDict))
                {
                    return false;
                }
                foreach (var entry in peptideAnalysisDict)
                {
                    if (entry.Value.Key != null)
                    {
                        peptideAnalyses[entry.Key] = entry.Value.Key;
                    }
                }
                return true;
            }
        }

        public PeptideAnalysis LoadPeptideAnalysis(long id)
        {
            while (true)
            {
                lock(this)
                {
                    var dict = new Dictionary<long, PeptideAnalysis>{{id,null}};
                    if (!LoadPeptideAnalyses(dict, true))
                    {
                        return null;
                    }
                    return dict[id];
                }
            }
        }
        
        public bool ReconcileNow()
        {
            lock(this)
            {
                var dict = GetActivePeptideAnalyses().ToDictionary(kvp => kvp.Key,
                                                                   kvp =>
                                                                   new KeyValuePair<PeptideAnalysis, bool>(
                                                                       kvp.Value, kvp.Value.ChromatogramsWereLoaded));
                return ReconcileNow(dict);
            }
        }


        private const int max_requery_count = 100;
        private bool ReconcileNow(Dictionary<long, KeyValuePair<PeptideAnalysis, bool>> activePeptideAnalyses)
        {
            var msDataFileIds = new HashSet<long>();
            var peptideIds = new HashSet<long>();
            var peptideAnalysisIds = new HashSet<long>();
            var fileAnalysisIdsForResultCalculator = new List<long>();
            var fileAnalysisIdsForChromatogramGenerator = new List<long>();
            Dictionary<long, DbSetting> settings = null;
            Dictionary<long, DbModification> modifications = null;
            Dictionary<long, DbTracerDef> tracerDefs = null;
            var workspaceIds = new HashSet<long>();
            bool workspaceChanged;
            Dictionary<long, DbPeptide> peptides;
            Dictionary<long, DbMsDataFile> msDataFiles;
            Dictionary<long, DbPeptideAnalysis> peptideAnalyses;
            Dictionary<long, PeptideAnalysisSnapshot> peptideAnalysisSnapshots;
            DbWorkspace dbWorkspace = null;
            using (var session = _workspace.OpenSession())
            {
                peptides = EntitiesWithIdGreaterThan<DbPeptide>(session, _lastPeptideId);
                msDataFiles = EntitiesWithIdGreaterThan<DbMsDataFile>(session, _lastMsDataFileId);
                peptideAnalyses = EntitiesWithIdGreaterThan<DbPeptideAnalysis>(session, _lastPeptideAnalysisId);
                peptideAnalysisIds.UnionWith(peptideAnalyses.Keys);

                long maxChangeId = _lastChangeId;
                if (_workspace.IsLoaded)
                {
                    var query = session.CreateQuery("FROM DbChangeLog WHERE Id > :lastChangeId")
                        .SetParameter("lastChangeId", _lastChangeId);
                    foreach (DbChangeLog dbChangeLog in query.List())
                    {
                        maxChangeId = Math.Max(maxChangeId, dbChangeLog.Id.Value);
                        AddIfLessThan(msDataFileIds, _lastMsDataFileId, dbChangeLog.MsDataFileId);
                        AddIfLessThan(peptideIds, _lastPeptideId, dbChangeLog.PeptideId);
                        AddIfLessThan(peptideAnalysisIds, _lastPeptideAnalysisId, dbChangeLog.PeptideAnalysisId);
                        if (dbChangeLog.WorkspaceId.HasValue)
                        {
                            workspaceIds.Add(dbChangeLog.WorkspaceId.Value);
                        }
                    }
                    AddEntities(session, peptides, peptideIds);
                    AddEntities(session, msDataFiles, msDataFileIds);
                    workspaceChanged = workspaceIds.Count > 0;
                }
                else
                {
                    workspaceChanged = true;
                }
                if (workspaceChanged) {
                    maxChangeId = (long?) session.CreateQuery("SELECT Max(T.Id) FROM " + typeof (DbChangeLog) + " T")
                        .UniqueResult() ?? 0;
                    settings = EntitiesWithIdGreaterThan<DbSetting>(session, 0);
                    modifications = EntitiesWithIdGreaterThan<DbModification>(session, 0);
                    tracerDefs = EntitiesWithIdGreaterThan<DbTracerDef>(session, 0);
                    dbWorkspace = (DbWorkspace) session.CreateCriteria(typeof (DbWorkspace)).UniqueResult();
                }

                var peptideAnalysisIdsToSnapshot = new HashSet<long>(activePeptideAnalyses.Keys.Where(
                    id=>activePeptideAnalyses[id].Value && (activePeptideAnalyses[id].Key == null || peptideAnalysisIds.Contains(id))));
                var peptideAnalysisIdsToSnapshotWithoutChromatograms =
                    new HashSet<long>(
                        activePeptideAnalyses.Keys.Where(
                            id =>
                            !activePeptideAnalyses[id].Value && 
                            (activePeptideAnalyses[id].Key == null || peptideAnalysisIds.Contains(id))));
                foreach (var entry in activePeptideAnalyses)
                {
                    if (entry.Value.Value)
                    {
                        if (entry.Value.Key == null || !entry.Value.Key.ChromatogramsWereLoaded)
                        {
                            peptideAnalysisIdsToSnapshot.Add(entry.Key);
                        }
                    }
                    else if (entry.Value.Key == null)
                    {
                        peptideAnalysisIdsToSnapshotWithoutChromatograms.Add(entry.Key);
                    }
                }

                if (peptideAnalysisIdsToSnapshot.Count + peptideAnalysisIdsToSnapshotWithoutChromatograms.Count > 0)
                {
                    peptideAnalysisSnapshots = PeptideAnalysisSnapshot.Query(session, peptideAnalysisIdsToSnapshot, peptideAnalysisIdsToSnapshotWithoutChromatograms);
                }
                else
                {
                    peptideAnalysisSnapshots = new Dictionary<long, PeptideAnalysisSnapshot>();
                }
                if (peptideAnalysisIds.Count > 0)
                {
                    if (_workspace.IsLoaded)
                    {
                        if (peptideAnalysisIds.Count < max_requery_count)
                        {
                            session.CreateQuery("SELECT F.Id FROM " + typeof(DbPeptideFileAnalysis) +
                                                " F WHERE F.PeptideAnalysis.Id IN (" + Lists.Join(peptideAnalysisIds, ",") + ") AND F.ChromatogramSet IS NOT NULL AND F.PeakCount = 0")
                                .List(fileAnalysisIdsForResultCalculator);
                            session.CreateQuery("SELECT F.Id FROM " + typeof(DbPeptideFileAnalysis) +
                                                " F WHERE F.PeptideAnalysis.Id IN (" + Lists.Join(peptideAnalysisIds, ",") +
                                                ") AND F.ChromatogramSet IS NULL")
                                .List(fileAnalysisIdsForChromatogramGenerator);
                        }
                    }
                    var peptideAnalysisList = new List<DbPeptideAnalysis>();
                    session.CreateQuery("FROM " + typeof (DbPeptideAnalysis) + " A WHERE A.Id IN(" +
                                        Lists.Join(peptideAnalysisIds, ",") + ")").List(peptideAnalysisList);
                    var peptideAnalysisDict = peptideAnalysisList.ToDictionary(a => a.Id.Value);
                    foreach (var id in peptideAnalysisIds)
                    {
                        DbPeptideAnalysis dbPeptideAnalysis;
                        peptideAnalysisDict.TryGetValue(id, out dbPeptideAnalysis);
                        peptideAnalyses[id] = dbPeptideAnalysis;
                    }
                }
                if (maxChangeId == 0 
                    && peptides.Count == 0 
                    && msDataFiles.Count == 0 
                    && !workspaceChanged 
                    && peptideAnalysisIdsToSnapshot.Count == 0 
                    && peptideAnalyses.Count == 0)
                {
                    return true;
                }
                foreach (var changeLog in EntitiesWithIdGreaterThan<DbChangeLog>(session, maxChangeId).Values)
                {
                    if (changeLog.WorkspaceId != null || changeLog.PeptideId != null || changeLog.MsDataFileId != null)
                    {
                        return false;
                    }
                    if (changeLog.PeptideAnalysisId != null && peptideAnalysisSnapshots.ContainsKey(changeLog.PeptideAnalysisId.Value))
                    {
                        return false;
                    }
                }
                
                _lastPeptideId = Math.Max(_lastPeptideId, GetMaxId(peptides));
                _lastMsDataFileId = Math.Max(_lastMsDataFileId, GetMaxId(msDataFiles));
                _lastPeptideAnalysisId = Math.Max(_lastPeptideAnalysisId, GetMaxId(peptideAnalyses));
                _lastChangeId = maxChangeId;
            }
            using (_workspace.GetWriteLock())
            {
                if (workspaceChanged)
                {
                    _workspace.Load(dbWorkspace, settings.Values, modifications.Values, tracerDefs.Values);
                }
                foreach (var dbPeptide in peptides.Values)
                {
                    var peptide = _workspace.Peptides.GetChild(dbPeptide.Id.Value);
                    if (peptide == null)
                    {
                        peptide = new Peptide(_workspace, dbPeptide);
                        _workspace.Peptides.AddChild(dbPeptide.Id.Value, peptide);
                        _workspace.AddEntityModel(peptide);
                    }
                    else
                    {
                        peptide.Merge(dbPeptide);
                    }
                }
                foreach (var dbMsDataFile in msDataFiles.Values)
                {
                    var msDataFile = _workspace.MsDataFiles.GetChild(dbMsDataFile.Id.Value);
                    if (msDataFile == null)
                    {
                        msDataFile = new MsDataFile(_workspace, dbMsDataFile);
                        _workspace.MsDataFiles.AddChild(dbMsDataFile.Id.Value, msDataFile);
                        _workspace.AddEntityModel(msDataFile);
                    }
                    else
                    {
                        msDataFile.Merge(dbMsDataFile);
                    }
                }
                foreach (var entry in peptideAnalysisSnapshots)
                {
                    var peptideAnalysis = _workspace.PeptideAnalyses.GetChild(entry.Key);
                    if (entry.Value == null)
                    {
                        if (peptideAnalysis != null)
                        {
                            _workspace.RemoveEntityModel(peptideAnalysis);
                        }
                        _workspace.PeptideAnalyses.RemoveChild(entry.Key);
                    }
                    else
                    {
                        if (peptideAnalysis == null)
                        {
                            peptideAnalysis = new PeptideAnalysis(_workspace, entry.Value.DbPeptideAnalysis);
                            peptideAnalysis.Merge(entry.Value);
                            _workspace.PeptideAnalyses.AddChild(peptideAnalysis.Id.Value, peptideAnalysis);
                        }
                        else
                        {
                            peptideAnalysis.Merge(entry.Value);
                        }
                    }
                    if (peptideAnalysis != null)
                    {
                        activePeptideAnalyses[peptideAnalysis.Id.Value] = new KeyValuePair<PeptideAnalysis, bool>(
                            peptideAnalysis, activePeptideAnalyses[peptideAnalysis.Id.Value].Value);
                    }
                }
                _workspace.AddChangedPeptideAnalyses(peptideAnalyses);
                _workspace.ResultCalculator.AddPeptideFileAnalysisIds(fileAnalysisIdsForResultCalculator);
                _workspace.ChromatogramGenerator.AddPeptideFileAnalysisIds(fileAnalysisIdsForChromatogramGenerator);
                if (workspaceChanged || peptideAnalysisIds.Count >= max_requery_count)
                {
                    _workspace.ChromatogramGenerator.SetRequeryPending();
                    _workspace.ResultCalculator.SetRequeryPending();
                }
                _workspace.CheckDirty();
            }
            return true;
        }

        private static void AddIfLessThan(ICollection<long> set, long maxId, long? key)
        {
            if (!key.HasValue || key > maxId)
            {
                return;
            }
            set.Add(key.Value);
        }

        private static Dictionary<long, T> EntitiesWithIdGreaterThan<T>(ISession session, long id) where T : IDbEntity
        {
            var criteria = session.CreateCriteria(typeof (T))
                .Add(Restrictions.Gt("Id", id));
            var result = new Dictionary<long, T>();
            foreach (T entity in criteria.List())
            {
                result.Add(entity.Id.Value, entity);
            }
            return result;
        }
        private static List<long> ListEntityIdsGreaterThan<T>(ISession session, long id) where T : IDbEntity
        {
            var query = session.CreateQuery("SELECT Id FROM " + typeof (T) + " T WHERE Id > :id").SetParameter("id", id);
            var result = new List<long>();
            query.List(result);
            return result;
        }
        private static void AddEntities<T>(ISession session, Dictionary<long,T> dict, ICollection<long> ids) where T : IDbEntity
        {
            if (ids.Count == 0)
            {
                return;
            }
            var query = session.CreateQuery("FROM " + typeof (T) + " T WHERE T.Id IN (" + Lists.Join(ids, ",") + ")");
            foreach (T entity in query.List())
            {
                dict.Add(entity.Id.Value, entity);
            }
        }
        private static long GetMaxId<T>(ISession session) where T : IDbEntity
        {
            var value = session.CreateQuery("SELECT Max(T.Id) FROM " + typeof (T) + " T").UniqueResult();
            if (value == null)
            {
                return 0;
            }
            return Convert.ToInt64(value);
        }
        private static long GetMaxId<T>(Dictionary<long,T> dict) where T : IDbEntity
        {
            if (dict.Count == 0)
            {
                return 0;
            }
            return dict.Keys.Max();
        }
        public void Wake()
        {
            _eventWaitHandle.Set();
        }
        public long LastChangeId { get { return _lastChangeId; } }
    }

    public class WorkspaceData
    {
        private readonly IDictionary<Type, IDictionary<long, object>> _data = new Dictionary<Type, IDictionary<long, object>>();

        public IEnumerable<long> GetIdsOfType(Type type)
        {
            IDictionary<long, object> dict;
            if (_data.TryGetValue(type, out dict))
            {
                return dict.Keys.ToArray();
            }
            return null;
        }
        public IEnumerable<Type> GetTypes()
        {
            return _data.Keys;
        }
        public object GetData(Type type, long id)
        {
            return _data[type][id];
        }

    }
}
