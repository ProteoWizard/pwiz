using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class IonChain : IReadOnlyList<FragmentIonType>
    {
        public IonChain(IEnumerable<FragmentIonType> parts)
        {
            Ions = ImmutableList.ValueOfOrEmpty(parts);
        }

        public static IonChain FromIons(IEnumerable<FragmentIonType> ions)
        {
            return ions as IonChain ?? new IonChain(ions);
        }

        public static IonChain FromIons(params FragmentIonType[] ions)
        {
            return new IonChain(ions);
        }

        public ImmutableList<FragmentIonType> Ions { get; private set; }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<FragmentIonType> GetEnumerator()
        {
            return Ions.GetEnumerator();
        }

        public int Count => Ions.Count;

        public FragmentIonType this[int index] => Ions[index];

        public IEnumerable<IonType> IonTypes
        {
            get
            {
                return Ions.Select(part => part.Type).OfType<IonType>();
            }
        }

        public override string ToString()
        {
            return string.Join(@"-", Ions);
        }

        protected bool Equals(IonChain other)
        {
            return Ions.Equals(other.Ions);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((IonChain) obj);
        }

        public override int GetHashCode()
        {
            return Ions.GetHashCode();
        }

        public bool IsEmpty
        {
            get
            {
                return Ions.All(ion => ion.IsEmpty);
            }
        }

        public bool IsPrecursor
        {
            get
            {
                return Ions.All(ion => ion.Type == IonType.precursor);
            }
        }
    }
}
