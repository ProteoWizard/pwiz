/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.Crosslinking
{
    /// <summary>
    /// A <see cref="IonType"/> and an integer ordinal.
    /// If the <see cref="Type"/> is <see cref="IonType.precursor"/>, or <see cref="IonType.custom"/>,
    /// then <see cref="Ordinal"/> must be zero.
    ///
    /// This object can also represent an empty ion, which the transition fragment includes zero atoms from one
    /// of the crosslinked peptides.
    /// </summary>
    public struct IonOrdinal : IComparable<IonOrdinal>
    {
        private IonType _ionType;

        public static IonOrdinal Empty
        {
            get { return default(IonOrdinal); }
        }

        public static IonOrdinal Precursor
        {
            get { return new IonOrdinal(IonType.precursor, 0); }
        }

        public static IonOrdinal Y(int ordinal)
        {
            return new IonOrdinal(IonType.y, ordinal);
        }

        public static IonOrdinal B(int ordinal)
        {
            return new IonOrdinal(IonType.b, ordinal);
        }

        public IonOrdinal(IonType ionType, int ordinal) : this()
        {
            _ionType = ionType;
            if (_ionType != IonType.precursor && _ionType != IonType.custom)
            {
                if (ordinal <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(ordinal));
                }

                Ordinal = ordinal;
            }
        }

        public bool IsEmpty
        {
            get
            {
                return Equals(default(IonOrdinal));
            }
        }

        public IonType? Type
        {
            get
            {
                return IsEmpty ? (IonType?) null : _ionType;
            }
        }

        public int Ordinal { get; private set; }

        public static IonOrdinal FromTransition(Transition transition)
        {
            return transition == null ? Empty : new IonOrdinal(transition.IonType, transition.Ordinal);
        }

        public override string ToString()
        {
            if (IsEmpty)
            {
                return @"*";
            }

            if (Type == IonType.precursor)
            {
                return @"p";
            }

            return Type.ToString() + Ordinal;
        }

        public char? GetAminoAcid(string sequence)
        {
            if (IsEmpty || Type == IonType.precursor)
            {
                return null;
            }
            int offset = Transition.OrdinalToOffset(Type.Value, Ordinal, sequence.Length);
            if (Type.Value.IsCTerminal())
            {
                return sequence[offset + 1];
            }

            return sequence[offset];
        }

        public bool IncludesAaIndex(Peptide peptide, int aaIndex)
        {
            if (IsEmpty)
            {
                return false;
            }
            if (Type == IonType.precursor)
            {
                return true;
            }

            int offset = Transition.OrdinalToOffset(Type.Value, Ordinal, peptide.Length);
            if (Type.Value.IsCTerminal())
            {
                return offset < aaIndex;
            }

            return offset >= aaIndex;
        }

        private int IonTypeOrder
        {
            get
            {
                int order = (int) Type;
                if (order >= 0 && order < Transition.PEPTIDE_ION_TYPES_ORDERS.Length)
                {
                    return Transition.PEPTIDE_ION_TYPES_ORDERS[order];
                }

                return order;
            }
        }

        public int CompareTo(IonOrdinal ionFragment)
        {
            if (Equals(ionFragment))
            {
                return 0;
            }
            int result = IsEmpty.CompareTo(ionFragment.IsEmpty);
            if (result == 0)
            {
                result = IonTypeOrder.CompareTo(ionFragment.IonTypeOrder);
            }
            if (result != 0)
            {
                return result;
            }
            if (Type.Value.IsCTerminal())
            {
                return -Ordinal.CompareTo(ionFragment.Ordinal);
            }

            return Ordinal.CompareTo(ionFragment.Ordinal);
        }
    }
}
