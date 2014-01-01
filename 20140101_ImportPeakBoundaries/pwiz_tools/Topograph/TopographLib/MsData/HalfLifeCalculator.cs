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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using pwiz.Common.DataAnalysis;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.Model.Data;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.MsData
{
    public class HalfLifeCalculator
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
        /// For bugfix 395 : Automated outlier-trimming algorithm/QC filter
        /// Apply Evvie's special criteria to filter out outliers:
        /// 1) Treat each time point separately.
        /// 2) Divide standard deviation by mean.
        /// 3) If the ratio is less than 0.3, set a cutoff of 2 standard deviations above or below the *median*--not the mean. If the ratio is 0.3 or greater, set a cutoff of 1 standard deviation.
        /// 4) Exclude any data point falling outside the cutoffs.
        /// 
        /// Also, for bugfix 91:
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
            Debug.Assert(Equals(rowData.AreaUnderCurve, rowData.PeakAreas.Sum()));
            Debug.Assert(Equals(rowData.AreaUnderCurve, rowData.Peaks.Select(peakData => peakData.Area).Sum()));
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
            if (rowData.MsDataFile.PrecursorPool.HasValue)
            {
                result.CurrentPrecursorPool = 100 * rowData.MsDataFile.PrecursorPool.Value.DoubleValue;
            }
            if (!result.CurrentPrecursorPool.HasValue)
            {
                switch (HalfLifeSettings.PrecursorPoolCalculation)
                {
                    case PrecursorPoolCalculation.Fixed:
                        result.CurrentPrecursorPool = HalfLifeSettings.CurrentPrecursorPool;
                        break;
                    case PrecursorPoolCalculation.MedianPerSample:
                        double precursorPool;
                        if (_precursorPools.TryGetValue(rowData.MsDataFile.Id, out precursorPool))
                        {
                            result.CurrentPrecursorPool = precursorPool;
                        }
                        break;
                    case PrecursorPoolCalculation.Individual:
                        result.CurrentPrecursorPool = rowData.IndPrecursorEnrichment * 100;
                        break;
                }
            }
            switch (HalfLifeSettings.NewlySynthesizedTracerQuantity)
            {
                case TracerQuantity.LabeledAminoAcid:
                    result.RawValue = rowData.TracerPercent/100;
                    break;
                case TracerQuantity.UnlabeledPeptide:
                    if (rowData.Peaks.Count > 0) 
                    {
                        result.RawValue = rowData.Peaks[0].Area/rowData.AreaUnderCurve;
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
                            
                            turnoverCalculator.ComputeTurnover(result.CurrentPrecursorPool.Value, turnoverCalculator.ToTracerFormulaDict(rowData.PeakAreas), out turnover, out turnoverScore);
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
            if (null == result.RejectReason && !result.Turnover.HasValue)
            {
                result.RejectReason = RejectReason.ValueOutOfRange;
            }
            return result;
        }

        private IDictionary<long, double> GetPrecursorPools()
        {
            var minScore = Workspace.GetMinDeconvolutionScoreForAvgPrecursorPool();
            var valueLists = Workspace.PeptideAnalyses
                .SelectMany(peptideAnalysis => peptideAnalysis.FileAnalyses)
                .Where(peptideFileAnalysis => null != peptideFileAnalysis.PeakData.Peaks
                                              && peptideFileAnalysis.PeakData.PrecursorEnrichment.HasValue
                                              && ValidationStatus.reject != peptideFileAnalysis.ValidationStatus
                                              && peptideFileAnalysis.PeakData.DeconvolutionScore >= minScore)
                .ToLookup(peptideFileAnalysis => peptideFileAnalysis.MsDataFile.Id,
                          peptideFileAnalysis => peptideFileAnalysis.PeakData.PrecursorEnrichment.GetValueOrDefault());
            return valueLists.ToDictionary(grouping => grouping.Key, grouping => new Statistics(grouping.ToArray()).Median() * 100.0);
        }

        public void Run(LongOperationBroker longOperationBroker)
        {
            if (RequiresPrecursorPool)
            {
                longOperationBroker.UpdateStatusMessage("Querying precursor pools");
                _precursorPools = GetPrecursorPools();
            }
            RowDatas = new List<RawRowData>();
            foreach (var peptideAnalysis in Workspace.PeptideAnalyses.ToArray())
            {
                foreach (var peptideFileAnalysis in peptideAnalysis.FileAnalyses)
                {
                    if (longOperationBroker.WasCancelled)
                    {
                        return;
                    }
                    var rawRowData = ToRawRowData(peptideFileAnalysis);
                    if (null != rawRowData)
                    {
                        RowDatas.Add(rawRowData);
                    }
                }
            }
            var groupedRowDatas = new Dictionary<String, List<RawRowData>>();
            var cohorts = new HashSet<String> {""};
            longOperationBroker.UpdateStatusMessage("Grouping results");
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
                key = key ?? "";
                List<RawRowData> list;
                if (!groupedRowDatas.TryGetValue(key, out list))
                {
                    list = new List<RawRowData>();
                    groupedRowDatas.Add(key, list);
                }
                list.Add(rowData);
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

        private ResultData CalculateHalfLife(ICollection<ProcessedRowData> rowDatas)
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
                    Workspace.MsDataFiles.Select(msDataFile => msDataFile.TimePoint)
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

        public ProcessedRowData ToRowData(PeptideFileAnalysis peptideFileAnalysis)
        {
            var rawRowData = ToRawRowData(peptideFileAnalysis);
            if (null == rawRowData)
            {
                return null;
            }
            return ComputeAvgTurnover(rawRowData);
        }

        RawRowData ToRawRowData(PeptideFileAnalysis peptideFileAnalysis)
        {
            if (!peptideFileAnalysis.PeakData.IsCalculated)
            {
                return null;
            }
            return new RawRowData(peptideFileAnalysis.Data)
            {
                MsDataFile = peptideFileAnalysis.MsDataFile,
                Peptide = peptideFileAnalysis.Peptide,
                PeptideFileAnalysisId = peptideFileAnalysis.Id,
                PsmCount = peptideFileAnalysis.PsmCount,
            };
        }

        public ResultData CalculateHalfLife(IEnumerable<PeptideFileAnalysis> peptideFileAnalyses)
        {
            if (RequiresPrecursorPool && _precursorPools == null)
            {
                _precursorPools = GetPrecursorPools();
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
                    var processedRowData = ComputeAvgTurnover(rowData);
                    if (null == processedRowData.Turnover && null == processedRowData.RejectReason)
                    {
                        Trace.TraceInformation("Null turnover for " + processedRowData);
                    }
                    result.Add(ComputeAvgTurnover(rowData));
                }
            }
            return result;
        }

        public Workspace Workspace { get; private set; }
        public bool ByProtein { get { return HalfLifeSettings.ByProtein; } }
        public bool BySample { get { return HalfLifeSettings.BySample; } }
        public bool ByFile { get; set; }
        public double MinScore { get { return HalfLifeSettings.MinimumDeconvolutionScore; } }
        public double MinTurnoverScore { get { return HalfLifeSettings.MinimumTurnoverScore; } }
        public double MinAuc { get { return HalfLifeSettings.MinimumAuc; } }
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
            private PeptideFileAnalysisData _peptideFileAnalysisData;
            public RawRowData(PeptideFileAnalysisData peptideFileAnalysisData)
            {
                _peptideFileAnalysisData = peptideFileAnalysisData;
            }
            private PeptideFileAnalysisData.PeakSet PeakSet { get { return _peptideFileAnalysisData.Peaks; } }
            public double TracerPercent { get { return PeakSet.TracerPercent.GetValueOrDefault(double.NaN); } }
            public double DeconvolutionScore { get { return PeakSet.DeconvolutionScore.GetValueOrDefault(double.NaN); } }
            public double? IndPrecursorEnrichment { get { return PeakSet.PrecursorEnrichment; } }
            public double? IndTurnover { get { return PeakSet.Turnover; } }
            public double? IndTurnoverScore { get { return PeakSet.TurnoverScore; } }
            public Peptide Peptide { get; set; }
            public ValidationStatus ValidationStatus { get { return _peptideFileAnalysisData.ValidationStatus; } }
            public String ProteinName { get { return Peptide.ProteinName; } }
            public String ProteinDescription { get { return Peptide.ProteinDescription; } }
            public MsDataFile MsDataFile { get; set; }
            public long PeptideFileAnalysisId { get; set; }
            public int PsmCount { get; set; }
            public IntegrationNote IntegrationNote { get { return PeakSet.IntegrationNote; } }
            public IList<PeptideFileAnalysisData.Peak> Peaks
            {
                get { return PeakSet.Peaks; }
            }
            public double? AreaUnderCurve
            {
                get
                {
                    if (Peaks == null)
                    {
                        return null;
                    }
                    return Peaks.Select(p=>p.Area).Sum();
                }
            }
            public IList<double> PeakAreas
            {
                get
                {
                    return Peaks == null ? new double[0] : Peaks.Select(peak=>peak.Area).ToArray();
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
                    return Peaks.Select(p => p.StartTime).Min();
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
                    return Peaks.Select(p => p.EndTime).Max();
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
