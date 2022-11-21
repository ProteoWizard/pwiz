using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class BilinearCurveFitter
    {
        private const double SAME_LOQ_REL_THRESHOLD = .001;
        public BilinearCurveFitter()
        {
            MaxBootstrapIterations = 100;
            MinBootstrapIterations = 10;
            MinSameLoqCountForAccept = 25;
            GridSize = 100;
            CvThreshold = .2;
            Random = new Random();
            MinNumTransitions = 5;
        }
        public int MaxBootstrapIterations { get; set; }
        public int MinBootstrapIterations { get; set; }
        public int MinSameLoqCountForAccept { get; set; }
        public int GridSize { get; set; }
        public double CvThreshold { get; set; }
        public Random Random { get; set; }
        public int MinNumTransitions { get; set; }
        public CancellationToken CancellationToken { get; set; }

        public BilinearCurveFit FitBilinearCurve(IEnumerable<WeightedPoint> points)
        {
            var pointsList = points as IList<WeightedPoint> ?? points.ToList();
            return BilinearCurveFit.FromCalibrationCurve(RegressionFit.BILINEAR.Fit(pointsList), pointsList);
        }

        public BilinearCurveFit FitBilinearCurveWithOffset(double offset, IEnumerable<WeightedPoint> points)
        {
            var pointsList = points as IList<WeightedPoint> ?? points.ToList();
            return BilinearCurveFit.FromCalibrationCurve(
                BilinearRegressionFit.FitPointsWithOffset(offset, pointsList), pointsList);
        }

        public BilinearCurveFit ComputeBootstrapParams(IList<WeightedPoint> points)
        {
            var randomPoints = Enumerable.Range(0, points.Count)
                .Select(i => points[Random.Next(points.Count)]).ToList();
            return FitBilinearCurve(randomPoints);
        }

        public double ComputeBootstrappedLoq(IList<WeightedPoint> points)
        {
            var lod = ComputeLod(points);
            var maxConcentration = points.Max(pt => pt.X);
            var concentrationValues = Enumerable.Range(0, GridSize)
                .Select(i => lod + (maxConcentration - lod) * i / (GridSize - 1)).ToList();
            var areaGrid = Enumerable.Range(0, GridSize).Select(i => new RunningStatistics()).ToList();
            int numItersWithSameLoq = 0;
            double lastLoq = maxConcentration;
            for (int i = 0; i < MaxBootstrapIterations; i++)
            {
                CancellationToken.ThrowIfCancellationRequested();
                var p = ComputeBootstrapParams(points);
                for (int iConcentration = 0; iConcentration < concentrationValues.Count; iConcentration++)
                {
                    var area = p.CalibrationCurve.GetY(concentrationValues[iConcentration]);
                    areaGrid[iConcentration].Push(area);
                }

                if (i > MinBootstrapIterations)
                {
                    var loqCheck = maxConcentration;
                    for (int iConcentration = concentrationValues.Count - 1; iConcentration >= 0; iConcentration--)
                    {
                        if (GetCv(areaGrid[iConcentration]) > CvThreshold)
                        {
                            break;
                        }
                        else
                        {
                            loqCheck = concentrationValues[iConcentration];
                        }
                    }

                    if (loqCheck >= 0 && Math.Abs(loqCheck - lastLoq) / loqCheck < SAME_LOQ_REL_THRESHOLD)
                    {
                        numItersWithSameLoq++;
                    }
                    else
                    {
                        numItersWithSameLoq = 0;
                    }

                    lastLoq = loqCheck;
                    if (numItersWithSameLoq > MinSameLoqCountForAccept)
                    {
                        break;
                    }
                }
            }

            double loq = maxConcentration;
            for (int iConcentration = concentrationValues.Count - 1; iConcentration >= 0; iConcentration--)
            {
                var cv = GetCv(areaGrid[iConcentration]);
                if (cv > CvThreshold)
                {
                    break;
                }

                loq = concentrationValues[iConcentration];
            }

            return loq;
        }

        private double GetCv(RunningStatistics runningStatistics)
        {
            if (runningStatistics.Mean <= 0)
            {
                return CvThreshold * 2;
            }

            return runningStatistics.StandardDeviation / runningStatistics.Mean;
        }

        public double ComputeLod(IList<WeightedPoint> points)
        {
            if (points.Count == 0)
            {
                return double.MaxValue;
            }
            BilinearCurveFit fit = FitBilinearCurve(points);
            if (fit == null)
            {
                return double.MaxValue;
            }
            var largestConc = points.Max(pt => pt.X);
            var lodArea = fit.BaselineHeight + fit.StdDevBaseline;
            var smallestNonzeroConc = points.Where(pt => pt.X > 0).Select(pt => pt.X).Append(largestConc).Min();
            double lodConc;
            if (fit.Slope == 0)
            {
                lodConc = largestConc;
            }
            else
            {
                lodConc = (lodArea - fit.Intercept) / fit.Slope;
            }

            lodConc = Math.Max(smallestNonzeroConc, Math.Min(lodConc, largestConc));
            return lodConc;
        }

        public IDictionary<CalibrationPoint, double> GetCalibrationPoints(CalibrationCurveFitter calibrationCurveFitter)
        {
            var result = new Dictionary<CalibrationPoint, double>();
            foreach (var calibrationPoint in calibrationCurveFitter.EnumerateCalibrationPoints())
            {
                var concentration = calibrationCurveFitter.GetPeptideConcentration(calibrationPoint);
                if (!concentration.HasValue)
                {
                    continue;
                }

                var area = calibrationCurveFitter.GetNormalizedPeakArea(calibrationPoint);
                if (!area.HasValue)
                {
                    continue;
                }

                result[calibrationPoint] = area.Value;
            }

            return result;
        }

        public QuantLimit ComputeQuantLimits(IList<WeightedPoint> areas)
        {
            return new QuantLimit(ComputeLod(areas), ComputeBootstrappedLoq(areas));
        }

        public QuantLimit ComputeQuantLimits(CalibrationCurveFitter calibrationCurveFitter)
        {
            var weightedPoints = calibrationCurveFitter.EnumerateCalibrationPoints()
                .Select(calibrationCurveFitter.GetWeightedPoint).OfType<WeightedPoint>().ToList();
            var combinedWeightedPoints = new List<WeightedPoint>();
            foreach (var pointGroup in weightedPoints.GroupBy(pt => pt.X))
            {
                combinedWeightedPoints.Add(new WeightedPoint(pointGroup.Key, pointGroup.Average(pt=>pt.Y), pointGroup.First().Weight));
            }
            return ComputeQuantLimits(combinedWeightedPoints);
        }

        public IList<int> OptimizeTransitions(OptimizeType optimizeType, IList<IList<WeightedPoint>> areas, out QuantLimit finalQuantLimit)
        {
            var quantLimits = new List<Tuple<int, QuantLimit>>();

            OptimizeType otherOptimizeType;
            if (optimizeType == OptimizeType.LOD)
            {
                otherOptimizeType = OptimizeType.LOQ;
            }
            else
            {
                otherOptimizeType = OptimizeType.LOD;
            }

            for (int iTransition = 0; iTransition < areas.Count; iTransition++)
            {
                quantLimits.Add(Tuple.Create(iTransition, ComputeQuantLimits(areas[iTransition])));
            }

            var maxConcentration = areas.SelectMany(list => list).Max(point => point.X);
            var lowestLimits = new Dictionary<OptimizeType, double>()
            {
                {OptimizeType.LOD, quantLimits.Min(q=>q.Item2.Lod)},
                {OptimizeType.LOQ, quantLimits.Min(q=>q.Item2.Loq)}
            };
            if (lowestLimits[optimizeType] == maxConcentration && lowestLimits[otherOptimizeType] < maxConcentration)
            {
                quantLimits = quantLimits.OrderBy(q => q.Item2.GetQuantLimit(otherOptimizeType)).ToList();
            }
            else
            {
                quantLimits = quantLimits.OrderBy(q => q.Item2.GetQuantLimit(optimizeType)).ToList();
            }

            IList<int> acceptedFragmentIndices = new List<int>();
            var acceptedAreas = areas.First().Select(pt => new WeightedPoint(pt.X, 0, pt.Weight)).ToList();
            foreach (var quantLimit in quantLimits.Take(MinNumTransitions))
            {
                if (acceptedFragmentIndices.Count > 0 && quantLimit.Item2.GetQuantLimit(optimizeType) >= maxConcentration)
                {
                    break;
                }
                acceptedFragmentIndices.Add(quantLimit.Item1);
                acceptedAreas = acceptedAreas.Zip(areas[quantLimit.Item1],
                    (pt1, pt2) => new WeightedPoint(pt1.X, pt1.Y + pt2.Y, pt1.Weight)).ToList();
            }

            var optimizedQuantLimit = ComputeQuantLimits(acceptedAreas);
            int startIndex = Math.Min(acceptedFragmentIndices.Count, MinNumTransitions);
            var rejectedItems = new List<Tuple<int, QuantLimit>>();
            foreach (var quantLimitAndIndex in quantLimits.Skip(startIndex))
            {
                var fragmentIndex = quantLimitAndIndex.Item1;
                var possibleNewAreas = acceptedAreas.Zip(areas[fragmentIndex],
                    (pt1, pt2) => new WeightedPoint(pt1.X, pt1.Y + pt2.Y, pt1.Weight)).ToList();
                var prospectiveQuantLimit = ComputeQuantLimits(possibleNewAreas);
                // accept this transition if it helped the result
                if (prospectiveQuantLimit.GetQuantLimit(optimizeType) < optimizedQuantLimit.GetQuantLimit(optimizeType))
                {
                    optimizedQuantLimit = prospectiveQuantLimit;
                    acceptedAreas = possibleNewAreas;
                    acceptedFragmentIndices.Add(fragmentIndex);
                }
                else
                {
                    // save the limits in case we don't have enough limits at the end of this
                    rejectedItems.Add(Tuple.Create(fragmentIndex, prospectiveQuantLimit));
                    lowestLimits[OptimizeType.LOD] = Math.Min(lowestLimits[OptimizeType.LOD],
                        prospectiveQuantLimit.GetQuantLimit(OptimizeType.LOD));
                    lowestLimits[OptimizeType.LOQ] = Math.Min(lowestLimits[OptimizeType.LOQ],
                        prospectiveQuantLimit.GetQuantLimit(OptimizeType.LOQ));
                }
            }
            // if we still don't have enough transitions, for the case where there were transitions at the maximum limit
            if (acceptedFragmentIndices.Count < MinNumTransitions && rejectedItems.Any())
            {
                if (lowestLimits[optimizeType] == maxConcentration &&
                    lowestLimits[otherOptimizeType] < maxConcentration)
                {
                    rejectedItems = rejectedItems.OrderBy(item => item.Item2.GetQuantLimit(otherOptimizeType)).ToList();
                }
                else
                {
                    rejectedItems = rejectedItems.OrderBy(item => item.Item2.GetQuantLimit(optimizeType)).ToList();
                }

                int numTransitionsNeeded = MinNumTransitions - acceptedFragmentIndices.Count;
                foreach (var item in rejectedItems.Take(numTransitionsNeeded))
                {
                    acceptedFragmentIndices.Add(item.Item1);
                    acceptedAreas = acceptedAreas.Zip(areas[item.Item1],
                        (pt1, pt2) => new WeightedPoint(pt1.X, pt1.Y + pt2.Y, pt1.Weight)).ToList();
                }

                optimizedQuantLimit = ComputeQuantLimits(acceptedAreas);
            }

            finalQuantLimit = optimizedQuantLimit;
            return acceptedFragmentIndices;
        }
        
        public PeptideDocNode OptimizeTransitions(OptimizeType optimizeType, CalibrationCurveFitter calibrationCurveFitter)
        {
            var standardConcentrations = calibrationCurveFitter.GetStandardConcentrations();
            if (standardConcentrations.Count == 0)
            {
                return null;
            }
            OptimizeType otherOptimizeType;
            if (optimizeType == OptimizeType.LOD)
            {
                otherOptimizeType = OptimizeType.LOQ;
            }
            else
            {
                otherOptimizeType = OptimizeType.LOD;
            }

            var maxConcentration = standardConcentrations.Values.Max();
            var quantLimits = new List<Tuple<IdentityPath, QuantLimit>>();
            var peptideDocNode = calibrationCurveFitter.PeptideQuantifier.PeptideDocNode;
            foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
            {
                foreach (var transitionDocNode in transitionGroupDocNode.Transitions)
                {
                    var identityPath = new IdentityPath(
                        calibrationCurveFitter.PeptideQuantifier.PeptideGroupDocNode.PeptideGroup,
                        calibrationCurveFitter.PeptideQuantifier.PeptideDocNode.Peptide,
                        transitionGroupDocNode.TransitionGroup, transitionDocNode.Transition);
                    var transitionCalibrationCurveFitter =
                        calibrationCurveFitter.MakeCalibrationCurveFitterWithTransitions(
                            ImmutableList.Singleton(identityPath));
                    var quantLimit = ComputeQuantLimits(transitionCalibrationCurveFitter);
                    if (quantLimit != null)
                    {
                        quantLimits.Add(Tuple.Create(identityPath, quantLimit));
                    }
                }
            }
            var lowestLimits = new Dictionary<OptimizeType, double>()
            {
                {OptimizeType.LOD, quantLimits.Min(q=>q.Item2.Lod)},
                {OptimizeType.LOQ, quantLimits.Min(q=>q.Item2.Loq)}
            };
            if (lowestLimits[optimizeType] == maxConcentration && lowestLimits[otherOptimizeType] < maxConcentration)
            {
                quantLimits = quantLimits.OrderBy(q => q.Item2.GetQuantLimit(otherOptimizeType)).ToList();
            }
            else
            {
                quantLimits = quantLimits.OrderBy(q => q.Item2.GetQuantLimit(optimizeType)).ToList();
            }

            IList<IdentityPath> acceptedTransitionIdentityPaths = new List<IdentityPath>();
            foreach (var quantLimit in quantLimits.Take(MinNumTransitions))
            {
                if (acceptedTransitionIdentityPaths.Count > 0 && quantLimit.Item2.GetQuantLimit(optimizeType) >= maxConcentration)
                {
                    break;
                }
                acceptedTransitionIdentityPaths.Add(quantLimit.Item1);
            }

            var optimizedQuantLimit = ComputeQuantLimits(calibrationCurveFitter.MakeCalibrationCurveFitterWithTransitions(acceptedTransitionIdentityPaths));
            int startIndex = Math.Min(acceptedTransitionIdentityPaths.Count, MinNumTransitions);
            var rejectedItems = new List<Tuple<IdentityPath, QuantLimit>>();
            foreach (var quantLimitAndIndex in quantLimits.Skip(startIndex))
            {
                var possibleCalibrationCurveFitter = calibrationCurveFitter
                    .MakeCalibrationCurveFitterWithTransitions(acceptedTransitionIdentityPaths.Append(quantLimitAndIndex.Item1));
                var prospectiveQuantLimit = ComputeQuantLimits(possibleCalibrationCurveFitter);
                // accept this transition if it helped the result
                if (prospectiveQuantLimit.GetQuantLimit(optimizeType) < optimizedQuantLimit.GetQuantLimit(optimizeType))
                {
                    optimizedQuantLimit = prospectiveQuantLimit;
                    acceptedTransitionIdentityPaths.Add(quantLimitAndIndex.Item1);
                }
                else
                {
                    // save the limits in case we don't have enough limits at the end of this
                    rejectedItems.Add(Tuple.Create(quantLimitAndIndex.Item1, prospectiveQuantLimit));
                    lowestLimits[OptimizeType.LOD] = Math.Min(lowestLimits[OptimizeType.LOD],
                        prospectiveQuantLimit.GetQuantLimit(OptimizeType.LOD));
                    lowestLimits[OptimizeType.LOQ] = Math.Min(lowestLimits[OptimizeType.LOQ],
                        prospectiveQuantLimit.GetQuantLimit(OptimizeType.LOQ));
                }
            }
            // if we still don't have enough transitions, for the case where there were transitions at the maximum limit
            if (acceptedTransitionIdentityPaths.Count < MinNumTransitions && rejectedItems.Any())
            {
                if (lowestLimits[optimizeType] == maxConcentration &&
                    lowestLimits[otherOptimizeType] < maxConcentration)
                {
                    rejectedItems = rejectedItems.OrderBy(item => item.Item2.GetQuantLimit(otherOptimizeType)).ToList();
                }
                else
                {
                    rejectedItems = rejectedItems.OrderBy(item => item.Item2.GetQuantLimit(optimizeType)).ToList();
                }

                int numTransitionsNeeded = MinNumTransitions - acceptedTransitionIdentityPaths.Count;
                foreach (var item in rejectedItems.Take(numTransitionsNeeded))
                {
                    acceptedTransitionIdentityPaths.Add(item.Item1);
                }

                var acceptedCalibrationCurveFitter =
                    calibrationCurveFitter.MakeCalibrationCurveFitterWithTransitions(acceptedTransitionIdentityPaths);
                optimizedQuantLimit = ComputeQuantLimits(acceptedCalibrationCurveFitter);
            }

            return calibrationCurveFitter.PeptideQuantifier.WithQuantifiableTransitions(acceptedTransitionIdentityPaths).PeptideDocNode;
        }

        public class QuantLimit
        {
            public QuantLimit(double lod, double loq)
            {
                Lod = lod;
                Loq = loq;
            }

            public double Lod { get; }
            public double Loq { get; }

            public double GetQuantLimit(OptimizeType optimizeType)
            {
                if (optimizeType == OptimizeType.LOD)
                {
                    return Lod;
                }

                return Loq;
            }
        }
    }
}
