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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public enum IonType
    {
        precursor = -2, custom = -1, a, b, c, x, y, z, zh, zhh
    }

    public static class IonTypeExtension
    {
        private static readonly string[] VALUES = {string.Empty, string.Empty, @"a", @"b", @"c", @"x", @"y", @"z", @"z" + '\u2022', @"z" + '\u2032' };
        private static readonly Dictionary<IonType, string[]> INPUT_ALIASES = new Dictionary<IonType, string[]>()
        {
            {IonType.zh, new[]{@"z.", @"z*"}},
            { IonType.zhh, new[]{@"z'", @"z"""}}
        };

        private static readonly Color COLOR_A = Color.YellowGreen;
        private static readonly Color COLOR_X = Color.Green;
        private static readonly Color COLOR_B = Color.BlueViolet;
        private static readonly Color COLOR_Y = Color.Blue;
        private static readonly Color COLOR_C = Color.Orange;
        private static readonly Color COLOR_Z = Color.OrangeRed;
        private static readonly Color COLOR_ZH = Color.Brown;
        private static readonly Color COLOR_ZHH = Color.Sienna;
        private static readonly Color COLOR_OTHER_IONS = Color.DodgerBlue; // Other ion types, as in small molecule
        private static readonly Color COLOR_PRECURSOR = Color.DarkCyan;
        private static readonly Color COLOR_NONE = COLOR_A;

        private static string[] LOCALIZED_VALUES
        {
            get
            {
                VALUES[0] = ModelResources.IonTypeExtension_LOCALIZED_VALUES_precursor;
                VALUES[1] = ModelResources.IonTypeExtension_LOCALIZED_VALUES_custom;
                return VALUES;
            }
        }
        public static string GetLocalizedString(this IonType val)
        {
            return LOCALIZED_VALUES[(int) val + 2]; // To include precursor and custom
        }

        public static IEnumerable<string> GetInputAliases(this IonType val)
        {
            if (!INPUT_ALIASES.ContainsKey(val))
                return new[] {val.ToString()};
            return INPUT_ALIASES[val].Concat(new [] {val.ToString()});
        }

        public static IonType GetEnum(string enumValue)
        {
            int i = LOCALIZED_VALUES.IndexOf(v => Equals(v, enumValue));
            if (i >= 0)
                return (IonType) (i-2);
            var result = INPUT_ALIASES.Keys.First(ion => GetInputAliases(ion).Any(str => str.Equals(enumValue)));
            return result;
        }

        public static bool IsNTerminal(this IonType type)
        {
            return type == IonType.a || type == IonType.b || type == IonType.c || type == IonType.precursor;
        }

        public static bool IsCTerminal(this IonType type)
        {
            return type == IonType.x || type == IonType.y || type == IonType.z || type == IonType.zh || type == IonType.zhh;
        }

        public static List<IonType> GetFragmentList()
        {
            return Enumerable.Range(0, 1 + (int) IonType.zhh).Select(i => (IonType) i).ToList();
        }

        public static Color GetTypeColor(IonType? type, int rank = 0)
        {
            Color color;
            if(!type.HasValue)
                return COLOR_NONE;

            switch (type)
            {
                default: color = COLOR_NONE; break;
                case IonType.a: color = COLOR_A; break;
                case IonType.x: color = COLOR_X; break;
                case IonType.b: color = COLOR_B; break;
                case IonType.y: color = COLOR_Y; break;
                case IonType.c: color = COLOR_C; break;
                case IonType.z: color = COLOR_Z; break;
                case IonType.zh: color = COLOR_ZH; break;
                case IonType.zhh:color = COLOR_ZHH; break;
                case IonType.custom: color = (rank > 0) ? COLOR_OTHER_IONS : COLOR_NONE; break; // Small molecule fragments - only color if ranked
                case IonType.precursor: color = COLOR_PRECURSOR; break;
            }
            return color;
        }
    }

    public class Transition : Identity
    {
        public const int MIN_PRODUCT_CHARGE = 1;
        public const int MAX_PRODUCT_CHARGE = 20;

        public const int MIN_PRODUCT_DECOY_MASS_SHIFT = -5;
        public const int MAX_PRODUCT_DECOY_MASS_SHIFT = 5;

        /// <summary>
        /// MProphet reverse decoy algorithm requires these mass shifts for
        /// singly charged product ions
        /// </summary>
        private static readonly int[] MPROPHET_REVERSED_MASS_SHIFTS = {8, -8, 10, -10};

        public static ICollection<int> MassShifts { get { return MASS_SHIFTS; } }

        private static readonly HashSet<int> MASS_SHIFTS = new HashSet<int>(MassShiftEnum);

        private static IEnumerable<int> MassShiftEnum
        {
            get
            {
                for (int i = MIN_PRODUCT_DECOY_MASS_SHIFT; i <= MAX_PRODUCT_DECOY_MASS_SHIFT; i++)
                {
                    if (i != 0)
                        yield return i;
                }
                foreach (var i in MPROPHET_REVERSED_MASS_SHIFTS)
                {
                    yield return i;
                }
            }
        }

        /// <summary>
        /// Prioritized list of default product ion charges
        /// </summary>
        public static readonly Adduct[] DEFAULT_PEPTIDE_CHARGES = { Adduct.SINGLY_PROTONATED, Adduct.DOUBLY_PROTONATED, Adduct.TRIPLY_PROTONATED };
        public static readonly Adduct[] DEFAULT_PEPTIDE_LIBRARY_CHARGES = { Adduct.SINGLY_PROTONATED, Adduct.DOUBLY_PROTONATED, Adduct.TRIPLY_PROTONATED, Adduct.QUADRUPLY_PROTONATED };
        public static readonly Adduct[] DEFAULT_MOLECULE_CHARGES = { Adduct.M_MINUS_3H, Adduct.M_MINUS_2H, Adduct.M_MINUS_H, Adduct.M_MINUS, Adduct.M_PLUS, Adduct.M_PLUS_H, Adduct.M_PLUS_2H, Adduct.M_PLUS_3H };
        public static readonly Adduct[] DEFAULT_MOLECULE_FRAGMENT_CHARGES = { Adduct.M_MINUS, Adduct.M_MINUS_2, Adduct.M_MINUS_3, Adduct.M_PLUS, Adduct.M_PLUS_2, Adduct.M_PLUS_3 };

        /// <summary>
        /// Prioritize, paired list of non-custom product ion types
        /// </summary>
        public static readonly IonType[] PEPTIDE_ION_TYPES =
            {IonType.y, IonType.b, IonType.z, IonType.c, IonType.x, IonType.a, IonType.zh, IonType.zhh};
        // And its small molecule equivalent
        public static readonly IonType[] MOLECULE_ION_TYPES = { IonType.custom };
        public static readonly IonType[] DEFAULT_MOLECULE_FILTER_ION_TYPES = { IonType.custom }; 

        public static readonly int[] PEPTIDE_ION_TYPES_ORDERS;

        static Transition()
        {
            PEPTIDE_ION_TYPES_ORDERS = new int[PEPTIDE_ION_TYPES.Length];
            for (int i = 0; i < PEPTIDE_ION_TYPES.Length; i++)
            {
                PEPTIDE_ION_TYPES_ORDERS[(int) PEPTIDE_ION_TYPES[i]] = i;
            }
        }

        public static bool IsPrecursor(IonType type)
        {
            return type == IonType.precursor;
        }

        public static bool IsPeptideFragment(IonType type)
        {
            return type >= IonType.a;
        }

        public static bool IsCustom(IonType type, TransitionGroup parent)
        {
            return type == IonType.custom || (type == IonType.precursor && parent.IsCustomIon);
        }

        public static IonType[] GetTypePairs(ICollection<IonType> types)
        {
            var listTypes = new List<IonType>();
            for (int i = 0; i < PEPTIDE_ION_TYPES.Length; i++)
            {
                if (types.Contains(PEPTIDE_ION_TYPES[i]))
                {
                    if (i % 2 == 0)
                        i++;
                    listTypes.Add(PEPTIDE_ION_TYPES[i - 1]);
                    listTypes.Add(PEPTIDE_ION_TYPES[i]);
                }
            }
            return listTypes.ToArray();
        }

        public static char GetFragmentNTermAA(string sequence, int cleavageOffset)
        {
            return sequence[cleavageOffset + 1];
        }

        public static char GetFragmentCTermAA(string sequence, int cleavageOffset)
        {
            return sequence[cleavageOffset];
        }

        public static int OrdinalToOffset(IonType type, int ordinal, int len)
        {
            if (type.IsNTerminal())
                return ordinal - 1;
            else
                return len - ordinal - 1;
        }

        public static int OffsetToOrdinal(IonType type, int offset, int len)
        {
            if (type.IsNTerminal() || type==IonType.custom) // Custom for small molecule work
                return offset + 1;
            else
                return len - offset - 1;
        }

        public static string GetChargeIndicator(Adduct adduct)
        {
            return GetChargeIndicator(adduct, LocalizationHelper.CurrentCulture);
        }

        public static string GetChargeIndicator(Adduct adduct, CultureInfo cultureInfo)
        {
            return !adduct.IsProteomic && !adduct.IsChargeOnly
                ? adduct.AsFormulaOrSignedInt()
                : GetChargeIndicator(adduct.AdductCharge, cultureInfo);
        }

        public static string GetChargeIndicator(int charge)
        {
            return GetChargeIndicator(charge, LocalizationHelper.CurrentCulture);
        }

        public static string GetChargeIndicator(int charge, CultureInfo cultureInfo)
        {
            if (charge >= 0)
            {
                const string pluses = "++++";
                return charge <= pluses.Length
                    ? pluses.Substring(0, Math.Min(charge, pluses.Length))
                    : string.Format(@"{0} +{1}", GetChargeSeparator(cultureInfo), charge);
            }
            else
            {
                const string minuses = "--";
                charge = -charge;
                return charge <= minuses.Length
                    ? minuses.Substring(0, Math.Min(charge, minuses.Length))
                    : string.Format(@"{0} -{1}", GetChargeSeparator(cultureInfo), charge);
            }
        }

        private static string GetChargeSeparator(CultureInfo cultureInfo)
        {
            return cultureInfo.TextInfo.ListSeparator;
        }

        public static int FindAdductDescription(string line, out Adduct adduct)
        {
            // Check for adduct description
            var chargePos = -1;
            adduct = Adduct.EMPTY;
            var adductStart = line.LastIndexOf('[');
            if (adductStart >= 0)
            {
                if (adductStart + 2 > line.Length || line[adductStart + 1] == '+')
                {
                    // It was probably a modification like "[+57]", and we're being called by StripChargeIndicators on a peptide
                    return chargePos;
                }
                var adductText = line.Substring(adductStart);
                if (adductStart > 0 && line[adductStart - 1] == '(')
                {
                    adductText = adductText.TrimEnd(')', ' ');
                    adductStart--; // Consider adduct description as beginning at start of enclosing parens
                }
                if (!Adduct.TryParse(adductText, out adduct))
                {
                    // Whatever it was, it's not an adduct
                    return chargePos;
                }
                chargePos = adductStart;
            }
            return chargePos;
        }

        public static string StripChargeIndicators(string text, int min, int max, bool knownProteomic = false)
        {
            if (!MayHaveChargeIndicator(text))
                return text;

            var sequences = new List<string>();
            var consecutiveLinesWithoutChargeIndicators = 0;
            foreach (var line in text.Split('\n').Select(seq => seq.Trim()))
            {
                if (consecutiveLinesWithoutChargeIndicators > 1000)
                {
                    sequences.Add(line); // If we haven't seen anything like "PEPTIDER+++" by now, we aren't going to 
                    continue;
                }
                // Allow any run of charge indicators no matter how long, because users guess this might work
                int chargePos = FindChargeSymbolRepeatStart(line, min, max);
                var chargeSepLocal = GetChargeSeparator(CultureInfo.CurrentCulture);
                if (chargePos == -1)
                {
                    // Or any formal protonated charge state indicator
                    if (line.Contains(chargeSepLocal))
                        chargePos = FindChargeIndicatorPos(line, min, max, CultureInfo.CurrentCulture);
                    // Or the US/Invariant formatted version
                    if (chargePos == -1)
                    {
                        var chargeSepInvariant = GetChargeSeparator(CultureInfo.InvariantCulture);
                        if (!Equals(chargeSepLocal, chargeSepInvariant) && line.Contains(chargeSepInvariant))
                            chargePos = FindChargeIndicatorPos(line, min, max, CultureInfo.InvariantCulture);
                    }
                }
                if (chargePos == -1 && !knownProteomic)
                {
                    // Check for adduct description
                    Adduct adduct;
                    var adductStart = FindAdductDescription(line, out adduct);
                    if (adductStart >= 0)
                    {
                        var z = Math.Abs(adduct.AdductCharge);
                        if (min <= z && z <= max)
                        {
                            chargePos = adductStart;
                        }
                    }
                }

                if (chargePos == -1)
                {
                    consecutiveLinesWithoutChargeIndicators++;
                }
                else
                {
                    consecutiveLinesWithoutChargeIndicators = 0;
                }
                sequences.Add(chargePos == -1 ? line : line.Substring(0, chargePos));
            }
            return TextUtil.LineSeparate(sequences);
        }

        private static int FindChargeSymbolRepeatStart(string line, int min, int max)
        {
            int chargePos = FindChargeSymbolRepeatStart('+', line, min, max);
            if (chargePos == -1)
                chargePos = FindChargeSymbolRepeatStart('-', line, min, max);
            return chargePos;
        }

        private static int FindChargeSymbolRepeatStart(char c, string line, int min, int max)
        {
            int countCharges = CountEndsWith(c, line);
            if (min <= countCharges && countCharges <= max)
                return line.Length - countCharges;
            return -1;
        }

        private static int CountEndsWith(char c, string line)
        {
            int i = line.Length - 1;
            while (i >= 0 && line[i] == c)
                i--;
            i++; // Advance to the last position of a c
            if (i < line.Length)
                return line.Length - i;
            return -1;
        }

        private static int FindChargeIndicatorPos(string line, int min, int max, CultureInfo cultureInfo)
        {
            for (int i = max; i >= min; i--)
            {
                // Handle negative charges
                int pos = FindChargeIndicatorPos(line, GetChargeIndicator(Adduct.FromChargeProtonated(-i), cultureInfo));
                if (pos != -1)
                    return pos;
                // Handle positive charges
                pos = FindChargeIndicatorPos(line, GetChargeIndicator(Adduct.FromChargeProtonated(i), cultureInfo));
                if (pos != -1)
                    return pos;
            }

            return -1;
        }

        private static int FindChargeIndicatorPos(string line, string charge)
        {
            if (line.EndsWith(charge))
                return line.Length - charge.Length;
            // Try without the space, if the indicator contains a space
            if (charge.Contains(' '))
            {
                var chargeCompact = charge.Replace(@" ", string.Empty);
                if (line.EndsWith(chargeCompact))
                    return line.Length - chargeCompact.Length;
            }
            return -1;
        }

        public static bool MayHaveChargeIndicator(string text)
        {
            foreach (char c in text)
            {
                if (c == '-' || c == '+' || c== '[')  // looking for something like +++, --, or [M+Na]
                    return true;
            }
            return false;
        }

        public static Adduct GetChargeFromIndicator(string text, int min, int max)
        {
            return GetChargeFromIndicator(text, min, max, out _);
        }

        public static Adduct GetChargeFromIndicator(string text, int min, int max, Adduct defaultVal)
        {
            var result = GetChargeFromIndicator(text, min, max);
            return result.IsEmpty ? defaultVal : result;
        }

        public static Adduct GetChargeFromIndicator(string text, int min, int max, out int foundAt)
        {
            foundAt = -1;
            if (!MayHaveChargeIndicator(text))
            {
                return Adduct.EMPTY;
            }

            // Handle runs of charge characters no matter how long, because users guess this should work
            foundAt = FindChargeSymbolRepeatStart('+', text, min, max);
            if (foundAt != -1)
                return Adduct.FromChargeProtonated(text.Length - foundAt);
            foundAt = FindChargeSymbolRepeatStart('-', text, min, max);
            if (foundAt != -1)
                return Adduct.FromChargeProtonated(foundAt - text.Length);

            Adduct adduct;
            for (int i = max; i >= min; i--)
            {
                adduct = GetChargeFromIndicator(text, i, out foundAt);
                if (!adduct.IsEmpty)
                    return adduct;
                adduct = GetChargeFromIndicator(text, -i, out foundAt);
                if (!adduct.IsEmpty)
                    return adduct;
            }
            foundAt = FindAdductDescription(text, out adduct);
            if (foundAt != -1)
                return adduct;
            return Adduct.EMPTY;
        }

        private static Adduct GetChargeFromIndicator(string text, int i, out int foundAt)
        {
            var adduct = Adduct.FromChargeProtonated(i);
            foundAt = FindChargeIndicatorPos(text, GetChargeIndicator(adduct));
            if (foundAt == -1 && GetChargeSeparator(CultureInfo.CurrentCulture) != GetChargeSeparator(CultureInfo.InvariantCulture))
                foundAt = FindChargeIndicatorPos(text, GetChargeIndicator(adduct, CultureInfo.InvariantCulture));
            return foundAt != -1 ? adduct : Adduct.EMPTY;
        }

        public static string GetMassIndexText(int massIndex)
        {
            if (massIndex == 0)
                return string.Empty;

            return @" " + SequenceMassCalc.GetMassIDescripion(massIndex);
        }

        public static string GetDecoyText(int? decoyMassShift)
        {
            if (!decoyMassShift.HasValue || decoyMassShift.Value == 0)
                return string.Empty;
            return string.Format(@"({0}{1})",
                                 decoyMassShift.Value >= 0 ? @"+" : string.Empty,
                                 decoyMassShift.Value);
        }

        private readonly TransitionGroup _group;

        /// <summary>
        /// Creates a precursor transition
        /// </summary>
        /// <param name="group">The <see cref="TransitionGroup"/> which the transition represents</param>
        /// <param name="massIndex">Isotope mass shift</param>
        /// <param name="productAdduct">Adduct on the transition</param>
        /// <param name="customMolecule">Non-null if this is a custom transition</param>
        public Transition(TransitionGroup group, int massIndex, Adduct productAdduct, CustomMolecule customMolecule = null)
            : this(group, IonType.precursor, group.Peptide.Length - 1, massIndex, productAdduct, null, customMolecule)
        {
        }

        public Transition(TransitionGroup group, IonType type, int offset, int massIndex, Adduct charge)
            :this(group, type, offset, massIndex, charge, null)
        {
        }

        public Transition(TransitionGroup group, Adduct charge, int? massIndex, CustomMolecule customMolecule, IonType type=IonType.custom)
            :this(group, type, null, massIndex, charge, null, customMolecule)
        {
        }

        public Transition(TransitionGroup group, IonType type, int? offset, int? massIndex, Adduct adduct,
            int? decoyMassShift, CustomMolecule customMolecule = null)
        {
            _group = group;

            IonType = type;
            CleavageOffset = offset ?? 0;
            MassIndex = massIndex ?? 0;
            Adduct = adduct;
            DecoyMassShift = decoyMassShift;
            // Small molecule precursor transition should have same custom molecule as parent
            if (IsPrecursor(type) && group.IsCustomIon)
            {
                CustomIon = new CustomIon(group.CustomMolecule, adduct);
            }
            else if (customMolecule is CustomIon)
            {
                // As with reporter ions
                CustomIon = (CustomIon)customMolecule;
                Assume.IsTrue(Equals(adduct.AdductCharge, CustomIon.Adduct.AdductCharge));
                Adduct = CustomIon.Adduct; // Ion mass is part of formula, so use charge only adduct
            }
            else if (customMolecule != null)
            {
                CustomIon = new CustomIon(customMolecule, adduct);
            }
            // Derived values
            if (!IsCustom(type, group))
            {
                Peptide peptide = group.Peptide;
                Ordinal = OffsetToOrdinal(type, (int)offset, peptide.Length);
                AA = (IsNTerminal()
                    ? peptide.Sequence[(int)offset]
                    : peptide.Sequence[(int)offset + 1]);
            }
            else
            {
                // caller may have passed in offset = group.Peptide.Length - 1, which for custom ions gives -1
                CleavageOffset = 0;
            }
            Validate();
        }

        public TransitionGroup Group
        {
            get { return _group; }
        }

        // NB an adduct (rather than a simple integer charge) is arguably overkill for transitions that are not precursors, 
        // but it simplifies the code to treat them all as having a potentially complex charge mechanism
        public Adduct Adduct { get; private set; } 
        public int Charge { get { return Adduct.AdductCharge; } } 
        public IonType IonType { get; private set; }

        public int CleavageOffset { get; private set; }
        public int MassIndex { get; private set; }
        public int? DecoyMassShift { get; private set; }

        public CustomIon CustomIon { get; private set; } // May be instantiated as a CustomIon or SettingsCustomIon

        // Derived values
        public int Ordinal { get; private set; }
        public char AA { get; private set; }

        public string FragmentIonName
        {
            get { return GetFragmentIonName(LocalizationHelper.CurrentCulture); }
        }

        public string GetFragmentIonName(CultureInfo cultureInfo, MzTolerance tolerance=null)
        {
            if (IsCustom() && !IsPrecursor())
                return CustomIon.ToString(tolerance);
            string ionName = ReferenceEquals(cultureInfo, CultureInfo.InvariantCulture)
                ? IonType.ToString() : IonType.GetLocalizedString();
            if (!IsPrecursor())
                ionName += Ordinal;
            return ionName;
        }

        public bool IsNTerminal()
        {
            return IonType.IsNTerminal();
        }

        public bool IsCTerminal()
        {
            return IonType.IsCTerminal();
        }

        public bool IsPrecursor()
        {
            return IsPrecursor(IonType);
        }

        public bool IsNegative()
        {
            return Charge < 0;
        }

        public bool IsCustom()
        {
            return IsCustom(IonType, Group);
        }

        public bool ParticipatesInScoring => !IsReporterIon(); // Don't include things like TMT in retention time calcs

        public bool IsNonPrecursorNonReporterCustomIon()
        {
            return !IsPrecursor() && IsNonReporterCustomIon();
        }

        public bool IsNonReporterCustomIon()
        {
            return IsCustom() && !(CustomIon is SettingsCustomIon);
        }

        public bool IsReporterIon()
        {
            return IsCustom() && (CustomIon is SettingsCustomIon);
        }

        public char FragmentNTermAA
        {
            get { return GetFragmentNTermAA(_group.Peptide.Sequence, CleavageOffset); }
        }

        public char FragmentCTermAA
        {
            get { return GetFragmentCTermAA(_group.Peptide.Sequence, CleavageOffset); }
        }

        public static TypedMass CalcMass(TypedMass massH, TransitionLosses losses)
        {
            return massH - (losses != null ? losses.Mass : 0);
        }

        private void Validate()
        {
            if (IsCustom())
            {
                if (IsPrecursor())
                {
                    if (TransitionGroup.MIN_PRECURSOR_CHARGE > Math.Abs(Charge) || Math.Abs(Charge) > TransitionGroup.MAX_PRECURSOR_CHARGE)
                    {
                        throw new InvalidDataException(
                            string.Format(Resources.Transition_Validate_Precursor_charge__0__must_be_non_zero_and_between__1__and__2__,
                            Charge, -TransitionGroup.MAX_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE));
                    }
                }
                else if (MIN_PRODUCT_CHARGE > Math.Abs(Charge) || Math.Abs(Charge) > MAX_PRODUCT_CHARGE)
                {
                    throw new InvalidDataException(
                        string.Format(Resources.Transition_Validate_Product_ion_charge__0__must_be_non_zero_and_between__1__and__2__,
                                                                 Charge, -MAX_PRODUCT_CHARGE, MAX_PRODUCT_CHARGE));
                }
            }
            else
            {
                if (IsPrecursor())
                {
                    if (TransitionGroup.MIN_PRECURSOR_CHARGE > Charge || Charge > TransitionGroup.MAX_PRECURSOR_CHARGE)
                    {
                        throw new InvalidDataException(
                            string.Format(ModelResources.Transition_Validate_Precursor_charge__0__must_be_between__1__and__2__,
                            Charge, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE));                    
                    }
                }
                else if (MIN_PRODUCT_CHARGE > Charge || Charge > MAX_PRODUCT_CHARGE)
                {
                    throw new InvalidDataException(
                        string.Format(ModelResources.Transition_Validate_Product_ion_charge__0__must_be_between__1__and__2__,
                                                                 Charge, MIN_PRODUCT_CHARGE, MAX_PRODUCT_CHARGE));
                }
            }
            if (CustomIon != null)
            {
                if (!IsCustom())
                {
                    throw new InvalidDataException(
                        string.Format(
                            ModelResources.Transition_Validate_A_transition_of_ion_type__0__can_t_have_a_custom_ion, IonType));
                }    
            }
            else if (IsCustom())
            {
                    throw new InvalidDataException(
                        string.Format(
                           ModelResources.Transition_Validate_A_transition_of_ion_type__0__must_have_a_custom_ion_, IonType));
            }
            else
            {
                if (Ordinal < 1)
                    throw new InvalidDataException(string.Format(ModelResources.Transition_Validate_Fragment_ordinal__0__may_not_be_less_than__1__, Ordinal));
                if (IsPrecursor())
                {
                    if (Ordinal != Group.Peptide.Length)
                        throw new InvalidDataException(string.Format(ModelResources.Transition_Validate_Precursor_ordinal_must_be_the_lenght_of_the_peptide));
                }
                else if (Ordinal > Group.Peptide.Length - 1)
                {
                    throw new InvalidDataException(
                        string.Format(ModelResources.Transition_Validate_Fragment_ordinal__0__exceeds_the_maximum__1__for_the_peptide__2__,
                            Ordinal, Group.Peptide.Length - 1, Group.Peptide.Target));
                }

                if (DecoyMassShift.HasValue)
                {
                    var minShift = IsPrecursor() ? TransitionGroup.MIN_PRECURSOR_DECOY_MASS_SHIFT : MIN_PRODUCT_DECOY_MASS_SHIFT;
                    var maxShift = IsPrecursor() ? TransitionGroup.MAX_PRECURSOR_DECOY_MASS_SHIFT : MAX_PRODUCT_DECOY_MASS_SHIFT;
                    if ((DecoyMassShift.Value < minShift || DecoyMassShift.Value > maxShift) &&
                        !MPROPHET_REVERSED_MASS_SHIFTS.Contains(i => i == DecoyMassShift.Value))
                    {
                        throw new InvalidDataException(
                            string.Format(ModelResources.Transition_Validate_Fragment_decoy_mass_shift__0__must_be_between__1__and__2__,
                                DecoyMassShift, MIN_PRODUCT_DECOY_MASS_SHIFT, MAX_PRODUCT_DECOY_MASS_SHIFT));
                    }
                }
            }
            if (Charge < 0 != Group.PrecursorAdduct.AdductCharge < 0)
            {
                throw new InvalidDataException(Resources.Transition_Validate_Precursor_and_product_ion_polarity_do_not_agree_);
            }
        }

        /// <summary>
        /// True if a given transition is equivalent to this, ignoring the
        /// transition group.
        /// </summary>
        public bool Equivalent(Transition obj)
        {
            return Equivalent(this, obj);
        }

        public static bool Equivalent(Transition t, Transition obj)
        {
            if (ReferenceEquals(t, obj))
            {
                return true;
            }
            return Equals(obj.IonType, t.IonType) &&
                obj.CleavageOffset == t.CleavageOffset &&
                obj.Charge == t.Charge &&
                obj.MassIndex == t.MassIndex &&
                CustomMolecule.Equivalent(obj.CustomIon, t.CustomIon) && // Looks at unlabeled formula or name only
                (obj.DecoyMassShift.Equals(t.DecoyMassShift) || 
                // Deal with strange case of mProphet golden standard data set - only a concern for peptides, not small molecules
                (obj.DecoyMassShift.HasValue && t.DecoyMassShift.HasValue &&
                    (obj.Group.LabelType.IsLight && obj.DecoyMassShift == 0 && !t.Group.LabelType.IsLight && t.DecoyMassShift != 0) ||
                    (!obj.Group.LabelType.IsLight && obj.DecoyMassShift != 0 && t.Group.LabelType.IsLight && t.DecoyMassShift == 0)));
        }

        public static int GetEquivalentHashCode(Transition t)
        {
            unchecked
            {
                int result = t.IonType.GetHashCode();
                result = (result * 397) ^ t.CleavageOffset;
                result = (result * 397) ^ t.MassIndex;
                result = (result * 397) ^ t.Charge;
                result = (result * 397) ^ (t.DecoyMassShift ?? 0);
                result = (result * 397) ^ (t.CustomIon != null ? t.CustomIon.GetEquivalentHashCode() : 0);
                return result;
            }
        }
        public bool IncludesAaIndex(int aaIndex)
        {
            switch (IonType)
            {
                case IonType.precursor:
                    return true;
                case IonType.a:
                case IonType.b:
                case IonType.c:
                    return CleavageOffset >= aaIndex;
                case IonType.x:
                case IonType.y:
                case IonType.z:
                    return CleavageOffset < aaIndex;
                default:
                    return true;
            }
        }


        #region object overrides

        public bool Equals(Transition obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            var equal = Equals(obj._group, _group) &&
                obj.IonType == IonType &&
                Equals(obj.CustomIon, CustomIon) && 
                obj.CleavageOffset == CleavageOffset &&
                obj.MassIndex == MassIndex &&
                Equals(obj.Adduct, Adduct) && 
                obj.DecoyMassShift.Equals(DecoyMassShift);
            return equal; // For debugging convenience
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (Transition)) return false;
            return Equals((Transition) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = _group.GetHashCode();
                result = (result*397) ^ IonType.GetHashCode();
                result = (result*397) ^ CleavageOffset;
                result = (result*397) ^ MassIndex;
                result = (result*397) ^ Adduct.GetHashCode();
                result = (result*397) ^ (DecoyMassShift.HasValue ? DecoyMassShift.Value : 0);
                result = (result*397) ^ (CustomIon != null ? CustomIon.GetHashCode() : 0);
                return result;
            }
        }

        public override string ToString()
        {
            if (IsPrecursor())
            {
                return ModelResources.Transition_ToString_precursor + GetChargeIndicator(Adduct) +
                       GetMassIndexText(MassIndex);
            }

            if (IsCustom())
            {
                var text = CustomIon.ToString();
                // Was there enough information to generate a string more distinctive that just "Ion"?
                if (String.IsNullOrEmpty(CustomIon.Name) && 
                    CustomIon.ParsedMolecule.IsMassOnly)
                {
                    // No, add mz and charge to whatever generic text was used to describe it
                    var mz = Adduct.MzFromNeutralMass(CustomIon.MonoisotopicMass);
                    return string.Format(@"{0} {1:F04}{2}",
                        text, mz, GetChargeIndicator(Adduct));
                }
                return text;
            }

            return string.Format(@"{0} - {1}{2}{3}{4}",
                                 AA,
                                 IonType.ToString().ToLowerInvariant(),
                                 Ordinal,
                                 GetDecoyText(DecoyMassShift),
                                 GetChargeIndicator(Adduct));
        }

        #endregion // object overrides
    }

    public sealed class TransitionLossKey
    {
        public TransitionLossKey(TransitionGroupDocNode parent, TransitionDocNode transition, TransitionLosses losses)
        {
            Transition = transition.Transition;
            Losses = losses;
            ComplexFragmentIonName = transition.ComplexFragmentIon.GetName();
            if (Transition.IsCustom() && !transition.ComplexFragmentIon.IsCrosslinked)
            {
                if (!string.IsNullOrEmpty(transition.PrimaryCustomIonEquivalenceKey))
                    CustomIonEquivalenceTestValue = transition.PrimaryCustomIonEquivalenceKey;
                else if (!string.IsNullOrEmpty(transition.SecondaryCustomIonEquivalenceKey))
                    CustomIonEquivalenceTestValue = transition.SecondaryCustomIonEquivalenceKey;
                else if (Transition.IsNonReporterCustomIon())
                    CustomIonEquivalenceTestValue = @"_mzSortIndex_" + parent.Children.IndexOf(transition);
                else
                    CustomIonEquivalenceTestValue = null;
            }
            else
            {
                CustomIonEquivalenceTestValue = null;
            }
        }

        public Transition Transition { get; private set; }
        public TransitionLosses Losses { get; private set; }
        public string CustomIonEquivalenceTestValue { get; private set;  }
        public IonChain ComplexFragmentIonName { get; private set; }

        public bool Equivalent(TransitionLossKey other)
        {
            return Equals(CustomIonEquivalenceTestValue, other.CustomIonEquivalenceTestValue) &&
                   other.Transition.Equivalent(Transition) &&
                   Equals(other.Losses, Losses) &&
                   Equals(other.ComplexFragmentIonName, ComplexFragmentIonName);
        }

        #region object overrides

        public bool Equals(TransitionLossKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Transition, Transition) && Equals(other.Losses, Losses) &&
                   Equals(other.ComplexFragmentIonName, ComplexFragmentIonName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (TransitionLossKey)) return false;
            return Equals((TransitionLossKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Transition.GetHashCode();
                result = (result * 397) ^ (Losses != null ? Losses.GetHashCode() : 0);
                result = (result * 397) ^ (ComplexFragmentIonName != null ? ComplexFragmentIonName.GetHashCode() : 0);
                return result;
            }
        }

        public override string ToString()
        {
            return Transition + (Losses != null ? @" -" + Losses.Mass : string.Empty);
        }

        #endregion
    }
}
