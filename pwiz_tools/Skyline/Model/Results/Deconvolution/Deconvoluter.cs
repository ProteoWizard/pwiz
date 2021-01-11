using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Deconvolution
{
    public class Deconvoluter
    {
        public Deconvoluter(SrmSettings settings)
        {
            Settings = settings;
        }

        public DeconvolutionKey MakeDeconvolutionKey(PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupDocNode)
        {
            return new DeconvolutionKey(peptideDocNode, transitionGroupDocNode,
                GetMassDistribution(peptideDocNode, transitionGroupDocNode));
        }

        public SrmSettings Settings { get; private set; }
        private MassDistribution GetMassDistribution(PeptideDocNode peptideDocNode, TransitionGroupDocNode transitionGroupDocNode)
        {
            var settings = Settings;
            var calc = settings.GetPrecursorCalc(transitionGroupDocNode.IsCustomIon
                    ? IsotopeLabelType.light
                    : transitionGroupDocNode.TransitionGroup.LabelType,
                peptideDocNode.ExplicitMods);
            var isotopeAbundances = settings.TransitionSettings.FullScan.IsotopeAbundances;
            if (!transitionGroupDocNode.TransitionGroup.IsCustomIon)
            {
                return calc.GetMzDistribution(transitionGroupDocNode.Peptide.Target,
                    transitionGroupDocNode.PrecursorAdduct, isotopeAbundances);
            }
            if (transitionGroupDocNode.CustomMolecule.Formula != null)
            {
                return calc.GetMZDistributionFromFormula(transitionGroupDocNode.CustomMolecule.Formula,
                    transitionGroupDocNode.PrecursorAdduct, isotopeAbundances);
            }
            return calc.GetMZDistributionSinglePoint(transitionGroupDocNode.PrecursorMz);
        }

        public IEnumerable<DeconvolutedChromatograms> DeconvoluteChromatograms(IList<ChromatogramGroupInfo> chromGroups,
            IList<DeconvolutionKey> keys)
        {
            var predictedIntensities = keys.Select(key => new List<double>()).ToArray();
            
            var timeIntensities = new List<TimeIntensities>();
            foreach (var chromGroup in chromGroups)
            {
                if (chromGroup == null)
                {
                    continue;
                }
                for (int iChromatogram = 0; iChromatogram < chromGroup.NumTransitions; iChromatogram++)
                {
                    var chromatogram = chromGroup.GetTransitionInfo(iChromatogram, TransformChrom.raw);
                    if (chromatogram.Source != ChromSource.ms1)
                    {
                        continue;
                    }
                    timeIntensities.Add(chromatogram.TimeIntensities);
                    for (int iMassDist = 0; iMassDist < keys.Count; iMassDist++)
                    {
                        var massDist = keys[iMassDist].MassDistribution;
                        double mzMiddle = chromatogram.ProductMz.Value;
                        double mzLower = mzMiddle - chromatogram.ExtractionWidth.GetValueOrDefault(0) - mzMiddle * massDist.MassResolution;
                        double mzUpper = mzMiddle + chromatogram.ExtractionWidth.GetValueOrDefault(0) + mzMiddle * massDist.MassResolution;
                        double intensity = GetIntensityInRange(massDist, mzLower, mzUpper);
                        predictedIntensities[iMassDist].Add(intensity);
                    }
                }
            }

            foreach (var group in FindNonOverlappingGroups(predictedIntensities))
            {
                var activeIndexes = Enumerable.Range(0, timeIntensities.Count)
                    .Where(i => group.Any(groupMember => predictedIntensities[groupMember][i] != 0)).ToList();
                var activeTimeIntensities = activeIndexes.Select(i => timeIntensities[i]).ToList();
                var groupPredictedIntensities = new List<IList<double>>();
                foreach (int groupMember in group)
                {
                    groupPredictedIntensities.Add(activeIndexes.Select(i=>predictedIntensities[groupMember][i]).ToList());
                }

                var scores = new List<double>();
                var deconvolutedTimeIntensities = DeconvoluteTimeIntensities(activeTimeIntensities, groupPredictedIntensities, scores);
                var timeIntensityTuples = group.Select(groupMember=>keys[groupMember]).Zip(deconvolutedTimeIntensities, Tuple.Create);
                yield return new DeconvolutedChromatograms(timeIntensityTuples, scores);
            }
        }

        public List<double> DeconvoluteAreas(IList<DeconvolutionKey> keys,
            IList<KeyValuePair<double, double>> areas, double windowWidth)
        {
            var predictedIntensities = keys.Select(key => new List<double>()).ToArray();
            var observedAreas = new List<double>();
            foreach (var mzIntensity in areas)
            {
                observedAreas.Add(mzIntensity.Value);
                for (int iMassDist = 0; iMassDist < keys.Count; iMassDist++)
                {
                    var massDist = keys[iMassDist].MassDistribution;
                    double mzMiddle = mzIntensity.Key;
                    double mzLower = mzMiddle - windowWidth / 2;
                    double mzUpper = mzMiddle + windowWidth / 2;
                    double intensity = GetIntensityInRange(massDist, mzLower, mzUpper);
                    predictedIntensities[iMassDist].Add(intensity);
                }
            }

            var observedIntensities = observedAreas.Select(area => (IList<double>) ImmutableList.Singleton(area)).ToList();
            var scores = new List<double>();
            var deconvoluted = DeconvoluteIntensities(observedIntensities, predictedIntensities, scores);
            var result = new List<double>();
            for (int i = 0; i < keys.Count; i++)
            {
                var intensities = deconvoluted[i];
                Assume.AreEqual(1, intensities.Count);
                result.Add(intensities[0]);
            }

            return result;
        }

        private static double GetIntensityInRange(MassDistribution massDistribution, double minMz, double maxMz)
        {
            double totalIntensity = 0;
            foreach (var entry in massDistribution)
            {
                if (entry.Key < minMz || entry.Key > maxMz)
                {
                    continue;
                }
                totalIntensity += entry.Value;
            }
            return totalIntensity;
        }

        private static IList<TimeIntensities> DeconvoluteTimeIntensities(IList<TimeIntensities> timeIntensities, 
            IList<IList<double>> predictedIntensities, List<double> scores)
        {
            var allTimes = ImmutableList.ValueOf(timeIntensities.SelectMany(ti => ti.Times).Distinct().OrderBy(t => t));
            var mergedTimeIntensities = timeIntensities.Select(ti => ti.Interpolate(allTimes, false)).ToArray();
            var observedIntensities =
                mergedTimeIntensities.Select(ti => (IList<double>) ti.Intensities.Select(i => (double) i).ToList()).ToList();
            var deconvolutedIntensities = DeconvoluteIntensities(observedIntensities, predictedIntensities, scores);
            return deconvolutedIntensities.Select(intensities => new TimeIntensities(allTimes, intensities.Select(i=>(float)i), null, null)).ToArray();
        }

        private static IList<IList<double>> DeconvoluteIntensities(IList<IList<double>> observedIntensities,
            IList<IList<double>> predictedIntensities, List<double> scores)
        {
            int timeCount = observedIntensities[0].Count;
            var deconvolutedIntensities = predictedIntensities.Select(x => new List<double>(timeCount)).ToArray();
            var candidateVectors = predictedIntensities.Select(intens => (Vector<double>)new DenseVector(intens.ToArray())).ToList();
            for (int iTime = 0; iTime < timeCount; iTime++)
            {
                var observedVector = new DenseVector(observedIntensities.Select(intensities => intensities[iTime]).ToArray());
                Vector<double> amounts = FindBestCombinationFilterNegatives(observedVector, candidateVectors);
                Vector<double> totalPrediction = new DenseVector(observedIntensities.Count);
                for (int iCandidate = 0; iCandidate < deconvolutedIntensities.Length; iCandidate++)
                {
                    var scaledVector = candidateVectors[iCandidate].Multiply(amounts[iCandidate]);
                    totalPrediction = totalPrediction.Add(scaledVector);
                    deconvolutedIntensities[iCandidate].Add((float)amounts[iCandidate]);
                }
                if (scores != null)
                {
                    double score = observedVector.Normalize(2.0) * totalPrediction.Normalize(2.0);
                    scores.Add(score);
                }
            }

            return deconvolutedIntensities;
        }

        static Vector<double> FindBestCombinationFilterNegatives(Vector<double> observedIntensities,
            IList<Vector<double>> candidates)
        {
            List<int> remaining = new List<int>();
            for (int i = 0; i < candidates.Count; i++)
            {
                remaining.Add(i);
            }
            Vector<double> filteredResult;
            while(true)
            {
                Vector<double>[] curCandidates = new Vector<double>[remaining.Count];
                for (int i = 0; i < remaining.Count; i++)
                {
                    curCandidates[i] = candidates[remaining[i]];
                }
                filteredResult = FindBestCombination(observedIntensities, curCandidates);
                if (filteredResult == null)
                {
                    return null;
                }
                if (filteredResult.Min() >= 0)
                {
                    break;
                }
                List<int> newRemaining = new List<int>();
                for (int i = 0; i < remaining.Count; i++)
                {
                    if (filteredResult[i] >= 0)
                    {
                        newRemaining.Add(remaining[i]);
                    }
                }
                remaining = newRemaining;
                if (remaining.Count == 0)
                {
                    return null;
                }
            }
            Vector<double> result = new DenseVector(candidates.Count);
            for (int i = 0; i < remaining.Count; i++)
            {
                result[remaining[i]] = filteredResult[i];
            }
            return result;
        }

        internal static Vector<double> FindBestCombination(Vector<double> targetVector, params Vector<double>[] candidateVectors)
        {
            Matrix matrixA = new DenseMatrix(targetVector.Count, candidateVectors.Length);
            for (int j = 0; j < candidateVectors.Length; j++)
            {
                matrixA.SetColumn(j, candidateVectors[j]);
            }
            var matrixP = targetVector.ToColumnMatrix();
            var matrixAT = matrixA.Transpose();
            var matrixATA = matrixAT.Multiply(matrixA);
            if (matrixATA.Determinant() == 0)
            {
                return null;
            }
            var matrixATAInv = matrixATA.Inverse();
            var matrixResult = matrixATAInv.Multiply(matrixAT).Multiply(matrixP);
            return matrixResult.Column(0);
        }

        public IEnumerable<IList<int>> FindNonOverlappingGroups(IList<IList<double>> predictedIntensities)
        {
            var nonZeroPositions = new List<HashSet<int>>();
            foreach (var intensities in predictedIntensities)
            {
                nonZeroPositions.Add(new HashSet<int>(Enumerable.Range(0, intensities.Count).Where(i=>intensities[i] != 0)));
            }

            var remainingIndexes = Enumerable.Range(0, predictedIntensities.Count).ToList();
            while (remainingIndexes.Count > 0)
            {
                int firstIndex = remainingIndexes[0];
                remainingIndexes.RemoveAt(0);
                var group = new List<int>{firstIndex};
                var groupPositions = new HashSet<int>(nonZeroPositions[firstIndex]);
                bool anyFound = true;
                while (anyFound)
                {
                    anyFound = false;
                    for (int iTry = 0; iTry < remainingIndexes.Count; iTry++)
                    {
                        var nextIndex = remainingIndexes[iTry];
                        if (nonZeroPositions[nextIndex].Intersect(groupPositions).Any())
                        {
                            anyFound = true;
                            group.Add(nextIndex);
                            groupPositions.UnionWith(nonZeroPositions[nextIndex]);
                            remainingIndexes.RemoveAt(iTry);
                            break;
                        }
                    }
                }

                yield return group;
            }
        }
    }
}
