using pwiz.Common.Collections;
using pwiz.Skyline.Model.GroupComparison;
using static pwiz.Skyline.Model.DocSettings.AbsoluteQuantification.BilinearCurveFitter;
using System.Collections.Generic;
using System;
using System.Linq;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class TransitionOptimizer
    {
        private List<Tuple<IdentityPath, QuantLimit>> _singleTransitionQuantLimits;
        private TransitionOptimizer(BilinearCurveFitter bilinearCurveFitter, PeptideQuantifier peptideQuantifier,
            SrmSettings settings, OptimizeType optimizeType)
        {
            BilinearCurveFitter = bilinearCurveFitter;
            PeptideQuantifier = peptideQuantifier;
            Settings = settings;
        }
        public BilinearCurveFitter BilinearCurveFitter { get; }
        public PeptideQuantifier PeptideQuantifier { get; }
        public SrmSettings Settings { get; }

        public PeptideDocNode OptimizedPeptide
        {
            get;
            private set;
        }

        public Optimize OptimizeTransitions(int minNumTransitions)
        {
            _singleTransitionQuantLimits = new List<Tuple<IdentityPath, QuantLimit>>();
            var calibrationCurveFitter = new CalibrationCurveFitter(PeptideQuantifier, Settings);
            var standardConcentrations = calibrationCurveFitter.GetStandardConcentrations();
            if (standardConcentrations.Count == 0)
            {
                return false;
            }
            OptimizeType otherOptimizeType;
            if (OptimizeType == OptimizeType.LOD)
            {
                otherOptimizeType = OptimizeType.LOQ;
            }
            else
            {
                otherOptimizeType = OptimizeType.LOD;
            }

            var maxConcentration = standardConcentrations.Values.Max();
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
                    var quantLimit = BilinearCurveFitter.ComputeQuantLimits(transitionCalibrationCurveFitter);
                    if (quantLimit != null)
                    {
                        _singleTransitionQuantLimits.Add(Tuple.Create(identityPath, quantLimit));
                    }
                }
            }
            var lowestLimits = new Dictionary<OptimizeType, double>()
            {
                {OptimizeType.LOD, _singleTransitionQuantLimits.Min(q=>q.Item2.Lod)},
                {OptimizeType.LOQ, _singleTransitionQuantLimits.Min(q=>q.Item2.Loq)}
            };
            if (lowestLimits[OptimizeType] == maxConcentration && lowestLimits[otherOptimizeType] < maxConcentration)
            {
                _singleTransitionQuantLimits = _singleTransitionQuantLimits.OrderBy(q => q.Item2.GetQuantLimit(otherOptimizeType)).ToList();
            }
            else
            {
                _singleTransitionQuantLimits = _singleTransitionQuantLimits.OrderBy(q => q.Item2.GetQuantLimit(OptimizeType)).ToList();
            }

            IList<IdentityPath> acceptedTransitionIdentityPaths = new List<IdentityPath>();
            foreach (var quantLimit in _singleTransitionQuantLimits.Take(minNumTransitions))
            {
                if (acceptedTransitionIdentityPaths.Count > 0 && quantLimit.Item2.GetQuantLimit(OptimizeType) >= maxConcentration)
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
            if (acceptedTransitionIdentityPaths.Count < minNumTransitions && rejectedItems.Any())
            {
                if (lowestLimits[OptimizeType] == maxConcentration &&
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
    }
}
