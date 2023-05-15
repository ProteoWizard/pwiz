using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public struct SpectrumClassFilter : IEnumerable<SpectrumClassFilterClause>
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

        public int Count
        {
            get { return _clauses?.Count ?? 0; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<SpectrumClassFilterClause> GetEnumerator()
        {
            return _clauses.GetEnumerator();
        }

        public bool IsEmpty
        {
            get { return Count == 0; }
        }

        public Predicate<SpectrumMetadata> MakePredicate()
        {
            if (IsEmpty)
            {
                return x => true;
            }

            var predicates = this.Select(x => x.MakePredicate()).ToList();
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

        public IEnumerable<FilterSpec> FilterSpecs
        {
            get
            {
                return this.SelectMany(clause => clause.FilterSpecs);
            }
        }
    }
}
