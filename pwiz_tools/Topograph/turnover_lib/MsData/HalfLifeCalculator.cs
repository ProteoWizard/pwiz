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
        private IDictionary<long, double> _precursorPools;
        private readonly IDictionary<string, TurnoverCalculator> _turnoverCalculators 
            = new Dictionary<string, TurnoverCalculator>();
        public HalfLifeCalculator(Workspace workspace, HalfLifeCalculationType halfLifeCalculationType)
        {
            Workspace = workspace;
            HalfLifeCalculationType = halfLifeCalculationType;
            InitialPercent = 0;
            FinalPercent = 100;
        }

        public HalfLifeCalculationType HalfLifeCalculationType
        {
            get; private set;
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

        private bool RequiresPrecursorPool
        {
            get
            {
                return HalfLifeCalculationType == HalfLifeCalculationType.OldGroupPrecursorPool
                       || HalfLifeCalculationType == HalfLifeCalculationType.GroupPrecursorPool;
            }
        }

        private List<RowData> QueryRowDatas()
        {
            var hql = new StringBuilder();
            hql.Append("SELECT"
                       +"\nF.Turnover * 100,"
                       +"\nF.TracerPercent,"
                       +"\nF.DeconvolutionScore, "
                       + "\nF.PeptideAnalysis.Peptide.Id, "
                       + "\nF.MsDataFile.Id, "
                       + "\nF.Id,"
                       + "\nF.PrecursorEnrichment,"
                       + "\nF.TurnoverScore,"
                       + "\nF.ValidationStatus"
                       + "\nFROM " + typeof (DbPeptideFileAnalysis) + " F"
                       + "\nWHERE F.DeconvolutionScore >= :minScore"
                       + "\nAND F.TracerPercent IS NOT NULL");
            var query = Session.CreateQuery(hql.ToString())
                .SetParameter("minScore", MinScore);
            var peaksQuery = Session.CreateQuery("SELECT P.PeptideFileAnalysis.Id, P.Name, P.TotalArea"
                                                 + "\nFROM " + typeof (DbPeak) + " P");
            var peaksDict = new Dictionary<long, IDictionary<TracerFormula, double>>();
            foreach (object[] row in peaksQuery.List())
            {
                var peptideFileAnalysisId = Convert.ToInt32(row[0]);
                IDictionary<TracerFormula, double> dict;
                if (!peaksDict.TryGetValue(peptideFileAnalysisId, out dict))
                {
                    dict = new Dictionary<TracerFormula, double>();
                    peaksDict.Add(peptideFileAnalysisId, dict);
                }
                dict.Add(TracerFormula.Parse(Convert.ToString(row[1])), Convert.ToDouble(row[2]));
            }
            var result = new List<RowData>();
            foreach (object[] row in query.List())
            {
                try
                {
                    var peptideFileAnalysisId = Convert.ToInt32(row[5]);
                    IDictionary<TracerFormula, double> peaks;
                    peaksDict.TryGetValue(peptideFileAnalysisId, out peaks);
                    var rowData = new RowData
                                      {
                                          IndTurnover = (double?) row[0],
                                          IndPrecursorEnrichment =  (double?) row[6],
                                          IndTurnoverScore = (double?) row[7],
                                          TracerPercent = (double) row[1],
                                          DeconvolutionScore = (double) row[2],
                                          Peptide = Workspace.Peptides.GetChild((long) row[3]),
                                          MsDataFile = Workspace.MsDataFiles.GetChild((long) row[4]),
                                          Peaks = peaks,
                                          PeptideFileAnalysisId = peptideFileAnalysisId,
                                          ValidationStatus = (ValidationStatus) row[8],
                                      };
                    ComputeAvgTurnover(rowData);
                    result.Add(rowData);
                }
                catch (Exception e)
                {
                    Console.Out.WriteLine(e);
                }
            }
            return result;
        }

        private void ComputeAvgTurnover(RowData rowData)
        {
            if (!RequiresPrecursorPool)
            {
                return;
            }
            double precursorEnrichment;
            if (_precursorPools.TryGetValue(rowData.MsDataFile.Id.Value, out precursorEnrichment))
            {
                rowData.AvgPrecursorEnrichment = precursorEnrichment;
                if (HalfLifeCalculationType == HalfLifeCalculationType.GroupPrecursorPool)
                {
                    var turnoverCalculator = GetTurnoverCalculator(rowData.Peptide.Sequence);
                    double? turnover;
                    double? turnoverScore;
                    turnoverCalculator.ComputeTurnover(precursorEnrichment, rowData.Peaks, out turnover, out turnoverScore);
                    rowData.AvgTurnover = turnover * 100;
                    rowData.AvgTurnoverScore = turnoverScore;
                }
                else if (HalfLifeCalculationType == HalfLifeCalculationType.OldGroupPrecursorPool)
                {
                    rowData.AvgTurnover = 100 * (rowData.TracerPercent - InitialPercent) / (precursorEnrichment - InitialPercent);
                }
            }
        }

        private IDictionary<long, double> GetPrecursorPools(ISession session)
        {
            var query = session.CreateQuery("SELECT F.MsDataFile.Id, F.PrecursorEnrichment * 100 FROM " + typeof (DbPeptideFileAnalysis) + " F"
                        + "\nWHERE F.TracerPercent IS NOT NULL"
                        + "\nAND F.PrecursorEnrichment IS NOT NULL"
                        + "\nAND F.ValidationStatus <> " + (int) ValidationStatus.reject
                        + "\nAND F.DeconvolutionScore > :minScore").SetParameter("minScore", MinScore);
            var valueLists = new Dictionary<long, IList<double>>();
            foreach (object[] row in query.List())
            {
                IList<double> list;
                var dataFileId = (long) row[0];
                var precursorEnrichment = (double) row[1];
                if (!valueLists.TryGetValue(dataFileId, out list))
                {
                    list = new List<double>();
                    valueLists.Add(dataFileId, list);
                }
                list.Add(precursorEnrichment);
            }
            return valueLists.ToDictionary(kv => kv.Key, kv => new Statistics(kv.Value.ToArray()).Median());
        }

        public void Run(LongOperationBroker longOperationBroker)
        {
            using (Session = Workspace.OpenSession())
            {
                if (RequiresPrecursorPool)
                {
                    longOperationBroker.UpdateStatusMessage("Querying precursor pools");
                    _precursorPools = GetPrecursorPools(Session);
                }
                longOperationBroker.UpdateStatusMessage("Querying database");
                RowDatas = QueryRowDatas();
            }
            var groupedRowDatas = new Dictionary<String, List<RowData>>();
            var cohorts = new HashSet<String> {""};
            longOperationBroker.UpdateStatusMessage("Grouping results");
            using (Workspace.GetReadLock())
            {
                foreach (var rowData in RowDatas)
                {
                    if (longOperationBroker.WasCancelled)
                    {
                        return;
                    }
                    if (rowData.ValidationStatus == ValidationStatus.reject)
                    {
                        continue;
                    }
                    cohorts.Add(GetCohort(rowData));
                    var key = ByProtein ? rowData.ProteinName : rowData.Peptide.Sequence;
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
                var resultRow = CalculateResultRow(entry.Value);
                resultRows.Add(resultRow);
            }
            ResultRows = resultRows;
        }

        private String GetCohort(RowData rowData)
        {
            return rowData.MsDataFile.Cohort ?? "";
        }

        private double? GetTimePoint(RowData rowData)
        {
            return rowData.MsDataFile.TimePoint;
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
                resultRow.PeptideSequence = first.Peptide.Sequence;
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
                double? logValue = GetLogValue(rowData);
                if (!logValue.HasValue || double.IsNaN(logValue.Value) || double.IsInfinity(logValue.Value))
                {
                    continue;
                }
                double? timePoint = GetTimePoint(rowData);
                if (!timePoint.HasValue)
                {
                    continue;
                }
                logValues.Add(logValue.Value);
                timePoints.Add(timePoint.Value);
            }
            var statsTimePoints = new Statistics(timePoints.ToArray());
            var statsLogValues = new Statistics(logValues.ToArray());
            double rateConstant, stDevRateConstant, rateConstantError, yIntercept;
            double? rSquared = null;
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
                rSquared = Math.Pow(Statistics.R(statsLogValues, statsTimePoints), 2);
            }
            return new ResultData
                {
                    RateConstant = rateConstant,
                    RateConstantStdDev = stDevRateConstant,
                    RateConstantError = rateConstantError,
                    PointCount = timePoints.Count,
                    YIntercept = yIntercept,
                    RSquared = rSquared,
                };
        }

        public double InvertLogValue(double logValue)
        {
            if (HalfLifeCalculationType == HalfLifeCalculationType.TracerPercent)
            {
                return Math.Exp(logValue)*(InitialPercent - FinalPercent) + FinalPercent;
            }
            return 100*(1 - Math.Exp(logValue));
        }

        private double? GetValue(RowData rowData, out double? score)
        {
            score = null;
            if (rowData == null)
            {
                return null;
            }
            switch (HalfLifeCalculationType)
            {
                case HalfLifeCalculationType.TracerPercent:
                    return rowData.TracerPercent;
                case HalfLifeCalculationType.IndividualPrecursorPool:
                    return rowData.IndTurnover;
                case HalfLifeCalculationType.OldGroupPrecursorPool:
                case HalfLifeCalculationType.GroupPrecursorPool:
                    return rowData.AvgTurnover;    
                default:
                    throw new ArgumentException();
                
            }
        }

        public double? GetPrecursorPool(MsDataFile msDataFile)
        {
            double precursorPool;
            if (!_precursorPools.TryGetValue(msDataFile.Id.Value, out precursorPool))
            {
                return null;
            }
            return precursorPool;
        }

        private double? GetLogValue(RowData rowData)
        {
            double? score;
            double? value = GetValue(rowData, out score);
            if (!value.HasValue)
            {
                return null;
            }
            switch (HalfLifeCalculationType)
            {
                case HalfLifeCalculationType.TracerPercent:
                    return Math.Log((value.Value - FinalPercent) / (InitialPercent - FinalPercent));
                default:
                    return Math.Log(1- (value.Value / 100));
            }
        }


        public RowData ToRowData(PeptideFileAnalysis peptideFileAnalysis)
        {
            if (!peptideFileAnalysis.Peaks.IsCalculated)
            {
                return null;
            }
            var rowData = new RowData
                       {
                           MsDataFile = peptideFileAnalysis.MsDataFile,
                           Peptide = peptideFileAnalysis.Peptide,
                           DeconvolutionScore = peptideFileAnalysis.Peaks.DeconvolutionScore.Value,
                           TracerPercent = peptideFileAnalysis.Peaks.TracerPercent.Value,
                           IndTurnover = peptideFileAnalysis.Peaks.Turnover*100,
                           IndTurnoverScore = peptideFileAnalysis.Peaks.TurnoverScore,
                           IndPrecursorEnrichment = peptideFileAnalysis.Peaks.PrecursorEnrichment,
                           Peaks = peptideFileAnalysis.Peaks.ToDictionary(),
                       };
            ComputeAvgTurnover(rowData);
            return rowData;
        }

        public double? GetValue(PeptideFileAnalysis peptideFileAnalysis)
        {
            double? score;
            return GetValue(ToRowData(peptideFileAnalysis), out score);
        }

        public double? GetLogValue(PeptideFileAnalysis peptideFileAnalysis)
        {
            return GetLogValue(ToRowData(peptideFileAnalysis));
        }


        public ResultData CalculateHalfLife(IEnumerable<PeptideFileAnalysis> peptideFileAnalyses)
        {
            if (RequiresPrecursorPool && _precursorPools == null)
            {
                using (var session = Workspace.OpenSession())
                {
                    _precursorPools = GetPrecursorPools(session);
                }
            }
            var rowDatas = new List<RowData>();
            foreach (var peptideFileAnalysis in peptideFileAnalyses)
            {
                var rowData = ToRowData(peptideFileAnalysis);
                if (rowData == null)
                {
                    continue;
                }
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
                if (cohort == GetCohort(rowData))
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
        public IList<RowData> RowDatas { get; private set; }
        public class RowData
        {
            public double TracerPercent { get; set; }
            public double DeconvolutionScore { get; set; }
            public double? IndPrecursorEnrichment { get; set; }
            public double? IndTurnover { get; set; }
            public double? IndTurnoverScore { get; set; }
            public double? AvgPrecursorEnrichment { get; set; }
            public double? AvgTurnover { get; set; }
            public double? AvgTurnoverScore { get; set; }
            public Peptide Peptide { get; set; }
            public ValidationStatus ValidationStatus { get; set; }
            public String ProteinName { get { return Peptide.ProteinName; } }
            public String ProteinDescription { get { return Peptide.ProteinDescription; } }
            public MsDataFile MsDataFile { get; set; }
            public long PeptideFileAnalysisId { get; set; }
            public IDictionary<TracerFormula, double> Peaks { get; set; }
            public double? AreaUnderCurve
            {
                get
                {
                    if (Peaks == null)
                    {
                        return null;
                    }
                    return Peaks.Values.Sum();
                }
            }
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
            public double RateConstantStdDev { get; set; }
            public double RateConstantError { get; set; }
            public double? RSquared { get; set; }
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
        public TurnoverCalculator GetTurnoverCalculator(string peptideSequence)
        {
            peptideSequence = Peptide.TrimSequence(peptideSequence);
            TurnoverCalculator turnoverCalculator;
            if (_turnoverCalculators.TryGetValue(peptideSequence, out turnoverCalculator))
            {
                return turnoverCalculator;
            }
            turnoverCalculator = new TurnoverCalculator(Workspace, peptideSequence);
            _turnoverCalculators.Add(peptideSequence, turnoverCalculator);
            return turnoverCalculator;
        }
    }
    public enum HalfLifeCalculationType
    {
        TracerPercent,
        IndividualPrecursorPool,
        GroupPrecursorPool,
        OldGroupPrecursorPool,
    }
}
