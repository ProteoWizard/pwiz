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
using System.IO;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model
{
    public enum IonType
    {
        precursor = -1, a, b, c, x, y, z
    }

    public class Transition : Identity
    {
        public const int MIN_PRODUCT_CHARGE = 1;
        public const int MAX_PRODUCT_CHARGE = 5;

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
            const string pluses = "+++++++++++++++++++++++++++++++";
            return pluses.Substring(0, Math.Min(charge, pluses.Length-1));
        }

        public static string GetMassIndexText(int massIndex)
        {
            if (massIndex == 0)
                return "";

            // CONSIDER: Should this be based on the true neutral mass shift from the
            //           monoisotopic mass?
            return string.Format(" [M{0}{1}]", massIndex > 0 ? "+" : "", massIndex);
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
        {
            _group = group;

            IonType = type;
            CleavageOffset = offset;
            MassIndex = massIndex;
            Charge = charge;

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

        // Derived values
        public int Ordinal { get; private set; }
        public char AA { get; private set; }

        public string FragmentIonName
        {
            get
            {
                string ionName = IonType.ToString();
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
                    throw new InvalidDataException(string.Format("Precursor charge {0} must be between {1} and {2}.",
                        Charge, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE));                    
                }

            }
            else if (MIN_PRODUCT_CHARGE > Charge || Charge > MAX_PRODUCT_CHARGE)
            {
                throw new InvalidDataException(string.Format("Product ion charge {0} must be between {1} and {2}.",
                                                             Charge, MIN_PRODUCT_CHARGE, MAX_PRODUCT_CHARGE));
            }

            if (Ordinal < 1)
                throw new InvalidDataException(string.Format("Fragment ordinal {0} may not be less than 1.", Ordinal));
            if (IsPrecursor())
            {
                if (Ordinal != Group.Peptide.Length)
                    throw new InvalidDataException(string.Format("Precursor ordinal must be the lenght of the peptide."));
            }
            else if (Ordinal > Group.Peptide.Length - 1)
            {
                throw new InvalidDataException(string.Format("Fragment ordinal {0} exceeds the maximum {1} for the peptide {2}.",
                                                             Ordinal, Group.Peptide.Length - 1, Group.Peptide.Sequence));
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
                obj.Charge == Charge;
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
                obj.Charge == Charge;
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
                return result;
            }
        }

        public override string ToString()
        {
            if (IsPrecursor())
            {
                return "precursor" + GetChargeIndicator(Charge) + GetMassIndexText(MassIndex);
            }

            return string.Format("{0} - {1}{2}{3}",
                                 AA,
                                 IonType.ToString().ToLower(),
                                 Ordinal,
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