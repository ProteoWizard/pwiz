using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Data.Snapshot
{
    public class PeptideFileAnalysisSnapshot 
    {
        private DbPeptideFileAnalysis _peptideFileAnalysis;
        private IList<DbChromatogram> _chromatograms;
        private IList<DbPeak> _peaks;
        private IList<DbPeptideDistribution> _distributions;
        private IList<DbPeptideAmount> _amounts;

        public long Id { get { return _peptideFileAnalysis.Id.Value; } }
        public long PeptideAnalysisId { get { return _peptideFileAnalysis.PeptideAnalysis.Id.Value; } }
        public DbPeptideFileAnalysis DbPeptideFileAnalysis { get { return _peptideFileAnalysis; } }

        public Chromatograms GetChromatograms(PeptideFileAnalysis peptideFileAnalysis)
        {
            var result = new Chromatograms(peptideFileAnalysis, _peptideFileAnalysis);
            if (_chromatograms != null)
            {
                result.LoadChildren(_chromatograms, c => c.MzKey);
            }
            return result;
        }
        public Peaks GetPeaks(PeptideFileAnalysis peptideFileAnalysis)
        {
            var peaks = new Peaks(peptideFileAnalysis, _peptideFileAnalysis);
            if (_peaks != null)
            {
                peaks.LoadChildren(_peaks, p => p.MzKey);
            }
            return peaks;
        }
        public PeptideDistributions GetDistributions(PeptideFileAnalysis peptideFileAnalysis)
        {
            var distributions = new PeptideDistributions(peptideFileAnalysis);
            if (_distributions == null || _amounts == null)
            {
                return distributions;
            }
            var amountsDict = Lists.ToDict(_amounts, a => a.PeptideDistribution.Id.Value);
            foreach (var dbDistribution in _distributions)
            {
                var distribution = new PeptideDistribution(distributions.Workspace, dbDistribution)
                                       {
                                           Parent = distributions
                                       };
                IList<DbPeptideAmount> amounts;
                if (amountsDict.TryGetValue(distribution.Id.Value, out amounts))
                {
                    distribution.LoadChildren(amounts, a=>a.TracerFormula);
                }
                distributions.AddChild(distribution.PeptideQuantity, distribution);
            }
            return distributions;
        }
        public static List<PeptideFileAnalysisSnapshot> Query(ISession session, ICollection<long> peptideAnalysisIds, bool loadAllChromatograms)
        {
            var peptideFileAnalyses = new List<DbPeptideFileAnalysis>();
            var idList = "(" + Lists.Join(peptideAnalysisIds, ",") + ")";
            session.CreateQuery("FROM " + typeof (DbPeptideFileAnalysis) + " T WHERE T.PeptideAnalysis.Id IN " + idList)
                .List(peptideFileAnalyses);
            var chromatograms = new List<DbChromatogram>();
            var chromatogramQuery = "FROM " + typeof (DbChromatogram) +
                                    " T WHERE T.PeptideFileAnalysis.PeptideAnalysis.Id IN " + idList;
            if (!loadAllChromatograms)
            {
                chromatogramQuery += " AND T.PeptideFileAnalysis.PeakCount = 0";
            }
            session.CreateQuery(chromatogramQuery).List(chromatograms);
            var peaks = new List<DbPeak>();
            session.CreateQuery("FROM " + typeof (DbPeak) + " T WHERE T.PeptideFileAnalysis.PeptideAnalysis.Id IN " + idList)
                .List(peaks);
            var distributions = new List<DbPeptideDistribution>();
            session.CreateQuery("FROM " + typeof (DbPeptideDistribution) +
                                " T WHERE T.PeptideFileAnalysis.PeptideAnalysis.Id IN " + idList)
                                .List(distributions);
            var amounts = new List<DbPeptideAmount>();
            session.CreateQuery("FROM " + typeof (DbPeptideAmount) +
                                " T WHERE T.PeptideDistribution.PeptideFileAnalysis.PeptideAnalysis.Id IN " + idList).List(amounts);
            var chromatogramsDict = Lists.ToDict(chromatograms, c => c.PeptideFileAnalysis.Id.Value);
            var peaksDict = Lists.ToDict(peaks, p => p.PeptideFileAnalysis.Id.Value);
            var distributionsDict = Lists.ToDict(distributions, d => d.PeptideFileAnalysis.Id.Value);
            var amountsDict = Lists.ToDict(amounts, a => a.PeptideDistribution.PeptideFileAnalysis.Id.Value);
            var result = new List<PeptideFileAnalysisSnapshot>();
            foreach (var peptideFileAnalysis in peptideFileAnalyses)
            {
                var id = peptideFileAnalysis.Id.Value;
                var snapshot = new PeptideFileAnalysisSnapshot
                                   {
                                       _peptideFileAnalysis = peptideFileAnalysis
                                   };
                chromatogramsDict.TryGetValue(id, out snapshot._chromatograms);
                peaksDict.TryGetValue(id, out snapshot._peaks);
                distributionsDict.TryGetValue(id, out snapshot._distributions);
                amountsDict.TryGetValue(id, out snapshot._amounts);
                result.Add(snapshot);
            }
            return result;
        }
    }
}
