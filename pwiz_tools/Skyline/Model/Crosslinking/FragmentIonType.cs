using System;
using System.Windows.Forms;

namespace pwiz.Skyline.Model.Crosslinking
{
    public struct FragmentIonType : IComparable<FragmentIonType>
    {
        public static FragmentIonType? Empty
        {
            get { return null; }
        }

        public static FragmentIonType Precursor
        {
            get { return new FragmentIonType(IonType.precursor, 0); }
        }

        public FragmentIonType(IonType ionType, int ordinal) : this()
        {
            IonType = ionType;
            Ordinal = ionType == IonType.precursor ? 0 : ordinal;
        }

        public IonType IonType { get; private set; }
        public int Ordinal { get; private set; }

        public static FragmentIonType? FromTransition(Transition transition)
        {
            return transition == null ? Empty : new FragmentIonType(transition.IonType, transition.Ordinal);
        }

        public override string ToString()
        {
            string str = IonType.ToString();
            if (Ordinal != 0)
            {
                str += Ordinal;
            }

            return str;
        }

        public char? GetAminoAcid(string sequence)
        {
            if (IonType == IonType.precursor)
            {
                return null;
            }
            int offset = Transition.OrdinalToOffset(IonType, Ordinal, sequence.Length);
            if (Transition.IsCTerminal(IonType))
            {
                return sequence[offset + 1];
            }

            return sequence[offset];
        }

        public bool IncludesAaIndex(Peptide peptide, int aaIndex)
        {
            if (IonType == IonType.precursor)
            {
                return true;
            }

            int offset = Transition.OrdinalToOffset(IonType, Ordinal, peptide.Length);
            if (Transition.IsCTerminal(IonType))
            {
                return offset < aaIndex;
            }

            return offset >= aaIndex;
        }

        private int IonTypeOrder
        {
            get
            {
                int order = (int) IonType;
                if (order >= 0 && order < Transition.PEPTIDE_ION_TYPES_ORDERS.Length)
                {
                    return Transition.PEPTIDE_ION_TYPES_ORDERS[order];
                }

                return order;
            }
        }

        public int CompareTo(FragmentIonType ionFragment)
        {
            int result = IonTypeOrder.CompareTo(ionFragment.IonTypeOrder);
            if (result != 0)
            {
                return result;
            }
            if (Transition.IsCTerminal(IonType))
            {
                return -Ordinal.CompareTo(ionFragment.Ordinal);
            }

            return Ordinal.CompareTo(ionFragment.Ordinal);
        }
    }
}
