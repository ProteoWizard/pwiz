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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Common.Collections;
using pwiz.Topograph.Data;
using pwiz.Topograph.Data.Snapshot;
using pwiz.Topograph.Model;
using pwiz.Topograph.Model.Data;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.MsData
{
    public class DatabasePoller : IDisposable
    {
        private const int DelayMillis = 60000;
        private readonly Workspace _workspace;
        private readonly EventWaitHandle _eventWaitHandle = new EventWaitHandle(true, EventResetMode.AutoReset);

        private Thread _databasePollingThread;
        private bool _isRunning;
        public DatabasePoller(Workspace workspace)
        {
            _workspace = workspace;
        }

        public void Start()
        {
            lock(_eventWaitHandle)
            {
                if (_isRunning)
                {
                    return;
                }
                Trace.TraceInformation("Database polling started");
                _isRunning = true;
                if (_databasePollingThread == null)
                {
                    _databasePollingThread = new Thread(WorkerMethod)
                                            {
                                                Name = "Database Polling",
                                                Priority = ThreadPriority.BelowNormal
                                            };
                    _databasePollingThread.Start();
                    _eventWaitHandle.Set();
                }
            }
        }
        public void Stop()
        {
            lock (_eventWaitHandle)
            {
                if (!_isRunning)
                {
                    return;
                }
                Trace.TraceInformation("Database polling stopped");
                _isRunning = false;
                _eventWaitHandle.Set();
            }
        }
        private void WorkerMethod()
        {
            while (true)
            {
                _eventWaitHandle.WaitOne(DelayMillis);
                lock (_eventWaitHandle)
                {
                    if (!_isRunning)
                    {
                        return;
                    }
                }
                try
                {
                    LoadAndMergeChanges(null);
                }
                catch (Exception exception)
                {
                    if (!_isRunning)
                    {
                        return;
                    }
                    ErrorHandler.LogException("Reconciler", "Exception", exception);
                }
            }
        }
        
        public WorkspaceData LoadChanges(WorkspaceData savedData, IDictionary<long, bool> requestedPeptideAnalyses)
        {
            var workspaceData = savedData;
            lock (_eventWaitHandle)
            {
                if (!_isRunning)
                {
                    return null;
                }
            }
            var peptides = ToMutableDictionary(workspaceData.Peptides);
            var msDataFiles = ToMutableDictionary(workspaceData.MsDataFiles);
            var peptideAnalyses = ToMutableDictionary(workspaceData.PeptideAnalyses);

            using (var session = _workspace.OpenSession())
            {
                var changedPeptideIds = new HashSet<long>();
                var changedDataFileIds = new HashSet<long>();
                var changedPeptideAnalysisIds = new HashSet<long>();
                bool workspaceChanged = false;
                if (workspaceData.LastChangeLogId.HasValue)
                {
                    long lastChangeLogId = workspaceData.LastChangeLogId.Value;
                    var changeLogs = session.CreateCriteria<DbChangeLog>()
                                            .Add(Restrictions.Gt("Id", workspaceData.LastChangeLogId))
                                            .List<DbChangeLog>();
                    foreach (var dbChangeLog in changeLogs)
                    {
                        if (dbChangeLog.PeptideId.HasValue)
                        {
                            changedPeptideIds.Add(dbChangeLog.PeptideId.Value);
                            peptides.Remove(dbChangeLog.PeptideId.Value);
                        }
                        if (dbChangeLog.MsDataFileId.HasValue)
                        {
                            changedDataFileIds.Add(dbChangeLog.MsDataFileId.Value);
                            msDataFiles.Remove(dbChangeLog.MsDataFileId.Value);
                        }
                        if (dbChangeLog.PeptideAnalysisId.HasValue)
                        {
                            changedPeptideAnalysisIds.Add(dbChangeLog.PeptideAnalysisId.Value);
                            peptideAnalyses.Remove(dbChangeLog.PeptideAnalysisId.Value);
                        }
                        if (dbChangeLog.WorkspaceId.HasValue)
                        {
                            workspaceChanged = true;
                        }
                        lastChangeLogId = Math.Max(lastChangeLogId, dbChangeLog.GetId());
                    }
                    workspaceData = workspaceData.SetLastChangeLogId(lastChangeLogId);
                }
                else
                {
                    workspaceChanged = true;
                    long lastChangeLogId = (long?) session.CreateQuery("SELECT Max(T.Id) FROM " + typeof (DbChangeLog) + " T")
                        .UniqueResult() ?? 0;
                    var dbWorkspace = session.CreateCriteria<DbWorkspace>().UniqueResult<DbWorkspace>();
                    workspaceData = workspaceData
                        .SetLastChangeLogId(lastChangeLogId)
                        .SetDbWorkspaceId(dbWorkspace.Id);
                }

                if (workspaceChanged)
                {
                    var settings =
                        session.CreateCriteria<DbSetting>()
                               .List<DbSetting>()
                               .ToDictionary(dbSetting => dbSetting.Name, dbSetting => dbSetting.Value);
                    var tracerDefs =
                        session.CreateCriteria<DbTracerDef>()
                               .List<DbTracerDef>()
                               .ToDictionary(dbTracerDef => dbTracerDef.Name,
                                             dbTracerDef => new TracerDefData(dbTracerDef));
                    var modifications =
                        session.CreateCriteria<DbModification>()
                               .List<DbModification>()
                               .ToDictionary(dbModification => dbModification.Symbol,
                                             dbModification => dbModification.DeltaMass);
                    workspaceData = workspaceData
                        .SetSettings(ImmutableSortedList.FromValues(settings))
                        .SetTracerDefs(ImmutableSortedList.FromValues(tracerDefs))
                        .SetModifications(ImmutableSortedList.FromValues(modifications));
                }

                foreach (var dbPeptide in EntitiesWithIdGreaterThanOrOneOf<DbPeptide>(session, GetLastId(savedData.Peptides), changedPeptideIds))
                {
                    peptides.Add(dbPeptide.GetId(), new PeptideData(dbPeptide));
                }
                workspaceData = workspaceData.SetPeptides(ImmutableSortedList.FromValues(peptides));
                
                // Load the MSDataFiles
                var dataFileIdPredicate = new IdPredicate(GetLastId(savedData.MsDataFiles), changedDataFileIds);
                var psmsByDataFileId = session.CreateQuery("FROM " + typeof (DbPeptideSpectrumMatch) + " M WHERE " +
                                        dataFileIdPredicate.GetSql("M.MsDataFile.Id")).List<DbPeptideSpectrumMatch>().ToLookup(psm=>psm.MsDataFile.Id);
                foreach (var dbMsDataFile in EntitiesWithIdGreaterThanOrOneOf<DbMsDataFile>(session, GetLastId(savedData.MsDataFiles), changedDataFileIds))
                {
                    var msDataFileData = new MsDataFileData(dbMsDataFile);
                    var psmTimes = psmsByDataFileId[dbMsDataFile.GetId()].ToLookup(psm => psm.ModifiedSequence, psm=>psm.RetentionTime);
                    msDataFileData = msDataFileData.SetRetentionTimesByModifiedSequence(
                        psmTimes.Select(grouping => new KeyValuePair<string, double>(grouping.Key, grouping.Min())));
                    msDataFiles.Add(dbMsDataFile.GetId(), msDataFileData);
                }

                workspaceData = workspaceData.SetMsDataFiles(ImmutableSortedList.FromValues(msDataFiles));
                var idsToSnapshot = new HashSet<long>(changedPeptideAnalysisIds);
                var chromatogramsToSnapshot = new HashSet<long>();
                if (null != requestedPeptideAnalyses)
                {
                    foreach (var pair in requestedPeptideAnalyses)
                    {
                        PeptideAnalysisData existing;
                        if (peptideAnalyses.TryGetValue(pair.Key, out existing))
                        {
                            if (pair.Value)
                            {
                                if (existing.ChromatogramsWereLoaded)
                                {
                                    continue;
                                }
                            }
                            else
                            {
                                peptideAnalyses[pair.Key] = existing.UnloadChromatograms();
                                continue;
                            }
                        }
                        idsToSnapshot.Add(pair.Key);
                        if (pair.Value)
                        {
                            chromatogramsToSnapshot.Add(pair.Key);
                        }
                    }
                }
                var loadedPeptideAnalyses = PeptideAnalysisSnapshot.Query(session, 
                    new IdPredicate(GetLastId(workspaceData.PeptideAnalyses) + 1, idsToSnapshot), 
                    new IdPredicate(null, chromatogramsToSnapshot));
                foreach (var entry in loadedPeptideAnalyses)
                {
                    peptideAnalyses[entry.Key] = entry.Value;
                }
                workspaceData = workspaceData.SetPeptideAnalyses(ImmutableSortedList.FromValues(peptideAnalyses));
            }

            return workspaceData;
        }

        public bool TryLoadAndMergeChanges(IDictionary<long, bool> requestedPeptideAnalyses)
        {
            var savedData = _workspace.SavedData;
            var newWorkspaceData = LoadChanges(savedData, requestedPeptideAnalyses);
            if (null == newWorkspaceData)
            {
                return false;
            }
            if (Equals(savedData, newWorkspaceData))
            {
                return true;
            }
            return _workspace.RunOnEventQueue(() =>
            {
                if (Equals(savedData, _workspace.SavedData))
                {
                    _workspace.Merge(newWorkspaceData);
                    return true;
                }
                return false;
            });
        }

        public void LoadAndMergeChanges(IDictionary<long, bool> requestedPeptideAnalyses)
        {
            for (;;)
            {
                if (TryLoadAndMergeChanges(requestedPeptideAnalyses))
                {
                    return;
                }
            }
        }
        public void MergeChangesNow()
        {
            LoadAndMergeChanges(null);
        }

        private static IEnumerable<T> EntitiesWithIdGreaterThanOrOneOf<T>(ISession session, long id, ICollection<long> otherIds) where T : IDbEntity
        {
            ICriterion restriction = Restrictions.Gt("Id", id);
            if (otherIds.Count > 0)
            {
                restriction = Restrictions.Disjunction()
                    .Add(restriction)
                    .Add(Restrictions.In("Id", otherIds.ToArray()));
            }
            var criteria = session.CreateCriteria(typeof (T))
                .Add(restriction);
            return criteria.List<T>();
        } 
        public void Wake()
        {
            _eventWaitHandle.Set();
        }
        public void Dispose()
        {
            Stop();
        }
        private long GetLastId<T>(ImmutableSortedList<long, T> list)
        {
            if (list == null || list.Count == 0)
            {
                return 0;
            }
            return list.Keys[list.Count - 1];
        }
        private IDictionary<long, T> ToMutableDictionary<T>(ImmutableSortedList<long, T> immutableSortedList)
        {
            if (null == immutableSortedList)
            {
                return new Dictionary<long, T>();
            }
            return new Dictionary<long, T>(immutableSortedList.AsDictionary());
        }
    }
}
