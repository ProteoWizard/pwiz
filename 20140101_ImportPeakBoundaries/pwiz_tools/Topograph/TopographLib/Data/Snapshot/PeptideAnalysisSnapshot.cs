/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using NHibernate;
using pwiz.Common.Collections;
using pwiz.Topograph.Model.Data;

namespace pwiz.Topograph.Data.Snapshot
{
    public static class PeptideAnalysisSnapshot
    {
        public static Dictionary<long, PeptideAnalysisData> Query(ISession session, IdPredicate peptideAnalysisIds, IdPredicate chromatogramsToSnapshot)
        {
            if (peptideAnalysisIds.AlwaysFalse)
            {
                return new Dictionary<long, PeptideAnalysisData>();
            }
            string selectAnalyses = "FROM " + typeof (DbPeptideAnalysis) + " T WHERE " + peptideAnalysisIds.GetSql("T.Id");
            var query = session.CreateQuery(selectAnalyses);
            var peptideAnalyses = new List<DbPeptideAnalysis>();
            query.List(peptideAnalyses);
            var peptideAnalysisSnapshots = peptideAnalyses.ToDictionary(a=>a.Id.GetValueOrDefault(), a=>new PeptideAnalysisData(a));
            foreach (var entry in QueryFileAnalyses(session, peptideAnalysisIds))
            {
                PeptideAnalysisData snapshot;
                if (!peptideAnalysisSnapshots.TryGetValue(entry.Key, out snapshot))
                {
                    Trace.TraceWarning("DbPeptideAnalysis with Id {0} not found", entry.Key);
                    continue;
                }
                peptideAnalysisSnapshots[entry.Key] =
                    snapshot.SetFileAnalyses(ImmutableSortedList.FromValues(entry.Value));
            }

            if (!chromatogramsToSnapshot.AlwaysFalse)
            {
                var chromatogramSetsByFileAnalysisId = QueryChromatogramSets(session, chromatogramsToSnapshot);
                var peptideIds = peptideAnalysisSnapshots.Values.Select(snapshot => snapshot.PeptideId).ToArray();
                var psmTimesByPeptideId = LoadPsmTimesByPeptideAndFile(session, peptideIds);
                foreach (var entry in peptideAnalysisSnapshots.ToArray())
                {
                    if (!chromatogramsToSnapshot.Matches(entry.Key))
                    {
                        continue;
                    }
                    var peptideAnalysisData = entry.Value;
                    IDictionary<long, PsmTimes> psmTimesByDataFileId;
                    psmTimesByPeptideId.TryGetValue(entry.Value.PeptideId, out psmTimesByDataFileId);
                    var fileAnalysisEntries = entry.Value.FileAnalyses.ToArray();
                    for (int i = 0; i < fileAnalysisEntries.Length; i++)
                    {
                        var pair = fileAnalysisEntries[i];
                        var fileAnalysis = pair.Value;
                        ChromatogramSetData chromatogramSetData;
                        if (chromatogramSetsByFileAnalysisId.TryGetValue(pair.Key, out chromatogramSetData))
                        {
                            fileAnalysis = fileAnalysis.SetChromatogramSet(chromatogramSetData);
                        }
                        if (null != psmTimesByDataFileId)
                        {
                            PsmTimes psmTimes;
                            if (psmTimesByDataFileId.TryGetValue(pair.Value.MsDataFileId, out psmTimes))
                            {
                                fileAnalysis = fileAnalysis.SetPsmTimes(psmTimes);
                            }
                        }
                        fileAnalysisEntries[i] = new KeyValuePair<long, PeptideFileAnalysisData>(pair.Key, fileAnalysis);
                    }
                    peptideAnalysisData = peptideAnalysisData
                        .SetFileAnalyses(ImmutableSortedList.FromValues(fileAnalysisEntries), true);
                    peptideAnalysisSnapshots[entry.Key] = peptideAnalysisData;
                }
            }
            return peptideAnalysisSnapshots;
        }

        private static IDictionary<long, IDictionary<long, PeptideFileAnalysisData>> QueryFileAnalyses(ISession session, IdPredicate peptideAnalysisIds)
        {
            var peptideFileAnalyses = new List<DbPeptideFileAnalysis>();
            string selectFileAnalyses = "FROM " + typeof(DbPeptideFileAnalysis) + " T WHERE " + peptideAnalysisIds.GetSql("T.PeptideAnalysis");
            session.CreateQuery(selectFileAnalyses).List(peptideFileAnalyses);

            IDictionary<long, IList<PeptideFileAnalysisData.Peak>> allPeaks = QueryPeaks(session, peptideAnalysisIds);
            var result = new Dictionary<long, IDictionary<long, PeptideFileAnalysisData>>();
            foreach (var peptideFileAnalysis in peptideFileAnalyses)
            {
                IDictionary<long, PeptideFileAnalysisData> dict;
                if (!result.TryGetValue(peptideFileAnalysis.PeptideAnalysis.GetId(), out dict))
                {
                    dict = new Dictionary<long, PeptideFileAnalysisData>();
                    result.Add(peptideFileAnalysis.PeptideAnalysis.GetId(), dict);
                }
                IList<PeptideFileAnalysisData.Peak> peaks;
                allPeaks.TryGetValue(peptideFileAnalysis.GetId(), out peaks);
                dict.Add(peptideFileAnalysis.Id.GetValueOrDefault(), new PeptideFileAnalysisData(peptideFileAnalysis, peaks ?? new PeptideFileAnalysisData.Peak[0]));
            }
            return result;
        }

        private static IDictionary<long, ChromatogramSetData> QueryChromatogramSets(ISession session, IdPredicate peptideAnalysisIds)
        {
            string selectChromatogramSets = "FROM " + typeof(DbChromatogramSet) + " T WHERE " + peptideAnalysisIds.GetSql("T.PeptideFileAnalysis.PeptideAnalysis.Id");
            string selectChromatograms = "FROM " + typeof(DbChromatogram) + " T WHERE " + peptideAnalysisIds.GetSql("T.ChromatogramSet.PeptideFileAnalysis.PeptideAnalysis.Id");
            var chromatograms = session.CreateQuery(selectChromatograms).List<DbChromatogram>()
                .ToLookup(dbChromatogram => dbChromatogram.ChromatogramSet.GetId());
            var chromatogramSets = session.CreateQuery(selectChromatogramSets)
                .List<DbChromatogramSet>()
                .ToDictionary(dbChromatogramSet => dbChromatogramSet.PeptideFileAnalysis.GetId(),
                              dbChromatogramSet =>
                              new ChromatogramSetData(dbChromatogramSet, chromatograms[dbChromatogramSet.GetId()]));
            return chromatogramSets;
        }

        private static IDictionary<long, IList<PeptideFileAnalysisData.Peak>> QueryPeaks(ISession session,
                                                                                         IdPredicate peptideAnalysisIds)
        {
            var result = new Dictionary<long, IList<PeptideFileAnalysisData.Peak>>();
            long? peptideFileAnalysisId = null;
            var peaks = new List<PeptideFileAnalysisData.Peak>();
            string selectPeaks = "SELECT T.PeptideFileAnalysis.Id, StartTime, EndTime, Area FROM " + typeof(DbPeak) + " T"
                +"\nWHERE " + peptideAnalysisIds.GetSql("T.PeptideFileAnalysis.PeptideAnalysis.Id")
                +"\nORDER BY T.PeptideFileAnalysis.Id, T.PeakIndex";
            var query = session.CreateQuery(selectPeaks);
            foreach (var row in query.List<object[]>())
            {
                if (!Equals(row[0], peptideFileAnalysisId))
                {
                    if (null != peptideFileAnalysisId)
                    {
                        result.Add(peptideFileAnalysisId.Value, ImmutableList.ValueOf(peaks));
                        peaks.Clear();
                    }
                    peptideFileAnalysisId = (long) row[0];
                }
                peaks.Add(new PeptideFileAnalysisData.Peak
                              {
                                  StartTime = Convert.ToDouble(row[1]),
                                  EndTime = Convert.ToDouble(row[2]),
                                  Area = Convert.ToDouble(row[3]),
                              });
            }
            if (null != peptideFileAnalysisId)
            {
                result.Add(peptideFileAnalysisId.Value, peaks);
            }
            return result;
        }

        private static IDictionary<long, IDictionary<long, PsmTimes>> LoadPsmTimesByPeptideAndFile(ISession session, ICollection<long> peptideIds)
        {
            var dict = new Dictionary<long, IDictionary<long, PsmTimes>>();

            if (peptideIds.Count == 0)
            {
                return dict;
            }
            var strPeptideIds = string.Join(",", peptideIds.Select(id => id.ToString(CultureInfo.InvariantCulture)));
            var psms = session.CreateQuery("FROM " + typeof (DbPeptideSpectrumMatch) + " M"
                + "\nWHERE M.Peptide.Id IN (" + strPeptideIds + ")").List<DbPeptideSpectrumMatch>();
            var psmsByPeptide = psms.ToLookup(psm => psm.Peptide.GetId());
            foreach (var grouping in psmsByPeptide)
            {
                var psmTimesByFileId = grouping.ToLookup(psm => psm.MsDataFile.GetId())
                    .ToDictionary(group => group.Key, group => new PsmTimes(group));
                dict.Add(grouping.Key, psmTimesByFileId);
            }
            return dict;
        }
    }
}
