/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public abstract class AlignmentTarget : Immutable
    {
        public AlignmentTarget(RegressionMethodRT regressionMethod)
        {
            RegressionMethod = regressionMethod;
        }

        public RegressionMethodRT RegressionMethod { get; }

        protected bool Equals(AlignmentTarget other)
        {
            return RegressionMethod == other.RegressionMethod;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((AlignmentTarget)obj);
        }

        public override int GetHashCode()
        {
            return (int)RegressionMethod;
        }

        public double? ScoreTargets(IEnumerable<Target> candidateTargets)
        {
            return candidateTargets.Select(ScoreSequence).OfType<double>().FirstOrDefault();
        }
        protected abstract double? ScoreSequence(Target target);
        public abstract string DisplayName { get; }
        public abstract double ChooseUnknownScore();

        /// <summary>
        /// Whether run-to-run retention time alignment should be performed using the current peaks that may have been adjusted by
        /// the user, or the original peaks that Skyline chose.
        /// When all peptides are used in the alignment, the original peaks are used for alignment, because otherwise any change to
        /// peak boundaries will change the alignment.
        /// However, when the alignment only uses iRT standards, the current peak boundaries should be used.
        /// </summary>
        public virtual bool UseCurrentPeaks { get { return false; } }

        public virtual RetentionScoreCalculatorSpec AsRetentionScoreCalculator()
        {
            return new RetentionScoreCalculatorImpl(this);
        }


        public PiecewiseLinearMap PerformAlignment(IDictionary<Target, double> times,
            CancellationToken cancellationToken)
        {
            return PerformAlignment(times.Select(kvp =>
                new KeyValuePair<IEnumerable<Target>, double>(new[] { kvp.Key }, kvp.Value)), cancellationToken);
        }

        public virtual PiecewiseLinearMap PerformAlignment(IEnumerable<KeyValuePair<IEnumerable<Target>, double>> times, CancellationToken cancellationToken)
        {
            var points = new List<WeightedPoint>();
            foreach (var kvp in times)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double? xValue = null;
                foreach (var target in kvp.Key)
                {
                    xValue = ScoreSequence(target);
                    if (xValue.HasValue)
                    {
                        break;
                    }
                }

                if (xValue.HasValue)
                {
                    points.Add(new WeightedPoint(xValue.Value, kvp.Value));
                }
            }

            return PerformAlignment(RegressionMethod, points, cancellationToken);
        }

        public static PiecewiseLinearMap PerformAlignment(RegressionMethodRT regressionMethod, ICollection<WeightedPoint> points, CancellationToken cancellationToken)
        {
            if (!points.Select(pt => pt.Y).Distinct().Skip(1).Any()
                || !points.Select(pt => pt.X).Distinct().Skip(1).Any())
            {
                return null;
            }

            switch (regressionMethod)
            {
                case RegressionMethodRT.linear:
                {
                    var statRT = new Statistics(points.Select(pt=>pt.Y));
                    var stat = new Statistics(points.Select(pt=>pt.X));
                    var slope = statRT.Slope(stat);
                    var intercept = statRT.Intercept(stat);
                    return CreatePiecewiseLinearMap(new[] { 0.0, 1 }, new[] { intercept, slope + intercept });
                }
                case RegressionMethodRT.kde:
                {
                    if (points.Count <= 1)
                    {
                        return null;
                    }

                    var kdeAligner = new KdeAligner(-1, -1);
                    kdeAligner.Train(points.Select(pt=>pt.X).ToArray(), points.Select(pt=>pt.Y).ToArray(), cancellationToken);
                    kdeAligner.GetSmoothedValues(out var xSmoothed, out var ySmoothed);
                    return CreatePiecewiseLinearMap(xSmoothed, ySmoothed);
                }
                case RegressionMethodRT.log:
                {
                    double xMin = points.Min(pt => pt.X);
                    double xMax = points.Max(pt => pt.X);
                    if (xMin <= xMax)
                    {
                        return null;
                    }
                    var regressionFunction = new LogRegression(points.Select(pt=>pt.X).ToList(), points.Select(pt=>pt.Y).ToList(), true);
                    const int pointCount = 1000;

                    var xValues = Enumerable.Range(0, pointCount)
                        .Select(i => (i * xMax + (pointCount - i - 1) * xMin) / (pointCount - 1)).ToList();
                    var yValues = xValues.Select(regressionFunction.GetY);
                    return ReducePointCount(PiecewiseLinearMap.FromValues(xValues, yValues));
                }
                case RegressionMethodRT.loess:
                {
                    if (points.Count < 2)
                    {
                        return null;
                    }

                    var weightedPoints = points as IList<WeightedPoint> ?? points.ToList();

                    int binCount = Settings.Default.RtRegressionBinCount;
                    if (binCount > 0)
                    {
                        weightedPoints = DownsamplePoints(weightedPoints, binCount).ToList();
                    }

                    weightedPoints = weightedPoints.OrderBy(pt => pt.X).ToList();
                    var loessInterpolator = new LoessInterpolator(Math.Max(LoessInterpolator.DEFAULT_BANDWIDTH, 2.0 / weightedPoints.Count), LoessInterpolator.DEFAULT_ROBUSTNESS_ITERS);
                    var xArray = weightedPoints.Select(pt => pt.X).ToArray();
                    var smoothedYValues = loessInterpolator.Smooth(xArray,
                        weightedPoints.Select(pt => pt.Y).ToArray(), 
                        weightedPoints.Select(pt => pt.Weight).ToArray(),
                        cancellationToken);
                    if (smoothedYValues.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
                    {
                        return null;
                    }
                    return ReducePointCount(CreatePiecewiseLinearMap(xArray, smoothedYValues));
                }
                default:
                    return null;
            }
        }

        /// <summary>
        /// Reduce the number of points in a PiecewiseLinearMap in order to save the amount of space when it is serialized by <see cref="DocumentRetentionTimes.WriteAlignments"/>.
        /// </summary>
        private static PiecewiseLinearMap ReducePointCount(PiecewiseLinearMap map)
        {
            int pointCount = Settings.Default.RtRegressionSegmentCount;
            if (pointCount <= 0)
            {
                return map;
            }

            map = map.ReducePointCount(pointCount);
            // Also, convert everything to floats and back to doubles so that it round-trips to XML
            map = PiecewiseLinearMap.FromValues(map.XValues.Select(v=>(float) v).Select(v=>(double) v), map.YValues.Select(v=>(float) v).Select(v=>(double) v));
            return map;
        }

        /// <summary>
        /// Reduce the number of points by combining points that are near to each other.
        /// The number of returned points will typically be greater than the number of bins
        /// because the number of bins is along each coordinate axis and points must be
        /// close to each other in both coordinates to be put in the same bin.
        /// </summary>
        public static IList<WeightedPoint> DownsamplePoints(IList<WeightedPoint> points, int binCount)
        {
            if (points.Count <= binCount)
            {
                return points;
            }

            double xMin = points.Min(pt => pt.X);
            double xMax = points.Max(pt => pt.X);
            double yMin = points.Min(pt => pt.Y);
            double yMax = points.Max(pt => pt.Y);
            double dx = xMax - xMin;
            double dy = yMax - yMin;
            var binnedPoints = new List<WeightedPoint>();
            foreach (var group in points.GroupBy(pt=>Tuple.Create(Math.Round((pt.X - xMin) * binCount / dx), Math.Round(pt.Y - yMin) * binCount / dy)))
            {
                binnedPoints.Add(new WeightedPoint(group.Average(tuple=>tuple.X), group.Average(tuple=>tuple.Y), group.Sum(tuple=>tuple.Weight)));
            }

            return binnedPoints;
        }

        private static readonly int MAX_PIECEWISE_LINEAR_MAP_LENGTH = 1000;
        public static PiecewiseLinearMap CreatePiecewiseLinearMap(IList<double> xValues, IList<double> yValues)
        {
            var piecewiseLinearMap = PiecewiseLinearMap.FromValues(xValues.Zip(yValues, (x,y)=>new KeyValuePair<double, double>((float) x, (float) y)));
            piecewiseLinearMap = piecewiseLinearMap.RemoveOutOfOrder();
            return piecewiseLinearMap.ReducePointCount(MAX_PIECEWISE_LINEAR_MAP_LENGTH);
        }

        public static AlignmentTarget GetAlignmentTarget(SrmDocument document)
        {
            document.Settings.GetAlignmentTargetSpec().TryGetAlignmentTarget(document, out var target);
            return target;
        }

        /// <summary>
        /// Figures out what everything should be aligned to.
        /// If there is a retention time calculator, then uses that.
        /// Otherwise, if there is only one library that supports alignment (currently .blib or .elib)
        /// then uses that.
        ///
        /// If nothing suitable is found, return value will be true, and <paramref name="alignmentTarget"/> will be null.
        /// Returns false if the suitable alignment target has not finished loading.
        /// </summary>
        public static bool TryGetAlignmentTarget(SrmSettings settings, out AlignmentTarget alignmentTarget)
        {
            return settings.TryGetAlignmentTarget(out alignmentTarget);
        }

        public static bool TryGetCurrentAlignmentTarget(SrmDocument document, out AlignmentTarget alignmentTarget)
        {
            var alignmentTargetSpec = document.Settings.GetAlignmentTargetSpec();
            if (alignmentTargetSpec.IsChromatogramPeaks)
            {
                alignmentTarget = new MedianDocumentRetentionTimes(document);
                return true;
            }
            return alignmentTargetSpec.TryGetAlignmentTarget(document, out alignmentTarget);
        }

        public abstract string GetAxisTitle(RTPeptideValue rtValue);

        public abstract string GetAlignmentMenuItemText();
        private class RetentionScoreCalculatorImpl : RetentionScoreCalculatorSpec
        {
            private AlignmentTarget _alignmentTarget;
            private double _unknownScore;
            public RetentionScoreCalculatorImpl(AlignmentTarget alignmentTarget) : base(alignmentTarget.DisplayName)
            {
                _alignmentTarget = alignmentTarget;
                _unknownScore = alignmentTarget.ChooseUnknownScore();
            }

            public override double? ScoreSequence(Target modifiedSequence)
            {
                return _alignmentTarget.ScoreSequence(modifiedSequence);
            }

            public override double UnknownScore
            {
                get { return _unknownScore; }
            }
            public override IEnumerable<Target> ChooseRegressionPeptides(IEnumerable<Target> peptides, out int minCount)
            {
                minCount = 0;
                return peptides;
            }

            public override IEnumerable<Target> GetStandardPeptides(IEnumerable<Target> peptides)
            {
                return Array.Empty<Target>();
            }

            public override RetentionScoreProvider ScoreProvider
            {
                get { return null; }
            }

            public override bool IsAlignmentOnly
            {
                get { return true; }
            }
        }

        public class Irt : AlignmentTarget
        {
            public Irt(RegressionMethodRT regressionMethod, IRetentionScoreCalculator calculator) : base(
                regressionMethod)
            {
                Calculator = calculator;
            }
            public Irt(RetentionScoreCalculatorSpec calculator) : this(GetRegressionMethod(calculator), calculator)
            {
            }

            public IRetentionScoreCalculator Calculator { get; }


            protected override double? ScoreSequence(Target target)
            {
                return Calculator.ScoreSequence(target);
            }

            public override string DisplayName
            {
                get { return Calculator.Name; }
            }
            public override double ChooseUnknownScore()
            {
                return Calculator.UnknownScore;
            }


            public override string GetAxisTitle(RTPeptideValue rtPeptideValue)
            {
                if (rtPeptideValue == RTPeptideValue.Retention || rtPeptideValue == RTPeptideValue.All)
                {
                    return string.Format(Controls.Graphs.GraphsResources.RegressionUnconversion_CalculatorScoreFormat,
                        Calculator.Name);
                }

                return string.Format(Controls.Graphs.GraphsResources.RegressionUnconversion_CalculatorScoreValueFormat,
                    Calculator.Name, rtPeptideValue.ToLocalizedString());

            }

            public override string GetAlignmentMenuItemText()
            {
                return string.Format(Resources.SkylineWindow_ShowCalculatorScoreFormat, Calculator.Name);
            }

            public override PiecewiseLinearMap PerformAlignment(IEnumerable<KeyValuePair<IEnumerable<Target>, double>> times, CancellationToken cancellationToken)
            {
                var rCalcIrt = Calculator as RCalcIrt;
                int? standardCount = rCalcIrt?.GetStandardPeptides().Count();
                if (standardCount == null || standardCount == 0)
                {
                    return base.PerformAlignment(times, cancellationToken);
                }


                List<WeightedPoint> standardPoints = new List<WeightedPoint>();
                List<WeightedPoint> allPoints = new List<WeightedPoint>();
                
                foreach (var entry in times)
                {
                    var standardTarget = entry.Key.FirstOrDefault(rCalcIrt.IsStandard);
                    double? score;
                    if (standardTarget != null)
                    {
                        score = rCalcIrt.ScoreSequence(standardTarget);
                    }
                    else
                    {
                        score = entry.Key.Select(rCalcIrt.ScoreSequence).FirstOrDefault(v => v.HasValue);
                    }

                    if (score.HasValue)
                    {
                        var point = new WeightedPoint(score.Value, entry.Value);
                        if (standardTarget != null)
                        {
                            standardPoints.Add(point);
                        }
                        allPoints.Add(point);
                    }
                }

                if (standardPoints.Count >= RCalcIrt.MinStandardCount(standardCount.Value))
                {
                    return PerformAlignment(RegressionMethod, standardPoints, cancellationToken);
                }
                return PerformAlignment(RegressionMethod, allPoints, cancellationToken);
            }
            public static RegressionMethodRT GetRegressionMethod(RetentionScoreCalculatorSpec calculator)
            {
                if (!(calculator is RCalcIrt rCalcIrt))
                {
                    return RegressionMethodRT.loess;
                }

                var irtRegressionType = rCalcIrt.RegressionType;
                if (irtRegressionType == IrtRegressionType.LINEAR)
                {
                    return RegressionMethodRT.linear;
                }

                if (irtRegressionType == IrtRegressionType.LOGARITHMIC)
                {
                    return RegressionMethodRT.log;
                }

                if (irtRegressionType == IrtRegressionType.LOWESS)
                {
                    return RegressionMethodRT.loess;
                }

                return RegressionMethodRT.kde;
            }

            public override bool UseCurrentPeaks
            {
                get
                {
                    if (Calculator is RCalcIrt rCalcIrt)
                    {
                        return rCalcIrt.GetStandardPeptides().Any();
                    }
                    return false;
                }
            }
        }

        public class LibraryTarget : AlignmentTarget
        {
            private Lazy<LibraryRetentionTimes> _medianRetentionTimes;

            public LibraryTarget(RegressionMethodRT regressionMethod, Library library) : base(regressionMethod)
            {
                Library = library;
                _medianRetentionTimes = new Lazy<LibraryRetentionTimes>(GetMedianRetentionTimes);
            }

            public Library Library { get; private set; }

            protected override double? ScoreSequence(Target target)
            {
                return _medianRetentionTimes.Value?.GetRetentionTime(target);
            }

            public override string DisplayName
            {
                get
                {
                    return string.Format(RetentionTimesResources.Library_DisplayName_Library__0_, Library.Name);
}
            }
            public override double ChooseUnknownScore()
            {
                return IrtDb.ChooseUnknownScore(_medianRetentionTimes.Value.GetFirstRetentionTimes().Values);
            }


            private LibraryRetentionTimes GetMedianRetentionTimes()
            {
                var medianRetentionTimes = Library.GetMedianRetentionTimes();
                if (medianRetentionTimes == null)
                {
                    return null;
                }
                return LibraryRetentionTimes.FromRetentionTimes(string.Empty, TimeSource.scan, medianRetentionTimes);
            }

            public override string GetAxisTitle(RTPeptideValue rtPeptideValue)
            {
                if (rtPeptideValue == RTPeptideValue.Retention || rtPeptideValue == RTPeptideValue.All)
                {
                    return string.Format(RetentionTimesResources.LibraryTarget_GetAxisTitle_Retention_time_aligned_to_library___0__, Library.Name);
                }

                return string.Format(RetentionTimesResources.LibraryTarget_GetAxisTitle__0__aligned_to_library___1__, rtPeptideValue.ToLocalizedString(), Library.Name);
            }

            public override string GetAlignmentMenuItemText()
            {
                return string.Format(RetentionTimesResources.LibraryTarget_GetAlignmentMenuItemText_Align_to_Library___0__, Library.Name);
            }

            protected bool Equals(LibraryTarget other)
            {
                return base.Equals(other) && Library.Equals(other.Library);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((LibraryTarget)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (base.GetHashCode() * 397) ^ Library.GetHashCode();
                }
            }
        }

        public class MedianDocumentRetentionTimes : AlignmentTarget
        {
            private IDictionary<Target, double> _dictionary;
            public MedianDocumentRetentionTimes(IEnumerable<ResultFileAlignments.AlignmentSource> files) : base(RegressionMethodRT.loess)
            {
                _dictionary = files.SelectMany(file => file.GetTimesDictionary().SelectMany(kvp => kvp.Key.Select(target => new KeyValuePair<Target, double>(target, kvp.Value))))
                    .GroupBy(kvp => kvp.Key, kvp => kvp.Value)
                    .ToDictionary(group => group.Key, MathNet.Numerics.Statistics.Statistics.Median);
            }

            public MedianDocumentRetentionTimes(SrmDocument document)
                : this(ResultFileAlignments.GetAlignmentSources(document, null, null).Values)
            {
            }

            protected override double? ScoreSequence(Target target)
            {
                if (_dictionary.TryGetValue(target, out var score))
                {
                    return score;
                }

                return null;
            }

            public override string GetAxisTitle(RTPeptideValue rtPeptideValue)
            {
                if (rtPeptideValue == RTPeptideValue.Retention || rtPeptideValue == RTPeptideValue.All)
                {
                    return RetentionTimesResources.MedianDocumentRetentionTimes_GetAxisTitle_Normalized_retention_time;
                }

                return string.Format(RetentionTimesResources.MedianDocumentRetentionTimes_GetAxisTitle_Normalized__0_, rtPeptideValue.ToLocalizedString());
            }

            public override string GetAlignmentMenuItemText()
            {
                return string.Format(RetentionTimesResources.MedianDocumentRetentionTimes_GetAlignmentMenuItemText_Align_to_Median_Document_Retention_Times);
            }

            public override string DisplayName
            {
                get { return RetentionTimesResources.MedianDocumentRetentionTimes_DisplayName_Median_LC_Peak_Time; }
            }

            protected bool Equals(MedianDocumentRetentionTimes other)
            {
                return base.Equals(other) && CollectionUtil.EqualsDeep(_dictionary, other._dictionary);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((MedianDocumentRetentionTimes)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (base.GetHashCode() * 397) ^ CollectionUtil.GetHashCodeDeep(_dictionary);
                }
            }

            public override double ChooseUnknownScore()
            {
                return IrtDb.ChooseUnknownScore(_dictionary.Values);
            }
        }

        public static LoessRegression CreateLoessRegression(IEnumerable<double> x, IEnumerable<double> y,
            CancellationToken cancellationToken)
        {
            IList<WeightedPoint> weightedPoints = x.Zip(y, (a, b) => new WeightedPoint(a, b)).ToList();
            weightedPoints = DownsamplePoints(weightedPoints, Settings.Default.RtRegressionBinCount);
            return new LoessRegression(weightedPoints.Select(pt => pt.X).ToArray(),
                weightedPoints.Select(pt => pt.Y).ToArray(), true, cancellationToken);
        }
    }
}
