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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using pwiz.Common.Chemistry;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Enrichment
{
    public class TurnoverCalculator
    {
        private const double MassResolution = .1;
        private const double MinAbundance = .01;
        private readonly IList<MzRange> _masses;
        private readonly Dictionary<String, TracerDef> _tracerDefs;
        private readonly ICollection<String> _traceeSymbols;
        private readonly int _maxTracerCount;
        private readonly AminoAcidFormulas _aminoAcidFormulas;
        private readonly bool _errOnSideOfLowerAbundance;
        private readonly Dictionary<TracerFormula, MassDistribution> _massDistributions 
            = new Dictionary<TracerFormula, MassDistribution>();
        public TurnoverCalculator(Workspace workspace, String sequence)
        {
            Sequence = sequence;
            _errOnSideOfLowerAbundance = workspace.GetErrOnSideOfLowerAbundance();
            _aminoAcidFormulas = workspace.GetAminoAcidFormulasWithTracers();
            _tracerDefs = new Dictionary<String, TracerDef>();
            _traceeSymbols = new HashSet<String>();
            foreach (var tracerDef in workspace.GetTracerDefs())
            {
                if (!_traceeSymbols.Contains(tracerDef.TraceeSymbol))
                {
                    if (tracerDef.GetMaximumTracerCount(sequence) == 0)
                    {
                        continue;
                    }
                    _traceeSymbols.Add(tracerDef.TraceeSymbol);
                }
                _tracerDefs.Add(tracerDef.Name, tracerDef);
            }
            _maxTracerCount = workspace.GetMaxTracerCount(sequence);
            _masses = new ReadOnlyCollection<MzRange>(GetMasses());
        }

        private List<MzRange> GetMasses()
        {
            var massesAndFrequency = new Dictionary<double, double>();
            var tracerFormulaEnumerator = new TracerFormulaEnumerator(Sequence, _tracerDefs.Values);
            while (tracerFormulaEnumerator.MoveNext())
            {
                MassDistribution massDistribution = _aminoAcidFormulas.GetMassDistribution(
                    MoleculeFromTracerFormula(tracerFormulaEnumerator.Current), 0);
                massDistribution = massDistribution.OffsetAndDivide(_aminoAcidFormulas.GetMassShift(Sequence), 1);
                foreach (var entry in massDistribution)
                {
                    if (entry.Value < MinAbundance)
                    {
                        continue;
                    }
                    double currentAbundance;
                    massesAndFrequency.TryGetValue(entry.Key, out currentAbundance);
                    if (currentAbundance >= entry.Value)
                    {
                        continue;
                    }
                    massesAndFrequency[entry.Key] = entry.Value;
                }
            }
            var allMasses = new List<double>(massesAndFrequency.Keys);
            allMasses.Sort();
            var result = new List<MzRange>();
            foreach (var mass in allMasses)
            {
                if (result.Count == 0)
                {
                    result.Add(new MzRange(mass));
                    continue;
                }
                var lastMass = result[result.Count - 1];
                if (lastMass.Distance(mass) > MassResolution)
                {
                    result.Add(new MzRange(mass));
                    continue;
                }
                result[result.Count - 1] = lastMass.Union(mass);
            }
            return result;
        }

        public Molecule MoleculeFromTracerFormula(TracerFormula tracerFormula)
        {
            Molecule result = _aminoAcidFormulas.GetFormula(Sequence);
            foreach (var entry in tracerFormula)
            {
                var tracerDef = _tracerDefs[entry.Key];
                result = result.SetElementCount(
                    tracerDef.TraceeSymbol, result.GetElementCount(tracerDef.TraceeSymbol) - entry.Value);
                result = result.SetElementCount(tracerDef.Name, entry.Value);
            }
            return result;
        }

        public AminoAcidFormulas GetAminoAcidFormulas(TracerPercentFormula tracerPercents)
        {
            var result = _aminoAcidFormulas;
            foreach (var entry in tracerPercents)
            {
                var tracerDef = _tracerDefs[entry.Key];
                var isotopeAbundances = result.IsotopeAbundances;
                var massDistribution = isotopeAbundances[tracerDef.TraceeSymbol];
                massDistribution = massDistribution.Average(tracerDef.TracerMasses, entry.Value/100.0);
                isotopeAbundances = isotopeAbundances.SetAbundances(tracerDef.TraceeSymbol, massDistribution);
                result = result.SetIsotopeAbundances(isotopeAbundances);
            }
            return result;
        }

        public IList<MzRange> GetMzs(int charge)
        {
            if (charge == 0)
            {
                return _masses;
            }
            var result = new List<MzRange>();
            foreach (var mass in _masses)
            {
                result.Add(new MzRange(mass.Min/charge + AminoAcidFormulas.ProtonMass,
                            mass.Max/charge + AminoAcidFormulas.ProtonMass));
            }
            return result;
        }

        public static double Score(Vector observedVector, Vector prediction)
        {
            return observedVector.Normalize() * prediction.Normalize();
        }

        public static double Score(Vector observedVector, Vector prediction, Func<int,bool> excludeFunc)
        {
            return Score(FilterVector(observedVector, excludeFunc), FilterVector(prediction, excludeFunc));
        }

        /// <summary>
        /// Find the combination of linear combinations of the candidate vectors which results in 
        /// the least squares match of the target vectors.
        /// This uses the algorithm outlined in:
        /// <para>
        /// Least Squares Analysis and Simplification of Multi-Isotope Mass Spectra.
        /// J. I. Brauman
        /// Analytical Chemistry 1966 38 (4), 607-610
        /// </para>
        /// </summary>
        internal static Vector FindBestCombination(Vector targetVector, params Vector[] candidateVectors)
        {
            Matrix matrixA = new Matrix(targetVector.Length, candidateVectors.Length);
            for (int j = 0; j < candidateVectors.Length; j++)
            {
                matrixA.SetColumnVector(candidateVectors[j], j);
            }
            Matrix matrixP = targetVector.ToColumnMatrix();
            Matrix matrixAT = Matrix.Transpose(matrixA);
            var matrixATA = matrixAT.Multiply(matrixA);
            if (matrixATA.Determinant() == 0)
            {
                return null;
            }
            var matrixATAInv = matrixATA.Inverse();
            Matrix matrixResult = matrixATAInv.Multiply(matrixAT).Multiply(matrixP);
            return matrixResult.GetColumnVector(0);
        }

        internal Vector FindBestCombination(Vector targetVector, Vector[] candidateVectors, bool errOnSideOfLowerAbundance)
        {
            var result = FindBestCombination(targetVector, candidateVectors);
            if (!errOnSideOfLowerAbundance)
            {
                return result;
            }
            var newTargetVector = new Vector(targetVector.Length);
            for (int i = 0; i < newTargetVector.Length; i++)
            {
                var totalCandidate = 0.0;
                for (int iVector = 0; iVector < candidateVectors.Length; iVector++)
                {
                    totalCandidate += candidateVectors[iVector][i]*result[iVector];
                }
                newTargetVector[i] = Math.Min(totalCandidate, targetVector[i]);
            }
            return FindBestCombination(newTargetVector, candidateVectors);
        }

        Vector FindBestCombinationFilterNegatives(Vector observedIntensities, IList<Vector> candidates, Func<int,bool> excludeFunc)
        {
            Vector[] filteredCandidates = new Vector[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                filteredCandidates[i] = FilterVector(candidates[i], excludeFunc);
            }
            return FindBestCombinationFilterNegatives(FilterVector(observedIntensities, excludeFunc), filteredCandidates);
        }

        Vector FindBestCombinationFilterNegatives(Vector observedIntensities, IList<Vector> candidates)
        {
            List<int> remaining = new List<int>();
            for (int i = 0; i < candidates.Count(); i++)
            {
                remaining.Add(i);
            }
            Vector filteredResult;
            while(true)
            {
                Vector[] curCandidates = new Vector[remaining.Count];
                for (int i = 0; i < remaining.Count; i++)
                {
                    curCandidates[i] = candidates[remaining[i]];
                }
                filteredResult = FindBestCombination(observedIntensities, curCandidates, _errOnSideOfLowerAbundance);
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
            Vector result = new Vector(candidates.Count);
            for (int i = 0; i < remaining.Count; i++)
            {
                result[remaining[i]] = filteredResult[i];
            }
            return result;
        }

        private static Vector FilterVector(IList<double> list, Func<int,bool> isExcludedFunc)
        {
            List<double> filtered = new List<double>();
            for (int i = 0; i < list.Count; i++)
            {
                if (isExcludedFunc(i))
                {
                    continue;
                }
                filtered.Add(list[i]);
            }
            return new Vector(filtered.ToArray());
        }

//        public void GetEnrichmentAmounts(
//            PeptideDistribution precursorEnrichments,
//            IList<double> observedIntensities, 
//            int intermediateLevels,
//            out IDictionary<TracerPercentFormula, IList<double>> predictedIntensities)
//        {
//            var excludeFunc = ExcludeNaNs(observedIntensities);
//            var observedVector = new Vector(observedIntensities.ToArray());
//            var tracerPercents = new List<TracerPercentFormula>();
//            var tracerPercentEnumerator = new TracerPercentEnumerator(_tracerDefs.Values, intermediateLevels);
//            while (tracerPercentEnumerator.MoveNext())
//            {
//                tracerPercents.Add(tracerPercentEnumerator.Current);
//            }
//
//            Vector[] vectors = new Vector[tracerPercents.Count];
//            for (int i = 0; i < tracerPercents.Count; i++)
//            {
//                var aminoAcidFormulas = GetAminoAcidFormulas(tracerPercents[i]);
//                var massDistribution = aminoAcidFormulas.GetMassDistribution(Sequence, 0);
//                vectors[i] = IntensityDictionaryToVector(massDistribution, _masses);
//            }
//            Vector amounts = FindBestCombinationFilterNegatives(observedVector, vectors, excludeFunc);
//            predictedIntensities = new Dictionary<TracerPercentFormula, IList<double>>();
//            if (amounts == null)
//            {
//                amounts = new Vector(vectors.Count());
//            }
//            var totalPrediction = new Vector(observedIntensities.Count);
//            for (int i = 0; i < amounts.Count(); i++)
//            {
//                var tracerFormula = tracerPercents[i].ToString();
//                Vector scaledVector = vectors[i].Scale(amounts[i]);
//                predictedIntensities.Add(tracerPercents[i], scaledVector);
//                totalPrediction = totalPrediction.Add(scaledVector);
//                var precursorEnrichment = new DbPeptideAmount
//                                              {
//                                                  TracerFormula = tracerFormula,
//                                                  TracerPercent = GetTracerPercent(tracerPercents[i]),
//                                                  PercentAmountValue = 100*amounts[i]/amounts.Sum(),
//                                              };
//                precursorEnrichments.AddChild(tracerFormula, precursorEnrichment);
//            }
//            precursorEnrichments.Score = Score(observedVector, totalPrediction, excludeFunc);
//        }

        public double GetTracerPercent(TracerFormula tracerFormula)
        {
            if (_maxTracerCount == 0)
            {
                return 0;
            }
            return 100.0*tracerFormula.Values.Sum()/_maxTracerCount;
        }

        public double GetTracerPercent(TracerPercentFormula tracerPercentFormula)
        {
            if (_traceeSymbols.Count == 0)
            {
                return 0;
            }
            return (double) tracerPercentFormula.Values.Sum()/_traceeSymbols.Count;
        }

        private MassDistribution GetMassDistribution(TracerFormula tracerFormula)
        {
            lock(_massDistributions)
            {
                MassDistribution massDistribution;
                if (_massDistributions.TryGetValue(tracerFormula, out massDistribution))
                {
                    return massDistribution;
                }
                var molecule = MoleculeFromTracerFormula(tracerFormula);
                massDistribution = _aminoAcidFormulas.GetMassDistribution(molecule, 0);
                _massDistributions.Add(tracerFormula, massDistribution);
                return massDistribution;
            }
        }

        public IList<double[]> GetMassDistributions(Func<int,bool> excludeFunc)
        {
            var massDistributions = new List<double[]>();
            foreach (var tracerFormula in ListTracerFormulas())
            {
                var massDistribution = GetMassDistribution(tracerFormula);
                massDistribution = massDistribution.OffsetAndDivide(_aminoAcidFormulas.GetMassShift(Sequence), 1);
                massDistributions.Add(FilterVector(IntensityDictionaryToVector(massDistribution, _masses), excludeFunc).ToArray());
            }
            return massDistributions;
        }

        public double CalcTracerScore(double[] observedIntensities, IList<double[]> massDistributions)
        {
            Vector observedVector = new Vector(observedIntensities);
            var vectors = new Vector[massDistributions.Count];
            for (int i = 0; i < massDistributions.Count; i++)
            {
                vectors[i] = new Vector(massDistributions[i]);
            }
            Vector amounts = FindBestCombinationFilterNegatives(observedIntensities, vectors);
            if (amounts == null || amounts.Sum() <= 0)
            {
                return 0;
            }
            Vector totalPrediction = new Vector(observedIntensities.Length);
            for (int i = 0; i < massDistributions.Count; i++)
            {
                Vector scaledVector = vectors[i].Scale(amounts[i]);
                totalPrediction = totalPrediction.Add(scaledVector);
            }
            return Score(observedVector, totalPrediction);
        }
        
        public IList<IList<double>> GetTheoreticalIntensities(IList<TracerFormula> tracerFormulas)
        {
            var vectors = new List<IList<double>>();
            foreach (var tracerFormula in tracerFormulas)
            {
                var massDistribution = GetMassDistribution(tracerFormula);
                massDistribution = massDistribution.OffsetAndDivide(_aminoAcidFormulas.GetMassShift(Sequence), 1);
                vectors.Add(IntensityDictionaryToVector(massDistribution, _masses));
            }
            return vectors;
        }

        public void GetTracerAmounts(IList<double> observedIntensities, out double score, out IDictionary<TracerFormula, IList<double>> predictedIntensities)
        {
            var tracerFormulas = ListTracerFormulas();
            var vectors = GetTheoreticalIntensities(tracerFormulas);
            GetTracerAmounts(observedIntensities, out score, out predictedIntensities, tracerFormulas, vectors);
        }

        public IDictionary<TracerFormula, double> GetTracerAmounts(IList<double> observedIntensities, out double score, out IDictionary<TracerFormula, IList<double>> predictedIntensities, IList<TracerFormula> tracerFormulas, IList<IList<double>> theoreticalIntensities)
        {
            var result = new Dictionary<TracerFormula, double>();
            var vectors = new List<Vector>(theoreticalIntensities.Select(l => new Vector(l.ToArray())));
            var excludeFunc = ExcludeNaNs(observedIntensities);
            Vector observedVector = new Vector(observedIntensities.ToArray());
            Vector amounts = FindBestCombinationFilterNegatives(observedVector, vectors, excludeFunc);
            if (amounts == null)
            {
                amounts = new Vector(vectors.Count());
            }
            predictedIntensities = new Dictionary<TracerFormula, IList<double>>();
            Vector totalPrediction = new Vector(observedIntensities.Count);
            for (int i = 0; i < tracerFormulas.Count; i++)
            {
                Vector scaledVector = vectors[i].Scale(amounts[i]);
                predictedIntensities.Add(tracerFormulas[i], scaledVector);
                totalPrediction = totalPrediction.Add(scaledVector);
                result.Add(tracerFormulas[i], amounts[i]);
            }
            score = Score(observedVector, totalPrediction, excludeFunc);
            return result;
        }

        internal Vector IntensityDictionaryToVector(IDictionary<double, double> dict, IList<MzRange> mzs)
        {
            var result = new Vector(mzs.Count);
            for (int iMz = 0; iMz < mzs.Count; iMz++)
            {
                var mzRange = mzs[iMz];
                double total = 0;
                foreach (var entry in dict)
                {
                    if (mzRange.Distance(entry.Key) < MassResolution)
                    {
                        total += entry.Value;
                    }
                }
                result[iMz] = total;
            }
            return result;
        }

        public int MassCount { get { return _masses.Count;}}
        public String Sequence { get; private set; }

        public static Func<int,bool> ExcludeNaNs(IList<double> values)
        {
            return i => double.IsNaN(values[i]);
        }

        public List<TracerFormula> ListTracerFormulas()
        {
            var result = new List<TracerFormula>();
            var enumerator = new TracerFormulaEnumerator(Sequence, _tracerDefs.Values);
            while (enumerator.MoveNext())
            {
                result.Add(enumerator.Current);
            }
            return result;
        }
        public List<TracerPercentFormula> ListTracerPercents(int intermediateLevels)
        {
            var result = new List<TracerPercentFormula>();
            var enumerator = new TracerPercentEnumerator(_tracerDefs.Values, intermediateLevels);
            while (enumerator.MoveNext())
            {
                result.Add(enumerator.Current);
            }
            return result;
        }

        public TracerPercentFormula ComputePrecursorEnrichmentAndTurnover(IDictionary<TracerFormula, double> peptideDistribution, out double turnover, out double turnoverScore, out IDictionary<TracerFormula, double> bestMatch)
        {
            if (peptideDistribution.Count < 3)
            {
                turnover = 0;
                turnoverScore = 0;
                bestMatch = null;
                return null;
            }
            var tracerFormulaIndexes = new Dictionary<TracerFormula, int>();
            var observedPercentages = new Vector(peptideDistribution.Count);
            foreach (var entry in peptideDistribution)
            {
                var tracerFormula = entry.Key;
                observedPercentages[tracerFormulaIndexes.Count] = entry.Value;
                tracerFormulaIndexes.Add(tracerFormula, tracerFormulaIndexes.Count);
            }
            var initialVector = DistributionToVector(GetDistribution(GetInitialTracerPercents()), tracerFormulaIndexes);
            bestMatch = null;
            TracerPercentFormula bestFormula = null;
            double bestScore = 0;
            double bestTurnover = 0;
            var tracerPercentEnumerator = new TracerPercentEnumerator(_tracerDefs.Values);
            while (tracerPercentEnumerator.MoveNext())
            {
                var tracerPercents = tracerPercentEnumerator.Current;
                var distribution = GetDistribution(tracerPercents);
                var newlySynthesizedVector = DistributionToVector(distribution, tracerFormulaIndexes);
                var combination = FindBestCombination(observedPercentages, initialVector, newlySynthesizedVector);
                if (combination == null)
                {
                    continue;
                }
                if (combination[0] < 0 || combination[1] < 0 || combination.Sum() <= 0)
                {
                    continue;
                }
                if (combination.Sum() <= 0)
                {
                    continue;
                }
                var resultVector = initialVector.Scale(combination[0]).Add(newlySynthesizedVector.Scale(combination[1]));
                var score = Score(observedPercentages, resultVector);
                if (bestFormula == null || score > bestScore)
                {
                    bestFormula = tracerPercents;
                    bestScore = score;
                    bestTurnover = combination[1]/(combination[0] + combination[1]);
                    bestMatch = tracerFormulaIndexes.ToDictionary(kv => kv.Key, kv => resultVector[kv.Value]);
                }
            }
            turnover = bestTurnover;
            turnoverScore = bestScore;
            return bestFormula;
        }

        private TracerPercentFormula GetInitialTracerPercents()
        {
            var result = TracerPercentFormula.Empty;
            foreach (var tracerDef in _tracerDefs.Values)
            {
                result = result.SetElementCount(tracerDef.Name, tracerDef.InitialApe);
            }
            return result;
        }

        private static Vector DistributionToVector(IDictionary<TracerFormula, double> distribution, IDictionary<TracerFormula, int> indexes)
        {
            var result = new Vector(indexes.Count, double.NaN);
            foreach (var entry in distribution)
            {
                int index;
                if (indexes.TryGetValue(entry.Key, out index))
                {
                    result[indexes[entry.Key]] = entry.Value;
                }
            }
            return result;
        }
        
        public Dictionary<TracerFormula, double> GetDistribution(TracerPercentFormula tracerPercents)
        {
            var tracerFormulaEnumerator = new TracerFormulaEnumerator(Sequence, _tracerDefs.Values);
            var result = new Dictionary<TracerFormula, double>();
            while (tracerFormulaEnumerator.MoveNext())
            {
                result.Add(tracerFormulaEnumerator.Current, ComputeProbability(tracerFormulaEnumerator, tracerPercents));
            }
            return result;
        }

        private double ComputeProbability(TracerFormulaEnumerator tracerFormulaEnumerator, TracerPercentFormula tracerPercents)
        {
            var tracerFormula = tracerFormulaEnumerator.Current;
            var tracerSymbolCounts = tracerFormulaEnumerator.GetTracerSymbolCounts();

            var traceePercents = tracerSymbolCounts.Keys.ToDictionary(s=>s, s => 1.0);
            var result = 1.0;
            foreach (var tracerDef in _tracerDefs.Values)
            {
                int tracerTraceeCount = tracerSymbolCounts[tracerDef.TraceeSymbol];
                int tracerCount = tracerFormula.GetElementCount(tracerDef.Name);
                double probability = Math.Pow(tracerPercents.GetElementCount(tracerDef.Name) / 100, tracerCount)
                                     *Choose(tracerTraceeCount, tracerCount);
                result *= probability;
                tracerSymbolCounts[tracerDef.TraceeSymbol] = tracerTraceeCount - tracerCount;
                traceePercents[tracerDef.TraceeSymbol] = traceePercents[tracerDef.TraceeSymbol] -
                                                         tracerPercents.GetElementCount(tracerDef.Name)/100;
            }
            foreach (var entry in tracerSymbolCounts)
            {
                result *= Math.Pow(traceePercents[entry.Key], entry.Value);
            }
            return result;
        }

        public void ComputeTurnover(double precursorEnrichment, IDictionary<TracerFormula, double> peaks, out double? turnover, out double? turnoverScore)
        {
            if (peaks == null || _tracerDefs.Count != 1)
            {
                turnover = null;
                turnoverScore = null;
                return;
            }
            var tracerFormulaIndexes = new Dictionary<TracerFormula, int>();
            var observedPercentages = new Vector(peaks.Count);
            foreach (var entry in peaks)
            {
                var tracerFormula = entry.Key;
                observedPercentages[tracerFormulaIndexes.Count] = entry.Value;
                tracerFormulaIndexes.Add(tracerFormula, tracerFormulaIndexes.Count);
            }

            var initialVector = DistributionToVector(GetDistribution(GetInitialTracerPercents()), tracerFormulaIndexes);
            var tracerPercentFormula = TracerPercentFormula.Empty.SetElementCount(_tracerDefs.First().Key, precursorEnrichment);
            var distribution = GetDistribution(tracerPercentFormula);
            var newlySynthesizedVector = DistributionToVector(distribution, tracerFormulaIndexes);
            var combination = FindBestCombination(observedPercentages, initialVector, newlySynthesizedVector);
            if (combination == null || combination[0] <= 0 && combination[1] <= 0)
            {
                turnover = null;
                turnoverScore = null;
            }
            else if (combination[0] <= 0)
            {
                var combination2 = FindBestCombination(observedPercentages, newlySynthesizedVector);
                turnover = 1;
                turnoverScore = Score(observedPercentages, newlySynthesizedVector.Scale(combination2[0]));
            }
            else if (combination[1] <= 0)
            {
                var combination2 = FindBestCombination(observedPercentages, initialVector);
                turnover = 0;
                turnoverScore = Score(observedPercentages, initialVector.Scale(combination2[0]));
            }
            else
            {
                turnover = combination[1]/(combination[0] + combination[1]);
                turnoverScore = Score(observedPercentages,
                                      initialVector.Scale(combination[0]).Add(
                                          newlySynthesizedVector.Scale(combination[1])));
            }
        }

        public double ExpectedUnlabeledFraction(double precursorPool)
        {
            return Math.Pow(1 - precursorPool/100, _maxTracerCount);
        }

        static double Choose(int n, int c)
        {
            return Fn.Factorial(n)/Fn.Factorial(c)/Fn.Factorial(n - c);
        }
    }
}
