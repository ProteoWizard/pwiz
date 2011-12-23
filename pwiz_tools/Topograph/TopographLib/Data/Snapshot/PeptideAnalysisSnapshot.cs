using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Data.Snapshot
{
    public class PeptideAnalysisSnapshot
    {
        public DbPeptideAnalysis DbPeptideAnalysis { get; private set; }
        public Dictionary<long, PeptideFileAnalysisSnapshot> FileAnalysisSnapshots { get; private set;}
        public bool ChromatogramsWereLoaded { get; private set; }
        private static Dictionary<long, PeptideAnalysisSnapshot> Query(ISession session, ICollection<long> peptideAnalysisIds, bool loadAllChromatograms)
        {
            var result = new Dictionary<long, PeptideAnalysisSnapshot>();
            if (peptideAnalysisIds.Count == 0)
            {
                return result;
            }
            var allFileAnalysisSnapshots = PeptideFileAnalysisSnapshot.Query(session, peptideAnalysisIds, loadAllChromatograms);
            var query = session
                .CreateQuery("FROM " + typeof (DbPeptideAnalysis) + " T WHERE T.Id IN (" +
                                    Lists.Join(peptideAnalysisIds, ",") + ")");
            var peptideAnalyses = new List<DbPeptideAnalysis>();
            query.List(peptideAnalyses);
            var peptideAnalysesDict = peptideAnalyses.ToDictionary(a=>a.Id.Value);
            var fileAnalysisDict = Lists.ToDict(allFileAnalysisSnapshots, f => f.PeptideAnalysisId);
            foreach (var id in peptideAnalysisIds)
            {
                DbPeptideAnalysis dbPeptideAnalysis;
                if (!peptideAnalysesDict.TryGetValue(id, out dbPeptideAnalysis))
                {
                    result.Add(id, null);
                    continue;
                }
                IList<PeptideFileAnalysisSnapshot> fileAnalysisSnapshots;
                if (!fileAnalysisDict.TryGetValue(id, out fileAnalysisSnapshots))
                {
                    fileAnalysisSnapshots = new List<PeptideFileAnalysisSnapshot>();
                }
                result.Add(id, new PeptideAnalysisSnapshot
                                   {
                                       DbPeptideAnalysis = dbPeptideAnalysis,
                                       FileAnalysisSnapshots = fileAnalysisSnapshots.ToDictionary(f=>f.Id),
                                       ChromatogramsWereLoaded = loadAllChromatograms
                                   });
            }
            return result;
        }
        public static Dictionary<long, PeptideAnalysisSnapshot> Query(ISession session, ICollection<long> peptideAnalysisIdsToSnapshot, ICollection<long> peptideAnalysisIdsToSnapshotWithoutChromatograms)
        {
            Dictionary<long, PeptideAnalysisSnapshot> peptideAnalysisSnapshots = new Dictionary<long, PeptideAnalysisSnapshot>();
            if (peptideAnalysisIdsToSnapshot.Count != 0)
            {
                foreach (var entry in Query(session, peptideAnalysisIdsToSnapshot, true))
                {
                    peptideAnalysisSnapshots.Add(entry.Key, entry.Value);
                }
            }
            if (peptideAnalysisIdsToSnapshotWithoutChromatograms.Count != 0)
            {
                foreach (var entry in Query(session, peptideAnalysisIdsToSnapshotWithoutChromatograms, false))
                {
                    peptideAnalysisSnapshots.Add(entry.Key, entry.Value);
                }
            }
            return peptideAnalysisSnapshots;
        }
        public static PeptideAnalysisSnapshot LoadSnapshot(Workspace workspace, long id, bool loadAllChromatograms)
        {
            using (var session = workspace.OpenSession())
            {
                var snapshots = Query(session, new[] {id}, loadAllChromatograms);
                if (snapshots.Count == 0)
                {
                    return null;
                }
                return snapshots[id];
            }
        }
    }
}
