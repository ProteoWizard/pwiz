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
using MathNet.Numerics.LinearAlgebra;
using pwiz.Common.Chemistry;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Enrichment
{
    public class TurnoverCalculator
    {
        private const double MassResolution = .1;
        private const double MinAbundance = .01;
        private readonly IList<double> _masses;
        private readonly Dictionary<String, TracerDef> _tracerDefs;
        private readonly ICollection<String> _traceeSymbols;
        private readonly int _maxTracerCount;
        private readonly AminoAcidFormulas _aminoAcidFormulas;
        public TurnoverCalculator(Workspace workspace, String sequence)
        {
            Sequence = sequence;
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
            _masses = new ReadOnlyCollection<double>(GetMasses());
        }

        private List<double> GetMasses()
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
            var result = new List<double>();
            foreach (var mass in allMasses)
            {
                if (result.Count == 0)
                {
                    result.Add(mass);
                    continue;
                }
                double lastMass = result[result.Count - 1];
                if (mass - lastMass > MassResolution)
                {
                    result.Add(mass);
                    continue;
                }
                double lastAbundance = massesAndFrequency[lastMass];
                double abundance = massesAndFrequency[mass];
                if (abundance > lastAbundance)
                {
                    result[result.Count - 1] = mass;
                }
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

        public IList<double> GetMzs(int charge)
        {
            if (charge == 0)
            {
                return _masses;
            }
            var result = new List<double>();
            foreach (var mass in _masses)
            {
                result.Add(AminoAcidFormulas.ProtonMass + mass / charge);
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
            Matrix matrixATAInv;
            try
            {
                matrixATAInv = matrixAT.Multiply(matrixA).Inverse();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            Matrix matrixResult = matrixATAInv.Multiply(matrixAT).Multiply(matrixP);
            return matrixResult.GetColumnVector(0);
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

        public void GetEnrichmentAmounts(
            PeptideDistribution precursorEnrichments,
            IList<double> observedIntensities, 
            int intermediateLevels,
            out IDictionary<TracerPercentFormula, IList<double>> predictedIntensities)
        {
            var excludeFunc = ExcludeNaNs(observedIntensities);
            var observedVector = new Vector(observedIntensities.ToArray());
            var tracerPercents = new List<TracerPercentFormula>();
            var tracerPercentEnumerator = new TracerPercentEnumerator(_tracerDefs.Values, intermediateLevels);
            while (tracerPercentEnumerator.MoveNext())
            {
                tracerPercents.Add(tracerPercentEnumerator.Current);
            }

            Vector[] vectors = new Vector[tracerPercents.Count];
            for (int i = 0; i < tracerPercents.Count; i++)
            {
                var aminoAcidFormulas = GetAminoAcidFormulas(tracerPercents[i]);
                var massDistribution = aminoAcidFormulas.GetMassDistribution(Sequence, 0);
                vectors[i] = IntensityDictionaryToVector(massDistribution, _masses);
            }
            Vector amounts = FindBestCombinationFilterNegatives(observedVector, vectors, excludeFunc);
            precursorEnrichments.Clear();
            predictedIntensities = new Dictionary<TracerPercentFormula, IList<double>>();
            if (amounts == null)
            {
                amounts = new Vector(vectors.Count());
            }
            var totalPrediction = new Vector(observedIntensities.Count);
            for (int i = 0; i < amounts.Count(); i++)
            {
                var tracerFormula = tracerPercents[i].ToString();
                Vector scaledVector = vectors[i].Scale(amounts[i]);
                predictedIntensities.Add(tracerPercents[i], scaledVector);
                totalPrediction = totalPrediction.Add(scaledVector);
                var precursorEnrichment = new DbPeptideAmount
                                              {
                                                  TracerFormula = tracerFormula,
                                                  TracerPercent = GetTracerPercent(tracerPercents[i]),
                                                  PercentAmount = 100*amounts[i]/amounts.Sum(),
                                              };
                precursorEnrichments.AddChild(tracerFormula, precursorEnrichment);
            }
            precursorEnrichments.Score = Score(observedVector, totalPrediction, excludeFunc);
        }

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
        
        public void GetTracerAmounts(PeptideDistribution tracerAmounts, IList<double> observedIntensities, out IDictionary<TracerFormula, IList<double>> predictedIntensities)
        {
            var excludeFunc = ExcludeNaNs(observedIntensities);
            Vector observedVector = new Vector(observedIntensities.ToArray());
            var vectors = new List<Vector>();
            var tracerFormulas = ListTracerFormulas();
            foreach (var tracerFormula in tracerFormulas)
            {
                var molecule = MoleculeFromTracerFormula(tracerFormula);
                var massDistribution = _aminoAcidFormulas.GetMassDistribution(molecule, 0);
                massDistribution = massDistribution.OffsetAndDivide(_aminoAcidFormulas.GetMassShift(Sequence), 1);
                vectors.Add(IntensityDictionaryToVector(massDistribution, _masses));
            }
            Vector amounts = FindBestCombinationFilterNegatives(observedVector, vectors, excludeFunc);
            if (amounts == null)
            {
                amounts = new Vector(vectors.Count());
            }
            tracerAmounts.Clear();
            predictedIntensities = new Dictionary<TracerFormula, IList<double>>();
            Vector totalPrediction = new Vector(observedIntensities.Count);
            for (int i = 0; i < tracerFormulas.Count; i ++)
            {
                Vector scaledVector = vectors[i].Scale(amounts[i]);
                predictedIntensities.Add(tracerFormulas[i], scaledVector);
                totalPrediction = totalPrediction.Add(scaledVector);
                var tracerAmount
                    = new DbPeptideAmount
                          {
                              TracerFormula = tracerFormulas[i].ToString(),
                              TracerPercent = GetTracerPercent(tracerFormulas[i]),
                              PercentAmount = 100 * amounts[i] / amounts.Sum(),
                        };
                tracerAmounts.AddChild(tracerAmount.TracerFormula, tracerAmount);
            }
            tracerAmounts.Score = Score(observedVector, totalPrediction, excludeFunc);
        }

        internal Vector IntensityDictionaryToVector(IDictionary<double, double> dict, IList<double> mzs)
        {
            var result = new Vector(mzs.Count);
            for (int iMz = 0; iMz < mzs.Count; iMz++)
            {
                double mz = mzs[iMz];
                double total = 0;
                foreach (var entry in dict)
                {
                    if (Math.Abs(entry.Key - mz) < MassResolution)
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
    }
}
