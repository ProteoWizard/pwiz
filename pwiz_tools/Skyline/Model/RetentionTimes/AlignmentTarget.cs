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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using pwiz.Common.DataAnalysis;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

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

        protected abstract double? ScoreSequence(Target target);

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
                    // TODO
                    var x = "TODO";
                    return null;
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
                    var map = CreatePiecewiseLinearMap(xArray, smoothedYValues);
                    int pointCount = Settings.Default.RtRegressionSegmentCount;
                    if (pointCount > 0)
                    {
                        map = map.ReducePointCount(pointCount);
                        map = PiecewiseLinearMap.FromValues(map.XValues.Select(v=>(float) v).Select(v=>(double) v), map.YValues.Select(v=>(float) v).Select(v=>(double) v));

                    }
                    return map;
                }
                default:
                    return null;
            }
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
            TryGetAlignmentTarget(document.Settings, out var alignmentTarget);
            return alignmentTarget;
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
            if (!settings.HasResults)
            {
                alignmentTarget = null;
                return true;
            }
            var irtCalculator = settings.PeptideSettings.Prediction.RetentionTime?.Calculator as RCalcIrt;
            if (irtCalculator != null)
            {
                if (!irtCalculator.IsUsable)
                {
                    alignmentTarget = null;
                    return false;
                }

                // TODO: use actual regression type
                var regressionType = RegressionMethodRT.kde;
                if (irtCalculator.RegressionType == IrtRegressionType.LOWESS)
                {
                    regressionType = RegressionMethodRT.loess;
                }

                alignmentTarget = new Irt(regressionType, irtCalculator);
                return true;
            }

            var alignableLibraries = GetAlignableLibraries(settings.PeptideSettings.Libraries).ToList();
            if (alignableLibraries.Count == 1)
            {
                var library = alignableLibraries[0].Value;
                if (true != alignableLibraries[0].Value?.IsLoaded)
                {
                    alignmentTarget = null;
                    return false;
                }

                alignmentTarget = new LibraryTarget(RegressionMethodRT.loess, library);
                return true;
            }

            alignmentTarget = null;
            return true;
        }

        public abstract string GetAxisTitle(RTPeptideValue rtValue);

        public abstract string GetAlignmentMenuItemText();

        public class Irt : AlignmentTarget
        {
            public Irt(RegressionMethodRT regressionMethod, IRetentionScoreCalculator calculator) : base(
                regressionMethod)
            {
                Calculator = calculator;
            }

            public IRetentionScoreCalculator Calculator { get; }


            protected override double? ScoreSequence(Target target)
            {
                return Calculator.ScoreSequence(target);
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

            private LibraryRetentionTimes GetMedianRetentionTimes()
            {
                if (Library.TryGetIrts(out var libraryRetentionTimes))
                {
                    return libraryRetentionTimes;
                }

                return null;
            }

            public override string GetAxisTitle(RTPeptideValue rtPeptideValue)
            {
                if (rtPeptideValue == RTPeptideValue.Retention || rtPeptideValue == RTPeptideValue.All)
                {
                    return string.Format("Retention time aligned to library '{0}'", Library.Name);
                }

                return string.Format("{0} aligned to library '{1}'", rtPeptideValue.ToLocalizedString(), Library.Name);
            }

            public override string GetAlignmentMenuItemText()
            {
                return string.Format("Align to Library '{0}'", Library.Name);
            }
        }

        private static IEnumerable<KeyValuePair<LibrarySpec, Library>> GetAlignableLibraries(
            PeptideLibraries peptideLibraries)
        {
            for (int iLibrary = 0; iLibrary < peptideLibraries.Libraries.Count; iLibrary++)
            {
                var library = peptideLibraries.Libraries[iLibrary];
                var librarySpec = peptideLibraries.LibrarySpecs[iLibrary];
                if (CanBeAligned(librarySpec, library))
                {
                    yield return new KeyValuePair<LibrarySpec, Library>(librarySpec, library);
                }
            }
        }

        private static bool CanBeAligned(LibrarySpec librarySpec, Library library)
        {
            return librarySpec is BiblioSpecLiteSpec || librarySpec is EncyclopeDiaSpec ||
                   library is BiblioSpecLiteLibrary || library is EncyclopeDiaLibrary;
        }
    }
}