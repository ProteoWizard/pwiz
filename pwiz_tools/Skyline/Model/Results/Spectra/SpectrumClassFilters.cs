using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Filtering;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumClassFilters
    {
        private static FilterPage _ms1FilterPage = new FilterPage(() => "MS1",
            new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClassColumn.MsLevel)),
                FilterPredicate.CreateFilterPredicate(FilterOperations.OP_EQUALS, 1))
            ,
            SpectrumClassColumn.MS1.Select(col => col.PropertyPath));

        private static FilterPage _ms2FilterPage = new FilterPage(() => "MS2+",
            new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClassColumn.MsLevel)),
                FilterPredicate.CreateFilterPredicate(FilterOperations.OP_IS_GREATER_THAN, 1)),
            SpectrumClassColumn.ALL.Select(col => col.PropertyPath));

        private static FilterPage _generic = new FilterPage(SpectrumClassColumn.ALL.Select(col => col.PropertyPath));

        private static ImmutableList<FilterPage> _all =
            ImmutableList.ValueOf(new[] { _ms1FilterPage, _ms2FilterPage, _generic });

        public FilterPages GetFilterPages(TransitionGroupDocNode transitionGroupDocNode)
        {
            var standardPages = ImmutableList.ValueOf(GetStandardFilterPages(new []{transitionGroupDocNode}));
            if (transitionGroupDocNode.SpectrumClassFilter.IsEmpty)
            {
                return new FilterPages(standardPages,
                    Enumerable.Repeat(FilterClause.EMPTY, standardPages.Count));
            }

            if (transitionGroupDocNode.SpectrumClassFilter.Clauses.Count == standardPages.Count)
            {
                var filterPages = MatchFilterPages(standardPages, transitionGroupDocNode.SpectrumClassFilter);
                if (filterPages != null)
                {
                    return filterPages;
                }
            }

            var pages = new List<FilterPage>();
            var clauses = new List<FilterClause>();
            for (int i = 0; i < transitionGroupDocNode.SpectrumClassFilter.Clauses.Count; i++)
            {
                pages.Add(_generic);
                clauses.Add(transitionGroupDocNode.SpectrumClassFilter[i]);
            }

            return new FilterPages(pages, clauses);
        }

        public FilterPages GetFilterPages(SpectrumClassFilter spectrumClassFilter)
        {
            var pages = new List<FilterPage>();
            var clauses = new List<FilterClause>();
            foreach (var clause in spectrumClassFilter.Clauses)
            {
                foreach (var page in _all)
                {
                    var remainder = page.MatchDiscriminant(clause.FilterSpecs);
                    if (remainder != null)
                    {
                        pages.Add(page);
                        clauses.Add(remainder);
                        break;
                    }
                }
            }

            return new FilterPages(pages, clauses);
        }

        public IEnumerable<FilterPage> GetStandardFilterPages(IEnumerable<TransitionGroupDocNode> transitionGroupDocNodes)
        {
            bool anyMs1 = false;
            bool anyMs2 = false;
            foreach (var transitionGroupDocNode in transitionGroupDocNodes)
            {
                anyMs1 |= transitionGroupDocNode.Transitions.Any(t => t.IsMs1);
                anyMs2 |= transitionGroupDocNode.Transitions.Any(t => !t.IsMs1);
            }

            if (anyMs1 && anyMs2)
            {
                return new[] { _ms1FilterPage, _ms2FilterPage };
            }

            return ImmutableList.Singleton(_generic);
        }

        public FilterPages MatchFilterPages(IList<FilterPage> filterPages, SpectrumClassFilter filter)
        {
            if (filter.Clauses.Count != filterPages.Count)
            {
                return null;
            }

            var pageFilters = new List<FilterClause>();
            for (int iPage = 0; iPage < filterPages.Count; iPage++)
            {
                var remainder = filterPages[iPage].MatchDiscriminant(filter[iPage].FilterSpecs);
                if (remainder == null)
                {
                    return null;
                }
                pageFilters.Add(remainder);
            }

            return new FilterPages(filterPages, pageFilters);
        }
    }
}
