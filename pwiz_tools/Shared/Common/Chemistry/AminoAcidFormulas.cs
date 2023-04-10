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
using System.Linq;
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Chemistry
{
    public class AminoAcidFormulas : Immutable
    {
// ReSharper disable InconsistentNaming
        // public const double ProtonMass = 1.007276466879;  // per http://physics.nist.gov/cgi-bin/cuu/Value?mpu|search_for=proton+mass 12/18/2016
        public const double ProtonMass = 1.00727649;

        // ReSharper disable LocalizableElement
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
            {"Val",'V'},
            {"Sec",'U'},
            {"Pyl",'O'}
        });
        public static readonly IDictionary<char, String> FullNames = new ImmutableDictionary<char, String>(
            new Dictionary<char, string> {
            {'A',"Alanine"},
            {'R', "Arginine"},
            {'N', "Asparagine"},
            {'D', "Aspartic acid"},
            {'C', "Cysteine"},
            {'E', "Glutamic acid"},
            {'Q', "Glutamine"},
            {'G', "Glycine"},
            {'H', "Histidine"},
            {'I', "Isoleucine"},
            {'L', "Leucine"},
            {'K', "Lysine"},
            {'M', "Methionine"},
            {'F', "Phenylalanine"},
            {'P', "Proline"},
            {'S', "Serine"},
            {'T', "Threonine"},
            {'W', "Tryptophan"},
            {'Y', "Tyrosine"},
            {'V', "Valine"},
            {'U', "Selenocysteine"},
            {'O', "Pyrrolysine"}
        });
        // ReSharper restore LocalizableElement

        // ReSharper disable LocalizableElement
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
        // ReSharper restore LocalizableElement

        private Dictionary<char, Molecule> _molecules;
        private static readonly Molecule H2O = Molecule.Parse(@"H2O");

        public static readonly AminoAcidFormulas Default = new AminoAcidFormulas
                                                               {
                                                                   MassShifts = new ImmutableDictionary<char,double>(new Dictionary<char, double>()),
                                                                   _molecules = DefaultFormulas.ToDictionary(kvp=>kvp.Key, kvp=>Molecule.Parse(kvp.Value)),
                                                                   IsotopeAbundances = IsotopeAbundances.Default,
                                                                   MassResolution = .001,
                                                               };
// ReSharper restore InconsistentNaming

        public double MassResolution { get; private set; }
        public IDictionary<char, double> MassShifts { get; private set; }
        public IsotopeAbundances IsotopeAbundances { get; private set; }
        public AminoAcidFormulas SetFormula(char aminoAcid, String formula)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im._molecules = new Dictionary<char, Molecule>(_molecules);
                im._molecules[aminoAcid] = Molecule.Parse(formula);
            });
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

            return ChangeProp(ImClone(this), im => im.MassShifts = newMassShifts);
        }
        public AminoAcidFormulas SetIsotopeAbundances(IsotopeAbundances newAbundances)
        {
            return ChangeProp(ImClone(this), im => im.IsotopeAbundances = newAbundances);
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
            return ChangeProp(ImClone(this), im=>im.MassResolution = massResolution);
        }
        public MassDistribution GetMassDistribution(String peptide, int charge)
        {
            return GetMassDistribution(GetFormula(peptide), charge)
                .OffsetAndDivide(GetMassShift(peptide)/Math.Max(charge,1), 1);
        }
        public Molecule GetFormula(String peptide)
        {
            var formulas = peptide.Select(aa =>
                _molecules.TryGetValue(aa, out Molecule aaFormula) ? aaFormula : Molecule.Empty
            ).Append(H2O);
            return Molecule.Sum(formulas);
        }

        [CanBeNull]
        public Molecule GetAminoAcidFormula(char aa)
        {
            Molecule molecule;
            _molecules.TryGetValue(aa, out molecule);
            return molecule;
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
    }
}
