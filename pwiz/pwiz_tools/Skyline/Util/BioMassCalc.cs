/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Globalization;
using System.Text.RegularExpressions;
using pwiz.Common.Chemistry;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Enum used to specify the use of monoisotopic or average
    /// masses when calculating molecular masses.
    /// </summary>
    public enum MassType
    {
// ReSharper disable InconsistentNaming
        Monoisotopic, Average
// ReSharper restore InconsistentNaming
    }

    /// <summary>
    /// Calculates molecular masses based on atomic masses.
    /// Atomic masses come from http://www.unimod.org/unimod_help.html.
    /// Only supports the atoms H, O, N, C, S and P, and at most
    /// 2-digit atomic counts.
    /// The average mass of Carbon comes from Michael MacCoss, which he claims
    /// was derived by Dwight Matthews and John Hayes in the 70s.  It takes into
    /// account carbon 12 enrichment in living organisms:
    /// 
    /// http://www.madsci.org/posts/archives/2003-06/1055532737.Bc.r.html
    /// 
    /// But at 12.01085 is slightly higher than the current Unimod standard
    /// of 12.0107.
    ///  </summary>
    public class BioMassCalc
    {
        public static readonly BioMassCalc MONOISOTOPIC = new BioMassCalc(MassType.Monoisotopic);
        public static readonly BioMassCalc AVERAGE = new BioMassCalc(MassType.Average);

        public static readonly IsotopeAbundances DEFAULT_ABUNDANCES =
            MONOISOTOPIC.SynchMasses(IsotopeAbundances.Default);

        public const string H = "H";    // Hydrogen
        public const string H2 = "H'";  // Deuterium
        public const string C = "C";    // Carbon
        public const string C13 = "C'"; // Carbon13
        public const string N = "N";    // Nitrogen
        public const string N15 = "N'"; // Nitrogen15
        public const string O = "O";    // Oxygen
        public const string O17 = "O\"";// Oxygen17
        public const string O18 = "O'"; // Oxygen18
        public const string P = "P";    // Phosphorus
        public const string S = "S";    // Sulfur
// ReSharper disable InconsistentNaming
        public const string Se = "Se";  // Selenium
        public const string Li = "Li";  // Lithium
        public const string F = "F";    // Fluorine
        public const string Na = "Na";  // Sodium
        public const string Cl = "Cl";  // Chlorine
        public const string K = "K";    // Potassium
        public const string Ca = "Ca";  // Calcium
        public const string Fe = "Fe";  // Iron
        public const string Ni = "Ni";  // Nickle
        public const string Cu = "Cu";  // Copper
        public const string Zn = "Zn";  // Zinc
        public const string Br = "Br";  // Bromine
        public const string Mo = "Mo";  // Molybdenum
        public const string Ag = "Ag";  // Silver
        public const string I = "I";    // Iodine
        public const string Au = "Au";  // Gold
        public const string Hg = "Hg";  // Mercury
        public const string B = "B";    // Boron
        public const string As = "As";  // Arsenic
        public const string Cd = "Cd";  // Cadmium
        public const string Cr = "Cr";  // Chromium
        public const string Co = "Co";  // Cobalt
        public const string Mn = "Mn";  // Manganese
        public const string Mg = "Mg";  // Magnesium
// ReSharper restore InconsistentNaming

        /// <summary>
        /// A dictionary mapping heavy isotope symbols to their correspoding
        /// indices within the mass distributions of <see cref="IsotopeAbundances.Default"/>.
        /// </summary>
        private static readonly IDictionary<string, KeyValuePair<int, double>> DICT_SYMBOL_TO_ISOTOPE_INDEX =
            new Dictionary<string, KeyValuePair<int, double>>
                {
                    { H2, new KeyValuePair<int, double>(1, 0.98) },
                    { C13, new KeyValuePair<int, double>(1, 0.995) },
                    { N15, new KeyValuePair<int, double>(1, 0.995) },
                    { O17, new KeyValuePair<int, double>(1, 0.99) },
                    { O18, new KeyValuePair<int, double>(1, 0.99) },
                };

        public static IEnumerable<string> HeavySymbols { get { return DICT_SYMBOL_TO_ISOTOPE_INDEX.Keys; } }

        /// <summary>
        /// Returns the index of an atomic symbol the mass distribution
        /// from <see cref="IsotopeAbundances.Default"/>.
        /// </summary>
        public static int GetIsotopeDistributionIndex(string symbol)
        {
            KeyValuePair<int, double> pair;
            if (DICT_SYMBOL_TO_ISOTOPE_INDEX.TryGetValue(symbol, out pair))
                return pair.Key;
            return 0;
        }

        /// <summary>
        /// Returns the default atom percent enrichment for a heavy labeled atom.
        /// </summary>
        public static double GetIsotopeEnrichmentDefault(string symbol)
        {
            KeyValuePair<int, double> pair;
            if (DICT_SYMBOL_TO_ISOTOPE_INDEX.TryGetValue(symbol, out pair))
                return pair.Value;
            return 0;
        }

        /// <summary>
        /// Returns the monoisotopic symbol for the atomic symbols associated
        /// with <see cref="BioMassCalc"/>.
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static string GetMonoisotopicSymbol(string symbol)
        {
            if (DICT_SYMBOL_TO_ISOTOPE_INDEX.ContainsKey(symbol))
                return symbol.Substring(0, symbol.Length - 1);
            return symbol;
        }

        public static double MassProton
        {
            get { return 1.007276; }
        }

        /// <summary>
        /// Regular expression for possible characters that end an atomic
        /// symbol: capital letters, numbers or a space.
        /// </summary>
        private static readonly Regex REGEX_END_SYM = new Regex(@"[A-Z0-9 \-]");

        /// <summary>
        /// Find the first atomic symbol in a given expression.
        /// </summary>
        /// <param name="expression">The expression to search</param>
        /// <returns>The first atomic symbol</returns>
        private static string NextSymbol(string expression)
        {
            // Skip the first character, since it is always the start of
            // the symbol, and then look for the end of the symbol.
            Match match = REGEX_END_SYM.Match(expression, 1);
            if (!match.Success)
                return expression;
            return expression.Substring(0, match.Index);
        }

        private readonly Dictionary<string, double> _atomicMasses =
            new Dictionary<string, double>();

        /// <summary>
        /// Create a simple mass calculator for use in calculating
        /// protein, peptide and fragment masses.
        /// </summary>
        /// <param name="type">Monoisotopic or average mass calculations</param>
        public BioMassCalc(MassType type)
        {
            MassType = type;
            AddMass(H, 1.007825035, 1.00794);
            AddMass(H2, 2.01355321270, 2.01355321270);
            AddMass(O, 15.99491463, 15.9994);
            AddMass(O17, 16.9991322, 16.9991322);
            AddMass(O18, 17.9991604, 17.9991604);
            AddMass(N, 14.003074, 14.0067);
            AddMass(N15, 15.0001088984, 15.0001088984);
            AddMass(C, 12.0, 12.01085);
            AddMass(C13, 13.0033548378, 13.0033548378);
            AddMass(S, 31.9720707, 32.065);
            AddMass(P, 30.97376151, 30.973761);

            AddMass(Se, 79.9165213, 78.96); // Most abundant Se isotope is 80
            AddMass(Li, 7.016003, 6.941);
            AddMass(F, 18.99840322, 18.9984032);
            AddMass(Na, 22.9897677, 22.98977);
            AddMass(P, 30.973762, 30.973761);
            AddMass(S, 31.9720707, 32.065);
            AddMass(Cl, 34.96885272, 35.453);
            AddMass(K, 38.9637074, 39.0983);
            AddMass(Ca, 39.9625906, 40.078);
            AddMass(Fe, 55.9349393, 55.845);
            AddMass(Ni, 57.9353462, 58.6934);
            AddMass(Cu, 62.9295989, 63.546);
            AddMass(Zn, 63.9291448, 65.409);
            AddMass(Br, 78.9183361, 79.904);
            AddMass(Mo, 97.9054073, 95.94);
            AddMass(Ag, 106.905092, 107.8682);
            AddMass(I, 126.904473, 126.90447);
            AddMass(Au, 196.966543, 196.96655);
            AddMass(Hg, 201.970617, 200.59);
            AddMass(B, 11.0093055, 10.811);
            AddMass(As, 74.9215942, 74.9215942);
            AddMass(Cd, 113.903357, 112.411);
            AddMass(Cr, 51.9405098, 51.9961);
            AddMass(Co, 58.9331976, 58.933195);
            AddMass(Mn, 54.9380471, 54.938045);
            AddMass(Mg, 23.9850423, 24.305);
        }

        public MassType MassType { get; private set; }

        /// <summary>
        /// Calculate the mass of a molecule specified as a character
        /// string like "C6H11ON", or "[{atom}[count][spaces]]*", where the
        /// atoms are H, O, N, C, S or P.
        /// </summary>
        /// <param name="desc">The molecule description string</param>
        /// <returns>The mass of the specified molecule</returns>
        public double CalculateMass(string desc)
        {
            string parse = desc;
            double totalMass = ParseMass(ref parse);

            if (totalMass == 0.0 || parse.Length > 0)
                throw new ArgumentException("The expression '{0}' is not a valid chemical formula.");

            return totalMass;
        }

        /// <summary>
        /// Parses a chemical formula expressed as "[{atom}[count][spaces]]*",
        /// e.g. "C6H11ON", where supported atoms are H, O, N, C, S or P, etc.
        /// returning the total mass for the formula.
        /// 
        /// The parser removes atoms and counts until it encounters a character
        /// it does not understand as being part of the chemical formula.
        /// The remainder is returned in the desc parameter.
        /// </summary>
        /// <param name="desc">Input description, and remaining string after parsing</param>
        /// <returns>Total mass of formula parsed</returns>
        public double ParseMass(ref string desc)
        {
            double totalMass = 0.0;
            desc = desc.Trim();
            while (desc.Length > 0)
            {
                string sym = NextSymbol(desc);
                double massAtom = GetMass(sym);

                // Stop if unrecognized atom found.
                if (massAtom == 0)
                {
                    // CONSIDER: Throw with a useful message?
                    break;
                }

                desc = desc.Substring(sym.Length);
                int endCount = 0;
                while (endCount < desc.Length && Char.IsDigit(desc[endCount]))
                    endCount++;

                int count = 1;
                if (endCount > 0)
                    count = int.Parse(desc.Substring(0, endCount), CultureInfo.InvariantCulture);
                totalMass += massAtom * count;

                desc = desc.Substring(endCount).TrimStart();
            }

            return totalMass;            
        }

        /// <summary>
        /// Add or subtract the atom counts from a molecular formula to a <see cref="IDictionary{TKey,TValue}"/>
        /// of atomic symbols and counts.
        /// </summary>
        /// <param name="desc">Molecular formula</param>
        /// <param name="dictAtomCounts">Dictionary of atomic symbols and counts (may already contain counts from other formulas)</param>
        /// <param name="negative">True if counts should be subtracted</param>
        public void ParseCounts(ref string desc, IDictionary<string, int> dictAtomCounts, bool negative)
        {
            desc = desc.Trim();
            while (desc.Length > 0)
            {
                string sym = NextSymbol(desc);
                double massAtom = GetMass(sym);

                // Stop if unrecognized atom found.
                if (massAtom == 0)
                {
                    // CONSIDER: Throw with a useful message?
                    break;
                }

                desc = desc.Substring(sym.Length);
                int endCount = 0;
                while (endCount < desc.Length && Char.IsDigit(desc[endCount]))
                    endCount++;

                int count = 1;
                if (endCount > 0)
                    count = int.Parse(desc.Substring(0, endCount), CultureInfo.InvariantCulture);

                if (negative)
                    count = -count;

                if (dictAtomCounts.ContainsKey(sym))
                    dictAtomCounts[sym] += count;
                else
                    dictAtomCounts.Add(sym, count);

                if (dictAtomCounts[sym] == 0)
                    dictAtomCounts.Remove(sym);

                desc = desc.Substring(endCount).TrimStart();
            }
        }

        /// <summary>
        /// Get the mass of a single atom.
        /// </summary>
        /// <param name="sym">Character specifying the atom</param>
        /// <returns>The mass of the single atom</returns>
        public double GetMass(string sym)
        {
            double mass;
            if (_atomicMasses.TryGetValue(sym, out mass))
                return mass;
            return 0;
        }

        /// <summary>
        /// Adds atomic masses for a symbol character to a look-up table.
        /// </summary>
        /// <param name="sym">Atomic symbol character</param>
        /// <param name="mono">Monoisotopic mass</param>
        /// <param name="ave">Average mass</param>
        private void AddMass(string sym, double mono, double ave)
        {
            _atomicMasses[sym] = (MassType == MassType.Monoisotopic ? mono : ave);
        }

        /// <summary>
        /// Synchronizes the masses of an <see cref="IsotopeAbundances"/> object with
        /// the masses of this <see cref="BioMassCalc"/>, ensuring compatible mass calculations
        /// using the different classes.  This is only allowed with a monoisotopic mass calculator.
        /// </summary>
        /// <param name="abundances">An existing <see cref="IsotopeAbundances"/> object to be synchronized</param>
        /// <returns>An <see cref="IsotopeAbundances"/> object synchronized with this <see cref="BioMassCalc"/></returns>
        public IsotopeAbundances SynchMasses(IsotopeAbundances abundances)
        {
            if (MassType != MassType.Monoisotopic)
                throw new InvalidOperationException("Fixing isotope abundance masses requires a monoisotopic mass calculator");

            var dictFixes = new Dictionary<string, MassDistribution>();
            foreach (var atomAbundance in abundances)
            {
                double monoMassCalc;
                if (!_atomicMasses.TryGetValue(atomAbundance.Key, out monoMassCalc))
                    continue;
                double secondMassCalc, thirdMassCalc;
                _atomicMasses.TryGetValue(atomAbundance.Key + "'", out secondMassCalc);
                _atomicMasses.TryGetValue(atomAbundance.Key + "\"", out thirdMassCalc);
                var massDist = atomAbundance.Value;
                var massDistFixed = SynchDist(massDist, monoMassCalc, secondMassCalc, thirdMassCalc);
                if (!ReferenceEquals(massDist, massDistFixed))
                    dictFixes.Add(atomAbundance.Key, massDistFixed);
            }
            return abundances.SetAbundances(dictFixes);
        }

        /// <summary>
        /// Synchronizes a single <see cref="MassDistribution"/> object with corresponding
        /// masses from a <see cref="BioMassCalc"/>.
        /// </summary>
        private static MassDistribution SynchDist(MassDistribution massDist,
            double monoMassCalc, double secondMassCalc, double thirdMassCalc)
        {
            var massDistOrdered = massDist.MassesSortedByAbundance();
            if (EqualDistMasses(massDistOrdered, monoMassCalc, secondMassCalc, thirdMassCalc))
                return massDist;
            var dictFixDist = new Dictionary<double, double>(massDist);
            ReplaceMass(dictFixDist, massDistOrdered, 0, monoMassCalc);
            ReplaceMass(dictFixDist, massDistOrdered, 1, secondMassCalc);
            ReplaceMass(dictFixDist, massDistOrdered, 2, thirdMassCalc);
            return MassDistribution.NewInstance(dictFixDist, 0, 0);
        }

        /// <summary>
        /// Returns true if an ordered list of mass-distribution pairs are all
        /// equal to corresponding masses from a <see cref="BioMassCalc"/>.
        /// </summary>
        private static bool EqualDistMasses(IList<KeyValuePair<double, double>> massDistOrdered,
            double monoMassCalc, double secondMassCalc, double thirdMassCalc)
        {
            if (monoMassCalc != massDistOrdered[0].Key)
                return false;
            if (secondMassCalc != 0 && secondMassCalc != massDistOrdered[1].Key)
                return false;
            if (thirdMassCalc != 0 && thirdMassCalc != massDistOrdered[2].Key)
                return false;
            return true;
        }

        /// <summary>
        /// Replaces a mass from a <see cref="MassDistribution"/> with a corresponding
        /// mass from a <see cref="BioMassCalc"/>.
        /// </summary>
        /// <param name="dictFixDist">A mass-distribution dictionary in which to replace the mass</param>
        /// <param name="massDistOrdered">A distribution ordered list of masses-distribution pairs</param>
        /// <param name="massIndex">The index of the mass in the ordered list which should be replaced</param>
        /// <param name="massCalc">The mass to use as the replacement</param>
        private static void ReplaceMass(IDictionary<double, double> dictFixDist,
            IList<KeyValuePair<double, double>> massDistOrdered, int massIndex, double massCalc)
        {
            if (massCalc == 0 || massCalc == massDistOrdered[massIndex].Key)
                return;
            dictFixDist[massCalc] = massDistOrdered[massIndex].Value;
            dictFixDist.Remove(massDistOrdered[massIndex].Key);
        }
    }
}