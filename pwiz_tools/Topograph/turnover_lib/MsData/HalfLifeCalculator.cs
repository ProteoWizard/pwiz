using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.MsData
{
    public class HalfLifeCalculator : ILongOperationJob
    {
        public HalfLifeCalculator(Workspace workspace)
        {
            Workspace = workspace;
            InitialPercent = 0;
            FinalPercent = 100;
        }

        public double InitialPercent
        {
            get; set;
        }

        public double FinalPercent
        {
            get; set;
        }

        public bool FixedInitialPercent
        {
            get; set;
        }

        private List<RowData> Query()
        {
            var hql = "SELECT T.TracerPercent, "
                      + "T.Score, "
                      + "T.PeptideFileAnalysis.PeptideAnalysis.Peptide.Id, "
                      + "T.PeptideFileAnalysis.MsDataFile.Cohort, "
                      + "T.PeptideFileAnalysis.MsDataFile.TimePoint\n"
                      + "FROM " + typeof (DbPeptideDistribution) + " T\n"
                      + "WHERE T.PeptideFileAnalysis.MsDataFile.TimePoint IS NOT NULL\n"
                      + "AND T.Score > :minScore\n" 
                      + "AND T.PeptideFileAnalysis.ValidationStatus != " + (int) ValidationStatus.reject + "\n";
            var query = Session.CreateQuery(hql)
                .SetParameter("minScore", MinScore);

            var result = new List<RowData>();
            foreach (object[] row in query.List())
            {
                var rowData = new RowData
                                  {
                                      TracerPercent = (double) row[0],
                                      Score = (double) row[1],
                                      PeptideId = (long) row[2],
                                      Cohort = (string) row[3],
                                      TimePoint = (double) row[4]
                                  };
                result.Add(rowData);
            }
            return result;
        }

        public void Run(LongOperationBroker longOperationBroker)
        {
            List<RowData> rowDatas;
            using (Session = Workspace.OpenSession())
            {
                longOperationBroker.UpdateStatusMessage("Querying database");
                rowDatas = Query();
            }
            var groupedRowDatas = new Dictionary<String, List<RowData>>();
            var cohorts = new HashSet<String> {""};
            longOperationBroker.UpdateStatusMessage("Grouping results");
            using (Workspace.GetReadLock())
            {
                foreach (var rowData in rowDatas)
                {
                    if (longOperationBroker.WasCancelled)
                    {
                        return;
                    }
                    var peptide = Workspace.Peptides.GetChild(rowData.PeptideId);
                    if (peptide == null)
                    {
                        continue;
                    }
                    cohorts.Add(rowData.Cohort ?? "");
                    rowData.PeptideSequence = peptide.Sequence;
                    rowData.ProteinName = peptide.ProteinName;
                    rowData.ProteinDescription = peptide.ProteinDescription;
                    var key = ByProtein ? rowData.ProteinName : rowData.PeptideSequence;
                    List<RowData> list;
                    if (!groupedRowDatas.TryGetValue(key, out list))
                    {
                        list = new List<RowData>();
                        groupedRowDatas.Add(key, list);
                    }
                    list.Add(rowData);
                }
            }
            Cohorts = cohorts;
            longOperationBroker.UpdateStatusMessage("Calculating results");
            var resultRows = new List<ResultRow>();
            foreach (var entry in groupedRowDatas)
            {
                if (longOperationBroker.WasCancelled)
                {
                    return;
                }
                resultRows.Add(CalculateResultRow(entry.Value));
            }
            ResultRows = resultRows;
        }

        private ResultRow CalculateResultRow(List<RowData> rowDatas)
        {
            var first = rowDatas[0];
            var resultRow = new ResultRow
                                {
                                    ProteinDescription = first.ProteinDescription, 
                                    ProteinName = first.ProteinName
                                };
            if (!ByProtein)
            {
                resultRow.PeptideSequence = first.PeptideSequence;
            }
            foreach (var cohort in Cohorts)
            {
                var resultData = CalculateHalfLife(FilterCohort(rowDatas, cohort));
                resultRow.ResultDatas.Add(cohort, resultData);
            }
            return resultRow;
        }

        private ResultData CalculateHalfLife(IEnumerable<RowData> rowDatas)
        {
            var timePoints = new List<double>();
            var logValues = new List<double>();
            foreach (var rowData in rowDatas)
            {
                var logValue = GetLogValue(rowData.TracerPercent);
                if (double.IsNaN(logValue) || double.IsInfinity(logValue))
                {
                    continue;
                }
                logValues.Add(logValue);
                timePoints.Add(rowData.TimePoint);
            }
            var statsTimePoints = new Statistics(timePoints.ToArray());
            var statsLogValues = new Statistics(logValues.ToArray());
            double rateConstant, stDevRateConstant, rateConstantError, yIntercept;
            if (FixedInitialPercent)
            {
                rateConstant = statsLogValues.SlopeWithoutIntercept(statsTimePoints);
                stDevRateConstant = Statistics.StdDevSlopeWithoutIntercept(statsLogValues, statsTimePoints);
                rateConstantError = stDevRateConstant * GetErrorFactor(timePoints.Count - 1);
                yIntercept = 0;
            }
            else
            {
                rateConstant = statsLogValues.Slope(statsTimePoints);
                stDevRateConstant = Statistics.StdDevB(statsLogValues, statsTimePoints);
                rateConstantError = stDevRateConstant*GetErrorFactor(timePoints.Count - 2);
                yIntercept = Statistics.Intercept(statsLogValues, statsTimePoints);
            }

            return new ResultData
                {
                    RateConstant = rateConstant,
                    RateConstantError = rateConstantError,
                    PointCount = timePoints.Count,
                    YIntercept = yIntercept,
                };
        }

        public double GetLogValue(double tracerPercent)
        {
            return Math.Log((tracerPercent - FinalPercent)/(InitialPercent - FinalPercent));
        }
        public double GetTracerPercent(double logValue)
        {
            return Math.Exp(logValue)*(InitialPercent - FinalPercent) + FinalPercent;
        }

        public ResultData CalculateHalfLife(IEnumerable<PeptideFileAnalysis> peptideFileAnalyses)
        {
            var rowDatas = new List<RowData>();
            foreach (var peptideFileAnalysis in peptideFileAnalyses)
            {
                var peptideDistribution = peptideFileAnalysis.PeptideDistributions.GetChild(PeptideQuantity.tracer_count);
                var rowData = new RowData
                                  {
                                      TimePoint = peptideFileAnalysis.MsDataFile.TimePoint.Value,
                                      TracerPercent = peptideDistribution.TracerPercent,
                                      Score = peptideDistribution.Score
                                  };
                rowDatas.Add(rowData);
            }
            return CalculateHalfLife(rowDatas);
        }

        public HashSet<String> Cohorts { get; private set; }

        private IList<RowData> FilterCohort(IList<RowData> rowDatas, String cohort)
        {
            if (string.IsNullOrEmpty(cohort))
            {
                return rowDatas;
            }
            var result = new List<RowData>();
            foreach (var rowData in rowDatas)
            {
                if (cohort == rowData.Cohort)
                {
                    result.Add(rowData);
                }
            }
            return result;
        }


        public bool Cancel()
        {
            if (Session != null)
            {
                Session.CancelQuery();
            }
            return true;
        }

        public Workspace Workspace { get; private set; }
        public bool ByProtein { get; set; }
        public double MinScore { get; set; }
        ISession Session { get; set; }
        public IList<ResultRow> ResultRows { get; private set; }
        class RowData
        {
            public double TracerPercent { get; set; }
            public double Score { get; set; }
            public long PeptideId { get; set; }
            public String Cohort { get; set; }
            public double TimePoint { get; set; }
            public String PeptideSequence { get; set; }
            public String ProteinName { get; set; }
            public String ProteinDescription { get; set; }
        }

        public class ResultRow
        {
            public ResultRow()
            {
                ResultDatas = new Dictionary<string, ResultData>();
            }
            public String PeptideSequence { get; set; }
            public String ProteinName { get; set; }
            public String ProteinDescription { get; set; }
            public Dictionary<String, ResultData> ResultDatas { get; private set;}
        }

        public class ResultData
        {
            public double YIntercept { get; set; }
            public double RateConstant { get; set; }
            public double RateConstantError { get; set; }
            public int PointCount { get; set; }
            public double HalfLife
            {
                get { return HalfLifeFromRateConstant(RateConstant);}
            }
            public double MinHalfLife
            {
                get { return HalfLifeFromRateConstant(RateConstant - RateConstantError); }
            }
            public double MaxHalfLife
            {
                get { return HalfLifeFromRateConstant(RateConstant + RateConstantError); }
            }
            public double HalfLifeError 
            { 
                get 
                { 
                    return Math.Abs(HalfLifeFromRateConstant(RateConstant + RateConstantError) 
                        - HalfLifeFromRateConstant(RateConstant-RateConstantError))/2;
                }
            }
            private static double HalfLifeFromRateConstant(double rateConstant)
            {
                return Math.Log(.5)/rateConstant;
            }
        }

        private static double GetErrorFactor(int degreesOfFreedom)
        {
            var values = new[]
                             {
                                 Double.NaN,
                                 6.313752,
                                 2.919986,
                                 2.353363,
                                 2.131847,
                                 2.015048,
                                 1.94318,
                                 1.894579,
                                 1.859548,
                                 1.833113,
                                 1.812461,
                                 1.782288,
                                 1.770933,
                                 1.76131,
                                 1.75305,
                                 1.745884,
                                 1.739607,
                                 1.734064,
                                 1.729133,
                                 1.724718,
                                 1.720743,
                                 1.717144,
                                 1.713872,
                                 1.710882,
                                 1.708141,
                                 1.705618,
                                 1.703288,
                                 1.701131,
                                 1.699127,
                                 1.697261,
                                 1.644854,
                             };
            if (degreesOfFreedom < 0)
            {
                return double.NaN;
            }
            degreesOfFreedom = Math.Max(0, degreesOfFreedom);
            degreesOfFreedom = Math.Min(values.Length - 1, degreesOfFreedom);
            return values[degreesOfFreedom];
        }
    }
}
