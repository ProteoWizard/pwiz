using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace pwiz.Common.Collections
{
    public static class ImmutableList
    {
        public static IList<T> ValueOf<T>(IEnumerable<T> values)
        {
            if (values == null)
            {
                return null;
            }
            var immutableList = values as Impl<T>;
            if (immutableList != null)
            {
                return immutableList;
            }
            return new Impl<T>(values.ToArray());
        }
        class Impl<T> : ReadOnlyCollection<T>
        {
            public Impl(IList<T> list) : base(list)
            {
            }
            public override int GetHashCode()
            {
                return CollectionUtil.GetHashCodeDeep(this);
            }

            public override bool Equals(object o)
            {
                if (o == null)
                {
                    return false;
                }
                if (o == this)
                {
                    return true;
                }
                var that = o as Impl<T>;
                if (null == that)
                {
                    return false;
                }
                return this.SequenceEqual(that);
            }
        }
    }
}
