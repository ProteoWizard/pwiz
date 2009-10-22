using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using pwiz.Common.Chemistry;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
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
            foreach (var tracerName in GetTracerNames())
            {
                foreach (var cohort in GetCohorts().Keys)
                {
                    foreach (PeptideQuantity peptideQuantity in Enum.GetValues(typeof(PeptideQuantity)))
                    {
                        bool isComplete;
                        var rateKey = new RateKey(tracerName, peptideQuantity, cohort);
                        var points = GetPoints(rateKey, out isComplete);
                        var peptideRate = GetPeptideRate(rateKey, points);
                        peptideRate.IsComplete = isComplete;
                        peptideRateDict.Add(rateKey, peptideRate);
                    }
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

        private DbPeptideRate GetPeptideRate(RateKey rateKey, ICollection<KeyValuePair<double,double>> points)
        {
            var xValues = new List<double>();
            var yValues = new List<double>();
            var tracerDef = GetTracerDef(rateKey.TracerName);
            double initialEnrichment = tracerDef.InitialApe;
            double finalEnrichment = tracerDef.FinalApe;
            foreach (var entry in points)
            {
                double logValue = Math.Log((entry.Value - finalEnrichment)/(initialEnrichment - finalEnrichment));
                yValues.Add(logValue);
                xValues.Add(entry.Key);
            }
            var statisticsX = new Statistics(xValues.ToArray());
            var statisticsY = new Statistics(yValues.ToArray());
            double rateConstant = statisticsY.Slope(statisticsX);
            double yIntercept = statisticsY.Intercept(statisticsX);
            double actualSquared = 0;
            double predictedSquared = 0;
            double dotProduct = 0;

            foreach (var point in points)
            {
                var predictedValue = Math.Exp(yIntercept)*Math.Exp(point.Key*rateConstant);
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

        private TracerDef GetTracerDef(String name)
        {
            foreach (var tracerDef in Workspace.GetTracerDefs())
            {
                if (name == tracerDef.Name)
                {
                    return tracerDef;
                }
            }
            return null;
        }

        public IDictionary<String,String> GetCohorts()
        {
            return GetCohorts(PeptideAnalysis.FileAnalyses.ListPeptideFileAnalyses(true));
        }

        public IList<String> GetTracerNames()
        {
            var result = new List<String>();
            foreach (var tracerDef in Workspace.GetTracerDefs())
            {
                result.Add(tracerDef.Name);
            }
            return result;
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

        public ICollection<KeyValuePair<double,double>> GetPoints(RateKey rateKey)
        {
            bool isComplete;
            return GetPoints(rateKey, out isComplete);
        }
        
        private ICollection<KeyValuePair<double, double>> GetPoints(RateKey rateKey, out bool isComplete)
        {
            var result = new List<KeyValuePair<double, double>>();
            var tracerDef = GetTracerDef(rateKey.TracerName);
            if (tracerDef == null)
            {
                isComplete = false;
                return result;
            }
            isComplete = true;
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
                double value = peptideDistribution.GetTracerPercent(tracerDef);
                if (double.IsNaN(value))
                {
                    continue;
                }
                result.Add(new KeyValuePair<double, double>(timePoint.Value, value));
            }
            return result;
        }
    }
}
