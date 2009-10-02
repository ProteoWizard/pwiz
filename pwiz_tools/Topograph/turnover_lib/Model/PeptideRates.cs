using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using pwiz.Topograph.Data;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Model
{
    public class PeptideRates : SimpleChildCollection<DbPeptideAnalysis, RateKey, DbPeptideRate>
    {
        public PeptideRates(PeptideAnalysis peptideAnalysis, DbPeptideAnalysis dbPeptideAnalysis)
            : base(peptideAnalysis.Workspace, dbPeptideAnalysis)
        {
            PeptideAnalysis = peptideAnalysis;
        }

        public PeptideFileAnalyses FileAnalyses
        {
            get { return PeptideAnalysis.FileAnalyses; }
        }

        protected override int GetChildCount(DbPeptideAnalysis parent)
        {
            return parent.PeptideRateCount;
        }

        protected override void SetChildCount(DbPeptideAnalysis parent, int childCount)
        {
            parent.PeptideRateCount = childCount;
        }

        protected override IEnumerable<KeyValuePair<RateKey, DbPeptideRate>> GetChildren(DbPeptideAnalysis parent)
        {
            foreach (var peptideRate in parent.PeptideRates)
            {
                yield return new KeyValuePair<RateKey, DbPeptideRate>(peptideRate.RateKey, peptideRate);
            }
        }

        protected override void SetParent(DbPeptideRate child, DbPeptideAnalysis parent)
        {
            child.PeptideAnalysis = parent;
        }

        public PeptideAnalysis PeptideAnalysis { get; private set; }

        public bool Calculate()
        {
            var peptideRateDict = new Dictionary<RateKey, DbPeptideRate>();
            foreach (var cohort in GetCohorts().Keys)
            {
                foreach (PeptideQuantity peptideQuantity in Enum.GetValues(typeof(PeptideQuantity)))
                {
                    bool isComplete;
                    var rateKey = new RateKey(peptideQuantity, cohort);
                    var points = GetPoints(rateKey, out isComplete);
                    if (points.Count <= 1)
                    {
                    //    Thread.Sleep(1000);
                    //    var rateKey2 =
                    //        new RateKey(peptideQuantity == PeptideQuantity.tracer_count
                    //                        ? PeptideQuantity.precursor_enrichment
                    //                        : PeptideQuantity.tracer_count, rateKey.Cohort);
                    //    var points2 = GetPoints(fileAnalyses, rateKey2);
                    //    if (points2.Count > 1)
                    //    {
                    //        Console.Out.WriteLine(PeptideAnalysis.Peptide.FullSequence);
                    //    }
                        continue;
                    }
                    var peptideRate = GetPeptideRate(rateKey, points);
                    peptideRate.IsComplete = isComplete;
                    peptideRateDict.Add(rateKey, peptideRate);
                }
            }
            lock(Lock)
            {
                Clear();
                foreach (var entry in peptideRateDict)
                {
                    AddChild(entry.Key, entry.Value);
                }
                if (ChildCount % 2 != 0)
                {
                    Console.Out.WriteLine(PeptideAnalysis.Peptide.FullSequence + ChildCount);
                }
            }
            return true;
        }

        private DbPeptideRate GetPeptideRate(RateKey rateKey, IDictionary<double,double> points)
        {
            var yValues = new List<double>();
            var initialEnrichment = PeptideAnalysis.InitialEnrichment;
            var finalEnrichment = PeptideAnalysis.FinalEnrichment;
            foreach (var value in points.Values)
            {
                double logValue = Math.Log((value - finalEnrichment)/(initialEnrichment - finalEnrichment));
                yValues.Add(logValue);
            }
            var statisticsX = new Statistics(points.Keys.ToArray());
            var statisticsY = new Statistics(yValues.ToArray());
            double rateConstant = statisticsY.Slope(statisticsX);
            double yIntercept = statisticsY.Intercept(statisticsX);
            var predicted = new Dictionary<double,double>();
            foreach (var point in points)
            {
                predicted.Add(point.Key, Math.Exp(yIntercept) * Math.Exp(point.Key * rateConstant));
            }
            double actualSquared = 0;
            double predictedSquared = 0;
            double dotProduct = 0;

            foreach (var point in points)
            {
                var predictedValue = predicted[point.Key];
                dotProduct += predictedValue*point.Value;
                actualSquared += point.Value*point.Value;
                predictedSquared += predictedValue*predictedValue;
            }
            double score = dotProduct/Math.Sqrt(actualSquared*predictedSquared);
            
            return new DbPeptideRate
                       {
                           RateKey = rateKey,
                           LimitValue = finalEnrichment,
                           HalfLife = ConstrainDouble(Math.Log(.5) / rateConstant),
                           InitialTurnover = ConstrainDouble(100 - Math.Exp(yIntercept) * 100),
                           Score = ConstrainDouble(score),
                       };
        }

        private double ConstrainDouble(double value)
        {
            if (double.IsInfinity(value))
            {
                return double.NaN;
            }
            return value;
        }

        public IDictionary<String,String> GetCohorts()
        {
            return GetCohorts(PeptideAnalysis.FileAnalyses.ListPeptideFileAnalyses(true));
        }

        private IDictionary<String,String> GetCohorts(ICollection<PeptideFileAnalysis> fileAnalyses)
        {
            var cohorts = new SortedDictionary<String, String> { { "", "<All>" } };
            foreach (var fileAnalysis in fileAnalyses)
            {
                var cohort = fileAnalysis.MsDataFile.Cohort;
                if (String.IsNullOrEmpty(cohort))
                {
                    continue;
                }
                cohorts[cohort] = cohort;
            }
            return cohorts;
        }

        public IDictionary<double,double> GetPoints(RateKey rateKey)
        {
            bool isComplete;
            return GetPoints(rateKey, out isComplete);
        }
        
        private IDictionary<double, double> GetPoints(RateKey rateKey, out bool isComplete)
        {
            isComplete = true;
            var totals = new Dictionary<double, double>();
            var counts = new Dictionary<double, int>();
            foreach (var fileAnalysis in PeptideAnalysis.FileAnalyses.ListPeptideFileAnalyses(true))
            {
                if (!String.IsNullOrEmpty(rateKey.Cohort) && rateKey.Cohort != fileAnalysis.MsDataFile.Cohort)
                {
                    continue;
                }
                var peptideDistribution = fileAnalysis.GetPeptideDistribution(rateKey.PeptideQuantity);
                if (peptideDistribution == null)
                {
                    isComplete = false;
                    continue;
                }
                double? timePoint = fileAnalysis.MsDataFile.TimePoint;
                if (!timePoint.HasValue)
                {
                    continue;
                }
                double value = peptideDistribution.AverageEnrichmentValue;
                if (double.IsNaN(value))
                {
                    continue;
                }
                double total;
                totals.TryGetValue(timePoint.Value, out total);
                total += value;
                totals[timePoint.Value] = total;
                int count;
                counts.TryGetValue(timePoint.Value, out count);
                count++;
                counts[timePoint.Value] = count;
            }
            var result = new SortedDictionary<double, double>();
            foreach (var entry in totals)
            {
                result.Add(entry.Key, entry.Value/counts[entry.Key]);
            }
            return result;
        }
    }
}
