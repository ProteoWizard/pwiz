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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public enum IonType
    {
         precursor = -2, custom = -1, a, b, c, x, y, z
    }

    public static class IonTypeExtension
    {
        private static readonly string[] VALUES = {string.Empty, string.Empty, "a", "b", "c", "x", "y", "z"}; // Not L10N

        private static string[] LOCALIZED_VALUES
        {
            get
            {
                VALUES[0] = Resources.IonTypeExtension_LOCALIZED_VALUES_precursor;
                VALUES[1] = Resources.IonTypeExtension_LOCALIZED_VALUES_custom;
                return VALUES;
            }
        }
        public static string GetLocalizedString(this IonType val)
        {
            return LOCALIZED_VALUES[(int) val + 2]; // To include precursor and custom
        }

        public static IonType GetEnum(string enumValue)
        {
            return Helpers.EnumFromLocalizedString<IonType>(enumValue, LOCALIZED_VALUES);
        }

        public static IonType GetEnum(string enumValue, IonType defaultValue)
        {
            return Helpers.EnumFromLocalizedString(enumValue, LOCALIZED_VALUES, defaultValue);
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
            {IonType.y, IonType.b, IonType.z, IonType.c, IonType.x, IonType.a};
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

        public static bool IsNTerminal(IonType type)
        {
            return type == IonType.a || type == IonType.b || type == IonType.c || type == IonType.precursor;
        }

        public static bool IsCTerminal(IonType type)
        {
            return type == IonType.x || type == IonType.y || type == IonType.z;
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
            if (IsNTerminal(type))
                return ordinal - 1;
            else
                return len - ordinal - 1;
        }

        public static int OffsetToOrdinal(IonType type, int offset, int len)
        {
            if (IsNTerminal(type) || type==IonType.custom) // Custom for small molecule work
                return offset + 1;
            else
                return len - offset - 1;
        }

        public static string GetChargeIndicator(Adduct adduct)
        {
            if (!adduct.IsProteomic && !adduct.IsChargeOnly)
            {
                return adduct.AsFormulaOrSignedInt();
            }
            var charge = adduct.AdductCharge;
            if (charge >= 0)
            {
                const string pluses = "++++"; // Not L10N
                return charge <= pluses.Length
                    ? pluses.Substring(0, Math.Min(charge, pluses.Length))
                    : string.Format("{0} +{1}", LocalizationHelper.CurrentCulture.NumberFormat.NumberGroupSeparator, charge); // Not L10N
            }
            else
            {
                const string minuses = "--"; // Not L10N
                charge = -charge;
                return charge <= minuses.Length
                    ? minuses.Substring(0, Math.Min(charge, minuses.Length))
                    : string.Format("{0} -{1}", LocalizationHelper.CurrentCulture.NumberFormat.NumberGroupSeparator, charge); // Not L10N
            }
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
                if (!Adduct.TryParse(adductText, out adduct)) // Not L10N
                {
                    // Whatever it was, it's not an adduct
                    return chargePos;
                }
                chargePos = adductStart;
            }
            return chargePos;
        }

        public static string StripChargeIndicators(string text, int min, int max)
        {
            if (!MayHaveChargeIndicator(text))
                return text;

            var sequences = new List<string>();
            foreach (var line in text.Split('\n').Select(seq => seq.Trim()))
            {
                int chargePos = -1;
                for (int i = max; i >= min; i--)
                {
                    // Handle negative charges
                    var charge = GetChargeIndicator(Adduct.FromChargeProtonated(-i));
                    if (line.EndsWith(charge))
                    {
                        chargePos = line.LastIndexOf(charge, StringComparison.CurrentCulture);
                        break;
                    }
                    charge = GetChargeIndicator(Adduct.FromChargeProtonated(i));
                    if (line.EndsWith(charge))
                    {
                        chargePos = line.LastIndexOf(charge, StringComparison.CurrentCulture);
                        break;
                    }
                }
                if (chargePos == -1)
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
                sequences.Add(chargePos == -1 ? line : line.Substring(0, chargePos));
            }
            return TextUtil.LineSeparate(sequences);
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
            int foundAt;
            return GetChargeFromIndicator(text, min, max, out foundAt);
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
            Adduct adduct;
            for (int i = max; i >= min; i--)
            {
                // Handle negative charges
                adduct = Adduct.FromChargeProtonated(-i);
                var chargeIndicator = GetChargeIndicator(adduct);
                if (text.EndsWith(chargeIndicator))
                {
                    foundAt = text.Length - chargeIndicator.Length;
                    return adduct;
                }
                adduct = Adduct.FromChargeProtonated(i);
                chargeIndicator = GetChargeIndicator(adduct);
                if (text.EndsWith(chargeIndicator))
                {
                    foundAt = text.Length - chargeIndicator.Length;
                    return adduct;
                }
            }
            var adductStart = FindAdductDescription(text, out adduct);
            if (adductStart >= 0)
            {
                foundAt = adductStart;
                return adduct;
            }
            return Adduct.EMPTY;
        }

        public static string GetMassIndexText(int massIndex)
        {
            if (massIndex == 0)
                return string.Empty;

            return " " + SequenceMassCalc.GetMassIDescripion(massIndex); // Not L10N
        }

        public static string GetDecoyText(int? decoyMassShift)
        {
            if (!decoyMassShift.HasValue || decoyMassShift.Value == 0)
                return string.Empty;
            return string.Format("({0}{1})", // Not L10N
                                 decoyMassShift.Value >= 0 ? "+" : string.Empty, // Not L10N
                                 decoyMassShift.Value);
        }

        private readonly TransitionGroup _group;

        /// <summary>
        /// Creates a precursor transition
        /// </summary>
        /// <param name="group">The <see cref="TransitionGroup"/> which the transition represents</param>
        /// <param name="massIndex">Isotope mass shift</param>
        /// <param name="customMolecule">Non-null if this is a custom transition</param>
        public Transition(TransitionGroup group, int massIndex, CustomMolecule customMolecule = null)
            :this(group, IonType.precursor, group.Peptide.Length - 1, massIndex, group.PrecursorAdduct, null, customMolecule)
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

        public Transition(TransitionGroup group, IonType type, int? offset, int? massIndex, Adduct adduct, int? decoyMassShift, CustomMolecule customMolecule = null)
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

        public string GetFragmentIonName(CultureInfo cultureInfo, double? tolerance=null)
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
            return IsNTerminal(IonType);
        }

        public bool IsCTerminal()
        {
            return IsCTerminal(IonType);
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

        public bool IsNonPrecursorNonReporterCustomIon()
        {
            return !IsPrecursor() && IsNonReporterCustomIon();
        }

        public bool IsNonReporterCustomIon()
        {
            return IsCustom() && !(CustomIon is SettingsCustomIon);
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
                            string.Format(Resources.Transition_Validate_Precursor_charge__0__must_be_between__1__and__2__,
                            Charge, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE));                    
                    }
                }
                else if (MIN_PRODUCT_CHARGE > Charge || Charge > MAX_PRODUCT_CHARGE)
                {
                    throw new InvalidDataException(
                        string.Format(Resources.Transition_Validate_Product_ion_charge__0__must_be_between__1__and__2__,
                                                                 Charge, MIN_PRODUCT_CHARGE, MAX_PRODUCT_CHARGE));
                }
            }
            if (CustomIon != null)
            {
                if (!IsCustom())
                {
                    throw new InvalidDataException(
                        string.Format(
                            Resources.Transition_Validate_A_transition_of_ion_type__0__can_t_have_a_custom_ion, IonType));
                }    
            }
            else if (IsCustom())
            {
                    throw new InvalidDataException(
                        string.Format(
                           Resources.Transition_Validate_A_transition_of_ion_type__0__must_have_a_custom_ion_, IonType));
            }
            else
            {
                if (Ordinal < 1)
                    throw new InvalidDataException(string.Format(Resources.Transition_Validate_Fragment_ordinal__0__may_not_be_less_than__1__, Ordinal));
                if (IsPrecursor())
                {
                    if (Ordinal != Group.Peptide.Length)
                        throw new InvalidDataException(string.Format(Resources.Transition_Validate_Precursor_ordinal_must_be_the_lenght_of_the_peptide));
                }
                else if (Ordinal > Group.Peptide.Length - 1)
                {
                    throw new InvalidDataException(
                        string.Format(Resources.Transition_Validate_Fragment_ordinal__0__exceeds_the_maximum__1__for_the_peptide__2__,
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
                            string.Format(Resources.Transition_Validate_Fragment_decoy_mass_shift__0__must_be_between__1__and__2__,
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
                return Resources.Transition_ToString_precursor + GetChargeIndicator(Adduct) +
                       GetMassIndexText(MassIndex);
            }

            if (IsCustom())
            {
                var text = CustomIon.ToString();
                // Was there enough information to generate a string more distinctive that just "Ion"?
                if (String.IsNullOrEmpty(CustomIon.Name) && 
                    String.IsNullOrEmpty(CustomIon.NeutralFormula))
                {
                    // No, add mz and charge to whatever generic text was used to describe it
                    var mz = Adduct.MzFromNeutralMass(CustomIon.MonoisotopicMass);
                    return string.Format("{0} {1:F04}{2}",  // Not L10N
                        text, mz, GetChargeIndicator(Adduct));
                }
                return text;
            }
            return string.Format("{0} - {1}{2}{3}{4}", // Not L10N
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
            if (Transition.IsCustom())
            {
                if (!string.IsNullOrEmpty(transition.PrimaryCustomIonEquivalenceKey))
                    CustomIonEquivalenceTestValue = transition.PrimaryCustomIonEquivalenceKey;
                else if (!string.IsNullOrEmpty(transition.SecondaryCustomIonEquivalenceKey))
                    CustomIonEquivalenceTestValue = transition.SecondaryCustomIonEquivalenceKey;
                else if (Transition.IsNonReporterCustomIon())
                    CustomIonEquivalenceTestValue = "_mzSortIndex_" + parent.Children.IndexOf(transition); // Not L10N
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

        public bool Equivalent(TransitionLossKey other)
        {
            return Equals(CustomIonEquivalenceTestValue, other.CustomIonEquivalenceTestValue) &&
                other.Transition.Equivalent(Transition) &&
                Equals(other.Losses, Losses);
        }

        #region object overrides

        public bool Equals(TransitionLossKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Transition, Transition) && Equals(other.Losses, Losses);
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
                return (Transition.GetHashCode()*397) ^ (Losses != null ? Losses.GetHashCode() : 0);
            }
        }

        #endregion
    }

    public sealed class TransitionLossEquivalentKey
    {
        /// <summary>
        /// In the case of small molecule transitions specified by mass only, position within 
        /// the parent's list of transitions is the only meaningful key.  So we need to know our parent.
        /// </summary>
        public TransitionLossEquivalentKey(TransitionGroupDocNode parent, TransitionDocNode transition, TransitionLosses losses)
        {
            Key = new TransitionEquivalentKey(parent, transition);
            Losses = losses;
        }

        public TransitionEquivalentKey Key { get; private set; }
        public TransitionLosses Losses { get; private set; }

        #region object overrides

        public bool Equals(TransitionLossEquivalentKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Key, Key) && Equals(other.Losses, Losses);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(TransitionLossEquivalentKey)) return false;
            return Equals((TransitionLossEquivalentKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Key.GetHashCode() * 397) ^ (Losses != null ? Losses.GetHashCode() : 0);
            }
        }

        #endregion
    }

    public sealed class TransitionEquivalentKey
    {
        private readonly Transition _nodeTran;
        private readonly string _customIonEquivalenceTestText; // For use with small molecules

        public TransitionEquivalentKey(TransitionGroupDocNode parent, TransitionDocNode nodeTran)
        {
            _nodeTran = nodeTran.Transition;
            _customIonEquivalenceTestText = new TransitionLossKey(parent, nodeTran, null).CustomIonEquivalenceTestValue; 
        }

        #region object overrides

        private bool Equals(TransitionEquivalentKey other)
        {
            return Equals(_customIonEquivalenceTestText, other._customIonEquivalenceTestText) &&
                Transition.Equivalent(_nodeTran, other._nodeTran);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is TransitionEquivalentKey && Equals((TransitionEquivalentKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_customIonEquivalenceTestText == null ? 0 : _customIonEquivalenceTestText.GetHashCode() * 397) ^ Transition.GetEquivalentHashCode(_nodeTran);
            }
        }

        #endregion
    }
}
