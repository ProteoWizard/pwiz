using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public struct SpectrumClassFilter : IEquatable<SpectrumClassFilter>, IComparable, IComparable<SpectrumClassFilter>
    {
        private ImmutableList<SpectrumClassFilterClause> _clauses;

        public SpectrumClassFilter(IEnumerable<SpectrumClassFilterClause> alternatives)
        {
            var list = ImmutableList.ValueOf(alternatives);
            if (list?.Count > 0)
            {
                _clauses = list;
            }
            else
            {
                _clauses = null;
            }
        }

        public SpectrumClassFilter(params SpectrumClassFilterClause[] alternatives) : this(
            (IEnumerable<SpectrumClassFilterClause>)alternatives)
        {
        }

        public static SpectrumClassFilter FromFilterPages(FilterPages filterPages)
        {
            var clauses = new List<SpectrumClassFilterClause>();
            for (int iPage = 0; iPage < filterPages.Pages.Count; iPage++)
            {
                clauses.Add(new SpectrumClassFilterClause(filterPages.Pages[iPage].Discriminant
                    .Concat(filterPages.Clauses[iPage])));
            }

            return new SpectrumClassFilter(clauses);
        }

        public ImmutableList<SpectrumClassFilterClause> Clauses
        {
            get
            {
                return _clauses ?? ImmutableList<SpectrumClassFilterClause>.EMPTY;
            }
        }

        public SpectrumClassFilterClause this[int index]
        {
            get
            {
                return Clauses[index];
            }
        }

        public bool IsEmpty
        {
            get { return Clauses.Count == 0; }
        }

        public Predicate<SpectrumMetadata> MakePredicate()
        {
            if (IsEmpty)
            {
                return x => true;
            }

            var predicates = Clauses.Select(x => x.MakePredicate()).ToList();
            return x =>
            {
                foreach (var predicate in predicates)
                {
                    if (predicate(x))
                    {
                        return true;
                    }
                }

                return false;
            };
        }

        public string GetAbbreviatedText()
        {
            return TextUtil.SpaceSeparate(Clauses.Select(clause => clause.GetAbbreviatedText()));
        }

        public bool Equals(SpectrumClassFilter other)
        {
            return Equals(_clauses, other._clauses);
        }

        public override bool Equals(object obj)
        {
            return obj is SpectrumClassFilter other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (_clauses != null ? _clauses.GetHashCode() : 0);
        }

        public int CompareTo(SpectrumClassFilter other)
        {
            for (int i = 0; i < Clauses.Count && i < other.Clauses.Count; i++)
            {
                int result = Clauses[i].CompareTo(other.Clauses[i]);
                if (result != 0)
                {
                    return result;
                }
            }

            return Clauses.Count.CompareTo(other.Clauses.Count);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            return CompareTo((SpectrumClassFilter)obj);
        }
    }
}
