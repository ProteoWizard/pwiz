using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using pwiz.Common.Collections;
using ZedGraph;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class BilinearTransitionOptimizer
    {
        public BilinearTransitionOptimizer()
        {
            OptimizeTransitionSettings = OptimizeTransitionSettings.DEFAULT;
            BootstrapFiguresOfMeritCalculator = new BootstrapFiguresOfMeritCalculator(.2);
        }

        public OptimizeTransitionSettings OptimizeTransitionSettings { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public BootstrapFiguresOfMeritCalculator BootstrapFiguresOfMeritCalculator { get; set; }

        public double ComputeBootstrappedLoq(IList<WeightedPoint> points, List<ImmutableList<PointPair>> bootstrapCurves)
        {
            return BootstrapFiguresOfMeritCalculator.ComputeBootstrappedLoq(points, bootstrapCurves);
        }

        public static double ComputeLod(IList<WeightedPoint> points)
        {
            return BootstrapFiguresOfMeritCalculator.ComputeLod(points);
        }

        public QuantLimit ComputeQuantLimits(IList<WeightedPoint> areas, List<ImmutableList<PointPair>> bootstrapCurves = null)
        {
            var lod = ComputeLod(areas);
            var loq = ComputeBootstrappedLoq(areas, bootstrapCurves);
            if (loq < lod)
            {
                loq = lod;
            }
            return new QuantLimit(lod, loq);
        }

        public QuantLimit ComputeQuantLimits(CalibrationCurveFitter calibrationCurveFitter)
        {
            var weightedPoints = calibrationCurveFitter.GetStandardPoints().ToList();
            List<WeightedPoint> combinedWeightedPoints;
            if (OptimizeTransitionSettings.CombinePointsWithSameConcentration)
            {
                combinedWeightedPoints = new List<WeightedPoint>();
                foreach (var pointGroup in weightedPoints.GroupBy(pt => pt.X))
                {
                    combinedWeightedPoints.Add(new WeightedPoint(pointGroup.Key, pointGroup.Average(pt => pt.Y), pointGroup.First().Weight));
                }
            }
            else
            {
                combinedWeightedPoints = weightedPoints;
            }
            return ComputeQuantLimits(combinedWeightedPoints);
        }

        public IList<int> OptimizeTransitions(OptimizeTransitionSettings settings, IList<IList<WeightedPoint>> areas, out QuantLimit finalQuantLimit)
        {
            var quantLimits = new List<Tuple<int, QuantLimit>>();

            var optimizeType = settings.OptimizeType;
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
            foreach (var quantLimit in quantLimits.Take(settings.MinimumNumberOfTransitions))
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
            int startIndex = Math.Min(acceptedFragmentIndices.Count, settings.MinimumNumberOfTransitions);
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
            if (acceptedFragmentIndices.Count < settings.MinimumNumberOfTransitions && rejectedItems.Any())
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

                int numTransitionsNeeded = settings.MinimumNumberOfTransitions - acceptedFragmentIndices.Count;
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
        
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        public PeptideDocNode OptimizeTransitions(CalibrationCurveFitter calibrationCurveFitter, OptimizeTransitionDetails details)
        {
            var standardConcentrations = calibrationCurveFitter.GetStandardConcentrations();
            if (standardConcentrations.Count == 0)
            {
                return null;
            }

            var optimizeType = OptimizeTransitionSettings.OptimizeType;
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
            var singleQuantLimits = new List<TransitionsQuantLimit>();
            var peptideDocNode = calibrationCurveFitter.PeptideQuantifier.PeptideDocNode;
            foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
            {
                foreach (var transitionDocNode in transitionGroupDocNode.Transitions)
                {
                    if (OptimizeTransitionSettings.PreserveNonQuantitative && !transitionDocNode.ExplicitQuantitative)
                    {
                        continue;
                    }
                    var identityPath = new IdentityPath(
                        calibrationCurveFitter.PeptideQuantifier.PeptideGroupDocNode.PeptideGroup,
                        calibrationCurveFitter.PeptideQuantifier.PeptideDocNode.Peptide,
                        transitionGroupDocNode.TransitionGroup, transitionDocNode.Transition);
                    var quantLimit = ComputeTransitionQuantLimit(calibrationCurveFitter, identityPath);
                    if (quantLimit != null)
                    {
                        singleQuantLimits.Add(quantLimit);
                    }
                }
            }

            if (singleQuantLimits.Count == 0)
            {
                return peptideDocNode;
            }

            if (details != null)
            {
                details.Original = ComputeTransitionsQuantLimit(calibrationCurveFitter,
                    singleQuantLimits.SelectMany(q => q.TransitionIdentityPaths));
            }
            var lowestLimits = new Dictionary<OptimizeType, double>()
            {
                {OptimizeType.LOD, singleQuantLimits.Min(q=>q.QuantLimit.Lod)},
                {OptimizeType.LOQ, singleQuantLimits.Min(q=>q.QuantLimit.Loq)}
            };
            if (lowestLimits[optimizeType] == maxConcentration && lowestLimits[otherOptimizeType] < maxConcentration)
            {
                singleQuantLimits = singleQuantLimits.OrderBy(q => q.QuantLimit.GetQuantLimit(otherOptimizeType)).ToList();
            }
            else
            {
                singleQuantLimits = singleQuantLimits.OrderBy(q => q.QuantLimit.GetQuantLimit(optimizeType)).ToList();
            }
            details?.SingleQuantLimits.AddRange(singleQuantLimits);
            IList<IdentityPath> acceptedTransitionIdentityPaths = new List<IdentityPath>();
            foreach (var quantLimit in singleQuantLimits.Take(OptimizeTransitionSettings.MinimumNumberOfTransitions))
            {
                if (acceptedTransitionIdentityPaths.Count > 0 && quantLimit.QuantLimit.GetQuantLimit(optimizeType) >= maxConcentration)
                {
                    break;
                }
                acceptedTransitionIdentityPaths.Add(quantLimit.TransitionIdentityPaths.Single());
            }

            var optimizedQuantLimit = ComputeTransitionsQuantLimit(calibrationCurveFitter, acceptedTransitionIdentityPaths);
            details?.AcceptedQuantLimits.Add(optimizedQuantLimit);
            int startIndex = Math.Min(acceptedTransitionIdentityPaths.Count, OptimizeTransitionSettings.MinimumNumberOfTransitions);
            var rejectedItems = new List<TransitionsQuantLimit>();
            foreach (var quantLimitAndIndex in singleQuantLimits.Skip(startIndex))
            {
                var prospectiveQuantLimit = ComputeTransitionsQuantLimit(calibrationCurveFitter,
                    acceptedTransitionIdentityPaths.Append(quantLimitAndIndex.TransitionIdentityPaths.Single()));
                // accept this transition if it helped the result
                if (prospectiveQuantLimit.GetQuantLimit(optimizeType) < optimizedQuantLimit.GetQuantLimit(optimizeType))
                {
                    optimizedQuantLimit = prospectiveQuantLimit;
                    acceptedTransitionIdentityPaths.Add(quantLimitAndIndex.TransitionIdentityPaths.Single());
                    details?.AcceptedQuantLimits.Add(prospectiveQuantLimit);
                }
                else
                {
                    // save the limits in case we don't have enough limits at the end of this
                    rejectedItems.Add(prospectiveQuantLimit);
                    lowestLimits[OptimizeType.LOD] = Math.Min(lowestLimits[OptimizeType.LOD],
                        prospectiveQuantLimit.GetQuantLimit(OptimizeType.LOD));
                    lowestLimits[OptimizeType.LOQ] = Math.Min(lowestLimits[OptimizeType.LOQ],
                        prospectiveQuantLimit.GetQuantLimit(OptimizeType.LOQ));
                    details?.RejectedQuantLimits.Add(prospectiveQuantLimit);
                }
            }
            // if we still don't have enough transitions, for the case where there were transitions at the maximum limit
            if (acceptedTransitionIdentityPaths.Count < OptimizeTransitionSettings.MinimumNumberOfTransitions && rejectedItems.Any())
            {
                if (lowestLimits[optimizeType] == maxConcentration &&
                    lowestLimits[otherOptimizeType] < maxConcentration)
                {
                    rejectedItems = rejectedItems.OrderBy(item => item.GetQuantLimit(otherOptimizeType)).ToList();
                }
                else
                {
                    rejectedItems = rejectedItems.OrderBy(item => item.GetQuantLimit(optimizeType)).ToList();
                }

                int numTransitionsNeeded = OptimizeTransitionSettings.MinimumNumberOfTransitions - acceptedTransitionIdentityPaths.Count;
                foreach (var item in rejectedItems.Take(numTransitionsNeeded))
                {
                    acceptedTransitionIdentityPaths.Add(item.TransitionIdentityPaths.Last());
                }

                optimizedQuantLimit = ComputeTransitionsQuantLimit(calibrationCurveFitter, acceptedTransitionIdentityPaths);
                details?.AcceptedQuantLimits.Add(optimizedQuantLimit);
            }

            return calibrationCurveFitter.PeptideQuantifier.WithQuantifiableTransitions(acceptedTransitionIdentityPaths).PeptideDocNode;
        }

        private TransitionsQuantLimit ComputeTransitionQuantLimit(CalibrationCurveFitter calibrationCurveFitter,
            IdentityPath transitionIdentityPath)
        {
            return ComputeTransitionsQuantLimit(calibrationCurveFitter,
                ImmutableList.Singleton(transitionIdentityPath));
        }
        private TransitionsQuantLimit ComputeTransitionsQuantLimit(CalibrationCurveFitter calibrationCurveFitter,
            IEnumerable<IdentityPath> identityPaths)
        {
            var identityPathList = ImmutableList.ValueOf(identityPaths);
            var transitionCalibrationCurveFitter =
                calibrationCurveFitter.MakeCalibrationCurveFitterWithTransitions(identityPathList);
            var quantLimit = ComputeQuantLimits(transitionCalibrationCurveFitter);
            return new TransitionsQuantLimit(quantLimit, identityPathList);
        }
    }
}
