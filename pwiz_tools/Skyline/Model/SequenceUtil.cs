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
using pwiz.Common.Collections;
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
            return c - 'A';
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

#pragma warning disable 1570 /// invalid character (&) in XML comment, and this URL doesn't work if we replace "&" with "&amp;"
        /// <summary>
        /// Average mass of an amino acid from 
        /// http://www.sciencedirect.com/science?_ob=ArticleURL&_udi=B6TH2-3VXYTSN-G&_user=582538&_rdoc=1&_fmt=&_orig=search&_sort=d&view=c&_acct=C000029718&_version=1&_urlVersion=0&_userid=582538&md5=ee0d1eba6e6c7e34d031d85ce9613eec
        /// </summary>
#pragma warning restore 1570
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
        public static TypedMass PersistentMH(TypedMass mh)
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
        public static TypedMass PersistentNeutral(TypedMass mh)
        {
            Assume.IsTrue(mh.IsMassH());
            return new TypedMass(Math.Round(mh - BioMassCalc.MassProton, MassPrecision), mh.MassType);
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

        /// <summary>
        /// Returns a m/z value for a mass given an adduct.
        /// </summary>
        public static double GetMZ(TypedMass mass, Adduct adduct)
        {
            return adduct.MzFromNeutralMass(mass);
        }

        public static double GetMZ(TypedMass mass, int charge)
        {
            if (mass.IsMassH())
                return (mass + (charge - 1) * BioMassCalc.MassProton) / Math.Abs(charge);
            else
                return (mass - (charge * BioMassCalc.MassElectron)) /
                       Math.Abs(charge); // As with reporter ions, where charge is built into the formula
        }

        public static TypedMass
            GetMH(double mz, Adduct adduct,
                MassType massType) // CONSIDER(bspratt) internally standardize on mass rather than massH?
        {
            Assume.IsTrue(adduct.IsProtonated, @"Expected a protonated adduct");
            return new TypedMass(mz * adduct.AdductCharge - (adduct.AdductCharge - 1) * BioMassCalc.MassProton,
                massType.IsMonoisotopic() ? MassType.MonoisotopicMassH : MassType.AverageMassH);
        }

        public static double GetMH(double mz, int charge)
        {
            return mz*charge - (charge - 1)*BioMassCalc.MassProton;
        }

        public static double GetPpm(double mz, double deltaMz)
        {
            return deltaMz*1000*1000/mz;
        }

        public static TypedMass FormulaMass(BioMassCalc calc, string desc, int? precision = null)
        {
            string parse = desc;
            double totalMass = calc.ParseMassExpression(ref parse);
            if (totalMass == 0.0 || parse.Length > 0)
                calc.ThrowArgumentException(desc);

            return new TypedMass(precision.HasValue ? Math.Round(totalMass, precision.Value) : totalMass,
                calc.MassType);
        }

        public static string[] ParseModParts(BioMassCalc calc, string desc)
        {
            string parse = desc;
            calc.ParseMass(ref parse);

            string part1 = desc.Substring(0, desc.Length - parse.Length).Trim();
            string part2 = string.Empty;

            if (parse.Length > 0 && parse[0] == '-')
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
            if (parse.Length > 0 && parse[0] == '-')
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
            var precisionRequired = 1;
            if (mod == null && format == SequenceModFormatType.three_letter_code)
                format = SequenceModFormatType.mass_diff_narrow;
            // ReSharper disable FormatStringProblem
            switch (format)
            {
                case SequenceModFormatType.full_precision:
                {
                    return @"[" + MassModification.FromMass(massDiff) + @"]";
                }
                case SequenceModFormatType.lib_precision:
                {
                    return @"[" + MassModification.FromMassForLib(massDiff) + @"]";
                }
                case SequenceModFormatType.mass_diff:
                {
                    string formatString = @"[{0}{1:F0" + precisionRequired + @"}]";
                    // Non-narrow format is used for library look-up and must be consistent with LibKey format
                    return string.Format(CultureInfo.InvariantCulture, formatString,
                        massDiff > 0 ? @"+" : string.Empty, massDiff);
                }
                case SequenceModFormatType.mass_diff_narrow:
                    // Narrow format allows for removal of .0 when decimal is not present
                    // One of the more important cases is 15N labeling which produces a lot of
                    // [+1] and [+2] values.  Also assumed to be for UI, so use local format.
                    return string.Format(CultureInfo.InvariantCulture, @"[{0}{1}]", massDiff > 0 ? @"+" : string.Empty, Math.Round(massDiff, precisionRequired));
                case SequenceModFormatType.three_letter_code:
                    // ReSharper disable once PossibleNullReferenceException
                    var shortName = mod.ShortName;
                    if (string.IsNullOrEmpty(shortName))
                    {
                        bool isStructural;
                        var foundMod = UniMod.GetModification(mod.Name, out isStructural);
                        if (foundMod != null)
                            shortName = foundMod.ShortName;
                    }
                    return shortName != null
                        ? string.Format(@"[{0}]", shortName)
                        : GetModDiffDescription(massDiff, null, SequenceModFormatType.mass_diff_narrow);
                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }
            // ReSharper restore FormatStringProblem
        }

        public static string GetMassIDescripion(int massIndex)
        {
            // CONSIDER(bspratt) this is uncomfortably like an adduct description - change for small mol docs?
            return string.Format(@"[M{0}{1}]", massIndex > 0 ? @"+" : string.Empty, massIndex);
        }

        public double GetAAMass(char c)
        {
            return _aminoMasses[c];
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

        // For internal use only - similar to Molecule class, but this is not immutable and not sorted (for speed)
        private sealed class MoleculeUnsorted
        {
            public Dictionary<string, int> Elements { get; private set; }

            public MoleculeUnsorted(Dictionary<string, int> elements)
            {
                Elements = elements;
            }

            public static MoleculeUnsorted Parse(string formula)
            {
                Molecule ion;
                Adduct adduct;
                string neutralFormula;
                Assume.IsFalse(IonInfo.IsFormulaWithAdduct(formula, out ion, out adduct, out neutralFormula));
                return new MoleculeUnsorted(Molecule.ParseExpressionToDictionary(formula));
            }

            public MoleculeUnsorted SetElementCount(string element, int count)
            {
                if (Elements.ContainsKey(element))
                {
                    Elements[element] = count;
                }
                else
                {
                    Elements.Add(element, count);
                }
                return this;
            }

            public int GetElementCount(string element)
            {
                int count;
                if (Elements.TryGetValue(element, out count))
                {
                    return count;
                }
                return 0;
            }

            public override string ToString()
            {
                var result = new StringBuilder();
                var sortedKeys = Elements.Keys.ToList();
                sortedKeys.Sort();
                foreach (var key in sortedKeys)
                {
                    result.Append(key);
                    var value = Elements[key];
                    if (value != 1)
                    {
                        result.Append(value);
                    }
                }
                return result.ToString();
            }

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

        public HashSet<StaticMod> Labels { get; private set; }

        public bool HasLabels
        {
            get { return Labels != null && Labels.Any(); }
        }

        // private readonly double _massWater;
        // private readonly double _massAmmonia;
        private readonly TypedMass _massDiffA;

        private readonly TypedMass _massDiffB;
        private readonly TypedMass _massDiffC;
        private readonly TypedMass _massDiffX;
        private readonly TypedMass _massDiffY;
        private readonly TypedMass _massDiffZ;
        private readonly TypedMass _massDiffZH;
        private readonly TypedMass _massDiffZHH;
        private readonly TypedMass _massCleaveC;
        private readonly TypedMass _massCleaveN;

        // For mass distributions
        private readonly double _massResolution;

        private readonly double _minimumAbundance;

        public SequenceMassCalc(MassType type)
        {
            // These values will be used to calculate masses that are later assumed to be massH
            type = type.IsMonoisotopic() ? MassType.MonoisotopicMassH : MassType.AverageMassH;

            _massCalc = new BioMassCalc(type);

            Labels = new HashSet<StaticMod>(); // Used by small molecules



            // Mass of a proton, i.e. +1 positive charge, hydrogen atom without its electron.
            // See http://antoine.frostburg.edu/chem/senese/101/atoms/index.shtml
            // _massWater = _massCalc.CalculateMass("H2O");
            // _massAmmonia = _massCalc.CalculateMass("NH3");

            // ReSharper disable LocalizableElement
            _massDiffB = new TypedMass(0.0, type);
            _massDiffA = _massDiffB - _massCalc.CalculateMassFromFormula("CO");
            _massDiffC = _massCalc.CalculateMassFromFormula("NH3");
            _massDiffY = _massCalc.CalculateMassFromFormula("H2O");
            _massDiffX = _massCalc.CalculateMassFromFormula("CO2");
            _massDiffZ = _massDiffY - _massCalc.CalculateMassFromFormula("NH3");
            _massDiffZH = _massDiffY - _massCalc.CalculateMassFromFormula("NH2");
            _massDiffZHH = _massDiffY - _massCalc.CalculateMassFromFormula("NH");

            _massCleaveN = _massCalc.CalculateMassFromFormula("H");
            _massCleaveC = _massCalc.CalculateMassFromFormula("OH");
            // ReSharper restore LocalizableElement

            // These numbers are set intentionally smaller than any known instrument
            // can measure.  Filters are then applied to resulting distributions
            // to get more useful numbers.
            _massResolution = 0.001;
            _minimumAbundance = 0.00001;    // 0.001%

            InitAminoAcidMasses();
        }

        public double ParseModMass(string formula)
        {
            return FormulaMass(_massCalc, formula, MassPrecision);
        }

        public double GetModMass(char aa, StaticMod mod)
        {
            if (_massCalc.MassType.IsMonoisotopic())
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
                return FormulaMass(_massCalc, mod.Formula, MassPrecision);
            else if ((mod.LabelAtoms & LabelAtoms.LabelsAA) != LabelAtoms.None && AminoAcid.IsAA(aa))
                return FormulaMass(_massCalc, GetHeavyFormula(aa, mod.LabelAtoms), MassPrecision);
            return 0;
        }

        public string GetModFormula(char aa, StaticMod mod, out double unexplainedMass)
        {
            unexplainedMass = 0;
            if (!string.IsNullOrEmpty(mod.Formula))
                return mod.Formula;
            else if ((mod.LabelAtoms & LabelAtoms.LabelsAA) != LabelAtoms.None)
                return GetHeavyFormula(aa, mod.LabelAtoms);
            if (_massCalc.MassType.IsMonoisotopic())
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
                        double mass = GetModMass('\0', mod);
                        double unexplainedMass;
                        string formula = GetModFormula('\0', mod, out unexplainedMass);
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
                        for (char aa = 'A'; aa <= 'Z'; aa++)
                        {
                            if (AMINO_FORMULAS[aa] != null)
                                AddMod(aa, mod, modMasses._aminoModMasses, modMasses._aminoModMassesExtra, modMasses._aminoModFormulas);
                        }
                        Labels.Add(mod); // And save it for small molecule use  // CONSIDER: just keep and bitwise OR the LabelAtoms
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

        public bool IsModified(Target val)
        {
            if (!val.IsProteomic)
                return false;
            var seq = val.Sequence;
            if (string.IsNullOrEmpty(seq))
                return false;
            if (_modMasses._massModCleaveC + _modMasses._massModCleaveN != 0)
                return true;
            int len = seq.Length;
            if (_modMasses._aminoNTermModMasses[seq[0]] + _modMasses._aminoCTermModMasses[seq[len - 1]] != 0)
                return true;
            return seq.Any(c => _modMasses._aminoModMasses[c] != 0);
        }

        public Target GetModifiedSequence(Target seq, bool narrow)
        {
            return GetModifiedSequence(seq, null, narrow ? SequenceModFormatType.mass_diff_narrow : SequenceModFormatType.mass_diff, false);
        }

        public Target GetModifiedSequence(Target seq, SequenceModFormatType format, bool useExplicitModsOnly)
        {
            return GetModifiedSequence(seq, null, format, useExplicitModsOnly);
        }

        public Target GetModifiedSequence(Target seq, ExplicitSequenceMods mods, bool formatNarrow)
        {
            var format = formatNarrow ? SequenceModFormatType.mass_diff_narrow : SequenceModFormatType.mass_diff;
            return GetModifiedSequence(seq, mods, format, false);
        }
        
        public Target GetModifiedSequence(Target val, ExplicitSequenceMods mods, SequenceModFormatType format,
            bool useExplicitModsOnly)
        {
            if (!val.IsProteomic)
                return val;

            // If no modifications, do nothing
            if (!IsModified(val) && mods == null)
                return val;

            // Otherwise, build a modified sequence string like AMC[+57.0]LP[-37.1]K
            var seq = val.Sequence;
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
            return val.ChangeSequence(sb.ToString());
        }

        public Target GetModifiedSequenceDisplay(Target seq)
        {
            return GetModifiedSequence(seq, SequenceModFormatType.mass_diff_narrow, false);
        }

        public Adduct GetModifiedAdduct(Adduct adduct, string neutralFormula)
        {
            return HasLabels ? GetModifiedAdduct(adduct, neutralFormula, Labels) : adduct;
        }

        public static Adduct GetModifiedAdduct(Adduct adduct, string neutralFormula, IEnumerable<StaticMod> labels)
        {
            // Pick out any label atoms
            var atoms = labels.Aggregate(LabelAtoms.None, (current, staticMod) => current | staticMod.LabelAtoms);
            var heavy = GetHeavyFormula(neutralFormula, atoms);
            adduct = adduct.ChangeIsotopeLabels(BioMassCalc.MONOISOTOPIC.FindIsotopeLabelsInFormula(heavy));
            return adduct;
        }

        public static Target NormalizeModifiedSequence(Target rawModifiedSequence)
        {
            if (rawModifiedSequence.IsProteomic)
            {
                var seq = NormalizeModifiedSequence(rawModifiedSequence.Sequence);
                return rawModifiedSequence.ChangeSequence(seq);
            }
            return rawModifiedSequence;
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
                // TODO: no precision to 1 decimal; 1+ unchanged
                ///////
                var x = strMassDiff.IndexOfAny(new[] {'.', ','});
                var numdecimals = x >= 0 ? strMassDiff.Length - x - 1 : -1;
                if (numdecimals < 2)
                    normalizedSeq.Append(GetModDiffDescription(massDiff, null, SequenceModFormatType.mass_diff));
                else
                {
                    var massdiff2 = strMassDiff.TrimStart('+', '-');
                    normalizedSeq.Append(string.Format(CultureInfo.InvariantCulture, @"[{0}{1}]", massDiff > 0 ? @"+" : @"-", massdiff2));
                }
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

        public MassDistribution GetMzDistribution(Target target, Adduct adduct, IsotopeAbundances abundances)
        {
            return GetMzDistribution(target, adduct, abundances, null);
        }

        public MassDistribution GetMzDistribution(Target target, Adduct adduct, IsotopeAbundances abundances, ExplicitSequenceMods mods = null)
        {
            double unexplainedMass;
            MoleculeUnsorted molecule;
            if (target.IsProteomic)
            {
                molecule = GetFormula(target.Sequence, mods, out unexplainedMass);
            }
            else
            {
                molecule = MoleculeUnsorted.Parse(target.Molecule.Formula);
                unexplainedMass = 0;
            }
            return GetMzDistribution(molecule, adduct, abundances, unexplainedMass);
        }

        public MassDistribution GetMZDistributionFromFormula(string formula, Adduct adduct, IsotopeAbundances abundances)
        {
            var molecule = MoleculeUnsorted.Parse(formula);
            return GetMzDistribution(molecule, adduct, abundances, 0);
        }

        public MassDistribution GetMZDistributionSinglePoint(double mz)
        {
            return MassDistribution.NewInstance(new SortedDictionary<double, double> {{mz, 1.0}}, _massResolution, _minimumAbundance);
        }

        public string GetMolecularFormula(string peptideSequence)
        {
           return GetNeutralFormula(peptideSequence, null);
        }

        /// <summary>
        /// Convert a peptide to a small molecule formula (e.g. PEPTIDER => "C40H65N11O16")
        /// </summary>
        public string GetNeutralFormula(string seq, ExplicitSequenceMods mods)
        {
            double unexplainedMass;
            var molecule = GetFormula(seq, mods, out unexplainedMass);
            if (unexplainedMass != 0.0)
                throw new ArgumentException(@"Unexplained mass when deriving molecular formula from sequence "+seq);
            return molecule.ToString();
        }

// ReSharper disable once ParameterTypeCanBeEnumerable.Local
        private MassDistribution GetMzDistribution(MoleculeUnsorted molecule, Adduct adduct, IsotopeAbundances abundances, double unexplainedMass)
        {
            // Low resolution to get back only peaks at Dalton (i.e. neutron) boundaries
            var md = new MassDistribution(_massResolution, _minimumAbundance);
            var result = md;
            var charge = adduct.AdductCharge;
            // Note we use the traditional peptide-oriented calculation when adduct is protonated and not an n-mer, mostly for stability in tests
            var protonated = adduct.IsProtonated && (adduct.GetMassMultiplier() == 1);
            var mol = protonated ? molecule.Elements : adduct.ApplyToMolecule(molecule.Elements);
            foreach (var element in mol)
            {
                result = result.Add(md.Add(abundances[element.Key]).Multiply(element.Value));
            }
            return result.OffsetAndDivide(unexplainedMass + charge * (protonated ? BioMassCalc.MassProton : -BioMassCalc.MassElectron), charge);
        }

        private MoleculeUnsorted GetFormula(string seq, ExplicitSequenceMods mods, out double unexplainedMass)
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
            formula.Append(H2O); // N-term = H, C-term = OH
            unexplainedMass = formula.UnexplainedMass;
            return new MoleculeUnsorted(formula.DictAtomCounts);
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

            // ReSharper disable once UnusedMethodReturnValue.Local
            public FormulaBuilder Append(string formula, double unexplainedMass = 0)
            {
                _unexplainedMass += unexplainedMass;
                if (formula != null)
                    ParseModCounts(_massCalc, formula, _dictAtomCounts);
                return this;
            }

            public FormulaBuilder Append(IDictionary<string, int> formula, double unexplainedMass = 0)
            {
                _unexplainedMass += unexplainedMass;
                if (formula != null)
                {
                    foreach (var elementCount in formula)
                    {
                        int count;
                        if (_dictAtomCounts.TryGetValue(elementCount.Key, out count))
                        {
                            _dictAtomCounts[elementCount.Key] = count + elementCount.Value;
                        }
                        else
                        {
                            _dictAtomCounts.Add(elementCount.Key, elementCount.Value);
                        }
                    }
                }
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

            public Dictionary<string, int> DictAtomCounts
            {
                get { return _dictAtomCounts; }
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

        public TypedMass GetPrecursorMass(CustomMolecule mol, Adduct adductForIsotopeLabels, out string isotopicFormula)
        {
            return GetPrecursorMass(mol, null, adductForIsotopeLabels, out isotopicFormula);
        }

        public TypedMass GetPrecursorMass(CustomMolecule mol, TypedModifications typedMods, Adduct adductForIsotopeLabels, out string isotopicFormula)
        {
            var mass = MassType.IsMonoisotopic() ? mol.MonoisotopicMass : mol.AverageMass;
            var massCalc = MassType.IsMonoisotopic() ? BioMassCalc.MONOISOTOPIC : BioMassCalc.AVERAGE;

            // Isotope descriptions may be found in the typedMods, or in the adduct as when dealing with mass-only documents
            var isotopeDescriptionIsInAdduct = adductForIsotopeLabels.HasIsotopeLabels;
            if (!string.IsNullOrEmpty(mol.Formula) && typedMods != null && !isotopeDescriptionIsInAdduct)
            {
                isotopicFormula = typedMods.LabelType.IsLight || !typedMods.Modifications.Any() ? mol.Formula : GetHeavyFormula(mol.Formula, typedMods.Modifications[0].LabelAtoms);
                mass = massCalc.CalculateMassFromFormula(isotopicFormula);
            }
            else
            {
                isotopicFormula = null;
                if (isotopeDescriptionIsInAdduct)
                {
                    // Reduce an adduct like "[2M6Cl37+3H]" to "[M6Cl37]"
                    var adduct = adductForIsotopeLabels.ChangeMassMultiplier(1).ChangeIonFormula(null);
                    if (!string.IsNullOrEmpty(mol.Formula))
                    {
                        var ionInfo = new IonInfo(mol.Formula, adduct);
                        isotopicFormula = ionInfo.FormulaWithAdductApplied;
                        mass = massCalc.CalculateMassFromFormula(isotopicFormula);
                    }
                    else
                    {
                        // Assume that the isotope labeling can be fully applied: that is, if it's 6C13 then we can add 6*(massC13 - massC)
                        mass =  adduct.ApplyToMass(mass);
                    }
                }
            }
            return mass;
        }

        public TypedMass GetPrecursorMass(Target target)
        {
            if (target.IsProteomic)
                return GetPrecursorMass(target.Sequence);
            string ignored;
            return GetPrecursorMass(target.Molecule, Adduct.EMPTY, out ignored);
        }

        public TypedMass GetPrecursorMass(string seq)
        {
            return GetPrecursorMass(seq, null);
        }

        public TypedMass GetPrecursorMass(string seq, ExplicitSequenceMods mods)
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

            return new TypedMass(mass, MassType.IsMonoisotopic() ? MassType.MonoisotopicMassH : MassType.AverageMassH); // This is massH (due to +BioMassCalc.MassProton above)
        }

        public IonTable<TypedMass> GetFragmentIonMasses(Target seq)
        {
            return GetFragmentIonMasses(seq, null);
        }

        public IonTable<TypedMass> GetFragmentIonMasses(Target target, ExplicitSequenceMods mods)
        {
            if (!target.IsProteomic)
                return null;

            var modMasses = GetModMasses(mods);

            var seq = target.Sequence;
            int len = seq.Length - 1;
            var a = IonType.a;
            var b = IonType.b;
            var c = IonType.c;
            var x = IonType.x;
            var y = IonType.y;
            var z = IonType.z;

            var nTermMassB = _massDiffB + modMasses._massModCleaveN + BioMassCalc.MassProton;
            var deltaA = _massDiffA - _massDiffB;
            var deltaC = _massDiffC - _massDiffB;
            var cTermMassY = (_massDiffY + modMasses._massModCleaveC + BioMassCalc.MassProton).ChangeIsMassH(true);
            var deltaX = _massDiffX - _massDiffY;
            var deltaZ = _massDiffZ - _massDiffY;
            var deltaZH = _massDiffZH - _massDiffY;
            var deltaZHH = _massDiffZHH - _massDiffY;

            var masses = new IonTable<TypedMass>(IonType.zhh, len);

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
                masses[IonType.zh, iC] = cTermMassY + deltaZH;
                masses[IonType.zhh, iC] = cTermMassY + deltaZHH;
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

        public TypedMass GetFragmentMass(Transition transition, IsotopeDistInfo isotopeDist)
        {
            return GetFragmentMass(transition, isotopeDist, null);
        }

        public TypedMass GetFragmentMass(Transition transition, IsotopeDistInfo isotopeDist, ExplicitSequenceMods mods)
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
                else if (transition.IsNonReporterCustomIon() && // Don't apply labels to reporter ions
                    !string.IsNullOrEmpty(transition.CustomIon.NeutralFormula))
                {
                    if (Labels.Any())
                    {
                        var formula = Labels.Aggregate(transition.CustomIon.NeutralFormula, (current, staticMod) => GetHeavyFormula(current, staticMod.LabelAtoms));
                        return _massCalc.CalculateMassFromFormula(formula);
                    }
                    else if (Transition.IsPrecursor(type) && transition.Group.PrecursorAdduct.HasIsotopeLabels)
                    {
                        // Apply any labels found in the adduct description
                        var formula =
                            transition.Group.PrecursorAdduct.ApplyIsotopeLabelsToFormula(transition.CustomIon.NeutralFormula);
                        return _massCalc.CalculateMassFromFormula(formula);
                    }
                }
                return MassType.IsAverage() 
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

        public TypedMass GetPrecursorFragmentMass(CustomMolecule mol, Adduct adductForIsotopeLabels)
        {
            string isotopicFormula;
            return GetPrecursorMass(mol, adductForIsotopeLabels, out isotopicFormula);
        }

        public TypedMass GetPrecursorFragmentMass(Target target)
        {
            if (target.IsProteomic)
                return GetPrecursorFragmentMass(target.Sequence, null);
            return GetPrecursorFragmentMass(target.Molecule, Adduct.EMPTY);
        }

        public TypedMass GetPrecursorFragmentMass(string seq, ExplicitSequenceMods mods)
        {
            return GetFragmentMass(seq, IonType.precursor, seq.Length, null, 0, null, mods);
        }

        private TypedMass GetFragmentMass(string seq,
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

            return new TypedMass(mass, MassType.IsMonoisotopic() ? MassType.MonoisotopicMassH : MassType.AverageMassH); // This is massH ( + BioMassCalc.MassProton above)
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
                case IonType.zh: return _massDiffZH + modMasses._massModCleaveC;
                case IonType.zhh: return _massDiffZHH + modMasses._massModCleaveC;
                default:
                    throw new ArgumentException(@"Invalid ion type");
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
                case IonType.zh: return _massDiffZH - _massDiffY;
                case IonType.zhh: return _massDiffZHH - _massDiffY;
                default:
                    throw new ArgumentException(@"Invalid ion type");
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
                var formula = AMINO_FORMULAS[i];
                if (formula != null)
                    _aminoMasses[i] = _massCalc.CalculateMassFromFormula(formula);
            }



            // ReSharper disable CharImplicitlyConvertedToNumeric
            // Handle values for non-amino acids
            // Wikipedia says Aspartic acid or Asparagine, this seems to be average of Cytosine and Cyanoalanine
            _aminoMasses['b'] = _aminoMasses['B'] =
                (_massCalc.CalculateMassFromFormula(@"C4H5NO3") + _massCalc.CalculateMassFromFormula(@"C4H6N2O2")) / 2;
            _aminoMasses['j'] = _aminoMasses['J'] = 0.0;
            _aminoMasses['x'] = _aminoMasses['X'] = 111.060000;	// Why?
            // Wikipedia says Glutamic acid or Glutamine
            _aminoMasses['z'] = _aminoMasses['Z'] =
                (_massCalc.CalculateMassFromFormula(@"C5H6ON2") + _massCalc.CalculateMassFromFormula(@"C5H8N2O2")) / 2;
            // ReSharper restore CharImplicitlyConvertedToNumeric
        }

        private static readonly Molecule[] AMINO_FORMULAS = new Molecule[128];
        private static readonly Molecule H2O = Molecule.Parse(@"H2O");
        static SequenceMassCalc()
        {



            // ReSharper disable CharImplicitlyConvertedToNumeric
            // ReSharper disable LocalizableElement
            // CONSIDER(bspratt): what about B and Z? (see average values above for masses)
            AMINO_FORMULAS['a'] = AMINO_FORMULAS['A'] = Molecule.Parse("C3H5ON");
            AMINO_FORMULAS['c'] = AMINO_FORMULAS['C'] = Molecule.Parse("C3H5ONS");
            AMINO_FORMULAS['d'] = AMINO_FORMULAS['D'] = Molecule.Parse("C4H5O3N");
            AMINO_FORMULAS['e'] = AMINO_FORMULAS['E'] = Molecule.Parse("C5H7O3N");
            AMINO_FORMULAS['f'] = AMINO_FORMULAS['F'] = Molecule.Parse("C9H9ON");
            AMINO_FORMULAS['g'] = AMINO_FORMULAS['G'] = Molecule.Parse("C2H3ON");
            AMINO_FORMULAS['h'] = AMINO_FORMULAS['H'] = Molecule.Parse("C6H7ON3");
            AMINO_FORMULAS['i'] = AMINO_FORMULAS['I'] = Molecule.Parse("C6H11ON");
            AMINO_FORMULAS['k'] = AMINO_FORMULAS['K'] = Molecule.Parse("C6H12ON2");
            AMINO_FORMULAS['l'] = AMINO_FORMULAS['L'] = Molecule.Parse("C6H11ON");
            AMINO_FORMULAS['m'] = AMINO_FORMULAS['M'] = Molecule.Parse("C5H9ONS");
            AMINO_FORMULAS['n'] = AMINO_FORMULAS['N'] = Molecule.Parse("C4H6O2N2");
            AMINO_FORMULAS['o'] = AMINO_FORMULAS['O'] = Molecule.Parse("C12H19N3O2");
            AMINO_FORMULAS['p'] = AMINO_FORMULAS['P'] = Molecule.Parse("C5H7ON");
            AMINO_FORMULAS['q'] = AMINO_FORMULAS['Q'] = Molecule.Parse("C5H8O2N2");
            AMINO_FORMULAS['r'] = AMINO_FORMULAS['R'] = Molecule.Parse("C6H12ON4");
            AMINO_FORMULAS['s'] = AMINO_FORMULAS['S'] = Molecule.Parse("C3H5O2N");
            AMINO_FORMULAS['t'] = AMINO_FORMULAS['T'] = Molecule.Parse("C4H7O2N");
            AMINO_FORMULAS['u'] = AMINO_FORMULAS['U'] = Molecule.Parse("C3H5NOSe");
            AMINO_FORMULAS['v'] = AMINO_FORMULAS['V'] = Molecule.Parse("C5H9ON");
            AMINO_FORMULAS['w'] = AMINO_FORMULAS['W'] = Molecule.Parse("C11H10ON2");
            AMINO_FORMULAS['y'] = AMINO_FORMULAS['Y'] = Molecule.Parse("C9H9O2N");
            // ReSharper restore LocalizableElement
            // ReSharper restore CharImplicitlyConvertedToNumeric
        }

        public static Molecule GetAminoAcidFormula(char aa)
        {
            return Molecule.FromDict(ImmutableSortedList.FromValues(AMINO_FORMULAS[aa]));
        }

        public static string GetHeavyFormula(char aa, LabelAtoms labelAtoms)
        {
            var formulaAA = AMINO_FORMULAS[aa];
            if (formulaAA == null)
                throw new ArgumentOutOfRangeException(string.Format(Resources.SequenceMassCalc_GetHeavyFormula_No_formula_found_for_the_amino_acid___0__, aa));
            var formula = formulaAA.ToString();
            return GetHeavyFormula(formula, labelAtoms) + @" - " + formula;
        }

        private static readonly ImmutableList<Tuple<LabelAtoms, string, string>> 
            ALL_LABEL_SUBSTITUTIONS = ImmutableList.ValueOf(new[]
        {
            Tuple.Create(LabelAtoms.C13, BioMassCalc.C, BioMassCalc.C13),
            Tuple.Create(LabelAtoms.N15, BioMassCalc.N, BioMassCalc.N15),
            Tuple.Create(LabelAtoms.O18, BioMassCalc.O, BioMassCalc.O18),
            Tuple.Create(LabelAtoms.H2, BioMassCalc.H, BioMassCalc.H2),
            Tuple.Create(LabelAtoms.Cl37, BioMassCalc.Cl, BioMassCalc.Cl37),
            Tuple.Create(LabelAtoms.Br81, BioMassCalc.Br, BioMassCalc.Br81),
            Tuple.Create(LabelAtoms.P32, BioMassCalc.P, BioMassCalc.P32),
            Tuple.Create(LabelAtoms.S34, BioMassCalc.S, BioMassCalc.S34),

        });
        public static string GetHeavyFormula(string formula, LabelAtoms labelAtoms)
        {
            if (labelAtoms == LabelAtoms.None)
            {
                return formula;
            }
            var subsitutions = ALL_LABEL_SUBSTITUTIONS
                .Where(tuple => (tuple.Item1 & labelAtoms) != 0).ToArray();
            StringBuilder result = new StringBuilder();
            foreach (var symbol in TokenizeFormula(formula))
            {
                var subTuple = subsitutions.FirstOrDefault(tuple => tuple.Item2 == symbol);
                if (subTuple == null)
                {
                    result.Append(symbol);
                }
                else
                {
                    result.Append(subTuple.Item3);
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// Split a formula up into its individual tokens.
        /// A token is one of an element name, an integer, or the special characters space and minus sign.
        /// </summary>
        public static IEnumerable<string> TokenizeFormula(string formula)
        {
            int? ichElementStart = null;
            int? ichCountStart = null;
            for (int ich = 0; ich < formula.Length; ich++)
            {
                char ch = formula[ich];
                bool isDigit = ch >= '0' && ch <= '9';
                bool isElementNameStart = ch >= 'A' && ch <= 'Z';
                bool isSpecial = ch == '-' || ch == ' ';
                if (isDigit && ichCountStart.HasValue)
                {
                    continue;
                }
                if (!isDigit && !isSpecial && !isElementNameStart)
                {
                    // any other character is considered part of an element name, unless
                    if (ichElementStart.HasValue)
                    {
                        continue;
                    }
                    // characters before the start of an element name are garbage, but we preserve them
                    isSpecial = true;
                }
                if (ichElementStart.HasValue)
                {
                    yield return formula.Substring(ichElementStart.Value, ich - ichElementStart.Value);
                    ichElementStart = null;
                }
                if (ichCountStart.HasValue)
                {
                    yield return formula.Substring(ichCountStart.Value, ich - ichCountStart.Value);
                    ichCountStart = null;
                }
                if (isDigit)
                {
                    ichCountStart = ich;
                }
                if (isElementNameStart)
                {
                    ichElementStart = ich;
                }
                if (isSpecial)
                {
                    yield return new string(ch, 1);
                }
            }
            if (ichElementStart.HasValue)
            {
                yield return formula.Substring(ichElementStart.Value);
            }
            if (ichCountStart.HasValue)
            {
                yield return formula.Substring(ichCountStart.Value);
            }
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

        public TypedMass GetPrecursorMass(CustomMolecule mol, TypedModifications mods, Adduct adductForIsotopeLabels, out string isotopicFormula)
        {
            return _massCalcBase.GetPrecursorMass(mol, mods, adductForIsotopeLabels, out isotopicFormula);
        }

        public TypedMass GetPrecursorMass(string seq)
        {
            return _massCalcBase.GetPrecursorMass(seq, _mods);
        }

        public TypedMass GetPrecursorMass(Target target)
        {
            if (target.IsProteomic)
                return GetPrecursorMass(target.Sequence);
            string ignored;
            return GetPrecursorMass(target.Molecule, null, Adduct.EMPTY, out ignored);
        }



        public bool HasLabels { get { return _massCalcBase.HasLabels; } }

        public bool IsModified(Target seq)
        {
            return _massCalcBase.IsModified(seq) ||
                _mods.ModMasses.IndexOf(m => m != 0) != -1; // If any non-zero modification values
        }

        public Target GetModifiedSequence(Target seq, SequenceModFormatType format, bool useExplicitModsOnly)
        {
            return _massCalcBase.GetModifiedSequence(seq, _mods, format, useExplicitModsOnly);
        }

        public Target GetModifiedSequence(Target seq, bool narrow)
        {
            return GetModifiedSequence(seq,
                                       narrow ? SequenceModFormatType.mass_diff_narrow : SequenceModFormatType.mass_diff,
                                       false);
        }

        public Target GetModifiedSequenceDisplay(Target seq)
        {
            return GetModifiedSequence(seq, SequenceModFormatType.mass_diff_narrow, false);
        }

        public Adduct GetModifiedAdduct(Adduct adduct, string neutralFormula)
        {
            return HasLabels ? 
                SequenceMassCalc.GetModifiedAdduct(adduct, neutralFormula, _massCalcBase.Labels) : 
                adduct;
        }

        public double GetAAModMass(char aa, int seqIndex, int seqLength)
        {
            return _massCalcBase.GetAAModMass(aa, seqIndex, seqLength, _mods);
        }

        public string GetMolecularFormula(string seq)
        {
            return _massCalcBase.GetNeutralFormula(seq, _mods);
        }

        public MassDistribution GetMzDistribution(Target target, Adduct adduct, IsotopeAbundances abundances)
        {
            return _massCalcBase.GetMzDistribution(target, adduct, abundances, _mods);
        }

        public MassDistribution GetMZDistributionFromFormula(string formula, Adduct adduct, IsotopeAbundances abundances)
        {
            return _massCalcBase.GetMZDistributionFromFormula(formula, adduct, abundances);
        }

        public MassDistribution GetMZDistributionSinglePoint(double mz)
        {
            return  _massCalcBase.GetMZDistributionSinglePoint(mz);
        }

        public IonTable<TypedMass> GetFragmentIonMasses(Target seq)
        {
            return _massCalcBase.GetFragmentIonMasses(seq, _mods);
        }

        public TypedMass GetFragmentMass(Transition transition, IsotopeDistInfo isotopeDist)
        {
            return _massCalcBase.GetFragmentMass(transition, isotopeDist, _mods);
        }

        public TypedMass GetPrecursorFragmentMass(CustomMolecule mol, Adduct adductForIsotopeLabels)
        {
            return _massCalcBase.GetPrecursorFragmentMass(mol, adductForIsotopeLabels);
        }

        public TypedMass GetPrecursorFragmentMass(Target target)
        {
            if (target.IsProteomic)
                return _massCalcBase.GetPrecursorFragmentMass(target.Sequence, _mods);
            return _massCalcBase.GetPrecursorFragmentMass(target.Molecule, Adduct.EMPTY);
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
