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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public enum IonType
    {
        precursor = -1, a, b, c, x, y, z
    }

    public static class IonTypeExtension
    {
        private static readonly string[] LOCALIZED_VALUES = new[] { Resources.IonTypeExtension_LOCALIZED_VALUES_precursor, "a", "b", "c", "x", "y", "z"}; // Not L10N
        public static string GetLocalizedString(this IonType val)
        {
            return LOCALIZED_VALUES[(int) val + 1]; // To include precursor
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
        public const int MAX_PRODUCT_CHARGE = 5;

        public const int MIN_PRODUCT_DECOY_MASS_SHIFT = -5;
        public const int MAX_PRODUCT_DECOY_MASS_SHIFT = 5;

        /// <summary>
        /// MProphet reverse decoy algorithm requires these mass shifts for
        /// singly charged product ions
        /// </summary>
        private static readonly int[] MPROPHET_REVERSED_MASS_SHIFTS = new[] {8, -8, 10, -10};

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
        /// Prioritized list of all possible product ion charges
        /// </summary>
        public static readonly int[] ALL_CHARGES = { 1, 2, 3 };

        /// <summary>
        /// Prioritize, paired list of all possible product ion types
        /// </summary>
        public static readonly IonType[] ALL_TYPES =
            {IonType.y, IonType.b, IonType.z, IonType.c, IonType.x, IonType.a};

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

        public static IonType[] GetTypePairs(ICollection<IonType> types)
        {
            var listTypes = new List<IonType>();
            for (int i = 0; i < ALL_TYPES.Length; i++)
            {
                if (types.Contains(ALL_TYPES[i]))
                {
                    if (i % 2 == 0)
                        i++;
                    listTypes.Add(ALL_TYPES[i - 1]);
                    listTypes.Add(ALL_TYPES[i]);
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
            if (IsNTerminal(type))
                return offset + 1;
            else
                return len - offset - 1;
        }

        public static string GetChargeIndicator(int charge)
        {
            const string pluses = "++++"; // Not L10N
            return charge < 5
                ? pluses.Substring(0, Math.Min(charge, pluses.Length))
                : string.Format("{0} +{1}", CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator, charge); // Not L10N
        }

        public static string GetMassIndexText(int massIndex)
        {
            if (massIndex == 0)
                return string.Empty;

            return " " + SequenceMassCalc.GetMassIDescripion(massIndex);
        }

        public static string GetDecoyText(int? decoyMassShift)
        {
            if (!decoyMassShift.HasValue || decoyMassShift.Value == 0)
                return string.Empty;
            return string.Format("({0}{1})",
                                 decoyMassShift.Value >= 0 ? "+" : string.Empty, // Not L10N
                                 decoyMassShift.Value);
        }

        private readonly TransitionGroup _group;

        /// <summary>
        /// Creates a precursor transition
        /// </summary>
        /// <param name="group">The <see cref="TransitionGroup"/> which the transition represents</param>
        /// <param name="massIndex">Isotope mass shift</param>
        public Transition(TransitionGroup group, int massIndex)
            : this(group, IonType.precursor, group.Peptide.Length - 1, massIndex, group.PrecursorCharge)
        {            
        }

        public Transition(TransitionGroup group, IonType type, int offset, int massIndex, int charge)
            :this(group, type, offset, massIndex, charge, null)
        {
        }

        public Transition(TransitionGroup group, IonType type, int offset, int massIndex, int charge, int? decoyMassShift)
        {
            _group = group;

            IonType = type;
            CleavageOffset = offset;
            MassIndex = massIndex;
            Charge = charge;
            DecoyMassShift = decoyMassShift;

            // Derived values
            Peptide peptide = group.Peptide;
            Ordinal = OffsetToOrdinal(type, offset, peptide.Length);
            AA = (IsNTerminal() ? peptide.Sequence[offset] :
                peptide.Sequence[offset + 1]);

            Validate();
        }

        public TransitionGroup Group
        {
            get { return _group; }
        }

        public int Charge { get; private set; }
        public IonType IonType { get; private set; }
        public int CleavageOffset { get; private set; }
        public int MassIndex { get; private set; }
        public int? DecoyMassShift { get; private set; }

        // Derived values
        public int Ordinal { get; private set; }
        public char AA { get; private set; }

        public string FragmentIonName
        {
            get
            {
                string ionName = IonType.GetLocalizedString();
                if (!IsPrecursor())
                    ionName += Ordinal;
                return ionName;
            }
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

        public char FragmentNTermAA
        {
            get { return GetFragmentNTermAA(_group.Peptide.Sequence, CleavageOffset); }
        }

        public char FragmentCTermAA
        {
            get { return GetFragmentCTermAA(_group.Peptide.Sequence, CleavageOffset); }
        }

        public static double CalcMass(double massH, TransitionLosses losses)
        {
            return massH - (losses != null ? losses.Mass : 0);
        }

        private void Validate()
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
                                                             Ordinal, Group.Peptide.Length - 1, Group.Peptide.Sequence));
            }

            if (DecoyMassShift.HasValue)
            {
                if ((DecoyMassShift.Value < MIN_PRODUCT_DECOY_MASS_SHIFT || DecoyMassShift.Value > MAX_PRODUCT_DECOY_MASS_SHIFT) &&
                        !MPROPHET_REVERSED_MASS_SHIFTS.Contains(i => i == DecoyMassShift.Value))
                {
                    throw new InvalidDataException(
                        string.Format(Resources.Transition_Validate_Fragment_decoy_mass_shift__0__must_be_between__1__and__2__,
                                      DecoyMassShift, MIN_PRODUCT_DECOY_MASS_SHIFT, MAX_PRODUCT_DECOY_MASS_SHIFT));
                }
            }
        }

        /// <summary>
        /// True if a given transition is equivalent to this, ignoring the
        /// transition group.
        /// </summary>
        public bool Equivalent(Transition obj)
        {
            return Equals(obj.IonType, IonType) &&
                obj.CleavageOffset == CleavageOffset &&
                obj.Charge == Charge &&
                (obj.DecoyMassShift.Equals(DecoyMassShift) || 
                // Deal with strange case of mProphet golden standard data set
                (obj.DecoyMassShift.HasValue && DecoyMassShift.HasValue &&
                    (obj.Group.LabelType.IsLight && obj.DecoyMassShift == 0 && !Group.LabelType.IsLight && DecoyMassShift != 0) ||
                    (!obj.Group.LabelType.IsLight && obj.DecoyMassShift != 0 && Group.LabelType.IsLight && DecoyMassShift == 0)));
        }

        #region object overrides

        public bool Equals(Transition obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj._group, _group) &&
                Equals(obj.IonType, IonType) &&
                obj.CleavageOffset == CleavageOffset &&
                obj.MassIndex == MassIndex &&
                obj.Charge == Charge && 
                obj.DecoyMassShift.Equals(DecoyMassShift);
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
                result = (result*397) ^ Charge;
                result = (result*397) ^ (DecoyMassShift.HasValue ? DecoyMassShift.Value : 0);
                return result;
            }
        }

        public override string ToString()
        {
            if (IsPrecursor())
            {
                return Resources.Transition_ToString_precursor + GetChargeIndicator(Charge) +
                       GetMassIndexText(MassIndex);
            }

            return string.Format("{0} - {1}{2}{3}{4}", // Not L10N
                                 AA,
                                 IonType.ToString().ToLower(),
                                 Ordinal,
                                 GetDecoyText(DecoyMassShift),
                                 GetChargeIndicator(Charge));
        }

        #endregion // object overrides
    }

    public sealed class TransitionLossKey
    {
        public TransitionLossKey(Transition transition, TransitionLosses losses)
        {
            Transition = transition;
            Losses = losses;
        }

        public Transition Transition { get; private set; }
        public TransitionLosses Losses { get; private set; }

        public bool Equivalent(TransitionLossKey other)
        {
            return other.Transition.Equivalent(Transition) && Equals(other.Losses, Losses);
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
}
