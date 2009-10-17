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
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Enrichment
{
    public class TurnoverCalculator
    {
        private Dictionary<int,IList<double>> _chargeToMzs = new Dictionary<int, IList<double>>();
        private Dictionary<double, Vector> _apeToIntensities = new Dictionary<double, Vector>();
        private Dictionary<int, Vector> _tracerCountToIntensities = new Dictionary<int, Vector>();
        public TurnoverCalculator(EnrichmentDef enrichment, String sequence)
        {
            Enrichment = enrichment;
            Sequence = sequence;
            var chargedPeptide = new ChargedPeptide(sequence, 1);
            MaxTracerCount = Enrichment.GetMaximumTracerCount(chargedPeptide);
            MassCount = Enrichment.GetMassCount(chargedPeptide);
        }

        public EnrichmentDef Enrichment { get; private set; }
        public IList<double> GetMzs(int charge)
        {
            lock(this)
            {
                IList<double> result;
                if (_chargeToMzs.TryGetValue(charge, out result))
                {
                    return result;
                }
                result = Enrichment.GetMzs(new ChargedPeptide(Sequence, charge));
                _chargeToMzs.Add(charge, new ReadOnlyCollection<double>(result.ToArray()));
                return result;
            }
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

        Vector FindBestCombinationFilterNegatives(Vector observedIntensities, Vector[] candidates, Func<int,bool> excludeFunc)
        {
            Vector[] filteredCandidates = new Vector[candidates.Length];
            for (int i = 0; i < candidates.Length; i++)
            {
                filteredCandidates[i] = FilterVector(candidates[i], excludeFunc);
            }
            return FindBestCombinationFilterNegatives(FilterVector(observedIntensities, excludeFunc), filteredCandidates);
        }

        Vector FindBestCombinationFilterNegatives(Vector observedIntensities, params Vector[] candidates)
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
            Vector result = new Vector(candidates.Length);
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
            IList<double> apesToTry, 
            out IList<IList<double>> predictedIntensities)
        {
            var excludeFunc = ExcludeNaNs(observedIntensities);
            Vector observedVector = new Vector(observedIntensities.ToArray());
            Vector[] vectors = new Vector[apesToTry.Count];
            for (int i = 0; i < apesToTry.Count; i++)
            {
                vectors[i] = GetIntensitiesFromApe(apesToTry[i]);
            }
            Vector amounts = FindBestCombinationFilterNegatives(observedVector, vectors, excludeFunc);
            precursorEnrichments.Clear();
            predictedIntensities = new List<IList<double>>();
            if (amounts == null)
            {
                amounts = new Vector(vectors.Count());
            }
            Vector totalPrediction = new Vector(observedIntensities.Count);
            for (int i = 0; i < amounts.Count(); i++)
            {
                Vector scaledVector = vectors[i].Scale(amounts[i]);
                predictedIntensities.Add(scaledVector);
                totalPrediction = totalPrediction.Add(scaledVector);
                var precursorEnrichment = new DbPeptideAmount
                                              {
                                                  EnrichmentIndex = i,
                                                  EnrichmentValue = apesToTry[i],
                                                  PercentAmount = 100*amounts[i]/amounts.Sum(),
                                              };
                precursorEnrichments.AddChild(i, precursorEnrichment);
            }
            precursorEnrichments.Score = Score(observedVector, totalPrediction, excludeFunc);
        }

        public void GetTracerAmounts(PeptideDistribution tracerAmounts, IList<double> observedIntensities, out IList<IList<double>> predictedIntensities)
        {
            var excludeFunc = ExcludeNaNs(observedIntensities);
            Vector observedVector = new Vector(observedIntensities.ToArray());
            Vector[] vectors = new Vector[MaxTracerCount + 1];
            for (int i = 0; i <= MaxTracerCount; i++)
            {
                vectors[i] = GetIntensitiesFromTracerCount(i);
            }
            Vector amounts = FindBestCombinationFilterNegatives(observedVector, vectors, excludeFunc);
            if (amounts == null)
            {
                amounts = new Vector(vectors.Count());
            }
            tracerAmounts.Clear();
            predictedIntensities = new List<IList<double>>();
            Vector totalPrediction = new Vector(observedIntensities.Count);
            for (int i = 0; i < amounts.Count(); i++)
            {
                Vector scaledVector = vectors[i].Scale(amounts[i]);
                predictedIntensities.Add(scaledVector);
                totalPrediction = totalPrediction.Add(scaledVector);
                var tracerAmount 
                    = new DbPeptideAmount()
                        {
                            EnrichmentIndex = i,
                            EnrichmentValue = 100.0*i/MaxTracerCount,
                            PercentAmount = 100 * amounts[i] / amounts.Sum(),
                        };
                tracerAmounts.AddChild(i, tracerAmount);
            }
            tracerAmounts.Score = Score(observedVector, totalPrediction, excludeFunc);
        }

        internal Vector GetIntensitiesFromApe(double ape)
        {
            lock (this)
            {
                Vector result;
                if (_apeToIntensities.TryGetValue(ape, out result))
                {
                    return result;
                }
                var res = Enrichment.GetResidueComposition(ape);
                var chargedPeptide = new ChargedPeptide(Sequence, 1);
                var dict = chargedPeptide.GetMassDistribution(res);
                result = IntensityDictionaryToVector(dict, GetMzs(1));
                _apeToIntensities.Add(ape, result);
                return result;
            }
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
                    if (Math.Abs(entry.Key - mz) * 50000 < mz)
                    {
                        total += entry.Value;
                    }
                }
                result[iMz] = total;
            }
            return result;
        }
        internal Vector GetIntensitiesFromTracerCount(int tracerCount)
        {
            lock(this)
            {
                Vector result;
                if (_tracerCountToIntensities.TryGetValue(tracerCount, out result))
                {
                    return result;
                }
                var chargedPeptide = new ChargedPeptide(Sequence, 1);
                var dict = Enrichment.GetEnrichedSpectrum(chargedPeptide, tracerCount);
                result = IntensityDictionaryToVector(dict, GetMzs(1));
                _tracerCountToIntensities.Add(tracerCount, result);
                return result;
            }
        }


        public int MassCount { get; private set; }
        public String Sequence { get; private set; }
        public int MaxTracerCount { get; private set; }

        public static Func<int,bool> ExcludeNaNs(IList<double> values)
        {
            return i => double.IsNaN(values[i]);
        }
    }
}
