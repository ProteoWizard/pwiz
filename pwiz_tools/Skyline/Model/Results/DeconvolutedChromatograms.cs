using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Results
{
    public class DeconvolutedChromatograms
    {
        public static IList<TimeIntensities> Deconvolute(SrmSettings settings, IList<ChromatogramGroupInfo> chromGroups,
            IList<PeptideDocNode> peptideDocNodes, 
            IList<TransitionGroupDocNode> nodeGroups, List<double> scores)
        {
            var massDistributions = new List<MassDistribution>(nodeGroups.Count);
            for (int iNode = 0; iNode < nodeGroups.Count; iNode++)
            {
                massDistributions.Add(GetMassDistribution(settings, peptideDocNodes[iNode], nodeGroups[iNode]));
            }
            return DeconvoluteChromatograms(chromGroups, massDistributions, scores);
        }

        private static MassDistribution GetMassDistribution(SrmSettings settings, PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupDocNode)
        {
            var calc = settings.GetPrecursorCalc(transitionGroupDocNode.IsCustomIon ? IsotopeLabelType.light 
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

        public static IList<TimeIntensities> DeconvoluteChromatograms(IList<ChromatogramGroupInfo> chromGroups,
            IList<MassDistribution> massDistributions, List<double> scores)
        {
            var predictedIntensities = massDistributions.Select(md => new List<double>()).ToArray();
            
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
                    for (int iMassDist = 0; iMassDist < massDistributions.Count; iMassDist++)
                    {
                        var massDist = massDistributions[iMassDist];
                        double mzMiddle = chromatogram.ProductMz.Value;
                        double mzLower = mzMiddle - chromatogram.ExtractionWidth.GetValueOrDefault(0) - mzMiddle * massDist.MassResolution;
                        double mzUpper = mzMiddle + chromatogram.ExtractionWidth.GetValueOrDefault(0) + mzMiddle * massDist.MassResolution;
                        double intensity = GetIntensityInRange(massDist, mzLower, mzUpper);
                        predictedIntensities[iMassDist].Add(intensity);
                    }
                }
            }
            return Deconvolute(timeIntensities, predictedIntensities, scores);
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

        public static IList<TimeIntensities> Deconvolute(IList<TimeIntensities> timeIntensities, 
            IList<IList<double>> deconvolutedAmounts, List<double> scores)
        {
            var allTimes = ImmutableList.ValueOf(timeIntensities.SelectMany(ti => ti.Times).Distinct().OrderBy(t => t));
            var mergedTimeIntensities = timeIntensities.Select(ti => ti.Interpolate(allTimes, false)).ToArray();
            var deconvolutedIntensities = deconvolutedAmounts.Select(x => new List<float>(allTimes.Count)).ToArray();
            var candidateVectors = deconvolutedAmounts.Select(intens => (Vector<double>)new DenseVector(intens.ToArray())).ToList();
            for (int iTime = 0; iTime < allTimes.Count; iTime++)
            {
                var observedVector = new DenseVector(mergedTimeIntensities.Select(ti => (double) ti.Intensities[iTime]).ToArray());
                Vector<double> amounts = FindBestCombinationFilterNegatives(observedVector, candidateVectors);
                Vector<double> totalPrediction = new DenseVector(timeIntensities.Count);
                for (int iCandidate = 0; iCandidate < deconvolutedIntensities.Length; iCandidate++)
                {
                    var scaledVector = candidateVectors[iCandidate].Multiply(amounts[iCandidate]);
                    totalPrediction = totalPrediction.Add(scaledVector);
                    deconvolutedIntensities[iCandidate].Add((float) amounts[iCandidate]);
                }
                if (scores != null)
                {
                    double score = observedVector.Normalize(2.0) * totalPrediction.Normalize(2.0);
                    scores.Add(score);
                }
            }
            
            return deconvolutedIntensities.Select(intensities => new TimeIntensities(allTimes, intensities, null, null)).ToArray();
        }

        static Vector<double> FindBestCombinationFilterNegatives(Vector<double> observedIntensities,
            IList<Vector<double>> candidates)
        {
            List<int> remaining = new List<int>();
            for (int i = 0; i < candidates.Count(); i++)
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

    }
}
