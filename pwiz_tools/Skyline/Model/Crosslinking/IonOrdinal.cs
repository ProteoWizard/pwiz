using System;

namespace pwiz.Skyline.Model.Crosslinking
{
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
            if (Transition.IsCTerminal(Type.Value))
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
            if (Transition.IsCTerminal(Type.Value))
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
            int result = -IsEmpty.CompareTo(ionFragment.IsEmpty);
            if (result == 0)
            {
                result = IonTypeOrder.CompareTo(ionFragment.IonTypeOrder);
            }
            if (result != 0)
            {
                return result;
            }
            if (Transition.IsCTerminal(Type.Value))
            {
                return -Ordinal.CompareTo(ionFragment.Ordinal);
            }

            return Ordinal.CompareTo(ionFragment.Ordinal);
        }
    }
}
