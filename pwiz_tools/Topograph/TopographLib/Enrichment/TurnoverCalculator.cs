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
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Topograph.Model;

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

        private readonly IList<TracerFormula> _tracerFormulae;
        private readonly IDictionary<TracerFormula, int> _tracerFormulaIndexes;
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
            var tracerFormulae = new List<TracerFormula>();
            _tracerFormulaIndexes = new Dictionary<TracerFormula, int>();
            var enumerator = new TracerFormulaEnumerator(Sequence, _tracerDefs.Values);
            while (enumerator.MoveNext())
            {
                _tracerFormulaIndexes.Add(enumerator.Current, tracerFormulae.Count);
                tracerFormulae.Add(enumerator.Current);
            }
            _tracerFormulae = ImmutableList.ValueOf(tracerFormulae);
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

        public static double Score(Vector<double> observedVector, Vector<double> prediction)
        {
            return observedVector.Normalize(2.0) * prediction.Normalize(2.0);
        }

        public static double Score(Vector<double> observedVector, Vector<double> prediction, Func<int, bool> excludeFunc)
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
        // ReSharper disable InconsistentNaming
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
        // ReSharper restore InconsistentNaming

        internal Vector<double> FindBestCombination(Vector<double> targetVector, Vector<double>[] candidateVectors, bool errOnSideOfLowerAbundance)
        {
            var result = FindBestCombination(targetVector, candidateVectors);
            if (!errOnSideOfLowerAbundance)
            {
                return result;
            }
            var newTargetVector = new DenseVector(targetVector.Count);
            for (int i = 0; i < newTargetVector.Count; i++)
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

        Vector<double> FindBestCombinationFilterNegatives(Vector<double> observedIntensities, IList<Vector<double>> candidates, Func<int, bool> excludeFunc)
        {
            Vector<double>[] filteredCandidates = new Vector<double>[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                filteredCandidates[i] = FilterVector(candidates[i], excludeFunc);
            }
            return FindBestCombinationFilterNegatives(FilterVector(observedIntensities, excludeFunc), filteredCandidates);
        }

        Vector<double> FindBestCombinationFilterNegatives(Vector<double> observedIntensities, IList<Vector<double>> candidates)
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
            Vector<double> result = new DenseVector(candidates.Count);
            for (int i = 0; i < remaining.Count; i++)
            {
                result[remaining[i]] = filteredResult[i];
            }
            return result;
        }

        private static Vector<double> FilterVector(IList<double> list, Func<int, bool> isExcludedFunc)
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
            return new DenseVector(filtered.ToArray());
        }

        public double GetTracerPercent(TracerFormula tracerFormula)
        {
            if (_maxTracerCount == 0)
            {
                return 0;
            }
            return 100.0*tracerFormula.Dictionary.Values.Sum()/_maxTracerCount;
        }

        public double GetTracerPercent(TracerPercentFormula tracerPercentFormula)
        {
            if (_traceeSymbols.Count == 0)
            {
                return 0;
            }
            return tracerPercentFormula.Dictionary.Values.Sum()/_traceeSymbols.Count;
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
            Vector<double> observedVector = new DenseVector(observedIntensities);
            var vectors = new Vector<double>[massDistributions.Count];
            for (int i = 0; i < massDistributions.Count; i++)
            {
                vectors[i] = new DenseVector(massDistributions[i]);
            }
            Vector<double> amounts = FindBestCombinationFilterNegatives(observedVector, vectors);
            if (amounts == null || amounts.Sum() <= 0)
            {
                return 0;
            }
            Vector<double> totalPrediction = new DenseVector(observedIntensities.Length);
            for (int i = 0; i < massDistributions.Count; i++)
            {
                Vector<double> scaledVector = vectors[i].Multiply(amounts[i]);
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
            var vectors = new List<Vector<double>>(theoreticalIntensities.Select(l => new DenseVector(l.ToArray())));
            var excludeFunc = ExcludeNaNs(observedIntensities);
            Vector<double> observedVector = new DenseVector(observedIntensities.ToArray());
            Vector<double> amounts = FindBestCombinationFilterNegatives(observedVector, vectors, excludeFunc);
            if (amounts == null)
            {
                amounts = new DenseVector(vectors.Count());
            }
            predictedIntensities = new Dictionary<TracerFormula, IList<double>>();
            Vector<double> totalPrediction = new DenseVector(observedIntensities.Count);
            for (int i = 0; i < tracerFormulas.Count; i++)
            {
                Vector<double> scaledVector = vectors[i].Multiply(amounts[i]);
                predictedIntensities.Add(tracerFormulas[i], scaledVector);
                totalPrediction = totalPrediction.Add(scaledVector);
                result.Add(tracerFormulas[i], amounts[i]);
            }
            score = Score(observedVector, totalPrediction, excludeFunc);
            return result;
        }

        internal Vector IntensityDictionaryToVector(IDictionary<double, double> dict, IList<MzRange> mzs)
        {
            var result = new DenseVector(mzs.Count);
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

        public IList<TracerFormula> ListTracerFormulas()
        {
            return _tracerFormulae;
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
            var observedPercentages = new DenseVector(peptideDistribution.Count);
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
                var resultVector = initialVector.Multiply(combination[0]).Add(newlySynthesizedVector.Multiply(combination[1]));
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

        private static Vector<double> DistributionToVector(IDictionary<TracerFormula, double> distribution, IDictionary<TracerFormula, int> indexes)
        {
            var result = DenseVector.Create(indexes.Count, double.NaN);
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
            var observedPercentages = new DenseVector(peaks.Count);
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
                turnoverScore = Score(observedPercentages, newlySynthesizedVector.Multiply(combination2[0]));
            }
            else if (combination[1] <= 0)
            {
                var combination2 = FindBestCombination(observedPercentages, initialVector);
                turnover = 0;
                turnoverScore = Score(observedPercentages, initialVector.Multiply(combination2[0]));
            }
            else
            {
                turnover = combination[1]/(combination[0] + combination[1]);
                turnoverScore = Score(observedPercentages,
                                      initialVector.Multiply(combination[0]).Add(
                                          newlySynthesizedVector.Multiply(combination[1])));
            }
        }

        public IDictionary<TracerFormula, T> ToTracerFormulaDict<T>(IList<T> values)
        {
            var tracerFormulae = ListTracerFormulas();
            if (tracerFormulae.Count != values.Count)
            {
                return null;
            }
            var dict = new Dictionary<TracerFormula, T>();
            for (int i = 0; i < values.Count; i++)
            {
                dict.Add(tracerFormulae[i], values[i]);
            }
            return dict;
        }

        public double ExpectedUnlabeledFraction(double precursorPool)
        {
            return Math.Pow(1 - precursorPool/100, _maxTracerCount);
        }

        static double Choose(int n, int c)
        {
            return SpecialFunctions.Factorial(n) / SpecialFunctions.Factorial(c) / SpecialFunctions.Factorial(n - c);
        }
    }
}
