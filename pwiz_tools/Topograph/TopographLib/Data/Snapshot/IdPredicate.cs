using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace pwiz.Topograph.Data.Snapshot
{
    public class IdPredicate
    {
        public IdPredicate()
        {
            IsOneOf = new HashSet<long>();
        }
        public IdPredicate(long? greaterThanOrEqual, ICollection<long> isOneOf) : this()
        {
            GreaterThanOrEqual = greaterThanOrEqual;
            IsOneOf.UnionWith(isOneOf);
        }
        public long? GreaterThanOrEqual { get; private set; }
        public HashSet<long> IsOneOf { get; private set; }
        public bool AlwaysFalse
        {
            get { return !GreaterThanOrEqual.HasValue && IsOneOf.Count == 0; }
        }
        public string GetSql(string idValue)
        {
            var ids = IsOneOf.ToArray();
            Array.Sort(ids);
            string idList = String.Join(",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));
            if (GreaterThanOrEqual.HasValue)
            {
                if (IsOneOf.Count == 0)
                {
                    return String.Format("({0} >= {1})", idValue, GreaterThanOrEqual);
                }
                return String.Format("({0} >= {1} OR {0} IN ({2}))", idValue, GreaterThanOrEqual, idList);
            }
            return String.Format("({0} IN ({1}))", idValue, idList);
        }
        public bool Matches(long id)
        {
            if (GreaterThanOrEqual.HasValue && id >= GreaterThanOrEqual)
            {
                return true;
            }
            return IsOneOf.Contains(id);
        }
    }
}