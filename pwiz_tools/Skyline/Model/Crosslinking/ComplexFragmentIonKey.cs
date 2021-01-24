using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class ComplexFragmentIonKey
    {

        public ComplexFragmentIonKey(IEnumerable<IonFragment?> parts)
        {
            Parts = ImmutableList.ValueOf(parts);
        }

        public ImmutableList<IonFragment?> Parts { get; private set; }

        public IEnumerable<IonType> IonTypes
        {
            get
            {
                return Parts.Where(part => null != part).Select(part => part.Value.IonType);
            }
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            string strHyphen = string.Empty;
            for (int i = 0; i < Parts.Count; i++)
            {
                stringBuilder.Append(strHyphen);
                strHyphen = @"-";
                switch (Parts[i]?.IonType)
                {
                    case null:
                        stringBuilder.Append(@"*");
                        break;
                    case IonType.precursor:
                        stringBuilder.Append(@"p");
                        break;
                    default:
                        stringBuilder.Append(Parts[i]);
                        break;
                }
            }

            return stringBuilder.ToString();
        }

        protected bool Equals(ComplexFragmentIonKey other)
        {
            return Parts.Equals(other.Parts);
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
            return Parts.GetHashCode();
        }
    }
}
