using System;
using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Filtering
{
    public class FilterPages : Immutable, IEquatable<FilterPages>
    {
        public FilterPages(IEnumerable<FilterPage> pages, IEnumerable<FilterClause> clauses)
        {
            Pages = ImmutableList.ValueOf(pages);
            Clauses= ImmutableList.ValueOf(clauses);
            if (Clauses.Count != Pages.Count)
            {
                throw new ArgumentException();
            }
        }

        public ImmutableList<FilterPage> Pages { get; }
        public ImmutableList<FilterClause> Clauses { get; private set; }

        public FilterPages ReplaceClause(int pageIndex, FilterClause clause)
        {
            return ChangeProp(ImClone(this), im =>
                im.Clauses = im.Clauses.ReplaceAt(pageIndex, clause)
            );
        }

        public bool Equals(FilterPages other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Pages, other.Pages) && Equals(Clauses, other.Clauses);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FilterPages)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Pages.GetHashCode()* 397) ^ Clauses.GetHashCode();
            }
        }
    }
}
