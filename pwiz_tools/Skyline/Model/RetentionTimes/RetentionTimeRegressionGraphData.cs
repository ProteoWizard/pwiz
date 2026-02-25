/*
 * Extraction of RT regression/outlier calculation from Controls.Graphs.RTLinearRegressionGraphPane
 * into Model.RetentionTimes, so Model callers (e.g., RefinementSettings) can compute outliers
 * without referencing UI classes.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.RetentionTimes
{
    // Moved here from UI to keep Model self-contained for RT regression logic
    public enum PointsTypeRT
    {
        targets,
        targets_fdr,
        standards,
        decoys
    }

    public static class RetentionTimeRegressionGraphData
    {
        public class DataPoint
        {
            public DataPoint(IdentityPath identityPath, Target modifiedTarget, double? x, double? y)
            {
                IdentityPath = identityPath;
                ModifiedTarget = modifiedTarget;
                X = x;
                Y = y;
            }
            public Target ModifiedTarget { get; }
            public IdentityPath IdentityPath { get; }
            public double? X { get; }
            public double? Y { get; }
        }

        public class RegressionSnapshot
        {
            public RetentionTimeRegression RegressionPredict { get; internal set; }
            public IRegressionFunction ConversionPredict { get; internal set; }
            public RetentionTimeStatistics StatisticsPredict { get; internal set; }

            public RetentionTimeRegression RegressionAll { get; internal set; }
            public RetentionTimeStatistics StatisticsAll { get; internal set; }

            public RetentionTimeRegression RegressionRefined { get; internal set; }
            public RetentionTimeStatistics StatisticsRefined { get; internal set; }

            public RetentionScoreCalculatorSpec Calculator { get; internal set; }

            public ImmutableList<DataPoint> AllPoints { get; internal set; }
            public ImmutableList<DataPoint> RefinedPoints { get; internal set; }
            public ImmutableList<DataPoint> Outliers { get; internal set; }
        }

        /// <summary>
        /// Compute a snapshot of RT regression inputs/outputs for UI consumption without duplicating logic.
        /// </summary>
        public static RegressionSnapshot ComputeSnapshot(RetentionTimeRegressionSettings settings,
            ProductionMonitor monitor)
        {
            var graphData = new InstanceData(settings, monitor);
            return new RegressionSnapshot
            {
                RegressionPredict = graphData._regressionPredict,
                ConversionPredict = graphData._conversionPredict,
                StatisticsPredict = graphData._statisticsPredict,
                RegressionAll = graphData._regressionAll,
                StatisticsAll = graphData._statisticsAll,
                RegressionRefined = graphData._regressionRefined,
                StatisticsRefined = graphData._statisticsRefined,
                Calculator = graphData.Calculator,
                AllPoints = graphData.AllPoints.Select(p => new DataPoint(p.IdentityPath, p.ModifiedTarget, p.X, p.Y)).ToImmutable(),
                RefinedPoints = graphData.RefinedPoints.Select(p => new DataPoint(p.IdentityPath, p.ModifiedTarget, p.X, p.Y)).ToImmutable(),
                Outliers = graphData.Outliers.Select(p => new DataPoint(p.IdentityPath, p.ModifiedTarget, p.X, p.Y)).ToImmutable(),
            };
        }

        /// <summary>
        /// Calculate peptide outliers for RT regression based on the given parameters.
        /// Returns all outliers plus any peptides with missing values (treated as outliers for removal).
        /// </summary>
        public static PeptideDocNode[] CalcOutliers(
            SrmDocument document,
            double threshold,
            int? precision,
            bool bestResult,
            PointsTypeRT pointsType,
            RegressionMethodRT regressionMethod,
            RtCalculatorOption calculatorOption,
            bool refine,
            int targetIndex = -1,
            int originalIndex = -1,
            bool isRunToRun = false)
        {
            var regressionSettings = new RetentionTimeRegressionSettings(
                    document,
                    targetIndex,
                    originalIndex,
                    bestResult,
                    threshold,
                    refine,
                    pointsType,
                    regressionMethod,
                    calculatorOption,
                    isRunToRun)
                .ChangeThresholdPrecision(precision);

            var productionMonitor = new ProductionMonitor(CancellationToken.None, _ => { });
            var graphData = new InstanceData(regressionSettings, productionMonitor);
            if (ReferenceEquals(graphData.RegressionRefined, graphData._regressionAll))
            {
                return Array.Empty<PeptideDocNode>();
            }

            return graphData.Outliers
                .Select(pt => pt.IdentityPath)
                .Distinct()
                .Select(document.FindNode)
                .Cast<PeptideDocNode>()
                .ToArray();
        }

        /// <summary>
        /// Holds the data used for RT regression and outlier identification.
        /// UI-free version of the logic from RTLinearRegressionGraphPane.GraphData
        /// </summary>
        private sealed class InstanceData : Immutable
        {
            private readonly ImmutableList<PointInfo> _points;
            private ImmutableList<PointInfo> _outlierPoints;
            private ImmutableList<PointInfo> _refinedPoints;

            public readonly RetentionTimeRegression _regressionPredict;
            public readonly IRegressionFunction _conversionPredict;
            public readonly RetentionTimeStatistics _statisticsPredict;

            public readonly RetentionTimeRegression _regressionAll;
            public readonly RetentionTimeStatistics _statisticsAll;

            public RetentionTimeRegression _regressionRefined;
            public RetentionTimeStatistics _statisticsRefined;

            private readonly RetentionScoreCalculatorSpec _calculator;
            public RetentionScoreCalculatorSpec Calculator => _calculator;

            public class PointInfo : Immutable
            {
                public PointInfo(IdentityPath identityPath, Target modifiedTarget, double? x, double? y)
                {
                    IdentityPath = identityPath;
                    ModifiedTarget = modifiedTarget;
                    X = x;
                    Y = y;
                }
                public Target ModifiedTarget { get; }
                public IdentityPath IdentityPath { get; }
                public double? X { get; private set; }

                public PointInfo ChangeX(double? value)
                {
                    return ChangeProp(ImClone(this), im => im.X = value);
                }

                public double? Y { get; private set; }
                public PointInfo ChangeY(double? value)
                {
                    return ChangeProp(ImClone(this), im => im.Y = value);
                }
            }

            public InstanceData(RetentionTimeRegressionSettings regressionSettings, ProductionMonitor productionMonitor)
            {
                RegressionSettings = regressionSettings;
                var document = Document;
                var token = productionMonitor.CancellationToken;
                bool refine = regressionSettings.Refine;
                var pointInfos = new List<PointInfo>();
                var standards = new HashSet<Target>();
                if (RegressionSettings.PointsType == PointsTypeRT.standards)
                    standards = document.GetRetentionTimeStandards();

                // Only used if we are comparing two runs
                var modifiedTargets = IsRunToRun ? new HashSet<Target>() : null;
                int moleculeCount = document.MoleculeCount;
                int iMolecule = 0;
                foreach (var peptideGroupDocNode in document.MoleculeGroups)
                foreach (var nodePeptide in peptideGroupDocNode.Molecules)
                {
                    productionMonitor.SetProgress(iMolecule * 100 / moleculeCount);
                    iMolecule++;
                    if (false == modifiedTargets?.Add(nodePeptide.ModifiedTarget))
                    {
                        continue;
                    }
                    var identityPath = new IdentityPath(peptideGroupDocNode.PeptideGroup, nodePeptide.Peptide);
                    productionMonitor.CancellationToken.ThrowIfCancellationRequested();
                    switch (RegressionSettings.PointsType)
                    {
                        case PointsTypeRT.targets:
                            if (nodePeptide.IsDecoy)
                                continue;
                            break;
                        case PointsTypeRT.targets_fdr:
                        {
                            if(nodePeptide.IsDecoy)
                                continue;

                            if (TargetIndex != -1 && GetMaxQValue(nodePeptide, TargetIndex) >= 0.01 ||
                                OriginalIndex != -1 && GetMaxQValue(nodePeptide, OriginalIndex) >= 0.01)
                                continue;
                            break;
                        }
                        case PointsTypeRT.standards:
                            if (!standards.Contains(document.Settings.GetModifiedSequence(nodePeptide))
                                || nodePeptide.GlobalStandardType != StandardType.IRT)
                                continue;
                            break;
                        case PointsTypeRT.decoys:
                            if (!nodePeptide.IsDecoy)
                                continue;
                            break;
                    }

                    float? rtTarget = null;

                    // Only used if we are doing run to run, otherwise we use scores
                    float? rtOrig = null;

                    if (RegressionSettings.OriginalIndex >= 0)
                        rtOrig = nodePeptide.GetSchedulingTime(RegressionSettings.OriginalIndex);

                    if (RegressionSettings.BestResult)
                    {
                        int iBest = nodePeptide.BestResult;
                        if (iBest != -1)
                            rtTarget = nodePeptide.GetSchedulingTime(iBest);
                    }
                    else
                        rtTarget = nodePeptide.GetSchedulingTime(RegressionSettings.TargetIndex);

                    pointInfos.Add(new PointInfo(identityPath, nodePeptide.ModifiedTarget, rtOrig, rtTarget));
                }

                bool includeMissingValues = IncludeMissingValuesInRegression &&
                                            RegressionSettings.RegressionMethod == RegressionMethodRT.linear;
                var targetTimes = pointInfos.Where(pt => includeMissingValues || pt.Y.HasValue)
                    .Select(pt => new MeasuredRetentionTime(pt.ModifiedTarget, pt.Y ?? 0)).ToList();

                if (IsRunToRun)
                {
                    var targetTimesDict = pointInfos.Where(pt => pt.Y.HasValue)
                        .ToDictionary(pt => pt.ModifiedTarget, pt => pt.Y.Value);
                    var origTimesDict = pointInfos.Where(pt => pt.X.HasValue)
                        .ToDictionary(pt => pt.ModifiedTarget, pt => pt.X.Value);
                    _calculator = new DictionaryRetentionScoreCalculator(XmlNamedElement.NAME_INTERNAL, DocumentRetentionTimes.ConvertToMeasuredRetentionTimes(origTimesDict));
                    var alignedRetentionTimes = AlignedRetentionTimes.AlignLibraryRetentionTimes(targetTimesDict, origTimesDict, refine ? RegressionSettings.Threshold : 0, RegressionSettings.RegressionMethod, token);
                    if (alignedRetentionTimes != null)
                    {
                        _regressionAll = alignedRetentionTimes.Regression;
                        _statisticsAll = alignedRetentionTimes.RegressionStatistics;
                    }
                }
                else
                {
                    var usableCalculators = RegressionSettings.GetCalculators().Where(calc => calc.IsUsable).ToList();
                    var calcName = RegressionSettings.CalculatorName;
                    if (calcName == null)
                    {
                        var summary = RetentionTimeRegression.CalcBestRegressionBackground(XmlNamedElement.NAME_INTERNAL, usableCalculators, targetTimes, null, true,
                            RegressionSettings.RegressionMethod, token);

                        _calculator = summary.Best.Calculator;
                        _statisticsAll = summary.Best.Statistics;
                        _regressionAll = summary.Best.Regression;
                    }
                    else
                    {
                        // If the named calculator was not found, the list can end up containing
                        // all usable calculators, and returning the first will just mean showing
                        // another calculator's scores as coming from the named one. Check by name
                        // for iRT calculators, since they are currently the only type that can fail
                        // to load (missing .irtdb).
                        // CONSIDER: If another calculator type is added that requires loading and
                        //           could be unavailable, this name check would need to be
                        //           generalized for that type.
                        var calc = calcName is RtCalculatorOption.Irt irtOption
                            ? usableCalculators.FirstOrDefault(c => c.Name == irtOption.Name)
                            : usableCalculators.FirstOrDefault();
                        if (calc != null)
                        {
                            _regressionAll = RetentionTimeRegression.CalcSingleRegression(XmlNamedElement.NAME_INTERNAL,
                                calc,
                                targetTimes,
                                null,
                                true,
                                RegressionSettings.RegressionMethod,
                                out _statisticsAll,
                                out _,
                                token);
                        }
                        token.ThrowIfCancellationRequested();
                        _calculator = calc;
                    }

                    if (_calculator != null)
                    {
                        pointInfos = pointInfos.Select(pt =>
                            pt.ChangeX(_calculator.ScoreSequence(pt.ModifiedTarget))).ToList();
                    }
                }

                _regressionPredict = (IsRunToRun || RegressionSettings.RegressionMethod != RegressionMethodRT.linear)  ? null : document.Settings.PeptideSettings.Prediction.RetentionTime;
                if (_regressionPredict != null)
                {
                    if (!Equals(_calculator, _regressionPredict.Calculator))
                        _regressionPredict = null;
                    else
                    {
                        IDictionary<Target, double> scoreCache = null;
                        if (_regressionAll != null && Equals(_regressionAll.Calculator, _regressionPredict.Calculator))
                            scoreCache = _statisticsAll.ScoreCache;
                        // Replicate with a single file support
                        ChromFileInfoId fileId = null;
                        if (!RegressionSettings.BestResult && RegressionSettings.TargetIndex != -1)
                        {
                            var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[RegressionSettings.TargetIndex];
                            if (chromatogramSet.FileCount > 0)
                            {
                                fileId = chromatogramSet.MSDataFileInfos[0].FileId;
                                _conversionPredict = _regressionPredict.GetConversion(fileId);
                            }
                        }
                        _statisticsPredict = _regressionPredict.CalcStatistics(targetTimes, scoreCache, fileId);
                    }
                }

                // Initial split: points with both X and Y are "refined", missing values are "outliers"
                _points = pointInfos.ToImmutable();
                _refinedPoints = _points.Where(pt=>pt.X.HasValue && pt.Y.HasValue).ToImmutable();
                _outlierPoints = _points.Where(pt => !pt.X.HasValue || !pt.Y.HasValue).ToImmutable();
                
                // If requested and not already above threshold, refine to remove statistical outliers
                if (refine && !IsRefined())
                {
                    Refine(productionMonitor.CancellationToken);
                }
            }

            /// <summary>
            /// Whether missing values should be included in the regression.
            /// All versions of Skyline have included missing values in the "unrefined" linear regression
            /// for the "Score to Run" graph.
            /// We should probably change this since it makes no sense mathematically, but that would require
            /// changing a few tutorials and updating expected values in a few unit tests.
            /// </summary>
            private bool IncludeMissingValuesInRegression
            {
                get
                {
                    if (IsRunToRun)
                    {
                        // Missing values have always been filtered out of the run-to-run regression.
                        return false;
                    }
                    return true;
                }
            }

            public SrmDocument Document => RegressionSettings.Document;
            public RetentionTimeRegressionSettings RegressionSettings { get; }

            public bool IsRunToRun => RegressionSettings.IsRunToRun;

            private float GetMaxQValue(PeptideDocNode node, int replicateIndex)
            {
                var chromInfos = node.TransitionGroups
                    .Select(tr => tr.GetSafeChromInfo(TargetIndex).FirstOrDefault(ci => ci.OptimizationStep == 0))
                    .Where(ci => ci?.QValue != null).ToArray();

                if (chromInfos.Length == 0)
                    return 1.0f;

                return chromInfos.Max(ci => ci.QValue.Value);
            }

            public int TargetIndex => RegressionSettings.TargetIndex;

            public int OriginalIndex => RegressionSettings.OriginalIndex;

            public RetentionTimeRegression RegressionRefined => _regressionRefined ?? _regressionAll;

            public RetentionTimeStatistics StatisticsRefined => _statisticsRefined ?? _statisticsAll;

            public bool RegressionRefinedNull => _regressionRefined == null;

            public bool IsRefined()
            {
                if (_regressionRefined != null)
                    return true;
                if (_statisticsAll == null)
                    return false;
                return RetentionTimeRegression.IsAboveThreshold(_statisticsAll.R, RegressionSettings.Threshold);
            }

            private void Refine(CancellationToken cancellationToken)
            {
                if(_calculator == null || !_calculator.IsUsable)
                    return;

                var outlierPoints = new List<PointInfo>();
                var validPoints = new List<PointInfo>();
                foreach (var pt in _points)
                {
                    if (pt.X.HasValue && pt.Y.HasValue)
                    {
                        validPoints.Add(pt);
                    }
                    else if (IncludeMissingValuesInRegression)
                    {
                        validPoints.Add(pt.ChangeX(pt.X ?? _calculator?.UnknownScore ?? 0).ChangeY(pt.Y ?? 0));
                    }
                    else
                    {
                        outlierPoints.Add(pt);
                    }
                }
                var variableTargetPeptides = validPoints.Select(pt =>
                    new MeasuredRetentionTime(pt.ModifiedTarget, pt.Y ?? 0, true)).ToList();
                var variableOrigPeptides =
                    validPoints.Select(pt => new MeasuredRetentionTime(pt.ModifiedTarget, pt.X.Value, true)).ToList();

                var outlierIndexes = new HashSet<int>();
                RetentionTimeStatistics statisticsRefined = null;
                var regressionRefined = _regressionAll?.FindThreshold(RegressionSettings.Threshold,
                                                                         RegressionSettings.ThresholdPrecision,
                                                                         0,
                                                                         variableTargetPeptides.Count,
                                                                         Array.Empty<MeasuredRetentionTime>(),
                                                                         variableTargetPeptides,
                                                                         variableOrigPeptides,
                                                                         _statisticsAll,
                                                                         _calculator,
                                                                         RegressionSettings.RegressionMethod,
                                                                         null,
                                                                         cancellationToken,
                                                                         ref statisticsRefined,
                                                                         ref outlierIndexes);

                if (ReferenceEquals(regressionRefined, _regressionAll))
                    return;

                _outlierPoints = outlierPoints.Concat(outlierIndexes.Select(i => validPoints[i])).ToImmutable();
                _refinedPoints = Enumerable.Range(0, validPoints.Count).Where(i => !outlierIndexes.Contains(i))
                    .Select(i => validPoints[i]).ToImmutable();
                _regressionRefined = regressionRefined;
                _statisticsRefined = statisticsRefined;
            }

            public bool HasOutliers => 0 < _outlierPoints.Count;
            public ImmutableList<PointInfo> AllPoints => _points;
            public ImmutableList<PointInfo> RefinedPoints => _refinedPoints;
            public ImmutableList<PointInfo> Outliers => _outlierPoints;
        }
    }
    public class RetentionTimeRegressionSettings : Immutable, ICachingParameters
    {
        private Lazy<RetentionScoreCalculatorSpec> _calculator;
        public RetentionTimeRegressionSettings(SrmDocument document, int targetIndex, int originalIndex, bool bestResult,
            double threshold, bool refine, PointsTypeRT pointsType, RegressionMethodRT regressionMethod,
            RtCalculatorOption calculatorOption, bool isRunToRun)
        {
            Document = document;
            TargetIndex = targetIndex;
            OriginalIndex = originalIndex;
            BestResult = bestResult;
            Threshold = threshold;
            Refine = refine;
            PointsType = pointsType;
            RegressionMethod = regressionMethod;
            AllCalculators = Settings.Default.RTScoreCalculatorList.ToImmutable();
            CalculatorName = calculatorOption;
            if (CalculatorName != null)
            {
                _calculator = new Lazy<RetentionScoreCalculatorSpec>(() =>
                    CalculatorName.GetRetentionScoreCalculatorSpec(Document, AllCalculators));
            }
            IsRunToRun = isRunToRun;
        }

        protected bool Equals(RetentionTimeRegressionSettings other)
        {
            return ReferenceEquals(Document, other.Document) && TargetIndex == other.TargetIndex &&
                   OriginalIndex == other.OriginalIndex && BestResult == other.BestResult &&
                   Threshold.Equals(other.Threshold) && Refine == other.Refine && PointsType == other.PointsType &&
                   RegressionMethod == other.RegressionMethod && Equals(CalculatorName, other.CalculatorName) &&
                   Equals(AllCalculators, other.AllCalculators) && IsRunToRun == other.IsRunToRun;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((RetentionTimeRegressionSettings)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = RuntimeHelpers.GetHashCode(Document);
                hashCode = (hashCode * 397) ^ TargetIndex;
                hashCode = (hashCode * 397) ^ OriginalIndex;
                hashCode = (hashCode * 397) ^ BestResult.GetHashCode();
                hashCode = (hashCode * 397) ^ Threshold.GetHashCode();
                hashCode = (hashCode * 397) ^ Refine.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)PointsType;
                hashCode = (hashCode * 397) ^ (int)RegressionMethod;
                hashCode = (hashCode * 397) ^ (CalculatorName != null ? CalculatorName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (AllCalculators?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ IsRunToRun.GetHashCode();
                return hashCode;
            }
        }

        public SrmDocument Document { get; private set; }
        public int TargetIndex { get; private set; }
        public int OriginalIndex { get; private set; }
        public bool BestResult { get; private set; }
        public double Threshold { get; private set; }
        public int? ThresholdPrecision { get; private set; }

        public RetentionTimeRegressionSettings ChangeThresholdPrecision(int? value)
        {
            return ChangeProp(ImClone(this), im => im.ThresholdPrecision = value);
        }
        public bool Refine { get; private set; }
        public PointsTypeRT PointsType { get; private set; }
        public RegressionMethodRT RegressionMethod { get; private set; }
        public RtCalculatorOption CalculatorName { get; private set; }
        private ImmutableList<RetentionScoreCalculatorSpec> AllCalculators { get; }
        public IEnumerable<RetentionScoreCalculatorSpec> GetCalculators()
        {
            var singleCalculator = _calculator?.Value;
            if (singleCalculator != null)
            {
                return ImmutableList.Singleton(singleCalculator);
            }

            return AllCalculators;
        }

        public bool IsRunToRun { get; private set; }

        // ICachingParameters implementation
        int ICachingParameters.CacheKey => TargetIndex;

        object ICachingParameters.CacheSettings => new
        {
            BestResult, Threshold, Refine, PointsType,
            RegressionMethod, CalculatorName, IsRunToRun, OriginalIndex
        };
    }
}
