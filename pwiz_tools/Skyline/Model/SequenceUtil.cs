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
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
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
                    throw new InvalidDataException(string.Format(ModelResources.AminoAcid_ValidateAAList_Invalid_amino_acid__0__found_in_the_value__1__, c, seq));
                if (seen.Contains(c))
                    throw new InvalidDataException(string.Format(ModelResources.AminoAcid_ValidateAAList_The_amino_acid__0__is_repeated_in_the_value__1__, c, seq));
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
        public static int MassPrecision { get { return BioMassCalc.MassPrecision; } }
        public static double MassTolerance { get { return BioMassCalc.MassTolerance; } }

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
            var mol = ParsedMolecule.Create(desc);
            return FormulaMass(calc, mol, precision);
        }

        public static TypedMass FormulaMass(BioMassCalc calc, Molecule mol, int? precision = null)
        {
            var totalMass = calc.CalculateMass((IDictionary<string, int>)mol);
            return (precision.HasValue ? totalMass.ChangeMass(Math.Round(totalMass, precision.Value)) : totalMass);
        }

        public static TypedMass FormulaMass(BioMassCalc calc, ParsedMolecule mol, int? precision = null)
        {
            var totalMass = calc.CalculateMass(mol);
            return (precision.HasValue ? totalMass.ChangeMass( Math.Round(totalMass, precision.Value)) : totalMass);
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
                        var foundMod = UniMod.GetModification(mod.Name, out _);
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
            public readonly Molecule[] _aminoModFormulas = new Molecule[128];
            public readonly Molecule[] _aminoNTermModFormulas = new Molecule[128];
            public readonly Molecule[] _aminoCTermModFormulas = new Molecule[128];
            public Molecule _massModCleaveCFormula;
            public Molecule _massModCleaveNFormula;
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

            _massCalc = BioMassCalc.GetBioMassCalc(type);

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
            if (!ParsedMolecule.IsNullOrEmpty(mod.ParsedMolecule))
                return FormulaMass(_massCalc, mod.ParsedMolecule, MassPrecision);
            else if ((mod.LabelAtoms & LabelAtoms.LabelsAA) != LabelAtoms.None && AminoAcid.IsAA(aa))
                return FormulaMass(_massCalc, GetHeavyFormula(aa, mod.LabelAtoms), MassPrecision);
            return 0;
        }

        public MoleculeMassOffset GetModFormula(char aa, StaticMod mod)
        {
            if (!ParsedMolecule.IsNullOrEmpty(mod.ParsedMolecule))
                return mod.GetMoleculeMassOffset(); // If it has a formula, use it
            else if ((mod.LabelAtoms & LabelAtoms.LabelsAA) != LabelAtoms.None)
                return GetHeavyFormula(aa, mod.LabelAtoms).GetMoleculeMassOffset(); // If it has a label, use the labeled version of the amino acid
            return mod.GetMoleculeMassOffset(); // Return the mass offset information
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
                        var formulaAndMassOffset = GetModFormula('\0', mod);
                        var formula = formulaAndMassOffset.Molecule;
                        var unexplainedMass = formulaAndMassOffset.GetMassOffset(_massCalc.MassType.IsMonoisotopic());
                        if (mod.Terminus == ModTerminus.C)
                        {
                            modMasses._massModCleaveC += mass;
                            modMasses._massModCleaveCExtra += unexplainedMass;
                            modMasses._massModCleaveCFormula = 
                                Molecule.IsNullOrEmpty(modMasses._massModCleaveCFormula) ? formula : modMasses._massModCleaveCFormula.Plus(formula);
                        }
                        else
                        {
                            modMasses._massModCleaveN += mass;
                            modMasses._massModCleaveNExtra += unexplainedMass;
                            modMasses._massModCleaveNFormula = 
                                Molecule.IsNullOrEmpty(modMasses._massModCleaveNFormula) ? formula : modMasses._massModCleaveNFormula.Plus(formula);
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

        private void AddMod(char aa, StaticMod mod, double[] modMasses, double[] modMassesExtra, Molecule[] modFormulas)
        {
            modMasses[aa] = modMasses[char.ToLowerInvariant(aa)] += GetModMass(aa, mod);

            // Deal with formulas and unexplained masses
            var formula = GetModFormula(aa, mod);
            modFormulas[aa] = modFormulas[char.ToLowerInvariant(aa)] = 
                Molecule.IsNullOrEmpty(modFormulas[aa]) ? formula.Molecule : modFormulas[aa].Plus(formula.Molecule);
            modMassesExtra[aa] = modMassesExtra[char.ToLowerInvariant(aa)] += formula.GetMassOffset(_massCalc.MassType.IsMonoisotopic());
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

        public Adduct GetModifiedAdduct(Adduct adduct, ParsedMolecule neutralFormula)
        {
            return HasLabels ? GetModifiedAdduct(adduct, neutralFormula, Labels) : adduct;
        }

        public static Adduct GetModifiedAdduct(Adduct adduct, ParsedMolecule neutralFormula, IEnumerable<StaticMod> labels)
        {
            // Pick out any label atoms
            var atoms = labels.Aggregate(LabelAtoms.None, (current, staticMod) => current | staticMod.LabelAtoms);
            var heavy = GetHeavyFormula(neutralFormula, atoms);
            adduct = adduct.ChangeIsotopeLabels(BioMassCalc.FindIsotopeLabelsInFormula(heavy.Molecule));
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
            if (CrosslinkSequenceParser.TryParseCrosslinkLibraryKey(rawModifiedSequence, 0) != null)
            {
                return rawModifiedSequence;
            }
            var normalizedSeq = new StringBuilder();
            int ichLast = 0;
            for (int ichOpenBracket = rawModifiedSequence.IndexOf('[');
                 ichOpenBracket >= 0;
                 ichOpenBracket = rawModifiedSequence.IndexOf('[', ichOpenBracket + 1))
            {
                int ichCloseBracket = rawModifiedSequence.IndexOf(']', ichOpenBracket);
                if (ichCloseBracket < 0)
                {
                    throw new ArgumentException(string.Format(ModelResources.SequenceMassCalc_NormalizeModifiedSequence_Modification_definition__0__missing_close_bracket_, rawModifiedSequence.Substring(ichOpenBracket)));
                }
                string strMassDiff = rawModifiedSequence.Substring(ichOpenBracket + 1, ichCloseBracket - ichOpenBracket - 1);
                double massDiff;
                // Try parsing with both invariant culture and current number format
                const NumberStyles numStyle = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.Integer; // Don't allow thousands
                if (!double.TryParse(strMassDiff, numStyle, CultureInfo.InvariantCulture, out massDiff) &&
                    !double.TryParse(strMassDiff, numStyle, CultureInfo.CurrentCulture, out massDiff))
                {
                    throw new ArgumentException(string.Format(ModelResources.SequenceMassCalc_NormalizeModifiedSequence_The_modification__0__is_not_valid___Expected_a_numeric_delta_mass_, strMassDiff));
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
            MoleculeMassOffset molecule;
            if (target.IsProteomic)
            {
                molecule = GetFormula(target.Sequence, mods);
            }
            else
            {
                molecule = target.Molecule.ParsedMolecule.GetMoleculeMassOffset();
            }
            return GetMzDistribution(molecule, adduct, abundances);
        }

        public MassDistribution GetMZDistributionFromFormula(string formula, Adduct adduct,
            IsotopeAbundances abundances)
        {
            var molecule = ParsedMolecule.Create(formula).GetMoleculeMassOffset();
            return GetMzDistribution(molecule, adduct, abundances);
        }

        public MassDistribution GetMZDistribution(MoleculeMassOffset molecule, Adduct adduct,
            IsotopeAbundances abundances)
        {
            return GetMzDistribution(molecule, adduct, abundances);
        }

        public MassDistribution GetMZDistributionSinglePoint(double mz)
        {
            return MassDistribution.NewInstance(new SortedDictionary<double, double> {{mz, 1.0}}, _massResolution, _minimumAbundance);
        }

        public MoleculeMassOffset GetMolecularFormula(string peptideSequence)
        {
            return GetNeutralFormula(peptideSequence, null);
        }

        /// <summary>
        /// Convert a peptide to a small molecule formula (e.g. PEPTIDER => "C40H65N11O16")
        /// </summary>
        public MoleculeMassOffset GetNeutralFormula(string seq, ExplicitSequenceMods mods)
        {
            var molecule = GetFormula(seq, mods);
            if (molecule.MonoMassOffset != 0.0 || molecule.Molecule.Values.Any(v => v < 0))
                throw new ArgumentException(@"Unexplained mass when deriving molecular formula from sequence "+seq);
            return molecule;
        }

        // ReSharper disable once ParameterTypeCanBeEnumerable.Local

        private MassDistribution GetMzDistribution(MoleculeMassOffset molecule, Adduct adduct, IsotopeAbundances abundances)
        {
            // Low resolution to get back only peaks at Dalton (i.e. neutron) boundaries
            var md = new MassDistribution(_massResolution, _minimumAbundance);
            var result = md;
            var charge = adduct.AdductCharge;
            // Note we use the traditional peptide-oriented calculation when adduct is protonated and not an n-mer, mostly for stability in tests
            var protonated = adduct.IsProtonated && (adduct.GetMassMultiplier() == 1);
            var mol = protonated ? molecule.Molecule : adduct.ApplyToMolecule(molecule).Molecule;
            foreach (var element in mol)
            {
                result = result.Add(md.Add(abundances[element.Key]).Multiply(element.Value));
            }
            var unexplainedMass = _massCalc.MassType.IsMonoisotopic()
                ? molecule.MonoMassOffset
                : molecule.AverageMassOffset;
            return result.OffsetAndDivide(unexplainedMass + charge * (protonated ? BioMassCalc.MassProton : -BioMassCalc.MassElectron), charge);
        }

        private MoleculeMassOffset GetFormula(string seq, ExplicitSequenceMods mods)
        {
            var formula = new FormulaBuilder();
            var modMasses = GetModMasses(mods);
            formula.Append(modMasses._massModCleaveNFormula, modMasses._massModCleaveNExtra);
            formula.Append(modMasses._massModCleaveCFormula, modMasses._massModCleaveCExtra);
            for (int i = 0, len = seq.Length; i < len; i++)
            {
                char c = seq[i];

                formula.Append(AMINO_FORMULAS[c]);
                formula.Append(modMasses._aminoModFormulas[c], modMasses._aminoModMassesExtra[c]);
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
                    var modFormula = GetModFormula(seq[mod.IndexAA], mod.Modification);
                    formula.Append(modFormula);
                }
            }
            formula.Append(H2O); // N-term = H, C-term = OH
            return formula.Sum();
        }

        private sealed class FormulaBuilder
        {
            private List<Molecule> _listAtomCounts;
            private double _massOffsetMono;
            private double _massOffsetAverage;

            public FormulaBuilder()
            {
                _listAtomCounts = new List<Molecule>();
            }

            public void Append(MoleculeMassOffset formula)
            {
                if (!MoleculeMassOffset.IsNullOrEmpty(formula))
                {
                    _listAtomCounts.Add(formula.Molecule);
                    _massOffsetMono += formula.MonoMassOffset;
                    _massOffsetAverage += formula.AverageMassOffset;
                }
            }

            public void Append(ParsedMolecule formula)
            {
                if (!ParsedMolecule.IsNullOrEmpty(formula))
                {
                    _listAtomCounts.Add(formula.Molecule);
                    _massOffsetMono += formula.MonoMassOffset;
                    _massOffsetAverage += formula.AverageMassOffset;
                }
            }

            public void Append(Molecule formula, double extraMass = 0)
            {
                if (!Molecule.IsNullOrEmpty(formula))
                {
                    _listAtomCounts.Add(formula);
                }
                _massOffsetMono += extraMass;
                _massOffsetAverage += extraMass;
            }

            public MoleculeMassOffset Sum()
            {
                return MoleculeMassOffset.Create(Molecule.Sum(_listAtomCounts), _massOffsetMono, _massOffsetAverage);
            }

            public override string ToString()
            {
                var formulaText = new StringBuilder();
                var mol = Sum();
                foreach (var atomCount in mol.Molecule.OrderBy(p => p.Key).Where(p => p.Value > 0))
                {
                    formulaText.Append(atomCount.Key);
                    if (atomCount.Value > 1)
                        formulaText.Append(atomCount.Value);
                }
                var txt = formulaText.ToString();
                txt += MoleculeMassOffset.FormatMassModification(mol.MonoMassOffset, mol.AverageMassOffset, BioMassCalc.MassPrecision);
                return txt;
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

        public TypedMass GetPrecursorMass(CustomMolecule mol, Adduct adductForIsotopeLabels, out ParsedMolecule isotopicFormula)       {
            return GetPrecursorMass(mol, null, adductForIsotopeLabels, out isotopicFormula);
        }

        public TypedMass GetPrecursorMass(CustomMolecule mol, TypedModifications typedMods, Adduct adductForIsotopeLabels, out ParsedMolecule isotopicFormula)
        {
            TypedMass mass;

            // Isotope descriptions may be found in the typedMods, or in the adduct as when dealing with small molecules
            var isotopeDescriptionIsInAdduct = adductForIsotopeLabels.HasIsotopeLabels;
            var formula = mol.ParsedMolecule;
            if (typedMods != null && !isotopeDescriptionIsInAdduct)
            {
                isotopicFormula = typedMods.LabelType.IsLight || !typedMods.Modifications.Any() ? formula : GetHeavyFormula(formula, typedMods.Modifications[0].LabelAtoms);
            }
            else
            {
                isotopicFormula = adductForIsotopeLabels.ApplyIsotopeLabelsToMolecule(formula);
            }
            var massCalc = MassType.IsMonoisotopic() ? BioMassCalc.MONOISOTOPIC : BioMassCalc.AVERAGE;
            mass = massCalc.CalculateMass(isotopicFormula);
            return mass;
        }

        public TypedMass GetPrecursorMass(Target target)
        {
            if (target.IsProteomic)
                return GetPrecursorMass(target.Sequence);
            return GetPrecursorMass(target.Molecule, Adduct.EMPTY, out _);
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
            if (type.IsNTerminal())
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
                            string.Format(ModelResources.SequenceMassCalc_GetFragmentMass_Precursor_isotope__0__is_outside_the_isotope_distribution__1__to__2__,
                                            GetMassIDescripion(massIndex), isotopeDist.PeakIndexToMassIndex(0),
                                            isotopeDist.PeakIndexToMassIndex(isotopeDist.CountPeaks - 1)));
                    }
                    return isotopeDist.GetMassI(massIndex);
                }
                else if (transition.IsNonReporterCustomIon() && // Don't apply labels to reporter ions
                         !transition.CustomIon.ParsedMolecule.IsMassOnly)
                {
                    if (Labels.Any())
                    {
                        var union = Labels.Aggregate(LabelAtoms.None, (current, staticMod) => current | staticMod.LabelAtoms);
                        var formula = GetHeavyFormula(transition.CustomIon.ParsedMolecule, union);
                        return _massCalc.CalculateMass(formula).ChangeIsMassH(false);
                    }
                    else if (Transition.IsPrecursor(type) && transition.Group.PrecursorAdduct.HasIsotopeLabels)
                    {
                        // Apply any labels found in the adduct description
                        var formula =
                            transition.Group.PrecursorAdduct.ApplyIsotopeLabelsToMolecule(transition.CustomIon.ParsedMolecule);
                        return _massCalc.CalculateMass(formula).ChangeIsMassH(false);
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
            return GetPrecursorMass(mol, adductForIsotopeLabels, out _);
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
                            string.Format(ModelResources.SequenceMassCalc_GetFragmentMass_Precursor_isotope__0__is_outside_the_isotope_distribution__1__to__2__,
                                          GetMassIDescripion(massIndex), isotopeDists.PeakIndexToMassIndex(0),
                                          isotopeDists.PeakIndexToMassIndex(isotopeDists.CountPeaks - 1)));
                    }
                    return isotopeDists.GetMassI(massIndex, decoyMassShift);
                }
                return GetPrecursorMass(seq, mods);                
            }

            int len = seq.Length - 1;

            bool nterm = type.IsNTerminal();
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
                    _aminoMasses[i] = _massCalc.CalculateMass(formula);
            }



            // ReSharper disable CharImplicitlyConvertedToNumeric
            // Handle values for non-amino acids
            // Wikipedia says Aspartic acid or Asparagine, this seems to be average of Cytosine and Cyanoalanine
            _aminoMasses['b'] = _aminoMasses['B'] =
                (_massCalc.CalculateMassFromFormula(@"C4H5NO3") + _massCalc.CalculateMassFromFormula(@"C4H6N2O2")) / 2;
            _aminoMasses['j'] = _aminoMasses['J'] = 0.0;
            _aminoMasses['x'] = _aminoMasses['X'] = 111.060000;    // Why?
            // Wikipedia says Glutamic acid or Glutamine
            _aminoMasses['z'] = _aminoMasses['Z'] =
                (_massCalc.CalculateMassFromFormula(@"C5H6ON2") + _massCalc.CalculateMassFromFormula(@"C5H8N2O2")) / 2;
            // ReSharper restore CharImplicitlyConvertedToNumeric
        }

        private static readonly ParsedMolecule[] AMINO_FORMULAS = new ParsedMolecule[128];
        private static readonly ParsedMolecule H2O = ParsedMolecule.Create(@"H2O");
        static SequenceMassCalc()
        {



            // ReSharper disable CharImplicitlyConvertedToNumeric
            // ReSharper disable LocalizableElement
            // CONSIDER(bspratt): what about B and Z? (see average values above for masses)
            AMINO_FORMULAS['a'] = AMINO_FORMULAS['A'] = ParsedMolecule.Create("C3H5ON");
            AMINO_FORMULAS['c'] = AMINO_FORMULAS['C'] = ParsedMolecule.Create("C3H5ONS");
            AMINO_FORMULAS['d'] = AMINO_FORMULAS['D'] = ParsedMolecule.Create("C4H5O3N");
            AMINO_FORMULAS['e'] = AMINO_FORMULAS['E'] = ParsedMolecule.Create("C5H7O3N");
            AMINO_FORMULAS['f'] = AMINO_FORMULAS['F'] = ParsedMolecule.Create("C9H9ON");
            AMINO_FORMULAS['g'] = AMINO_FORMULAS['G'] = ParsedMolecule.Create("C2H3ON");
            AMINO_FORMULAS['h'] = AMINO_FORMULAS['H'] = ParsedMolecule.Create("C6H7ON3");
            AMINO_FORMULAS['i'] = AMINO_FORMULAS['I'] = ParsedMolecule.Create("C6H11ON");
            AMINO_FORMULAS['k'] = AMINO_FORMULAS['K'] = ParsedMolecule.Create("C6H12ON2");
            AMINO_FORMULAS['l'] = AMINO_FORMULAS['L'] = ParsedMolecule.Create("C6H11ON");
            AMINO_FORMULAS['m'] = AMINO_FORMULAS['M'] = ParsedMolecule.Create("C5H9ONS");
            AMINO_FORMULAS['n'] = AMINO_FORMULAS['N'] = ParsedMolecule.Create("C4H6O2N2");
            AMINO_FORMULAS['o'] = AMINO_FORMULAS['O'] = ParsedMolecule.Create("C12H19N3O2");
            AMINO_FORMULAS['p'] = AMINO_FORMULAS['P'] = ParsedMolecule.Create("C5H7ON");
            AMINO_FORMULAS['q'] = AMINO_FORMULAS['Q'] = ParsedMolecule.Create("C5H8O2N2");
            AMINO_FORMULAS['r'] = AMINO_FORMULAS['R'] = ParsedMolecule.Create("C6H12ON4");
            AMINO_FORMULAS['s'] = AMINO_FORMULAS['S'] = ParsedMolecule.Create("C3H5O2N");
            AMINO_FORMULAS['t'] = AMINO_FORMULAS['T'] = ParsedMolecule.Create("C4H7O2N");
            AMINO_FORMULAS['u'] = AMINO_FORMULAS['U'] = ParsedMolecule.Create("C3H5NOSe");
            AMINO_FORMULAS['v'] = AMINO_FORMULAS['V'] = ParsedMolecule.Create("C5H9ON");
            AMINO_FORMULAS['w'] = AMINO_FORMULAS['W'] = ParsedMolecule.Create("C11H10ON2");
            AMINO_FORMULAS['y'] = AMINO_FORMULAS['Y'] = ParsedMolecule.Create("C9H9O2N");
            // ReSharper restore LocalizableElement
            // ReSharper restore CharImplicitlyConvertedToNumeric
        }

        public static ParsedMolecule GetAminoAcidFormula(char aa)
        {
            return AMINO_FORMULAS[aa];
        }

        public static ParsedMolecule GetHeavyFormula(char aa, LabelAtoms labelAtoms)
        {
            var formulaAA = AMINO_FORMULAS[aa];
            if (formulaAA == null)
                throw new ArgumentOutOfRangeException(string.Format(ModelResources.SequenceMassCalc_GetHeavyFormula_No_formula_found_for_the_amino_acid___0__, aa));

            return formulaAA.ChangeMolecule(GetHeavyFormula(formulaAA, labelAtoms).Difference(formulaAA).Molecule);
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

        public static ParsedMolecule GetHeavyFormula(string formulaString, LabelAtoms labelAtoms)
        {
            var formula = ParsedMolecule.Create(formulaString);
            return GetHeavyFormula(formula, labelAtoms);
        }

        public static ParsedMolecule GetHeavyFormula(ParsedMolecule formula, LabelAtoms labelAtoms)
        {
            if (labelAtoms == LabelAtoms.None)
            {
                return formula;
            }
            var substitutions = ALL_LABEL_SUBSTITUTIONS
                .Where(tuple => (tuple.Item1 & labelAtoms) != 0).ToArray();
            var result = new Dictionary<string, int>();
            bool bHasSubstitutions = false;
            foreach (var kvp in formula.Molecule)
            {
                var symbol = kvp.Key;
                var subTuple = substitutions.FirstOrDefault(tuple => tuple.Item2 == symbol);
                if (subTuple != null)
                {
                    symbol = subTuple.Item3;
                    bHasSubstitutions = true;
                }

                if (result.TryGetValue(symbol, out var count)) // In case two or more substitutions map to same original key
                {
                    result[symbol] = count + kvp.Value;
                }
                else
                {
                    result.Add(symbol, kvp.Value);
                }
            }

            return bHasSubstitutions ? formula.ChangeMolecule(Molecule.FromDict(result)).ChangeIsHeavy(true) : formula;
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

        public TypedMass GetPrecursorMass(CustomMolecule mol, TypedModifications mods, Adduct adductForIsotopeLabels, out ParsedMolecule isotopicFormula)
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
            return GetPrecursorMass(target.Molecule, null, Adduct.EMPTY, out _);
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

        public Adduct GetModifiedAdduct(Adduct adduct,  ParsedMolecule neutralFormula)
        {
            return HasLabels ? 
                SequenceMassCalc.GetModifiedAdduct(adduct, neutralFormula, _massCalcBase.Labels) : 
                adduct;
        }

        public double GetAAModMass(char aa, int seqIndex, int seqLength)
        {
            return _massCalcBase.GetAAModMass(aa, seqIndex, seqLength, _mods);
        }

        public MoleculeMassOffset GetMolecularFormula(string seq)
        {
            return _massCalcBase.GetNeutralFormula(seq, _mods);
        }

        public MassDistribution GetMzDistribution(Target target, Adduct adduct, IsotopeAbundances abundances)
        {
            return _massCalcBase.GetMzDistribution(target, adduct, abundances, _mods);
        }

        public MassDistribution GetMZDistribution(MoleculeMassOffset molecule, Adduct adduct, IsotopeAbundances abundances)
        {
            return _massCalcBase.GetMZDistribution(molecule, adduct, abundances);
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
