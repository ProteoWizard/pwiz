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
using System.IO;
using System.Linq;
using System.Text;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Helpers for identifying amin acid characters.
    /// </summary>
    public static class AminoAcid
    {
        public static bool IsAA(char c)
        {
            // Not L10N
            switch (c)
            {
                case 'A':
                case 'C':
                case 'D':
                case 'E':
                case 'F':
                case 'G':
                case 'H':
                case 'I':
                case 'K':
                case 'L':
                case 'M':
                case 'N':
                case 'O':   // Pyrrolysine
                case 'P':
                case 'Q':
                case 'R':
                case 'S':
                case 'T':
                case 'U':   // Selenocysteine
                case 'V':
                case 'W':
                case 'Y':
                    return true;
                default:
                    return false;
            }
        }

        public static IEnumerable<char> All
        {
            get
            {
                for (char aa = 'A'; aa <= 'Z'; aa++)
                {
                    if (IsAA(aa))
                        yield return aa;
                }
            }
        }

        public static bool IsExAA(char c)
        {
            if (IsAA(c))
                return true;

            // Indeterminate symbols
            switch (c)
            {
                // Not L10N
                case 'B':   // Aspartic acid or Asparagine
                // TODO: Should J be allowed?
                case 'J':
                case 'X':   // Any
                case 'Z':   // Glutamic acid or Glutamine
                    return true;
                default:
                    return false;
            }
        }

        public static void ValidateAAList(IEnumerable<char> seq)
        {
            HashSet<char> seen = new HashSet<char>();
            foreach (char c in seq)
            {
                if (!IsAA(c))
                    throw new InvalidDataException(string.Format(Resources.AminoAcid_ValidateAAList_Invalid_amino_acid__0__found_in_the_value__1__, c, seq));
                if (seen.Contains(c))
                    throw new InvalidDataException(string.Format(Resources.AminoAcid_ValidateAAList_The_amino_acid__0__is_repeated_in_the_value__1__, c, seq));
                seen.Add(c);
            }
        }

        public static int ToIndex(char c)
        {
            return c - 'A'; // Not L10N
        }

        public static int Count(string seq, params char[] aas)
        {
            return seq.Count(aas.Contains);            
        }
    }

    /// <summary>
    /// Mass calculator for amino acid sequences.
    /// </summary>
    public class SequenceMassCalc : IPrecursorMassCalc, IFragmentMassCalc
    {
        public static int MassPrecision { get { return 6; } }
        public static double MassTolerance { get { return 1e-6; } }

        /// <summary>
        /// Average mass of an amino acid from 
#pragma warning disable 1570 // invalid character (&) in XML comment, and this URL doesn't work if we replace "&" with "&amp;"
        /// http://www.sciencedirect.com/science?_ob=ArticleURL&_udi=B6TH2-3VXYTSN-G&_user=582538&_rdoc=1&_fmt=&_orig=search&_sort=d&view=c&_acct=C000029718&_version=1&_urlVersion=0&_userid=582538&md5=ee0d1eba6e6c7e34d031d85ce9613eec
#pragma warning restore 1570
        /// </summary>
        public static double MassAveragine { get { return 111.1254; } }

        public const double MASS_PEPTIDE_INTERVAL = 1.00045475;

        public static double GetPeptideInterval(int? massShift)
        {
            return massShift.HasValue ? massShift.Value*MASS_PEPTIDE_INTERVAL : 0.0;
        }

        /// <summary>
        /// Returns a mass + H value that has been correctly rounded,
        /// to allow it to be persisted to XML that can be reloaded,
        /// and saved again without change.
        /// </summary>
        /// <param name="mh">Initial high-precision mass + h value</param>
        /// <returns>Rounded mass + h value</returns>
        public static double PersistentMH(double mh)
        {
            return PersistentNeutral(mh) + BioMassCalc.MassProton;
        }

        /// <summary>
        /// Returns a neutral mass rounded for output to XML.  The
        /// initial mass + h value should have come from a call to
        /// PersistentMH in order for this value to be reloaded and
        /// saved again without change.
        /// </summary>
        /// <param name="mh">Initial mass + h</param>
        /// <returns>Rounded neutral mass value</returns>
        public static double PersistentNeutral(double mh)
        {
            return Math.Round(mh - BioMassCalc.MassProton, MassPrecision);            
        }

        /// <summary>
        /// Returns a m/z value rounded for output to XML.
        /// </summary>
        /// <param name="mz">Initial m/z value</param>
        /// <returns>Rounded m/z value</returns>
        public static double PersistentMZ(double mz)
        {
            return Math.Round(mz, MassPrecision);
        }

        public static double GetMZ(double massH, int charge)
        {
            return (massH + (charge - 1)*BioMassCalc.MassProton)/charge;
        }

        public static double GetMH(double mz, int charge)
        {
            return mz*charge - (charge - 1)*BioMassCalc.MassProton;
        }

        public static double GetPpm(double mz, double deltaMz)
        {
            return deltaMz*1000*1000/mz;
        }

        public static double ParseModMass(BioMassCalc calc, string desc)
        {
            string parse = desc;
            double totalMass = calc.ParseMass(ref parse);
            if (parse.Length > 0 && parse[0] == '-') // Not L10N
            {
                parse = parse.Substring(1);
                totalMass -= calc.ParseMass(ref parse);
            }

            if (totalMass == 0.0 || parse.Length > 0)
                calc.ThrowArgumentException(desc);

            return Math.Round(totalMass, MassPrecision);
        }

        public static string[] ParseModParts(BioMassCalc calc, string desc)
        {
            string parse = desc;
            calc.ParseMass(ref parse);

            string part1 = desc.Substring(0, desc.Length - parse.Length).Trim();
            string part2 = string.Empty;

            if (parse.Length > 0 && parse[0] == '-') // Not L10N
            {
                parse = parse.Substring(1);
                part2 = parse.Trim();

                calc.ParseMass(ref parse);
            }

            if ((part1.Length == 0 && part2.Length == 0) || parse.Length > 0)
                calc.ThrowArgumentException(desc);

            return new[] { part1, part2 };
        }

        public void ParseModCounts(string desc, IDictionary<string, int> dictAtomCounts)
        {
            ParseModCounts(_massCalc, desc, dictAtomCounts);
        }

        public static void ParseModCounts(BioMassCalc calc, string desc, IDictionary<string, int> dictAtomCounts)
        {
            string parse = desc;
            calc.ParseCounts(ref parse, dictAtomCounts, false);
            if (parse.Length > 0 && parse[0] == '-') // Not L10N
            {
                parse = parse.Substring(1);
                calc.ParseCounts(ref parse, dictAtomCounts, true);
            }

            if (parse.Length > 0)
                calc.ThrowArgumentException(desc);
        }

        public static string GetModDiffDescription(double massDiff)
        {
            return GetModDiffDescription(massDiff, null, SequenceModFormatType.mass_diff);
        }

        public static string GetModDiffDescription(double massDiff, StaticMod mod, SequenceModFormatType format)
        {
            switch (format)
            {
                case SequenceModFormatType.mass_diff:
                    // Non-narrow format is used for library look-up and must be consistent with LibKey format
                    return string.Format(CultureInfo.InvariantCulture, "[{0}{1:F01}]", (massDiff > 0 ? "+" : string.Empty), massDiff); // Not L10N
                case SequenceModFormatType.mass_diff_narrow:
                    // Narrow format allows for removal of .0 when decimal is not present
                    // One of the more important cases is 15N labeling which produces a lot of
                    // [+1] and [+2] values.  Also assumed to be for UI, so use local format.
                    return string.Format("[{0}{1}]", (massDiff > 0 ? "+" : string.Empty), Math.Round(massDiff, 1)); // Not L10N
                case SequenceModFormatType.three_letter_code:
                    var shortName = mod.ShortName;
                    if (string.IsNullOrEmpty(shortName))
                    {
                        bool isStructural;
                        var foundMod = UniMod.GetModification(mod.Name, out isStructural);
                        if (foundMod != null)
                            shortName = foundMod.ShortName;
                    }
                    return shortName != null
                        ? string.Format("[{0}]", shortName) // Not L10N
                        : GetModDiffDescription(massDiff, null, SequenceModFormatType.mass_diff_narrow);
                default:
                    throw new ArgumentOutOfRangeException("format"); // Not L10N
            }
        }

        public static string GetMassIDescripion(int massIndex)
        {
            return string.Format("[M{0}{1}]", massIndex > 0 ? "+" : string.Empty, massIndex); // Not L10N 
        }

        private readonly BioMassCalc _massCalc;
        public readonly double[] _aminoMasses = new double[128];

        private sealed class ModMasses
        {
            public readonly double[] _aminoModMasses = new double[128];
            public readonly double[] _aminoNTermModMasses = new double[128];
            public readonly double[] _aminoCTermModMasses = new double[128];
            public double _massModCleaveC;
            public double _massModCleaveN;

            // Formula help
            public readonly double[] _aminoModMassesExtra = new double[128];
            public readonly double[] _aminoNTermModMassesExtra = new double[128];
            public readonly double[] _aminoCTermModMassesExtra = new double[128];
            public double _massModCleaveCExtra;
            public double _massModCleaveNExtra;
            public readonly string[] _aminoModFormulas = new string[128];
            public readonly string[] _aminoNTermModFormulas = new string[128];
            public readonly string[] _aminoCTermModFormulas = new string[128];
            public string _massModCleaveCFormula;
            public string _massModCleaveNFormula;
        }

        /// <summary>
        /// All summed modifications for this calculator
        /// </summary>
        private readonly ModMasses _modMasses = new ModMasses();

        /// <summary>
        /// Heavy modifications only, for use with explicit modifications,
        /// which have explicit light modifications but rely on default heavy
        /// modifications
        /// </summary>
        private ModMasses _modMassesHeavy;

        // private readonly double _massWater;
        // private readonly double _massAmmonia;
        private readonly double _massDiffA;
        private readonly double _massDiffB;
        private readonly double _massDiffC;
        private readonly double _massDiffX;
        private readonly double _massDiffY;
        private readonly double _massDiffZ;
        private readonly double _massCleaveC;
        private readonly double _massCleaveN;

        // For mass distributions
        private readonly double _massResolution;
        private readonly double _minimumAbundance;

        public SequenceMassCalc(MassType type)
        {
            _massCalc = new BioMassCalc(type);

            // Not L10N

            // Mass of a proton, i.e. +1 positive charge, hydrogen atom without its electron.
            // See http://antoine.frostburg.edu/chem/senese/101/atoms/index.shtml
            // _massWater = _massCalc.CalculateMass("H2O");
            // _massAmmonia = _massCalc.CalculateMass("NH3");

            // ReSharper disable NonLocalizedString
            _massDiffA = -_massCalc.CalculateMassFromFormula("CO");
            _massDiffB = 0.0;
            _massDiffC = _massCalc.CalculateMassFromFormula("NH3");
            _massDiffY = _massCalc.CalculateMassFromFormula("H2O");
            _massDiffX = _massCalc.CalculateMassFromFormula("CO2");
            _massDiffZ = _massDiffY - _massCalc.CalculateMassFromFormula("NH2");

            _massCleaveN = _massCalc.CalculateMassFromFormula("H");
            _massCleaveC = _massCalc.CalculateMassFromFormula("OH");
            // ReSharper restore NonLocalizedString

            // These numbers are set intentionally smaller than any known instrument
            // can measure.  Filters are then applied to resulting distributions
            // to get more useful numbers.
            _massResolution = 0.001;
            _minimumAbundance = 0.00001;    // 0.001%

            InitAminoAcidMasses();
        }

        public double ParseModMass(string formula)
        {
            return ParseModMass(_massCalc, formula);
        }

        public double GetModMass(char aa, StaticMod mod)
        {
            if (_massCalc.MassType == MassType.Monoisotopic)
            {
                if (mod.MonoisotopicMass.HasValue)
                    return mod.MonoisotopicMass.Value;
            }
            else
            {
                if (mod.AverageMass.HasValue)
                    return mod.AverageMass.Value;
            }
            if (!string.IsNullOrEmpty(mod.Formula))
                return ParseModMass(mod.Formula);
            else if (mod.LabelAtoms != LabelAtoms.None && AminoAcid.IsAA(aa))
                return ParseModMass(GetHeavyFormula(aa, mod.LabelAtoms));
            return 0;
        }

        public string GetModFormula(char aa, StaticMod mod, out double unexplainedMass)
        {
            unexplainedMass = 0;
            if (!string.IsNullOrEmpty(mod.Formula))
                return mod.Formula;
            else if (mod.LabelAtoms != LabelAtoms.None)
                return GetHeavyFormula(aa, mod.LabelAtoms);
            if (_massCalc.MassType == MassType.Monoisotopic)
            {
                if (mod.MonoisotopicMass.HasValue)
                    unexplainedMass = mod.MonoisotopicMass.Value;
            }
            else
            {
                if (mod.AverageMass.HasValue)
                    unexplainedMass = mod.AverageMass.Value;
            }
            return null;
        }

        public void AddStaticModifications(IEnumerable<StaticMod> mods)
        {
            AddModifications(mods, _modMasses);
        }

        public void AddHeavyModifications(IEnumerable<StaticMod> mods)
        {
            var modsArray = mods.ToArray(); // Avoid multiple iteration

            AddModifications(modsArray, _modMasses);

            _modMassesHeavy = new ModMasses();
            AddModifications(modsArray, _modMassesHeavy);
        }

        private void AddModifications(IEnumerable<StaticMod> mods, ModMasses modMasses)
        {
            foreach (StaticMod mod in mods)
            {
                if (mod.AAs == null)
                {
                    if (mod.Terminus != null)
                    {
                        double mass = GetModMass('\0', mod); // Not L10N
                        double unexplainedMass;
                        string formula = GetModFormula('\0', mod, out unexplainedMass); // Not L10N
                        if (mod.Terminus == ModTerminus.C)
                        {
                            modMasses._massModCleaveC += mass;
                            modMasses._massModCleaveCExtra += unexplainedMass;
                            modMasses._massModCleaveCFormula = CombineFormulas(modMasses._massModCleaveCFormula, formula);
                        }
                        else
                        {
                            modMasses._massModCleaveN += mass;
                            modMasses._massModCleaveNExtra += unexplainedMass;
                            modMasses._massModCleaveNFormula = CombineFormulas(modMasses._massModCleaveNFormula, formula);
                        }
                    }
                    else
                    {
                        // Label all amino acids with this label
                        for (char aa = 'A'; aa <= 'Z'; aa++) // Not L10N
                        {
                            if (AMINO_FORMULAS[aa] != null)
                                AddMod(aa, mod, modMasses._aminoModMasses, modMasses._aminoModMassesExtra, modMasses._aminoModFormulas);
                        }
                    }
                }
                else
                {
                    foreach (var aa in mod.AminoAcids)
                    {
                        switch (mod.Terminus)
                        {
                            default:
                                AddMod(aa, mod, modMasses._aminoModMasses, modMasses._aminoModMassesExtra, modMasses._aminoModFormulas);
                                break;
                            case ModTerminus.N:
                                AddMod(aa, mod, modMasses._aminoNTermModMasses, modMasses._aminoNTermModMassesExtra, modMasses._aminoNTermModFormulas);
                                break;
                            case ModTerminus.C:
                                AddMod(aa, mod, modMasses._aminoCTermModMasses, modMasses._aminoCTermModMassesExtra, modMasses._aminoCTermModFormulas);
                                break;
                        }
                    }
                }
            }            
        }

        private void AddMod(char aa, StaticMod mod, double[] modMasses, double[] modMassesExtra, string[] modFormulas)
        {
            modMasses[aa] = modMasses[char.ToLowerInvariant(aa)] += GetModMass(aa, mod);
            
            // Deal with formulas and unexplained masses
            double unexplainedMass;
            string formula = GetModFormula(aa, mod, out unexplainedMass);
            modFormulas[aa] = modFormulas[char.ToLowerInvariant(aa)] = CombineFormulas(modFormulas[aa], formula);
            modMassesExtra[aa] = modMassesExtra[char.ToLowerInvariant(aa)] += unexplainedMass;
        }

        private string CombineFormulas(string formula1, string formula2)
        {
            if (formula1 == null)
                return formula2;
            if (formula2 == null)
                return formula1;

            var parts1 = ParseModParts(_massCalc, formula1);
            var parts2 = ParseModParts(_massCalc, formula2);

            var sb = new StringBuilder();
            sb.Append(parts1[0]).Append(parts2[0]);
            if (parts1[1].Length > 0 || parts2[1].Length > 0)
                sb.Append('-').Append(parts1[1]).Append(parts2[1]);
            return sb.ToString();
        }

        public bool IsModified(string seq)
        {
            if (string.IsNullOrEmpty(seq))
                return false;
            if (_modMasses._massModCleaveC + _modMasses._massModCleaveN != 0)
                return true;
            int len = seq.Length;
            if (_modMasses._aminoNTermModMasses[seq[0]] + _modMasses._aminoCTermModMasses[seq[len - 1]] != 0)
                return true;
            return seq.Any(c => _modMasses._aminoModMasses[c] != 0);
        }

        public string GetModifiedSequence(string seq, bool formatNarrow)
        {
            return GetModifiedSequence(seq, null, formatNarrow);
        }

        public string GetModifiedSequence(string seq, SequenceModFormatType format, bool useExplicitModsOnly)
        {
            return GetModifiedSequence(seq, null, format, useExplicitModsOnly);
        }

        public string GetModifiedSequence(string seq, ExplicitSequenceMods mods, bool formatNarrow)
        {
            var format = formatNarrow ? SequenceModFormatType.mass_diff_narrow : SequenceModFormatType.mass_diff;
            return GetModifiedSequence(seq, mods, format, false);
        }
        
        public string GetModifiedSequence(string seq, ExplicitSequenceMods mods, SequenceModFormatType format, bool useExplicitModsOnly)
        {
            // If no modifications, do nothing
            if (!IsModified(seq) && mods == null)
                return seq;

            // Otherwise, build a modified sequence string like AMC[+57.0]LP[-37.1]K
            StringBuilder sb = new StringBuilder();
            for (int i = 0, len = seq.Length; i < len; i++)
            {
                char c = seq[i];
                var modMass = GetAAModMass(c, i, len, mods);
                sb.Append(c);
                if (modMass != 0)
                {
                    StaticMod mod = mods != null ? mods.FindFirstMod(i) : null;
                    if (mod == null && useExplicitModsOnly) 
                        continue;

                    sb.Append(GetModDiffDescription(modMass, mod, format));
                }
            }
            return sb.ToString();
        }

        public static string NormalizeModifiedSequence(string rawModifiedSequence)
        {
            var normalizedSeq = new StringBuilder();
            int ichLast = 0;
            for (int ichOpenBracket = rawModifiedSequence.IndexOf('[');
                 ichOpenBracket >= 0;
                 ichOpenBracket = rawModifiedSequence.IndexOf('[', ichOpenBracket + 1))
            {
                int ichCloseBracket = rawModifiedSequence.IndexOf(']', ichOpenBracket);
                if (ichCloseBracket < 0)
                {
                    throw new ArgumentException(string.Format(Resources.SequenceMassCalc_NormalizeModifiedSequence_Modification_definition__0__missing_close_bracket_, rawModifiedSequence.Substring(ichOpenBracket)));
                }
                string strMassDiff = rawModifiedSequence.Substring(ichOpenBracket + 1, ichCloseBracket - ichOpenBracket - 1);
                double massDiff;
                // Try parsing with both invariant culture and current number format
                const NumberStyles numStyle = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.Integer; // Don't allow thousands
                if (!double.TryParse(strMassDiff, numStyle, CultureInfo.InvariantCulture, out massDiff) &&
                    !double.TryParse(strMassDiff, numStyle, CultureInfo.CurrentCulture, out massDiff))
                {
                    throw new ArgumentException(string.Format(Resources.SequenceMassCalc_NormalizeModifiedSequence_The_modification__0__is_not_valid___Expected_a_numeric_delta_mass_, strMassDiff));
                }
                normalizedSeq.Append(rawModifiedSequence.Substring(ichLast, ichOpenBracket - ichLast));
                normalizedSeq.Append(GetModDiffDescription(massDiff, null, SequenceModFormatType.mass_diff));
                ichLast = ichCloseBracket + 1;
            }
            normalizedSeq.Append(rawModifiedSequence.Substring(ichLast));
            string result = normalizedSeq.ToString();
            // Keep original string, if not changed
            Helpers.AssignIfEquals(ref result, rawModifiedSequence);
            return result;
        }

        public double GetAAModMass(char aa, int seqIndex, int seqLength)
        {
            return GetAAModMass(aa, seqIndex, seqLength, null);
        }

        public double GetAAModMass(char aa, int seqIndex, int seqLength, ExplicitSequenceMods mods)
        {
            var modMasses = GetModMasses(mods);
            double mod = modMasses._aminoModMasses[aa];
            // Explicit modifications
            if (mods != null && seqIndex < mods.ModMasses.Count)
                mod += mods.ModMasses[seqIndex];
            // Terminal modifications
            if (seqIndex == 0)
                mod += modMasses._massModCleaveN + modMasses._aminoNTermModMasses[aa];
            else if (seqIndex == seqLength - 1)
                mod += modMasses._massModCleaveC + modMasses._aminoCTermModMasses[aa];
            return mod;

        }

        public MassDistribution GetMzDistribution(string seq, int charge, IsotopeAbundances abundances)
        {
            return GetMzDistribution(seq, charge, abundances, null);
        }

        public MassDistribution GetMzDistribution(string seq, int charge, IsotopeAbundances abundances, ExplicitSequenceMods mods)
        {
            double unexplainedMass;
            Molecule molecule = GetFormula(seq, mods, out unexplainedMass);
            return GetMzDistribution(molecule, charge, abundances, unexplainedMass, true);
        }

        public MassDistribution GetMZDistributionFromFormula(string formula, int charge, IsotopeAbundances abundances)
        {
            Molecule molecule = Molecule.Parse(formula);
            return GetMzDistribution(molecule, charge, abundances, 0, false);
        }

        public MassDistribution GetMZDistributionSinglePoint(double mz, int charge)
        {
            return MassDistribution.NewInstance(new SortedDictionary<double, double> {{mz, charge}}, _massResolution, _minimumAbundance);
        }

        public string GetIonFormula(string peptideSequence, int charge)
        {
           return  GetIonFormula(peptideSequence, charge, null);
        }

        /// <summary>
        /// Convert a charged peptide to a small molecule ion formula
        /// </summary>
        public string GetIonFormula(string seq, int charge, ExplicitSequenceMods mods)
        {
            double unexplainedMass;
            var molecule = GetFormula(seq, mods, out unexplainedMass);
            if (unexplainedMass != 0.0)
                throw new ArgumentException("Unexplained mass when deriving ion formula from sequence "+seq); // Not L10N
            int chargeStep = (charge > 0) ? 1 : -1;  // Negative charge on a peptide is not a real world concern, but useful for test purposes
            for (int z = 0; z < Math.Abs(charge); z++)
                molecule = molecule.SetElementCount("H", molecule.GetElementCount("H") + chargeStep); // Not L10N
            return molecule.ToString();
        }

// ReSharper disable once ParameterTypeCanBeEnumerable.Local
        private MassDistribution GetMzDistribution(Molecule molecule, int charge, IsotopeAbundances abundances, double unexplainedMass, bool isMassH)
        {
            // Low resolution to get back only peaks at Dalton (i.e. neutron) boundaries
            var md = new MassDistribution(_massResolution, _minimumAbundance);
            var result = md;
            foreach (var element in molecule)
            {
                result = result.Add(md.Add(abundances[element.Key]).Multiply(element.Value));
            }
            return result.OffsetAndDivide(unexplainedMass + charge* (isMassH ? BioMassCalc.MassProton : -BioMassCalc.MassElectron), charge);
        }

        private Molecule GetFormula(string seq, ExplicitSequenceMods mods, out double unexplainedMass)
        {
            var formula = new FormulaBuilder(_massCalc);
            var modMasses = GetModMasses(mods);
            formula.Append(modMasses._massModCleaveNFormula, modMasses._massModCleaveNExtra);
            formula.Append(modMasses._massModCleaveCFormula, modMasses._massModCleaveCExtra);
            for (int i = 0, len = seq.Length; i < len; i++)
            {
                char c = seq[i];

                formula.Append(AMINO_FORMULAS[c])
                       .Append(modMasses._aminoModFormulas[c], modMasses._aminoModMassesExtra[c]);
                // Terminal modifications
                if (i == 0)
                    formula.Append(modMasses._aminoNTermModFormulas[c], modMasses._aminoNTermModMassesExtra[c]);
                else if (i == len - 1)
                    formula.Append(modMasses._aminoCTermModFormulas[c], modMasses._aminoCTermModMassesExtra[c]);
            }
            if (mods != null)
            {
                foreach (ExplicitMod mod in mods.AllMods)
                {
                    double modUnexplainedMass;
                    string modFormula = GetModFormula(seq[mod.IndexAA], mod.Modification, out modUnexplainedMass);
                    formula.Append(modFormula, modUnexplainedMass);
                }
            }
            formula.Append("H2O"); // N-term = H, C-term = OH // Not L10N
            unexplainedMass = formula.UnexplainedMass;
            // CONSIDER: More efficient translation between builder and Molecure.
            //           Both contain a dictionary for atom counts.
            return Molecule.Parse(formula.ToString());
        }

        private sealed class FormulaBuilder
        {
            private readonly BioMassCalc _massCalc;
            private readonly Dictionary<string, int> _dictAtomCounts;
            private double _unexplainedMass;

            public FormulaBuilder(BioMassCalc massCalc)
            {
                _massCalc = massCalc;
                _dictAtomCounts = new Dictionary<string, int>();
            }

            public FormulaBuilder Append(string formula, double unexplainedMass = 0)
            {
                _unexplainedMass += unexplainedMass;
                if (formula != null)
                    ParseModCounts(_massCalc, formula, _dictAtomCounts);
                return this;
            }

            /// <summary>
            /// Returns any accumulated unexplained mass, plus the mass of any atoms with
            /// negative counts.
            /// </summary>
            public double UnexplainedMass
            {
                get
                {
                    double unexplainedMass = _unexplainedMass;
                    foreach (var atomCount in _dictAtomCounts.Where(p => p.Value < 0))
                    {
                        unexplainedMass += _massCalc.CalculateMassFromFormula(atomCount.Key + (-atomCount.Value));
                    }
                    return unexplainedMass;
                }
            }

            public override string ToString()
            {
                var formulaText = new StringBuilder();
                foreach (var atomCount in _dictAtomCounts.OrderBy(p => p.Key).Where(p => p.Value > 0))
                {
                    formulaText.Append(atomCount.Key);
                    if (atomCount.Value > 1)
                        formulaText.Append(atomCount.Value);
                }
                return formulaText.ToString();
            }
        }

        private ModMasses GetModMasses(ExplicitSequenceMods mods)
        {
            // If there are explicit modifications and this is a heavy mass
            // calculator, then use only the heavy masses without the static
            // masses added in.
            if (mods != null && !mods.RequiresAllCalcMods && _modMassesHeavy != null)
                return _modMassesHeavy;
            return _modMasses;
        }

        public MassType MassType
        {
            get { return _massCalc.MassType; }
        }

        public double GetPrecursorMass(CustomIon customIon)
        {
            return (MassType == MassType.Average)
                ? customIon.AverageMass
                : customIon.MonoisotopicMass;
        }

        public double GetPrecursorMass(string seq)
        {
            return GetPrecursorMass(seq, null);
        }

        public double GetPrecursorMass(string seq, ExplicitSequenceMods mods)
        {
            var modMasses = GetModMasses(mods);
            double mass = _massCleaveN + modMasses._massModCleaveN +
                _massCleaveC + modMasses._massModCleaveC + BioMassCalc.MassProton;

            // Add any amino acid terminal specific modifications
            int len = seq.Length;
            if (len > 0)
                mass += modMasses._aminoNTermModMasses[seq[0]];
            if (len > 1)
                mass += modMasses._aminoCTermModMasses[seq[len - 1]];

            // Add masses of amino acids
            for (int i = 0; i < len; i++)
            {
                char c = seq[i];
                mass += _aminoMasses[c] + modMasses._aminoModMasses[c];
                if (mods != null && i < mods.ModMasses.Count)
                    mass += mods.ModMasses[i];
            }

            return mass;                
        }

        public double[,] GetFragmentIonMasses(string seq)
        {
            return GetFragmentIonMasses(seq, null);
        }

        public double[,] GetFragmentIonMasses(string seq, ExplicitSequenceMods mods)
        {
            var modMasses = GetModMasses(mods);

            int len = seq.Length - 1;
            const int a = (int) IonType.a;
            const int b = (int) IonType.b;
            const int c = (int) IonType.c;
            const int x = (int) IonType.x;
            const int y = (int) IonType.y;
            const int z = (int) IonType.z;

            double nTermMassB = _massDiffB + modMasses._massModCleaveN + BioMassCalc.MassProton;
            double deltaA = _massDiffA - _massDiffB;
            double deltaC = _massDiffC - _massDiffB;
            double cTermMassY = _massDiffY + modMasses._massModCleaveC + BioMassCalc.MassProton;
            double deltaX = _massDiffX - _massDiffY;
            double deltaZ = _massDiffZ - _massDiffY;

            double[,] masses = new double[z + 1, len];

            int iN = 0, iC = len;
            nTermMassB += modMasses._aminoNTermModMasses[seq[iN]];
            cTermMassY += modMasses._aminoCTermModMasses[seq[iC]];
            while (iC > 0)
            {
                char aa = seq[iN];
                nTermMassB += _aminoMasses[aa] + modMasses._aminoModMasses[aa];
                if (mods != null && iN < mods.ModMasses.Count)
                    nTermMassB += mods.ModMasses[iN];
                masses[a, iN] = nTermMassB + deltaA;
                masses[b, iN] = nTermMassB;
                masses[c, iN] = nTermMassB + deltaC;
                iN++;

                aa = seq[iC];
                cTermMassY += _aminoMasses[aa] + modMasses._aminoModMasses[aa];
                if (mods != null && iC < mods.ModMasses.Count)
                    cTermMassY += mods.ModMasses[iC];
                iC--;
                masses[x, iC] = cTermMassY + deltaX;
                masses[y, iC] = cTermMassY;
                masses[z, iC] = cTermMassY + deltaZ;
            }

            return masses;
        }

        public static IEnumerable<double> GetFragmentMasses(IonType type, double[,] masses)
        {
            int col = (int) type;
            int len = masses.GetLength(1);
            if (Transition.IsNTerminal(type))
            {
                for (int i = 0; i < len; i++)
                    yield return masses[col, i];
            }
            else
            {
                for (int i = len - 1; i >= 0; i--)
                    yield return masses[col, i];
            }
        }

        public double GetFragmentMass(Transition transition, IsotopeDistInfo isotopeDist)
        {
            return GetFragmentMass(transition, isotopeDist, null);
        }

        public double GetFragmentMass(Transition transition, IsotopeDistInfo isotopeDist, ExplicitSequenceMods mods)
        {
            if (transition.IsCustom())
            {
                var type = transition.IonType;
                var massIndex = transition.MassIndex;
                if (Transition.IsPrecursor(type) && (isotopeDist != null))
                {
                    var i = isotopeDist.MassIndexToPeakIndex(massIndex);
                    if (0 > i || i >= isotopeDist.CountPeaks)
                    {
                        throw new IndexOutOfRangeException(
                            string.Format(Resources.SequenceMassCalc_GetFragmentMass_Precursor_isotope__0__is_outside_the_isotope_distribution__1__to__2__,
                                            GetMassIDescripion(massIndex), isotopeDist.PeakIndexToMassIndex(0),
                                            isotopeDist.PeakIndexToMassIndex(isotopeDist.CountPeaks - 1)));
                    }
                    return isotopeDist.GetMassI(massIndex);
                }
                return (MassType == MassType.Average) 
                        ? transition.CustomIon.AverageMass
                        : transition.CustomIon.MonoisotopicMass;
            }

            return GetFragmentMass(transition.Group.Peptide.Sequence,
                                   transition.IonType,
                                   transition.Ordinal,
                                   transition.DecoyMassShift,
                                   transition.MassIndex,
                                   isotopeDist,
                                   mods);
        }

        public double GetPrecursorFragmentMass(string seq)
        {
            return GetPrecursorFragmentMass(seq, null);
        }

        public double GetPrecursorFragmentMass(string seq, ExplicitSequenceMods mods)
        {
            return GetFragmentMass(seq, IonType.precursor, seq.Length, null, 0, null, mods);
        }

        private double GetFragmentMass(string seq,
                                       IonType type,
                                       int ordinal,
                                       int? decoyMassShift,
                                       int massIndex,
                                       IsotopeDistInfo isotopeDists,
                                       ExplicitSequenceMods mods)
        {
            if (Transition.IsPrecursor(type))
            {
                if (isotopeDists != null)
                {
                    int i = isotopeDists.MassIndexToPeakIndex(massIndex);
                    if (0 > i || i >= isotopeDists.CountPeaks)
                    {
                        throw new IndexOutOfRangeException(
                            string.Format(Resources.SequenceMassCalc_GetFragmentMass_Precursor_isotope__0__is_outside_the_isotope_distribution__1__to__2__,
                                          GetMassIDescripion(massIndex), isotopeDists.PeakIndexToMassIndex(0),
                                          isotopeDists.PeakIndexToMassIndex(isotopeDists.CountPeaks - 1)));
                    }
                    return isotopeDists.GetMassI(massIndex, decoyMassShift);
                }
                return GetPrecursorMass(seq, mods);                
            }

            int len = seq.Length - 1;

            bool nterm = Transition.IsNTerminal(type);
            double mass = GetTermMass(nterm ? IonType.b : IonType.y, mods) + BioMassCalc.MassProton;

            int iA = (nterm ? 0 : len);
            int inc = (nterm ? 1 : -1);

            var modMasses = GetModMasses(mods);

            mass += (nterm ? modMasses._aminoNTermModMasses[seq[iA]] : modMasses._aminoCTermModMasses[seq[iA]]);

            for (int i = 0; i < ordinal; i++)
            {
                char aa = seq[iA];
                mass += _aminoMasses[aa] + modMasses._aminoModMasses[aa];
                if (mods != null && iA < mods.ModMasses.Count)
                    mass += mods.ModMasses[iA];
                iA += inc;
            }

            mass += GetTermDeltaMass(type);    // Exactly match GetFragmentIonMasses()

            return mass;
        }

        private double GetTermMass(IonType type, ExplicitSequenceMods mods)
        {
            var modMasses = GetModMasses(mods);

            switch (type)
            {
                case IonType.a: return _massDiffA + modMasses._massModCleaveN;
                case IonType.b: return _massDiffB + modMasses._massModCleaveN;
                case IonType.c: return _massDiffC + modMasses._massModCleaveN;
                case IonType.x: return _massDiffX + modMasses._massModCleaveC;
                case IonType.y: return _massDiffY + modMasses._massModCleaveC;
                case IonType.z: return _massDiffZ + modMasses._massModCleaveC;
                default:
                    throw new ArgumentException("Invalid ion type"); // Not L10N
            }
        }

        private double GetTermDeltaMass(IonType type)
        {
            switch (type)
            {
                case IonType.a: return _massDiffA - _massDiffB;
                case IonType.b: return 0;
                case IonType.c: return _massDiffC - _massDiffB;
                case IonType.x: return _massDiffX - _massDiffY;
                case IonType.y: return 0;
                case IonType.z: return _massDiffZ - _massDiffY;
                default:
                    throw new ArgumentException("Invalid ion type"); // Not L10N
            }
        }

        /// <summary>
        /// Initializes the masses for amino acid characters in the mass look-up.
        /// <para>
        /// See Wikipedia FASTA Format page for details:
        /// http://en.wikipedia.org/wiki/FASTA_format#Sequence_identifiers
        /// </para>
        /// </summary>
        private void InitAminoAcidMasses()
        {
            for (int i = 0; i < AMINO_FORMULAS.Length; i++)
            {
                string formula = AMINO_FORMULAS[i];
                if (formula != null)
                    _aminoMasses[i] = _massCalc.CalculateMassFromFormula(formula);
            }

            // Not L10N

            // ReSharper disable CharImplicitlyConvertedToNumeric
            // Handle values for non-amino acids
            // Wikipedia says Aspartic acid or Asparagine
            _aminoMasses['b'] = _aminoMasses['B'] =
                (_massCalc.CalculateMassFromFormula("C4H5NO3") + _massCalc.CalculateMassFromFormula("C4H6N2O2")) / 2; // Not L10N
            _aminoMasses['j'] = _aminoMasses['J'] = 0.0;
            _aminoMasses['x'] = _aminoMasses['X'] = 111.060000;	// Why?
            // Wikipedia says Glutamic acid or Glutamine
            _aminoMasses['z'] = _aminoMasses['Z'] =
                (_massCalc.CalculateMassFromFormula("C5H6ON2") + _massCalc.CalculateMassFromFormula("C5H8N2O2")) / 2; // Not L10N
            // ReSharper restore CharImplicitlyConvertedToNumeric
        }

        private static readonly string[] AMINO_FORMULAS = new string[128];

        static SequenceMassCalc()
        {
            // Not L10N


            // ReSharper disable CharImplicitlyConvertedToNumeric
            // ReSharper disable NonLocalizedString
            AMINO_FORMULAS['a'] = AMINO_FORMULAS['A'] = "C3H5ON";
            AMINO_FORMULAS['c'] = AMINO_FORMULAS['C'] = "C3H5ONS";
            AMINO_FORMULAS['d'] = AMINO_FORMULAS['D'] = "C4H5O3N";
            AMINO_FORMULAS['e'] = AMINO_FORMULAS['E'] = "C5H7O3N";
            AMINO_FORMULAS['f'] = AMINO_FORMULAS['F'] = "C9H9ON";
            AMINO_FORMULAS['g'] = AMINO_FORMULAS['G'] = "C2H3ON";
            AMINO_FORMULAS['h'] = AMINO_FORMULAS['H'] = "C6H7ON3";
            AMINO_FORMULAS['i'] = AMINO_FORMULAS['I'] = "C6H11ON";
            AMINO_FORMULAS['k'] = AMINO_FORMULAS['K'] = "C6H12ON2";
            AMINO_FORMULAS['l'] = AMINO_FORMULAS['L'] = "C6H11ON";
            AMINO_FORMULAS['m'] = AMINO_FORMULAS['M'] = "C5H9ONS";
            AMINO_FORMULAS['n'] = AMINO_FORMULAS['N'] = "C4H6O2N2";
            AMINO_FORMULAS['o'] = AMINO_FORMULAS['O'] = "C12H19N3O2";
            AMINO_FORMULAS['p'] = AMINO_FORMULAS['P'] = "C5H7ON";
            AMINO_FORMULAS['q'] = AMINO_FORMULAS['Q'] = "C5H8O2N2";
            AMINO_FORMULAS['r'] = AMINO_FORMULAS['R'] = "C6H12ON4";
            AMINO_FORMULAS['s'] = AMINO_FORMULAS['S'] = "C3H5O2N";
            AMINO_FORMULAS['t'] = AMINO_FORMULAS['T'] = "C4H7O2N";
            AMINO_FORMULAS['u'] = AMINO_FORMULAS['U'] = "C3H5NOSe";
            AMINO_FORMULAS['v'] = AMINO_FORMULAS['V'] = "C5H9ON";
            AMINO_FORMULAS['w'] = AMINO_FORMULAS['W'] = "C11H10ON2";
            AMINO_FORMULAS['y'] = AMINO_FORMULAS['Y'] = "C9H9O2N";
            // ReSharper restore NonLocalizedString
            // ReSharper restore CharImplicitlyConvertedToNumeric
        }

        public static string GetAminoAcidFormula(char aa)
        {
            return AMINO_FORMULAS[aa];
        }

        public static string GetHeavyFormula(char aa, LabelAtoms labelAtoms)
        {
            string formulaAA = AMINO_FORMULAS[aa];
            if (string.IsNullOrEmpty(formulaAA))
                throw new ArgumentOutOfRangeException(string.Format(Resources.SequenceMassCalc_GetHeavyFormula_No_formula_found_for_the_amino_acid___0__, aa));
            string formulaHeavy = formulaAA;
            if ((labelAtoms & LabelAtoms.C13) != 0)
                formulaHeavy = formulaHeavy.Replace(BioMassCalc.C, BioMassCalc.C13);
            if ((labelAtoms & LabelAtoms.N15) != 0)
                formulaHeavy = formulaHeavy.Replace(BioMassCalc.N, BioMassCalc.N15);
            if ((labelAtoms & LabelAtoms.O18) != 0)
                formulaHeavy = formulaHeavy.Replace(BioMassCalc.O, BioMassCalc.O18);
            if ((labelAtoms & LabelAtoms.H2) != 0)
                formulaHeavy = formulaHeavy.Replace(BioMassCalc.H, BioMassCalc.H2);
            return formulaHeavy + " - " + formulaAA; // Not L10N
        }
    }

    public sealed class TypedMassCalc
    {
        public TypedMassCalc(IsotopeLabelType labelType, SequenceMassCalc massCalc)
        {
            LabelType = labelType;
            MassCalc = massCalc;
        }

        public IsotopeLabelType LabelType { get; private set; }
        public SequenceMassCalc MassCalc { get; private set; }
    }

    public class ExplicitSequenceMassCalc : IPrecursorMassCalc, IFragmentMassCalc
    {
        private readonly SequenceMassCalc _massCalcBase;
        private readonly ExplicitSequenceMods _mods;

        public ExplicitSequenceMassCalc(ExplicitMods mods, SequenceMassCalc massCalcBase, IsotopeLabelType labelType)
        {
            _massCalcBase = massCalcBase;
            _mods = new ExplicitSequenceMods
                { 
                    Mods = mods.GetModifications(labelType),
                    StaticBaseMods = mods.GetStaticBaseMods(labelType),
                    ModMasses = mods.GetModMasses(_massCalcBase.MassType, labelType),
                    RequiresAllCalcMods = mods.IsVariableStaticMods
                };
        }

        public MassType MassType
        {
            get { return _massCalcBase.MassType; }
        }

        public double GetPrecursorMass(CustomIon ion)
        {
            return _massCalcBase.GetPrecursorMass(ion);
        }

        public double GetPrecursorMass(string seq)
        {
            return _massCalcBase.GetPrecursorMass(seq, _mods);
        }

        public bool IsModified(string seq)
        {
            return _massCalcBase.IsModified(seq) ||
                _mods.ModMasses.IndexOf(m => m != 0) != -1; // If any non-zero modification values
        }

        public string GetModifiedSequence(string seq, SequenceModFormatType format, bool useExplicitModsOnly)
        {
            return _massCalcBase.GetModifiedSequence(seq, _mods, format, useExplicitModsOnly);
        }

        public string GetModifiedSequence(string seq, bool formatNarrow)
        {
            return GetModifiedSequence(seq,
                                       formatNarrow
                                           ? SequenceModFormatType.mass_diff_narrow
                                           : SequenceModFormatType.mass_diff,
                                       false);
        }

        public double GetAAModMass(char aa, int seqIndex, int seqLength)
        {
            return _massCalcBase.GetAAModMass(aa, seqIndex, seqLength, _mods);
        }

        public string GetIonFormula(string seq, int charge)
        {
            return _massCalcBase.GetIonFormula(seq, charge, _mods);
        }

        public MassDistribution GetMzDistribution(string seq, int charge, IsotopeAbundances abundances)




        {
            return _massCalcBase.GetMzDistribution(seq, charge, abundances, _mods);
        }

        public MassDistribution GetMZDistributionFromFormula(string formula, int charge, IsotopeAbundances abundances)
        {
            return _massCalcBase.GetMZDistributionFromFormula(formula, charge, abundances);
        }

        public MassDistribution GetMZDistributionSinglePoint(double mz, int charge)
        {
            return  _massCalcBase.GetMZDistributionSinglePoint(mz, charge);
        }

        public double[,] GetFragmentIonMasses(string seq)
        {
            return _massCalcBase.GetFragmentIonMasses(seq, _mods);
        }

        public double GetFragmentMass(Transition transition, IsotopeDistInfo isotopeDist)
        {
            return _massCalcBase.GetFragmentMass(transition, isotopeDist, _mods);
        }

        public double GetPrecursorFragmentMass(string seq)
        {
            return _massCalcBase.GetPrecursorFragmentMass(seq, _mods);
        }
    }

    public class ExplicitSequenceMods
    {
        public IList<ExplicitMod> Mods { get; set; }
        public IList<ExplicitMod> StaticBaseMods { get; set; }
        public IList<double> ModMasses { get; set; }
        public bool RequiresAllCalcMods { get; set; }
        public IEnumerable<ExplicitMod> AllMods
        {
            get
            {
                return (Mods ?? new ExplicitMod[0]).Union(StaticBaseMods ?? new ExplicitMod[0]);
            }
        }
        public StaticMod FindFirstMod(int index)
        {
            var firstOrDefault = AllMods.FirstOrDefault(m => m.IndexAA == index);
            return firstOrDefault != null ? firstOrDefault.Modification : null;
        }
    }
}