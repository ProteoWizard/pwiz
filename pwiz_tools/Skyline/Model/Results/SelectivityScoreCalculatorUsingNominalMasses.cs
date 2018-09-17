/*
 * Original author: Sangtae Kim <sangtae.kim .at. gmail.com >,
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public class SelectivityScoreCalculator
    {
        private const int MAX_MASS = 6000;
        private static readonly double MassWater = BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula("H2O"); // Not L10N
        private static double[] _massProbabilities;
        private static readonly double SCALING_COEFFICIENT = SequenceMassCalc.MASS_PEPTIDE_INTERVAL;
        private static int MAX_ERROR = 4;

        public static float CalcScore(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            InitFrequencies();

            var max = 0.0f;
            foreach (var precursor in MQuestHelpers.GetAnalyteGroups(summaryPeakData))
            {
                var transitionData = MQuestHelpers.GetDefaultIonTypes(new[] {precursor});
                _massProbabilities = _massProbabilities ?? GetMassProbabilities(context.Document.Settings
                                              .PeptideSettings.Modifications.StaticModifications
                                              .Concat(context.Document.Settings.PeptideSettings.Modifications
                                                  .AllHeavyModifications).ToList());
                
                var precursorMass = precursor.NodeGroup.GetPrecursorIonMass(true);
                var intPrecursorMass = GetIntMass(precursorMass);

                var masses = new List<int>(transitionData.Count);
                foreach (var tr in transitionData)
                {
                    if (!tr.PeakData.IsForcedIntegration)
                    {
                        var m = GetMass(precursorMass, tr);
                        if (m.HasValue)
                            masses.Add(m.Value);
                    }
                }
                masses.Add(intPrecursorMass);
                masses.Sort();
                
                var err = MAX_ERROR;
                var result = 1.0;
                for (var i = 1; i < masses.Count; ++i)
                {
                    var index = masses[i] - masses[i - 1];
                    if (index < 0 || index >= _massProbabilities.Length)
                        continue;
                    var prob = _massProbabilities[index];
                    if (prob == 0.0)
                    {
                        for (var error = 1; error <= err; ++error)
                        {
                            double prevProb = 0.0, nextProb = 0.0;
                            if (index + error < _massProbabilities.Length)
                                nextProb = _massProbabilities[index + error];
                            if (index - error >= 0)
                                prevProb = _massProbabilities[index - error];

                            if (prevProb != 0.0 && nextProb != 0.0)
                                prob = (prevProb + nextProb) / 2.0;
                            else
                                prob = Math.Max(prevProb, nextProb);

                            if (prob != 0.0)
                                break;
                        }
                    }

                    result *= prob;
                }

                max = result == 0.0 ? 0.0f : Math.Max(max, (float) -Math.Log10(result));
            }

            return max;
        }

        private static double[] _aminoAcidFrequencies;

        private static void InitFrequencies()
        {
            if (_aminoAcidFrequencies != null)
                return;

            _aminoAcidFrequencies = new double[128];
            _aminoAcidFrequencies['A'] = _aminoAcidFrequencies['a'] = 0.0797658626927104;
            _aminoAcidFrequencies['C'] = _aminoAcidFrequencies['c'] = 0.0120557121294892;
            _aminoAcidFrequencies['D'] = _aminoAcidFrequencies['d'] = 0.0656024017154619;
            _aminoAcidFrequencies['E'] = _aminoAcidFrequencies['e'] = 0.0857244281174666;
            _aminoAcidFrequencies['F'] = _aminoAcidFrequencies['f'] = 0.035556163561998;
            _aminoAcidFrequencies['G'] = _aminoAcidFrequencies['g'] = 0.0669013096912933;
            _aminoAcidFrequencies['H'] = _aminoAcidFrequencies['h'] = 0.0247560297432987;
            _aminoAcidFrequencies['I'] = _aminoAcidFrequencies['i'] = 0.0510622304208986;
            _aminoAcidFrequencies['K'] = _aminoAcidFrequencies['k'] = 0.0501569104647708;
            _aminoAcidFrequencies['L'] = _aminoAcidFrequencies['l'] = 0.0963818686341526;
            _aminoAcidFrequencies['M'] = _aminoAcidFrequencies['m'] = 0.0224935583947329;
            _aminoAcidFrequencies['N'] = _aminoAcidFrequencies['n'] = 0.0401972248515637;
            _aminoAcidFrequencies['P'] = _aminoAcidFrequencies['p'] = 0.0590429900856694;
            _aminoAcidFrequencies['Q'] = _aminoAcidFrequencies['q'] = 0.0480579380222537;
            _aminoAcidFrequencies['R'] = _aminoAcidFrequencies['r'] = 0.0356355808234195;
            _aminoAcidFrequencies['S'] = _aminoAcidFrequencies['s'] = 0.0686274997181424;
            _aminoAcidFrequencies['T'] = _aminoAcidFrequencies['t'] = 0.0541853418443597;
            _aminoAcidFrequencies['V'] = _aminoAcidFrequencies['v'] = 0.0685721408543076;
            _aminoAcidFrequencies['W'] = _aminoAcidFrequencies['w'] = 0.00827394069862763;
            _aminoAcidFrequencies['Y'] = _aminoAcidFrequencies['y'] = 0.0269508675353835;
        }

        private static int GetIntMass(double mass)
        {
            return (int) (mass / SCALING_COEFFICIENT);
        }

        private static int? GetMass(double precursorMass, ITransitionPeakData<ISummaryPeakData> transitionPeakData)
        {
            var ionType = transitionPeakData.NodeTran.Transition.IonType;
            var mass = transitionPeakData.NodeTran.GetMoleculeMass().Value;

            switch (ionType)
            {
                case IonType.b:
                    return GetIntMass(mass);
                case IonType.y:
                    return GetIntMass(precursorMass - mass);
                default:
                    return null;
            }
        }

        public class PossibleAA : Immutable
        {
            public static readonly PossibleAA ROOT = new PossibleAA("", null);
            public PossibleAA(string aa, PossibleAA parent)
            {
                AA = aa;
                Parent = parent;
            }

            public PossibleAA ChangeParent(PossibleAA parent)
            {
                return ChangeProp(ImClone(this), im => im.Parent = parent);
            }

            public PossibleAA Parent { get; private set; }
            public string AA { get; private set; }
        }

        private static double[] GetMassProbabilities(IList<StaticMod> modifications)
        {
            var result = new double[MAX_MASS + 1];
            result[0] = 1.0;

            var aminoAcidMasses = new List<(char,double)>();
            var massCalc = new SequenceMassCalc(MassType.Monoisotopic);

            foreach (var mod in modifications.Where(m => m.HasMod && m.MonoisotopicMass.HasValue))
            {
                var modMass = mod.MonoisotopicMass.Value;
                aminoAcidMasses.AddRange(mod.AminoAcids.Select(aa => (aa, massCalc.GetAAMass(aa) + modMass)));
            }

            aminoAcidMasses.AddRange(AminoAcid.All.Select(aa => (aa, massCalc.GetAAMass(aa))));

            for (var i = 1; i < result.Length; ++i)
            {
                for (var aa = 0; aa < aminoAcidMasses.Count; ++aa)
                {
                    var indexNoAA = i - GetIntMass(aminoAcidMasses[aa].Item2);
                    if (indexNoAA < 0)
                        continue;
                    result[i] += result[indexNoAA] * _aminoAcidFrequencies[aminoAcidMasses[aa].Item1];
                }
            }

            return result;
        }
    }

    public class SelectivityScoreCalculatorUsingNominalMasses
    {
        private static readonly double MassWater = BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula("H2O"); // Not L10N
        private static readonly double OffsetB = BioMassCalc.MassProton;
        private static readonly double OffsetY = MassWater + BioMassCalc.MassProton;

        private const double RescalingConstant = 0.9995;

        private readonly IList<int> _integerAminoAcidMasses;
        private readonly IList<double> _aminoAcidProbabilities;

        private readonly int _maxMass;
        private readonly int _minPrecursorCharge;
        private readonly int _maxPrecursorCharge;

        private double[] _probability;

        public SelectivityScoreCalculatorUsingNominalMasses(
            IList<int> integerAminoAcidMasses,
            IList<double> aminoAcidProbabilities,
            int minPrecursorCharge = 2,
            int maxPrecursorCharge = 3,
            int maxMass = 3000)
        {
            _integerAminoAcidMasses = integerAminoAcidMasses;
            _aminoAcidProbabilities = aminoAcidProbabilities;
            _minPrecursorCharge = minPrecursorCharge;
            _maxPrecursorCharge = maxPrecursorCharge;
            _maxMass = maxMass;
            PrecomputeProbabilities();
        }

        public enum PeakType { none, b, y }

        private static int Pow(int b, int e)
        {
            var result = 1;
            for (var i = 0; i < e; ++i)
                result *= b;
            return result;
        }

        private static T[][] GetCombinations<T>(T[] possibleValues, T[] combinations, int expectedCount, int index)
        {
            if (index >= combinations.Length)
                return new[] { combinations };

            var resultIndex = 0;
            var result = new T[expectedCount][];
            for (var i = 0; i < possibleValues.Length; ++i)
            {
                var copy = new T[combinations.Length];
                Array.Copy(combinations, copy, combinations.Length);
                copy[index] = possibleValues[i];
                var subResult = GetCombinations(possibleValues, copy, expectedCount / possibleValues.Length, index + 1);
                Array.Copy(subResult, 0, result, resultIndex, subResult.Length);
                resultIndex += subResult.Length;
            }

            return result;
        }

        public static PeakType[][] GetPossiblePeakTypeCombinations(int transitionCount)
        {
            var possibleValues = new [] {PeakType.none, PeakType.b, PeakType.y};
            return GetCombinations(possibleValues, new PeakType[transitionCount],
                Pow(possibleValues.Length, transitionCount), 0);
        }

        public double[] GetPValues(double precursorMzTarget, double[] productMzTargets)
        {
            var numTransitions = productMzTargets.Length;
            // 3^numTransitions items that are arrays of length numTransitions and contain indices 0..2
            var combinations = CreateNtoTheKCombinations(3, numTransitions);

            var pValues = new double[numTransitions + 1];
            
            for (var precursorCharge = _minPrecursorCharge; precursorCharge <= _maxPrecursorCharge; precursorCharge++)
            {
                var intPeptideMass = GetIntMass((precursorMzTarget - BioMassCalc.MassProton) * precursorCharge - MassWater);
                foreach (var combination in combinations)
                {
                    var suffixList = new List<int>();
                    var score = 0;
                    for (var transitionIndex = 0; transitionIndex < combination.Length; transitionIndex++)
                    {
                        // Considering ith transition
                        var type = combination[transitionIndex];
                        if (type == 1) // Use as b
                        {
                            var intSuffixMass = intPeptideMass - GetIntMass(productMzTargets[transitionIndex] - OffsetB);
                            if(intSuffixMass > 0 && intSuffixMass < intPeptideMass)
                                suffixList.Add(intSuffixMass);
                            ++score;
                        }
                        else if (type == 2) // Use as y
                        {
                            var intSuffixMass = GetIntMass(productMzTargets[transitionIndex] - OffsetY);
                            if (intSuffixMass > 0 && intSuffixMass < intPeptideMass)
                                suffixList.Add(intSuffixMass);
                            ++score;
                        }
                        // Not used if type == 0
                    }
                    suffixList.Sort();
                    suffixList.Add(intPeptideMass);
                    var pValue = 1.0;
                    for (var i = 1; i < suffixList.Count; i++)
                    {
                        pValue *= _probability[suffixList[i] - suffixList[i - 1]];
                    }
                    pValues[score] += pValue * GetPriorProbability(intPeptideMass, precursorCharge);
                }
            }
            return pValues;
        }

        // TODO: this must be computed from real data
        private double GetPriorProbability(int peptideMass, int charge)
        {
            if (charge == 2) return 0.6;
            return 0.4;
        }

        private void PrecomputeProbabilities()
        {
            _probability = new double[_maxMass + 1];
            _probability[0] = 1.0;

            for (var i = 1; i < _probability.Length; i++)
            {
                for (var j = 0; j < _integerAminoAcidMasses.Count; j++)
                {
                    var prevMass = i - _integerAminoAcidMasses[j];
                    if (prevMass < 0)
                        continue;
                    _probability[i] += _probability[prevMass] * _aminoAcidProbabilities[j];
                }
            }
        }

        private int GetIntMass(double m)
        {
            return (int)Math.Round(m * RescalingConstant);
        }

        public static int[][] CreateNtoTheKCombinations(int n, int length)
        {
            if (n <= 0)
                return null;

            if (length == 0)
            {
                return new[] { new int[0] };
            }
            if (length == 1)
            {
                var combinations = new int[n][];
                for (var i = 0; i < n; i++)
                {
                    combinations[i] = new[] { i };
                }
                return combinations;
            }
            else
            {
                var prevCombinations = CreateNtoTheKCombinations(n, length - 1);
                var combinations = new List<int[]>();
                foreach (var combination in prevCombinations)
                {
                    for (var i = 0; i < n; i++)
                    {
                        var newCombination = new int[combination.Length + 1];
                        Array.Copy(combination, newCombination, combination.Length);
                        newCombination[newCombination.Length - 1] = i;
                        combinations.Add(newCombination);
                    }
                }
                return combinations.ToArray();
            }
        }
    }
}
