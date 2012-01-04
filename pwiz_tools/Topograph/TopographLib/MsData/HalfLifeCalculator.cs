using System;
using System.Collections.Generic;
using System.ComponentModel;
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
            ExcludedTimePoints = new double[0];
            MinScore = workspace.GetAcceptMinDeconvolutionScore();
            AcceptMissingMs2Id = workspace.GetAcceptSamplesWithoutMs2Id();
            AcceptIntegrationNotes = new HashSet<IntegrationNote>(workspace.GetAcceptIntegrationNotes());
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
        public bool AcceptMissingMs2Id { get; set; }
        public ICollection<IntegrationNote> AcceptIntegrationNotes { get; set; }
        public int? MaxResults { get; set; }
        /// <summary>
        /// For bug 395 : Automated outlier-trimming algorithm/QC filter
        /// Apply Evvie's special criteria to filter out outliers:
        /// 1) Treat each time point separately.
        /// 2) Divide standard deviation by mean.
        /// 3) If the ratio is less than 0.3, set a cutoff of 2 standard deviations above or below the *median*--not the mean. If the ratio is 0.3 or greater, set a cutoff of 1 standard deviation.
        /// 4) Exclude any data point falling outside the cutoffs.
        /// 
        /// Also, for bug 91:
        /// If EvviesFilter is EvviesFilterEnum.Oct2011:
        /// 1.1) Exclude all points more than 3 standard deviations above or below the MEDIAN.
        /// 1.2) Exclude all points that are 99% or 100% and are 2 standard deviations above the median.
        /// 1.3) Determine the *new* mean, median, and SD. 
        /// </summary>
        public EvviesFilterEnum EvviesFilter
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

        public ICollection<double> ExcludedTimePoints { get; set; }

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
                       + "\nF.ValidationStatus,"
                       + "\nF.PsmCount,"
                       + "\nF.IntegrationNote"
                       + "\nFROM " + typeof (DbPeptideFileAnalysis) + " F"
                       + "\nWHERE F.TracerPercent IS NOT NULL");
            var query = Session.CreateQuery(hql.ToString());
            if (MaxResults.HasValue)
            {
                query.SetMaxResults(MaxResults.Value);
            }
            var peaksQuery = Session.CreateQuery("SELECT P.PeptideFileAnalysis.Id, P.Name, P.TotalArea, P.StartTime, P.EndTime"
                                                 + "\nFROM " + typeof (DbPeak) + " P");
            var peaksDict = new Dictionary<long, IDictionary<TracerFormula, PeakData>>();
            foreach (object[] row in peaksQuery.List())
            {
                var peptideFileAnalysisId = Convert.ToInt32(row[0]);
                IDictionary<TracerFormula, PeakData> dict;
                if (!peaksDict.TryGetValue(peptideFileAnalysisId, out dict))
                {
                    dict = new Dictionary<TracerFormula, PeakData>();
                    peaksDict.Add(peptideFileAnalysisId, dict);
                }
                dict.Add(TracerFormula.Parse(Convert.ToString(row[1])),
                         new PeakData
                             {
                                 TotalArea = Convert.ToDouble(row[2]),
                                 StartTime = Convert.ToDouble(row[3]),
                                 EndTime = Convert.ToDouble(row[4]),
                        }
                    );
            }
            var result = new List<RowData>();
            foreach (object[] row in query.List())
            {
                try
                {
                    var peptideFileAnalysisId = Convert.ToInt32(row[5]);
                    IDictionary<TracerFormula, PeakData> peaks;
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
                                          PsmCount = (int) row[9],
                                          IntegrationNote = IntegrationNote.Parse((string) row[10]),
                                      };
                    rowData.Accept = IsAcceptable(rowData);
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

        private bool IsAcceptable(RowData rowData)
        {
            if (rowData.ValidationStatus == ValidationStatus.accept)
            {
                return true;
            }
            if (rowData.ValidationStatus == ValidationStatus.reject)
            {
                return false;
            }
            if (rowData.DeconvolutionScore < MinScore)
            {
                return false;
            }
            if (MinAuc > 0 && rowData.PeakAreas.Values.Sum() < MinAuc)
            {
                return false;
            }
            if (!AcceptMissingMs2Id && rowData.PsmCount == 0)
            {
                return false;
            }
            if (rowData.IntegrationNote != null && !AcceptIntegrationNotes.Contains(rowData.IntegrationNote))
            {
                return false;
            }
            return true;
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
                    turnoverCalculator.ComputeTurnover(precursorEnrichment, rowData.PeakAreas, out turnover, out turnoverScore);
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
                        + "\nAND F.DeconvolutionScore >= :minScore").SetParameter("minScore", Workspace.GetMinDeconvolutionScoreForAvgPrecursorPool());
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
            if (ByFile)
            {
                return rowData.MsDataFile.Name;
            }
            return GetCohort(rowData.MsDataFile, BySample);
        }

        public static string GetCohort(MsDataFile msDataFile, bool bySample)
        {
            var result = new StringBuilder();
            if (msDataFile.Cohort != null)
            {
                result.Append(msDataFile.Cohort);
            }
            if (bySample && !string.IsNullOrEmpty(msDataFile.Sample))
            {
                if (result.Length > 0)
                {
                    result.Append(" ");
                }
                result.Append(msDataFile.Sample);
            }
            return result.ToString();
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
                resultRow.Peptide = first.Peptide;
            }
            foreach (var cohort in Cohorts)
            {
                var resultData = CalculateHalfLife(FilterCohort(rowDatas, cohort));
                resultRow.HalfLives.Add(cohort, resultData);
            }
            return resultRow;
        }

        private ResultData CalculateHalfLife(IEnumerable<RowData> rowDatas)
        {
            IEnumerable<RowData> filteredRowDatas;
            if (EvviesFilter != EvviesFilterEnum.None)
            {
                var applicableRowDatas = new List<RowData>();
                var values = new Dictionary<double, List<double>>();
                foreach (var rowData in rowDatas)
                {
                    if (!rowData.Accept)
                    {
                        continue;
                    }
                    double? score;
                    var value = GetValue(rowData, out score);
                    if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
                    {
                        continue;
                    }
                    var timePoint = GetTimePoint(rowData);
                    if (!timePoint.HasValue)
                    {
                        continue;
                    }
                    List<double> list;
                    if (!values.TryGetValue(timePoint.Value, out list))
                    {
                        list = new List<double>();
                        values.Add(timePoint.Value, list);
                    }
                    list.Add(value.Value);
                    applicableRowDatas.Add(rowData);
                }
                if (EvviesFilter == EvviesFilterEnum.Oct2011)
                {
                    foreach (var entry in values.ToArray())
                    {
                        var statistics = new Statistics(entry.Value.ToArray());
                        var min = statistics.Median() - 3*statistics.StdDev();
                        var max = statistics.Median() + 3*statistics.StdDev();
                        if (statistics.Median() + 2 * statistics.StdDev() >= 99)
                        {
                            // Throw away any values of 100% or 99% if they are more than 2 SD above the median.
                            max = Math.Min(99, max);
                        }
                        var newValues = entry.Value.Where(v => v >= min && v <= max).ToList();
                        if (newValues.Count != entry.Value.Count)
                        {
                            values[entry.Key] = newValues;
                        }
                    }
                }

                var cutoffs = new Dictionary<double, KeyValuePair<double, double>>();
                foreach (var entry in values)
                {
                    var statistics = new Statistics(entry.Value.ToArray());
                    var mean = statistics.Mean();
                    var stdDev = statistics.StdDev();
                    double cutoff;
                    if (EvviesFilter == EvviesFilterEnum.TwoStdDev)
                    {
                        cutoff = 2*stdDev;
                    }
                    else
                    {
                        if (stdDev / mean < .3)
                        {
                            cutoff = 2 * stdDev;
                        }
                        else
                        {
                            cutoff = stdDev;
                        }
                    }
                    cutoffs.Add(entry.Key, new KeyValuePair<double, double>(mean - cutoff, mean + cutoff));
                }
                var filteredRowDataList = new List<RowData>();
                foreach (var rowData in applicableRowDatas)
                {
                    var cutoff = cutoffs[GetTimePoint(rowData).Value];
                    double? score;
                    var value = GetValue(rowData, out score);
                    if (value.Value < cutoff.Key || value.Value > cutoff.Value)
                    {
                        continue;
                    }
                    filteredRowDataList.Add(rowData);
                }
                filteredRowDatas = filteredRowDataList;
            }
            else
            {
                filteredRowDatas = rowDatas;
            }
            var timePoints = new List<double>();
            var logValues = new List<double>();
            foreach (var rowData in filteredRowDatas)
            {
                if (!rowData.Accept)
                {
                    continue;
                }
                double? logValue = GetLogValue(rowData);
                if (!logValue.HasValue || double.IsNaN(logValue.Value) || double.IsInfinity(logValue.Value))
                {
                    continue;
                }
                double? timePoint = GetTimePoint(rowData);
                if (!timePoint.HasValue || ExcludedTimePoints.Contains(timePoint.Value))
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
                    RowDatas = rowDatas.ToArray(),
                    FilteredRowDatas = filteredRowDatas.ToArray(),
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
            var peaks = new Dictionary<TracerFormula, PeakData>();
            foreach (var dbPeak in peptideFileAnalysis.Peaks.ListChildren())
            {
                peaks.Add(dbPeak.TracerFormula, new PeakData
                                                    {
                                                        TotalArea = dbPeak.TotalArea,
                                                        StartTime = dbPeak.StartTime,
                                                        EndTime = dbPeak.EndTime,
                                                    });
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
                                  Peaks = peaks,
                                  PeptideFileAnalysisId = peptideFileAnalysis.Id.Value,
                                  PsmCount = peptideFileAnalysis.PsmCount,
                                  IntegrationNote = peptideFileAnalysis.Peaks.IntegrationNote,
                       };
            rowData.Accept = IsAcceptable(rowData);
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
        public bool BySample { get; set; }
        public bool ByFile { get; set; }
        public double MinScore { get; set; }
        public double MinAuc { get; set; }
        ISession Session { get; set; }
        public IList<ResultRow> ResultRows { get; private set; }
        public IList<RowData> RowDatas { get; private set; }
        public class RowData
        {
            public bool Accept { get; set; }
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
            public int PsmCount { get; set; }
            public IntegrationNote IntegrationNote { get; set; }
            public IDictionary<TracerFormula, PeakData> Peaks { get; set; }
            public double? AreaUnderCurve
            {
                get
                {
                    if (Peaks == null)
                    {
                        return null;
                    }
                    return Peaks.Values.Select(p=>p.TotalArea).Sum();
                }
            }
            public IDictionary<TracerFormula, double> PeakAreas
            {
                get
                {
                    return Peaks == null ? new Dictionary<TracerFormula, double>() : Peaks.Keys.ToDictionary(k => k, k => Peaks[k].TotalArea);
                }
            }
            public double? StartTime
            {
                get
                {
                    if (Peaks == null)
                    {
                        return null;
                    }
                    return Peaks.Values.Select(p => p.StartTime).Min();
                }
            }
            public double? EndTime
            {
                get
                {
                    if (Peaks == null)
                    {
                        return null;
                    }
                    return Peaks.Values.Select(p => p.EndTime).Max();
                }
            }
        }
        public class PeakData
        {
            public double TotalArea { get; set; }
            public double StartTime { get; set; }
            public double EndTime { get; set; }
        }

        public class ResultRow
        {
            public ResultRow()
            {
                HalfLives = new Dictionary<string, ResultData>();
            }
            public Peptide Peptide { get; set; }
            public String ProteinName { get; set; }
            public String ProteinDescription { get; set; }
            public IDictionary<String, ResultData> HalfLives { get; private set;}
        }

        public class ResultData
        {
            public double YIntercept { get; set; }
            public double XIntercept {get
            {
                return -YIntercept/RateConstant;
            }}
            public double RateConstant { get; set; }
            public double RateConstantStdDev { get; set; }
            public double RateConstantError { get; set; }
            public double? RSquared { get; set; }
            public int PointCount { get; set; }
            [Browsable(false)]
            public IList<RowData> RowDatas { get; set; }
            [Browsable(false)]
            public IList<RowData> FilteredRowDatas { get; set; }
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
            public override string ToString()
            {
                return HalfLife + " [" + MinHalfLife + "," + MaxHalfLife + "]";
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

    public enum EvviesFilterEnum
    {
        None,
        Jun2011,
        Oct2011,
        TwoStdDev,
    }
}
