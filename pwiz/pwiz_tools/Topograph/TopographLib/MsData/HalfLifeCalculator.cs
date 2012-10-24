using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NHibernate;
using pwiz.Common.DataAnalysis;
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
        public HalfLifeCalculator(Workspace workspace, HalfLifeSettings halfLifeSettings)
        {
            Workspace = workspace;
            HalfLifeSettings = halfLifeSettings;
            InitialPercent = 0;
            FinalPercent = 100;
            ExcludedTimePoints = new double[0];
            AcceptMissingMs2Id = workspace.GetAcceptSamplesWithoutMs2Id();
            AcceptIntegrationNotes = new HashSet<IntegrationNote>(workspace.GetAcceptIntegrationNotes());
        }

        public HalfLifeSettings HalfLifeSettings { get; private set; }

        public TracerQuantity NewlySynthesizedTracerQuantity
        {
            get { return HalfLifeSettings.NewlySynthesizedTracerQuantity; }
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
            get { return HalfLifeSettings.ForceThroughOrigin; }
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
            get { return HalfLifeSettings.EvviesFilter; }
        }

        private bool RequiresPrecursorPool
        {
            get
            {
                return HalfLifeSettings.PrecursorPoolCalculation == PrecursorPoolCalculation.MedianPerSample;
            }
        }

        public ICollection<double> ExcludedTimePoints { get; set; }

        private List<RawRowData> QueryRowDatas(LongOperationBroker longOperationBroker)
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
            longOperationBroker.UpdateStatusMessage("Querying database");
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
            var result = new List<RawRowData>();
            var rowList = new List<object[]>();
            query.List(rowList);
            for (int iRow = 0; iRow < rowList.Count; iRow ++)
            {
                var row = rowList[iRow];
                longOperationBroker.UpdateStatusMessage(string.Format("Calculating row {0} of {1}", iRow, rowList.Count));
                try
                {
                    var peptideFileAnalysisId = Convert.ToInt32(row[5]);
                    IDictionary<TracerFormula, PeakData> peaks;
                    peaksDict.TryGetValue(peptideFileAnalysisId, out peaks);
                    var rowData = new RawRowData
                                      {
                                          IndTurnover = (double?)row[0],
                                          IndPrecursorEnrichment = (double?)row[6],
                                          IndTurnoverScore = (double?)row[7],
                                          TracerPercent = (double)row[1],
                                          DeconvolutionScore = (double)row[2],
                                          Peptide = Workspace.Peptides.GetChild((long)row[3]),
                                          MsDataFile = Workspace.MsDataFiles.GetChild((long)row[4]),
                                          Peaks = peaks,
                                          PeptideFileAnalysisId = peptideFileAnalysisId,
                                          ValidationStatus = (ValidationStatus)row[8],
                                          PsmCount = (int)row[9],
                                          IntegrationNote = IntegrationNote.Parse((string)row[10]),
                                      };
                    result.Add(rowData);
                }
                catch (Exception e)
                {
                    Console.Out.WriteLine(e);
                }
            }
            return result;
        }

        private RejectReason? IsAcceptable(RawRowData rowData)
        {
            if (rowData.ValidationStatus == ValidationStatus.accept)
            {
                return null;
            }
            if (rowData.ValidationStatus == ValidationStatus.reject)
            {
                return RejectReason.UserRejected;
            }
            if (rowData.DeconvolutionScore < MinScore)
            {
                return RejectReason.LowDeconvolutionScore;
            }
            Debug.Assert(Equals(rowData.AreaUnderCurve, rowData.PeakAreas.Values.Sum()));
            Debug.Assert(Equals(rowData.AreaUnderCurve, rowData.Peaks.Values.Select(peakData => peakData.TotalArea).Sum()));
            if (MinAuc > 0 && rowData.AreaUnderCurve < MinAuc)
            {
                return RejectReason.LowAreaUnderCurve;
            }
            if (!AcceptMissingMs2Id && rowData.PsmCount == 0)
            {
                return RejectReason.NoMs2Id;
            }
            if (rowData.IntegrationNote != null && !AcceptIntegrationNotes.Contains(rowData.IntegrationNote))
            {
                return RejectReason.RejectIntegrationNote;
            }
            return null;
        }

        public ProcessedRowData ComputeAvgTurnover(RawRowData rowData)
        {
            var result = new ProcessedRowData(rowData)
                             {
                                 RejectReason = IsAcceptable(rowData),
                             };
            result.InitialPrecursorPool = HalfLifeSettings.InitialPrecursorPool;
            switch (HalfLifeSettings.PrecursorPoolCalculation)
            {
                case PrecursorPoolCalculation.Fixed:
                    result.CurrentPrecursorPool = HalfLifeSettings.CurrentPrecursorPool;
                    break;
                case PrecursorPoolCalculation.MedianPerSample:
                    double precursorPool;
                    if (_precursorPools.TryGetValue(rowData.MsDataFile.Id.Value, out precursorPool))
                    {
                        result.CurrentPrecursorPool = precursorPool;
                    }
                    break;
                case PrecursorPoolCalculation.Individual:
                    result.CurrentPrecursorPool = rowData.IndPrecursorEnrichment * 100;
                    break;
            }

            switch (HalfLifeSettings.NewlySynthesizedTracerQuantity)
            {
                case TracerQuantity.LabeledAminoAcid:
                    result.RawValue = rowData.TracerPercent/100;
                    break;
                case TracerQuantity.UnlabeledPeptide:
                    PeakData peakData;
                    if (rowData.Peaks.TryGetValue(TracerFormula.Empty, out peakData))
                    {
                        result.RawValue = peakData.TotalArea/rowData.AreaUnderCurve;
                    }
                    break;
            }
            if (!result.CurrentPrecursorPool.HasValue)
            {
                if (rowData.TracerPercent == result.InitialPrecursorPool)
                {
                    // In an experiment with no labeling, we don't want to reject this item.
                    // Even if we don't know the CurrentPrecursorPool, we do know that the turnover is 0
                    result.Turnover = 0;
                }
                else
                {
                    result.RejectReason = RejectReason.NoPrecursorPool;
                    return result;
                }
            }
            else
            {
                switch (HalfLifeSettings.NewlySynthesizedTracerQuantity)
                {
                    case TracerQuantity.PartialLabelDistribution:
                        {
                            var turnoverCalculator = GetTurnoverCalculator(rowData.Peptide.Sequence);
                            double? turnover;
                            double? turnoverScore;
                            turnoverCalculator.ComputeTurnover(result.CurrentPrecursorPool.Value, rowData.PeakAreas, out turnover, out turnoverScore);
                            result.Turnover = turnover;
                            result.TurnoverScore = turnoverScore;
                        }
                        break;
                    case TracerQuantity.LabeledAminoAcid:
                        result.Turnover = (100 * result.RawValue - result.InitialPrecursorPool) /
                                          (result.CurrentPrecursorPool - result.InitialPrecursorPool);
                        break;
                    case TracerQuantity.UnlabeledPeptide:
                        {
                            var turnoverCalculator = GetTurnoverCalculator(rowData.Peptide.Sequence);
                            var initialRawValue = turnoverCalculator.ExpectedUnlabeledFraction(result.InitialPrecursorPool);
                            var finalRawValue = turnoverCalculator.ExpectedUnlabeledFraction(result.CurrentPrecursorPool.Value);
                            result.Turnover = (result.RawValue - initialRawValue) / (finalRawValue - initialRawValue);
                        }
                        break;
                }
                if (null == result.RejectReason && ValidationStatus.accept != result.RawRowData.ValidationStatus)
                {
                    if (0 < MinTurnoverScore && result.TurnoverScore < MinTurnoverScore)
                    {
                        result.RejectReason = RejectReason.LowTurnoverScore;
                    }
                }
            }
            return result;
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
                RowDatas = QueryRowDatas(longOperationBroker);
            }
            var groupedRowDatas = new Dictionary<String, List<RawRowData>>();
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
                    List<RawRowData> list;
                    if (!groupedRowDatas.TryGetValue(key, out list))
                    {
                        list = new List<RawRowData>();
                        groupedRowDatas.Add(key, list);
                    }
                    list.Add(rowData);
                }
            }
            Cohorts = cohorts;
            var groupRowDatasList = groupedRowDatas.ToArray();
            var resultRows = new List<ResultRow>();
            for (int iGroupRow = 0; iGroupRow < groupRowDatasList.Count(); iGroupRow++)
            {
                longOperationBroker.UpdateStatusMessage(string.Format("Calculating final results {0}/{1}", iGroupRow, groupRowDatasList.Length));
                var entry = groupRowDatasList[iGroupRow];
                if (longOperationBroker.WasCancelled)
                {
                    return;
                }
                var resultRow = CalculateResultRow(entry.Value);
                resultRows.Add(resultRow);
            }
            ResultRows = resultRows;
        }

        private String GetCohort(RawRowData rowData)
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

        private double? GetTimePoint(RawRowData rowData)
        {
            return rowData.MsDataFile.TimePoint;
        }

        private ResultRow CalculateResultRow(List<RawRowData> rowDatas)
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

        private ResultData CalculateHalfLife(IEnumerable<ProcessedRowData> rowDatas)
        {
            IEnumerable<ProcessedRowData> filteredRowDatas;
            if (EvviesFilter != EvviesFilterEnum.None)
            {
                var applicableRowDatas = new List<ProcessedRowData>();
                var values = new Dictionary<double, List<double>>();
                var filteredRowDataList = new List<ProcessedRowData>();
                foreach (var rowData in rowDatas)
                {
                    Debug.Assert(RejectReason.EvviesFilter != rowData.RejectReason);
                    if (null != rowData.RejectReason)
                    {
                        continue;
                    }
                    var value = rowData.Turnover;
                    if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
                    {
                        continue;
                    }
                    var timePoint = GetTimePoint(rowData.RawRowData);
                    if (!timePoint.HasValue)
                    {
                        filteredRowDataList.Add(rowData);
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
                        if (statistics.Median() + 2 * statistics.StdDev() >= .99)
                        {
                            // Throw away any values of 100% or 99% if they are more than 2 SD above the median.
                            max = Math.Min(.99, max);
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
                foreach (var rowData in applicableRowDatas)
                {
                    var cutoff = cutoffs[GetTimePoint(rowData.RawRowData).Value];
                    var value = rowData.Turnover;
                    rowData.EvviesFilterMin = cutoff.Key;
                    rowData.EvviesFilterMax = cutoff.Value;
                    // Only apply Evvie's Filter to rows that has a time point.
                    if (GetTimePoint(rowData.RawRowData).HasValue)
                    {
                        if (value.Value < cutoff.Key || value.Value > cutoff.Value)
                        {
                            Debug.Assert(null == rowData.RejectReason);
                            rowData.RejectReason = RejectReason.EvviesFilter;
                            continue;
                        }
                    }
                    filteredRowDataList.Add(rowData);
                }
                filteredRowDatas = filteredRowDataList;
            }
            else
            {
                filteredRowDatas = rowDatas.Where(rowData=>null == rowData.RejectReason).ToArray();
            }
            if (HalfLifeSettings.SimpleLinearRegression)
            {
                var timePoints = new List<double>();
                var logValues = new List<double>();
                foreach (var rowData in filteredRowDatas)
                {
                    if (null != rowData.RejectReason)
                    {
                        continue;
                    }
                    double? logValue = Math.Log(1-rowData.Turnover.Value);
                    if (!logValue.HasValue || double.IsNaN(logValue.Value) || double.IsInfinity(logValue.Value))
                    {
                        rowData.RejectReason = RejectReason.ValueOutOfRange;
                        continue;
                    }
                    double? timePoint = GetTimePoint(rowData.RawRowData);
                    if (!timePoint.HasValue || ExcludedTimePoints.Contains(timePoint.Value))
                    {
                        rowData.RejectReason = RejectReason.NoTimePoint;
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
                    rateConstantError = stDevRateConstant * GetErrorFactor(timePoints.Count - 2);
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
            else
            {
                var dataPoints = new List<KeyValuePair<double, double>>();
                foreach (var rowData in filteredRowDatas)
                {
                    double? time = rowData.RawRowData.MsDataFile.TimePoint;
                    double? y;
                    y = 1-rowData.Turnover;
                    if (!y.HasValue || !time.HasValue)
                    {
                        continue;
                    }
                    dataPoints.Add(new KeyValuePair<double, double>(time.Value, y.Value));
                }
                var timePoints =
                    Workspace.MsDataFiles.ListChildren().Select(msDataFile => msDataFile.TimePoint)
                    .Where(timePoint => timePoint.HasValue).ToList();
                var resultData = new ResultData
                                     {
                                         PointCount = dataPoints.Count,
                                         FilteredRowDatas = filteredRowDatas.ToArray(),
                                         RowDatas = rowDatas.ToArray(),
                                     };
                if (resultData.RowDatas.Count == 0 || timePoints.Count == 0)
                {
                    resultData.RateConstant = double.NaN;
                    resultData.YIntercept = double.NaN;
                    return resultData;
                }
                NelderMeadSimplex.SimplexConstant[] initialParameters;
                double convergenceTolerance = 0;
                int maxEvaluations = 1000;
                if (FixedInitialPercent)
                {
                    timePoints.Add(0);
                    double timePointDifference = timePoints.Max().Value - timePoints.Min().Value;
                    initialParameters = new[] {new NelderMeadSimplex.SimplexConstant(1/timePointDifference, 1.0/10/timePointDifference)};
                    var regressionResult = NelderMeadSimplex.Regress(initialParameters, convergenceTolerance,
                                                                     maxEvaluations,
                                                                     constants =>
                                                                     SumOfResidualsSquared(
                                                                         x => Math.Exp(-constants[0]*x), dataPoints));
                    resultData.RateConstant = -regressionResult.Constants[0];
                }
                else
                {
                    double timePointDifference = timePoints.Max().Value - timePoints.Min().Value;
                    initialParameters = new[]
                                            {
                                                new NelderMeadSimplex.SimplexConstant(1/timePointDifference,
                                                                                      1.0/10/timePointDifference),
                                                new NelderMeadSimplex.SimplexConstant(0, .1),
                                            };
                    var regressionResult = NelderMeadSimplex.Regress(initialParameters, convergenceTolerance, maxEvaluations,
                        constants=>SumOfResidualsSquared(x=>Math.Exp(-constants[0] * x + constants[1]), dataPoints));
                    resultData.RateConstant = -regressionResult.Constants[0];
                    resultData.YIntercept = regressionResult.Constants[1];
                }
                return resultData;
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

        public ProcessedRowData ToRowData(PeptideFileAnalysis peptideFileAnalysis)
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
            var rowData = new RawRowData
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
                                  ValidationStatus = peptideFileAnalysis.ValidationStatus,
                                  IntegrationNote = peptideFileAnalysis.Peaks.IntegrationNote,
                       };
            return ComputeAvgTurnover(rowData);
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
            var rowDatas = new List<ProcessedRowData>();
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

        private IList<ProcessedRowData> FilterCohort(IList<RawRowData> rowDatas, String cohort)
        {
            var result = new List<ProcessedRowData>();
            foreach (var rowData in rowDatas)
            {
                if (string.IsNullOrEmpty(cohort) || cohort == GetCohort(rowData))
                {
                    result.Add(ComputeAvgTurnover(rowData));
                }
            }
            return result;
        }


        public bool Cancel()
        {
            if (Session != null)
            {
                try
                {
                    Session.CancelQuery();
                }
                catch (ObjectDisposedException)
                {
                    // ignore
                }
            }
            return true;
        }

        public Workspace Workspace { get; private set; }
        public bool ByProtein { get { return HalfLifeSettings.ByProtein; } }
        public bool BySample { get { return HalfLifeSettings.BySample; } }
        public bool ByFile { get; set; }
        public double MinScore { get { return HalfLifeSettings.MinimumDeconvolutionScore; } }
        public double MinTurnoverScore { get { return HalfLifeSettings.MinimumTurnoverScore; } }
        public double MinAuc { get { return HalfLifeSettings.MinimumAuc; } }
        ISession Session { get; set; }
        public IList<ResultRow> ResultRows { get; private set; }
        public IList<RawRowData> RowDatas { get; private set; }
        public class ProcessedRowData
        {
            public ProcessedRowData(RawRowData rawRowData)
            {
                RawRowData = rawRowData;
            }

            public RejectReason? RejectReason { get; set; }
            public RawRowData RawRowData { get; private set; }
            public double? EvviesFilterMin { get; set; }
            public double? EvviesFilterMax { get; set; }
            public double? RawValue { get; set; }
            public double? Turnover { get; set; }
            public double? TurnoverScore { get; set; }
            public double InitialPrecursorPool { get; set; }
            public double? CurrentPrecursorPool { get; set; }
        }
        public class RawRowData
        {
            public double TracerPercent { get; set; }
            public double DeconvolutionScore { get; set; }
            public double? IndPrecursorEnrichment { get; set; }
            public double? IndTurnover { get; set; }
            public double? IndTurnoverScore { get; set; }
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

        public class ResultData: IComparable
        {
            public OutlierFilterData OutlierFilterData { get; set; }
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
            public IList<ProcessedRowData> RowDatas { get; set; }
            [Browsable(false)]
            public IList<ProcessedRowData> FilteredRowDatas { get; set; }
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
                return HalfLife.ToString("0.00") + " [" + MinHalfLife.ToString("0.00") + "," + MaxHalfLife.ToString("0.00") + "]";
            }

            public int CompareTo(object obj)
            {
                if (null == obj)
                {
                    return 1;
                }
                return HalfLife.CompareTo(((ResultData) obj).HalfLife);
            }
        }

        public class OutlierFilterData
        {
            public IList<RawRowData> AllRowDatas { get; set; }
            public IList<KeyValuePair<RawRowData, double>> PrefilteredRowDatas { get; set; }
            public double? FilterCutoffMin { get; set; }
            public double? FilterCutoffMax { get; set; }
            public IList<KeyValuePair<RawRowData, double>> PostFilteredRowDatas { get; set; }
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

        private double SumOfResidualsSquared(Func<double, double> func, IEnumerable<KeyValuePair<double,double>> points)
        {
            double result = 0.0;
            foreach (var point in points)
            {
                double funcValue = func.Invoke(point.Key);
                double difference = funcValue - point.Value;
                result += difference * difference;
            }
            return result;
        }
    }
    public enum HalfLifeCalculationType
    {
        TracerPercent,
        IndividualPrecursorPool,
        GroupPrecursorPool,
        OldGroupPrecursorPool,
    }

    public enum TracerQuantity
    {
        LabeledAminoAcid,
        UnlabeledPeptide,
        PartialLabelDistribution,
    }
    public enum PrecursorPoolCalculation
    {
        Fixed,
        MedianPerSample,
        Individual,
    }

    public enum EvviesFilterEnum
    {
        None,
        Jun2011,
        Oct2011,
        TwoStdDev,
    }

    public enum RejectReason
    {
        UserRejected,
        LowDeconvolutionScore,
        LowAreaUnderCurve,
        NoMs2Id,
        RejectIntegrationNote,
        LowTurnoverScore,
        EvviesFilter,
        ValueOutOfRange,
        NoTimePoint,
        NoPrecursorPool,
    }
}
