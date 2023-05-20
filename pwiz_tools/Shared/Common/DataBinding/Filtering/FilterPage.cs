using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Filtering
{
    public class FilterPage
    {
        public FilterPage(string caption, IEnumerable<FilterSpec> discriminant,
            IEnumerable<PropertyPath> availableColumns)
        {
            Caption = caption;
            Discriminant = new FilterClause(discriminant);
            AvailableColumns = ImmutableList.ValueOf(availableColumns);
        }

        public string Caption { get; }
        public virtual FilterClause Discriminant { get; }
        public virtual IEnumerable<PropertyPath> AvailableColumns { get; }

        public FilterClause MatchDiscriminant(IEnumerable<FilterSpec> filterSpecs)
        {
            var discriminant = Discriminant.FilterSpecs.ToHashSet();
            var remainder = new List<FilterSpec>();
            foreach (var filterSpec in filterSpecs)
            {
                if (!discriminant.Remove(filterSpec))
                {
                    remainder.Add(filterSpec);
                }
            }

            if (discriminant.Count == 0)
            {
                return new FilterClause(remainder);
            }

            return null;
        }

        protected bool Equals(FilterPage other)
        {
            return Caption == other.Caption && Discriminant.Equals(other.Discriminant) &&
                   AvailableColumns.Equals(other.AvailableColumns);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FilterPage)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Caption.GetHashCode();
                hashCode = (hashCode * 397) ^ Discriminant.GetHashCode();
                hashCode = (hashCode * 397) ^ AvailableColumns.GetHashCode();
                return hashCode;
            }
        }
    }
}
