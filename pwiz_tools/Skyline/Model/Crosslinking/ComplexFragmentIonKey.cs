using System;
using System.Collections.Generic;
using System.Text;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class ComplexFragmentIonKey
    {
        public ComplexFragmentIonKey(IEnumerable<IonType> types, IEnumerable<int> ordinals)
        {
            IonTypes = ImmutableList.ValueOf(types);
            IonOrdinals = ImmutableList.ValueOf(ordinals);
            if (IonTypes.Count != IonOrdinals.Count)
            {
                throw new ArgumentException();
            }
        }

        public ImmutableList<IonType> IonTypes { get; private set; }
        public ImmutableList<int> IonOrdinals { get; private set; }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            string strHyphen = string.Empty;
            for (int i = 0; i < IonTypes.Count; i++)
            {
                stringBuilder.Append(strHyphen);
                strHyphen = @"-";
                switch (IonTypes[i])
                {
                    case IonType.custom:
                        stringBuilder.Append(@"*");
                        break;
                    case IonType.precursor:
                        stringBuilder.Append(@"p");
                        break;
                    default:
                        stringBuilder.Append(IonTypes[i]);
                        stringBuilder.Append(IonOrdinals[i]);
                        break;
                }
            }

            return stringBuilder.ToString();
        }

        protected bool Equals(ComplexFragmentIonKey other)
        {
            return IonTypes.Equals(other.IonTypes) && IonOrdinals.Equals(other.IonOrdinals);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ComplexFragmentIonKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (IonTypes.GetHashCode() * 397) ^ IonOrdinals.GetHashCode();
            }
        }
    }
}
