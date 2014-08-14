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
using System.Text;
using pwiz.Common.Collections;

namespace pwiz.Common.Chemistry
{
    public class AminoAcidFormulas
    {
// ReSharper disable InconsistentNaming
        public const double ProtonMass = 1.00727649;

        // ReSharper disable NonLocalizedString
        public static readonly IDictionary<String, char> LongNames = new ImmutableDictionary<String,char>(
            new Dictionary<string, char> {
            {"Ala",'A'},
            {"Arg",'R'},
            {"Asn",'N'},
            {"Asp",'D'},
            {"Cys",'C'},
            {"Glu",'E'},
            {"Gln",'Q'},
            {"Gly",'G'},
            {"His",'H'},
            {"Ile",'I'},
            {"Leu",'L'},
            {"Lys",'K'},
            {"Met",'M'},
            {"Phe",'F'},
            {"Pro",'P'},
            {"Ser",'S'},
            {"Thr",'T'},
            {"Trp",'W'},
            {"Tyr",'Y'},
            {"Val",'V'}
        });
        // ReSharper restore NonLocalizedString

        // ReSharper disable NonLocalizedString
        public static readonly IDictionary<char, String> DefaultFormulas = new ImmutableDictionary<char, String>
            (
            new Dictionary<char, string>
                {
                    {'A', "C3H5ON"},
                    {'C', "C3H5ONS"},
                    {'D', "C4H5O3N"},
                    {'E', "C5H7O3N"},
                    {'F', "C9H9ON"},
                    {'G', "C2H3ON"},
                    {'H', "C6H7ON3"},
                    {'I', "C6H11ON"},
                    {'K', "C6H12ON2"},
                    {'L', "C6H11ON"},
                    {'M', "C5H9ONS"},
                    {'N', "C4H6O2N2"},
                    {'O', "C12H19N3O2"},
                    {'P', "C5H7ON"},
                    {'Q', "C5H8O2N2"},
                    {'R', "C6H12ON4"},
                    {'S', "C3H5O2N"},
                    {'T', "C4H7O2N"},
                    {'U', "C3H5NOSe"},
                    {'V', "C5H9ON"},
                    {'W', "C11H10ON2"},
                    {'Y', "C9H9O2N"},
                }
                );
        // ReSharper restore NonLocalizedString


        public static readonly AminoAcidFormulas Default = new AminoAcidFormulas
                                                               {
                                                                   MassShifts = new ImmutableDictionary<char,double>(new Dictionary<char, double>()),
                                                                   Formulas = DefaultFormulas,
                                                                   IsotopeAbundances = IsotopeAbundances.Default,
                                                                   MassResolution = .001,
                                                               };
// ReSharper restore InconsistentNaming

        public double MassResolution { get; private set; }
        public IDictionary<char, double> MassShifts { get; private set; }
        public IDictionary<char, String> Formulas { get; private set; }
        public IsotopeAbundances IsotopeAbundances { get; private set; }
        public AminoAcidFormulas SetFormula(char aminoAcid, String formula)
        {
            var newFormulas = new Dictionary<char, String>(Formulas);
            newFormulas[aminoAcid] = formula;
            var result = Clone();
            result.Formulas = newFormulas;
            return result;
        }
        public AminoAcidFormulas SetMassShift(char aminoAcid, double massShift)
        {
            return SetMassShifts(new Dictionary<char, double> {{aminoAcid, massShift}});
        }
        public AminoAcidFormulas SetMassShifts(Dictionary<char,double> dictionary)
        {
            var newMassShifts = new Dictionary<char, double>(MassShifts);
            foreach (var entry in dictionary)
            {
                newMassShifts[entry.Key] = entry.Value;
            }
            var result = Clone();
            result.MassShifts = new ImmutableDictionary<char, double>(newMassShifts);
            return result;
        }
        public AminoAcidFormulas SetIsotopeAbundances(IsotopeAbundances newAbundances)
        {
            var result = Clone();
            result.IsotopeAbundances = newAbundances;
            return result;
        }
        public MassDistribution GetMassDistribution(Molecule molecule, int charge)
        {
            var md = new MassDistribution(MassResolution, .00001);
            var result = md;
            foreach (var element in molecule)
            {
                result = result.Add(md.Add(IsotopeAbundances[element.Key]).Multiply(element.Value));
            }
            if (charge != 0)
            {
                result = result.OffsetAndDivide(charge * ProtonMass, charge);
            }
            return result;
        }
        public AminoAcidFormulas SetMassResolution(double massResolution)
        {
            var result = Clone();
            result.MassResolution = massResolution;
            return result;
        }
        public MassDistribution GetMassDistribution(String peptide, int charge)
        {
            return GetMassDistribution(GetFormula(peptide), charge)
                .OffsetAndDivide(GetMassShift(peptide)/Math.Max(charge,1), 1);
        }
        public Molecule GetFormula(String peptide)
        {
            var formula = new StringBuilder();
            foreach (var ch in peptide)
            {
                String aaFormula;
                if (!Formulas.TryGetValue(ch, out aaFormula))
                {
                    // TODO: error
                    continue;
                }
                formula.Append(aaFormula);
            }
            formula.Append("H2O"); // Not L10N
            return Molecule.Parse(formula.ToString());
        }
        public Double GetMassShift(String peptide)
        {
            var result = 0.0;
            foreach (var ch in peptide)
            {
                double shift;
                MassShifts.TryGetValue(ch, out shift);
                result += shift;
            }
            return result;
        }
        public double GetMonoisotopicMass(String peptide)
        {
            double result = GetMassShift(peptide);
            foreach (var element in GetFormula(peptide))
            {
                result += IsotopeAbundances[element.Key].MostAbundanceMass*element.Value;
            }
            return result;
        }

        public AminoAcidFormulas Clone()
        {
            return new AminoAcidFormulas
                       {
                           Formulas = Formulas,
                           IsotopeAbundances = IsotopeAbundances,
                           MassResolution = MassResolution,
                           MassShifts = MassShifts
                       };
        }
    }
}
