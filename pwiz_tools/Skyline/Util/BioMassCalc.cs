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
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Calculates molecular masses based on atomic masses.
    /// Atomic masses come from http://www.unimod.org/unimod_help.html.
    /// Some heavy isotopes come from pwiz/utility/chemistry/isotopes.text, which
    /// comes from http://physics.nist.gov/PhysRefData/Compositions/index.html
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
        // Reasonable values for comparison and serialization of masses
        public const int MassPrecision = 6;
        public const double MassTolerance = 1e-6;
        public const string MASS_FORMAT = @"0.######";


        public static readonly BioMassCalc MONOISOTOPIC = new BioMassCalc(MassType.Monoisotopic);
        public static readonly BioMassCalc AVERAGE = new BioMassCalc(MassType.Average);
        public static readonly BioMassCalc MONOISOTOPIC_MASSH = new BioMassCalc(MassType.MonoisotopicMassH);
        public static readonly BioMassCalc AVERAGE_MASSH = new BioMassCalc(MassType.AverageMassH);

        public static readonly IsotopeAbundances DEFAULT_ABUNDANCES = IsotopeAbundances.Default;

        public const string SKYLINE_ISOTOPE_HINT1 = @"'"; // Denotes most abundant isotope
        public const string SKYLINE_ISOTOPE_HINT2 = @""""; // Denotes second most abundant isotope

        // ReSharper disable LocalizableElement
        public const string H = "H";    // Hydrogen
        public const string H2 = "H'";  // Deuterium
        public const string H3 = "H\""; // Tritium
        public const string D = "D";    // Deuterium - IUPAC standard
        public const string T = "T";    // Tritium - IUPAC standard
        public const string C = "C";    // Carbon
        public const string C13 = "C'"; // Carbon13
        public const string C14 = "C\""; // Carbon14 (radioisotope, trace natural abundance)
        public const string N = "N";    // Nitrogen
        public const string N15 = "N'"; // Nitrogen15
        public const string O = "O";    // Oxygen
        public const string O17 = "O\"";// Oxygen17
        public const string O18 = "O'"; // Oxygen18
        public const string P = "P";    // Phosphorus
        public const string P32 = "P'";    // Phosphorus32 (radioisotope, trace natural abundance)
        public const string S = "S";    // Sulfur
        public const string S34 = "S'";    // Sulfur34 (4.2% natural abundance)
        public const string S33 = "S\"";    // Sulfur33 (0.75% natural abundance)
// ReSharper disable InconsistentNaming
        public const string Se = "Se";  // Selenium
        public const string Li = "Li";  // Lithium
        public const string F = "F";    // Fluorine
        public const string Na = "Na";  // Sodium
        public const string Cl = "Cl";  // Chlorine
        public const string Cl37 = "Cl'";  // Chlorine37
        public const string K = "K";    // Potassium
        public const string Ca = "Ca";  // Calcium
        public const string Fe = "Fe";  // Iron
        public const string Ni = "Ni";  // Nickle
        public const string Cu = "Cu";  // Copper
        public const string Cu65 = "Cu'";  // Copper65
        public const string Zn = "Zn";  // Zinc
        public const string Br = "Br";  // Bromine
        public const string Br81 = "Br'";  // Bromine81
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
        public const string Si = "Si";  //Silicon
        // ReSharper restore LocalizableElement
// ReSharper restore InconsistentNaming

        /// <summary>
        /// A dictionary mapping heavy isotope symbols to their corresponding
        /// indices within the mass distributions of <see cref="IsotopeAbundances.Default"/>,
        /// and default atom percent enrichment for <see cref="IsotopeEnrichmentItem"/>.
        /// This dictionary contains entries for Skyline-style isotope symbols (e.g. H' for Deuterium)
        /// DOES NOT contain synonyms (e.g. D for Deuterium)
        /// </summary>
        private static readonly IDictionary<string, KeyValuePair<double, double>> DICT_HEAVYSYMBOL_TO_MASS =
            new Dictionary<string, KeyValuePair<double, double>>
                {
                    { H2, new KeyValuePair<double, double>(2.014101779, 0.98) },
                    { C13, new KeyValuePair<double, double>(13.0033548378, 0.995) },
                    { C14, new KeyValuePair<double, double>(14.003241988, 0.99) }, // N.B. No idea if 0.99 is a realistic value
                    { N15, new KeyValuePair<double, double>(15.0001088984, 0.995) },
                    { O17, new KeyValuePair<double, double>(16.9991315, 0.99) },
                    { O18, new KeyValuePair<double, double>(17.9991604, 0.99) },
                    { Cl37, new KeyValuePair<double, double>(36.965902602, 0.99) },  // N.B. No idea if 0.99 is a realistic value
                    { Br81, new KeyValuePair<double, double>(80.9162897, 0.99) },  // N.B. No idea if 0.99 is a realistic value 
                    { P32, new KeyValuePair<double, double>(31.973907274, 0.99) },  // N.B. No idea if 0.99 is a realistic value 
                    { S33, new KeyValuePair<double, double>(32.971456, 0.99) },  // N.B. No idea if this 0.99 a realistic value 
                    { S34, new KeyValuePair<double, double>(33.967866, 0.99) },  // N.B. No idea if this 0.99 a realistic value 
                    { H3, new KeyValuePair<double, double>(3.01604928199, 0.99) },  // N.B. No idea if this is a realistic value 
                    { Cu65, new KeyValuePair<double, double>(64.92778970, 0.99) },  // N.B. No idea if this is a realistic value 
                };

        public static bool IsHeavySymbol(string symbol)  // True if matches D, T, or ends with ' or "
        {
            return MONOISOTOPIC._atomicMasses.TryGetValue(symbol, out var massInfo) && massInfo._bHeavy;
        }
        public static bool IsSkylineHeavySymbol(string symbol)  // True if ends with ' or ", but not synonyms like D or T
        {
            return symbol != null && (symbol.EndsWith(@"'") || symbol.EndsWith(@""""));
        }

        /// <summary>
        /// Returns a dictionary of common isotope representations (e.g. IUPAC's D for Deuterium) to Skyline's representation.
        /// CONSIDER(bspratt) would be trivial to add support for pwiz-style _2H -> H' _37Cl-> CL' etc
        /// NB if you do so, make sure to update BiblioSpec BuildParser.cpp which explicitly rejects '_' in formulas
        /// </summary>
        public static ReadOnlyDictionary<string, string> DICT_HEAVYSYMBOL_NICKNAMES => new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(){
                {D, H2}, // IUPAC Deuterium
                {T, H3} // IUPAC Tritium
            });

        /// <summary>
        /// A dictionary mapping heavy isotope symbols to their corresponding monoisotopic element.
        /// This dictionary contains entries for Skyline-style isotope symbols (e.g. H' for Deuterium -> H)
        /// as well as common synonyms (e.g. D for Deuterium -> H)
        /// </summary>
        public static readonly ReadOnlyDictionary<string, string> DICT_HEAVYSYMBOL_TO_MONOSYMBOL = // Map Cl' to Cl, D to H etc
            new ReadOnlyDictionary<string, string>(
             DICT_HEAVYSYMBOL_TO_MASS.ToDictionary(kvp => kvp.Key, kvp => kvp.Key)
                .ToArray().Concat(DICT_HEAVYSYMBOL_NICKNAMES.ToDictionary(kvp => kvp.Key, kvp => kvp.Value).ToArray())
                    .ToDictionary(kvp => kvp.Key,
                        kvp => kvp.Value.Replace(SKYLINE_ISOTOPE_HINT1, string.Empty).Replace(SKYLINE_ISOTOPE_HINT2, string.Empty)));


        /// <summary>
        /// A list of Skyline-style isotope symbols (e.g. H')
        /// DOES NOT include synonyms such as D for Deuterium
        /// </summary>
        public static readonly string[] HeavySymbols = DICT_HEAVYSYMBOL_TO_MASS.Keys.ToArray();

        /// <summary>
        /// Determine whether a string describes and isotope of an element
        /// </summary>
        /// <param name="xElement">string describing an element, possibly an isotope, e.g. "Cl" or "Cl'" or "D" </param>
        /// <param name="yElement">string describing another element that might be the light version of xElement</param>
        /// <returns>true if, for example, xElement is "N'" and yElement is "N"</returns>
        public static bool ElementIsIsotopeOf(string xElement, string yElement)
        {
            if (BioMassCalc.DICT_HEAVYSYMBOL_TO_MONOSYMBOL.TryGetValue(xElement, out var light) &&
                Equals(yElement, light))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the index of an atomic symbol the mass distribution
        /// from <see cref="IsotopeAbundances.Default"/>.
        /// </summary>
        public static double GetHeavySymbolMass(string symbol)
        {
            KeyValuePair<double, double> pair;
            if (DICT_HEAVYSYMBOL_TO_MASS.TryGetValue(symbol, out pair))
                return pair.Key;
            return 0;
        }

        /// <summary>
        /// Returns the default atom percent enrichment for a heavy labeled atom.
        /// </summary>
        public static double GetIsotopeEnrichmentDefault(string symbol)
        {
            KeyValuePair<double, double> pair;
            if (DICT_HEAVYSYMBOL_TO_MASS.TryGetValue(symbol, out pair))
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
            if (DICT_HEAVYSYMBOL_TO_MONOSYMBOL.TryGetValue(symbol, out var mono))
                return mono;
            return symbol;
        }

        public static double MassProton
        {
            //get { return AminoAcidFormulas.ProtonMass; } // was "1.007276" here, let's be consistent and use the higher precision value
            get { return 1.007276; }
        }

        public static double MassElectron
        {
          //get { return 0.000548579909070; }  // per http://physics.nist.gov/cgi-bin/cuu/Value?meu|search_for=electron+mass 12/18/2016
            get { return 0.00054857990946; } // per http://physics.nist.gov/cgi-bin/cuu/Value?meu|search_for=electron+mass
        }

        /// <summary>
        /// Find the first atomic symbol in a given expression.
        /// </summary>
        /// <param name="expression">The expression to search</param>
        /// <returns>The first atomic symbol</returns>
        private static string NextSymbol(string expression)
        {
            // Skip the first character, since it is always the start of
            // the symbol, and then look for the end of the symbol.
            var i = 1;
            foreach (var c in expression.Skip(1))
            {
                if (!char.IsLower(c) && c != '\'' && c != '"')
                {
                    return expression.Substring(0, i);
                }
                i++;
            }
            return expression;
        }

        private struct MassInfo
        {
            public double _mass;
            public bool _bHeavy;
        }

        private readonly Dictionary<string, MassInfo> _atomicMasses =
            new Dictionary<string, MassInfo>();



        /// <summary>
        /// Create a simple mass calculator for use in calculating
        /// molecule masses.
        /// </summary>
        /// <param name="type">Monoisotopic or average mass calculations</param>
        public static BioMassCalc GetBioMassCalc(MassType type)
        {
            switch (type)
            {
                case MassType.Average:
                    return AVERAGE;
                case MassType.AverageMassH:
                    return AVERAGE_MASSH;
                case MassType.Monoisotopic:
                    return MONOISOTOPIC;
                case MassType.MonoisotopicMassH:
                    return MONOISOTOPIC_MASSH;
            }
            return new BioMassCalc(type);
        }

        private BioMassCalc(MassType type)   
        {
            MassType = type;
            AddMass(H, 1.00794); //Unimod
            AddMass(H2, 2.014101779); //Unimod
            AddMass(H3, 3.01604928199); // Wikipedia
            AddMass(O, 15.9994); //Unimod
            AddMass(O17, 16.9991315); //NIST
            AddMass(O18, 17.9991604); //NIST, Unimod=17.9991603
            AddMass(N, 14.0067); //Unimod
            AddMass(N15, 15.0001088984); //NIST, Unimod=15.00010897
            AddMass(C, 12.01085); //MacCoss average
            AddMass(C13, 13.0033548378); //NIST, Unimod=13.00335483
            AddMass(C14, 14.003241988); //NIST
            AddMass(S, 32.065); //Unimod
            AddMass(P, 30.973761); //Unimod
            AddMass(P32, 31.973907274); // Wikipedia and http://periodictable.com/Isotopes/015.32/index3.p.full.html using Wolfram
            AddMass(Se, 78.96); //Unimod, Most abundant Se isotope is 80
            AddMass(Li, 6.941); //Unimod
            AddMass(F, 18.9984032); //Unimod
            AddMass(Na, 22.98977); //Unimod
            AddMass(S, 32.065); //Unimod
            var massS33 = IsotopeAbundances.Default[S].Keys[1]; // Just be consistent with the isotope masses we already use
            AddMass(S33, massS33);
            var massS34 = IsotopeAbundances.Default[S].Keys[2];  // Just be consistent with the isotope masses we already use
            AddMass(S34, massS34);
            AddMass(Cl, 35.453); //Unimod
            AddMass(Cl37, 36.965902602); //NIST
            AddMass(K, 39.0983); //Unimod
            AddMass(Ca, 40.078); //Unimod
            AddMass(Fe, 55.845); //Unimod
            AddMass(Ni, 58.6934); //Unimod
            AddMass(Cu, 63.546); //Unimod
            var massCu65 = IsotopeAbundances.Default[Cu].Keys[1]; // Just be consistent with the isotope masses we already use
            AddMass(Cu65, massCu65);
            AddMass(Zn, 65.409); //Unimod
            AddMass(Br, 79.904); //Unimod
            AddMass(Br81, 80.9162897); //NIST
            AddMass(Mo, 95.94); //Unimod
            AddMass(Ag, 107.8682); //Unimod
            AddMass(I, 126.90447); //Unimod
            AddMass(Au, 196.96655); //Unimod
            AddMass(Hg, 200.59); //Unimod
            AddMass(B, 10.811);
            AddMass(As, 74.9215942);
            AddMass(Cd, 112.411);
            AddMass(Cr, 51.9961);
            AddMass(Co, 58.933195);
            AddMass(Mn, 54.938045);
            AddMass(Mg, 24.305);
            AddMass(Si, 28.085); // Per Wikipedia
            // Add all other elements
            foreach (var entry in IsotopeAbundances.Default)
            {
                if (_atomicMasses.ContainsKey(entry.Key))
                {
                    continue;
                }
                AddMass(entry.Key, entry.Value.AverageMass);
            }

            // Add entries for isotope synonyms like D (H') and T (H")
            foreach (var kvp in DICT_HEAVYSYMBOL_NICKNAMES) 
            {
                _atomicMasses.Add(kvp.Key, _atomicMasses[kvp.Value]);
            }
        }

        /// <summary>
        /// Ensure that the entries for D and T are the same as the entries for H' and H".
        /// </summary>
        public static IsotopeAbundances AddHeavyNicknames(IsotopeAbundances isotopeAbundances)
        {
            var changes = new Dictionary<string, MassDistribution>();
            foreach (var kvp in DICT_HEAVYSYMBOL_NICKNAMES)
            {
                MassDistribution massDistribution;
                if (isotopeAbundances.TryGetValue(kvp.Value, out massDistribution))
                {
                    changes.Add(kvp.Key, massDistribution);
                }
            }

            return isotopeAbundances.SetAbundances(changes);
        }

        public MassType MassType { get; private set; }

        public static string FormatArgumentExceptionMessage(string desc)
        {
            string errmsg =
                string.Format(
                    Resources.BioMassCalc_CalculateMass_The_expression__0__is_not_a_valid_chemical_formula, desc) +
                UtilResources.BioMassCalc_FormatArgumentException__Supported_chemical_symbols_include__;
            foreach (var key in MONOISOTOPIC._atomicMasses.Keys)
                errmsg += key + @" "; 
            return errmsg;
        }

        public static void ThrowArgumentException(string desc)
        {
            throw new ArgumentException(FormatArgumentExceptionMessage(desc));
        }

        public static bool ContainsIsotopicElement(IEnumerable<KeyValuePair<string, int>> desc)
        {
            return desc.Any(kvp => MONOISOTOPIC._atomicMasses.TryGetValue(kvp.Key, out var massInfo) && massInfo._bHeavy); // Look for Cl', O", D, T etc
        }

        /// <summary>
        /// Calculate the mass of a molecule specified as a character
        /// string like "C6H11ON", or "[{atom}[count][spaces]]*", where the
        /// atoms are chemical symbols like H, or C, or C' etc.
        /// </summary>
        /// <param name="desc">The molecule description string</param>
        /// <param name="mol">The resulting molecule object</param>
        /// <returns>The mass of the specified molecule</returns>
        public TypedMass CalculateMassFromFormula(string desc, out ParsedMolecule mol)
        {
            var totalMass = ParseFormulaMass(desc, out mol);
            if (totalMass == 0.0)
                ThrowArgumentException(desc);
            return totalMass;
        }

        public TypedMass CalculateMassFromFormula(string desc)
        {
            return CalculateMassFromFormula(desc, out _);
        }

        public bool TryCalculateMassFromFormula(string desc, out TypedMass mass)
        {
            try
            {
                mass = CalculateMassFromFormula(desc, out _);
                return true;
            }
            catch
            {
                mass = new TypedMass(0, MassType);
            }
            return true;
        }

        public TypedMass CalculateMass(ParsedMolecule mol)
        {
            if (ParsedMolecule.IsNullOrEmpty(mol))
            {
                return new TypedMass(0, this.MassType);
            }
            return CalculateMass((IDictionary<string, int>)mol.Molecule) + (MassType.IsMonoisotopic()
                ? mol.MonoMassOffset
                : mol.AverageMassOffset);
        }

        public TypedMass CalculateMass(MoleculeMassOffset mol)
        {
            if (MoleculeMassOffset.IsNullOrEmpty(mol))
            {
                return new TypedMass(0, this.MassType);
            }
            return CalculateMass((IDictionary<string, int>)mol.Molecule) + (MassType.IsMonoisotopic()
                ? mol.MonoMassOffset
                : mol.AverageMassOffset);
        }

        public TypedMass CalculateMass(Molecule mol)
        {
            if (Molecule.IsNullOrEmpty(mol))
            {
                return new TypedMass(0, this.MassType);
            }
            return CalculateMass((IDictionary<string, int>)mol);
        }

        /// <summary>
        /// Remove, if necessary, any weirdness like "H0" (zero hydrogens) in a formula, preserving other less offensive idiosyncrasies like N1 instead of N
        /// e.g. XeC12N001H0 => XeC12N1
        /// Preserve things like COOOH instead of making it CO3H, e.g. COOOHN1S0 =>  COOOHN1 or COOOHNS0 =>  COOOHN
        /// </summary>
        /// <param name="desc">the formula</param>
        /// <returns>the tidied up formula</returns>
        public static string RegularizeFormula(string desc)
        {
            if (string.IsNullOrEmpty(desc))
            {
                return desc;
            }
            desc = desc.Trim();
            var atomCounts = new List<KeyValuePair<string, string>>();
            while (desc.Length > 0)
            {
                var atom = NextSymbol(desc);
                desc = desc.Substring(atom.Length);

                var nDigits = 0; // Looking for an explicit atom count declaration
                while (nDigits < desc.Length && char.IsDigit(desc[nDigits]))
                {
                    nDigits++;
                }

                var atomCount = 1;
                var atomCountString = "";
                if (nDigits > 0) // There was an explicit count declaration
                {
                    atomCount = int.Parse(desc.Substring(0, nDigits), CultureInfo.InvariantCulture);
                    atomCountString = atomCount.ToString(CultureInfo.InvariantCulture); // Change "H01" to "H1"
                }

                if (atomCount != 0) // Drop "H0"
                {
                    atomCounts.Add(new KeyValuePair<string, string>(atom, atomCountString));
                }

                desc = desc.Substring(nDigits).TrimStart();
            }

            return string.Concat(atomCounts.Select(atomCount =>
                $@"{atomCount.Key}{atomCount.Value}"));

        }

        /// <summary>
        /// Turn a formula like C5H9H'3NO2S into C5H12NO2S
        /// </summary>
        public string StripLabelsFromFormula(string desc)
        {
            if (string.IsNullOrEmpty(desc))
                return null;

            try
            {
                var molecule = ParsedMolecule.Create(desc); // Using ParsedMolecule to preserve the atom order
                // Look for any heavy isotopes in the formula and replace them with unlabeled versions
                var dictUnlabeled = StripLabelsFromFormula(molecule);
                return ParsedMolecule.IsNullOrEmpty(dictUnlabeled) ? null : dictUnlabeled.ToString();
            }
            catch (ArgumentException)
            {
                return desc; // That wasn't understood as a formula
            }
        }

        public static ParsedMolecule StripLabelsFromFormula(ParsedMolecule atomCounts)
        {
            return atomCounts.ChangeMolecule(StripLabelsFromFormula(atomCounts.Molecule));
        }

        public static MoleculeMassOffset StripLabelsFromFormula(MoleculeMassOffset atomCounts)
        {
            return atomCounts.ChangeMolecule(StripLabelsFromFormula(atomCounts.Molecule));
        }

        public static Molecule StripLabelsFromFormula(Molecule molecule)
        {
            if (!ContainsIsotopicElement(molecule))
            {
                return molecule;
            }
            var result = new Dictionary<string, int>(molecule);
            // Look for any heavy isotopes in the formula and replace them with unlabeled versions
            foreach (var kvp in molecule)
            {
                // For each heavy isotope in the formula
                if (DICT_HEAVYSYMBOL_TO_MONOSYMBOL.TryGetValue(kvp.Key, out var unlabeled))
                {
                    if (result.TryGetValue(unlabeled, out var count)) // Get current count of unlabeled version, if any
                    {
                        result[unlabeled] = count + kvp.Value; // Add the heavy version's count to the unlabeled version's count
                    }
                    else
                    {
                        result.Add(unlabeled, kvp.Value);
                    }
                    result.Remove(kvp.Key); // And remove heavy isotope from the formula
                }
            }

            return Molecule.FromDict(result);
        }

        /// <summary>
        /// Find the C'3O"2 in  C'3C2H9H'0NO2O"2S (yes, H'0 - seen in the wild - but drop zero counts)
        /// </summary>
        public static IDictionary<string, int> FindIsotopeLabelsInFormula(string desc)
        {
            if (string.IsNullOrEmpty(desc))
                return null;
            var mol = Molecule.Parse(desc);
            return FindIsotopeLabelsInFormula(mol);
        }

        public static IDictionary<string, int> FindIsotopeLabelsInFormula(IEnumerable<KeyValuePair<string, int>> desc)
        {
            return desc?.Where(pair => DICT_HEAVYSYMBOL_TO_MONOSYMBOL.ContainsKey(pair.Key)).ToDictionary(p => p.Key, p => p.Value);
        }

        /// <summary>
        /// Find the intersection of a list of formulas, ignoring labels
        /// e.g. for C12H3H'2S2, C10H5, and C10H4Nz, return C10H4
        /// </summary>
        public static Molecule FindFormulaIntersectionUnlabeled(IEnumerable<Molecule> formulas)
        {
            var unlabeled = formulas.Select(StripLabelsFromFormula).ToList();
            return FindFormulaIntersection(unlabeled);
        }

        /// <summary>
        /// Find the intersection of a list of formulas
        /// e.g. for C12H5S2, C10H5, and C10H4Nz, return C10H4
        /// </summary>
        public static Molecule FindFormulaIntersection(IList<Molecule> formulas)
        {
            if (formulas.Count == 0)
                return Molecule.Empty;
            if (formulas.Count == 1)
                return formulas[0];
            if (formulas.Count == 2 && formulas[0].Equals(formulas[1]))
                return formulas[0];
            var common = new Dictionary<string, int>(formulas[0]);
            for (var i = 1; i < formulas.Count; i++)
            {
                var next = formulas[i];
                foreach (var kvp in next)
                {
                    int count;
                    if (common.TryGetValue(kvp.Key, out count))
                    {
                        common[kvp.Key] = Math.Min(count, kvp.Value);
                    }
                }
                foreach (var key in common.Keys.ToArray())
                {
                    if (!next.ContainsKey(key) || next[key] == 0)
                    {
                        common[key] = 0;
                    }
                }
            }
            return Molecule.FromDict(common);
        }

        /// <summary>
        /// For test purposes
        /// </summary>
        public double CalculateIonMz(string desc, Adduct adduct)
        {
            var mass = CalculateMassFromFormula(desc);
            return adduct.MzFromNeutralMass(mass);
        }

        /// <summary>
        /// For test purposes
        /// </summary>
        public static double CalculateIonMz(TypedMass mass, Adduct adduct)
        {
            return adduct.MzFromNeutralMass(mass);
        }

        /// <summary>
        /// For test purposes
        /// </summary>
        public static double CalculateIonMass(TypedMass mass, Adduct adduct)
        {
            return adduct.ApplyToMass(mass);
        }

        /// <summary>
        /// Parses a chemical formula expressed as "[{atom}[count][spaces]]*",
        /// e.g. "C6H11ON", where supported atoms are H, O, N, C, S or P, etc.
        /// returning the total mass for the formula.
        ///
        /// Simple formula math like "C12H5-C3H2" is supported.
        /// 
        /// </summary>
        /// <param name="formula">Input description, and remaining string after parsing</param>
        /// <param name="molReturn">Returns the atoms and counts</param>
        /// <returns>Total mass of formula parsed</returns>
        public TypedMass ParseFormulaMass(string formula, out ParsedMolecule molReturn)
        {
            molReturn = ParsedMolecule.Create(formula);
            return CalculateMass(molReturn.Molecule) + molReturn.GetMassOffset(MassType);
        }

        public TypedMass CalculateMass(IDictionary<string, int> desc)
        {
            double totalMass = 0;
            var isHeavy = false;
            foreach (var elementCount in desc)
            {
                // Stop if unrecognized atom found.
                if (!_atomicMasses.TryGetValue(elementCount.Key, out var massInfo))
                {
                    ThrowArgumentException(elementCount.Key); // Did not parse completely
                }
                totalMass += massInfo._mass * elementCount.Value;
                isHeavy |= massInfo._bHeavy;
            }

            var massType = isHeavy ? (MassType | MassType.bHeavy) : MassType;
            return new TypedMass(totalMass, massType);
        }

        /// <summary>
        /// Get the mass of a single atom.
        /// </summary>
        /// <param name="sym">Character specifying the atom</param>
        /// <returns>The mass of the single atom</returns>
        public double GetMass(string sym)
        {
            if (_atomicMasses.TryGetValue(sym, out var massInfo))
                return massInfo._mass;
            return 0;
        }

        /// <summary>
        /// Adds atomic masses for a symbol character to a look-up table. The monoisotopic mass
        /// is looked up in <see cref="IsotopeAbundances.Default"/>, but the average mass
        /// is hard-coded for backwards compatibility reasons.
        /// </summary>
        /// <param name="sym">Atomic symbol character</param>
        /// <param name="ave">Average mass</param>
        private void AddMass(string sym, double ave)
        {
            var bHeavy = IsSkylineHeavySymbol(sym) || DICT_HEAVYSYMBOL_NICKNAMES.Keys.Any(sym.Equals); // Matches D, T, anything with ' or "

            if (MassType.IsMonoisotopic())
            {
                double monoMass;
                if (IsotopeAbundances.Default.TryGetValue(sym, out var massDistribution))
                {
                    monoMass = massDistribution.MostAbundanceMass;
                }
                else
                {
                    // It's a special element such as H" which is just a single isotope: the mono mass is the average mass
                    monoMass = ave;
                }

                _atomicMasses[sym] = new MassInfo() { _mass = monoMass, _bHeavy = bHeavy };
            }
            else
            {
                _atomicMasses[sym] = new MassInfo() { _mass = ave, _bHeavy = bHeavy };
            }
        }

        /// <summary>
        /// Return true if symbol is found in mass table
        /// </summary>
        /// <param name="sym"></param>
        /// <returns></returns>
        public bool IsKnownSymbol(string sym)
        {
            return _atomicMasses.ContainsKey(sym);
        }
    }
}
