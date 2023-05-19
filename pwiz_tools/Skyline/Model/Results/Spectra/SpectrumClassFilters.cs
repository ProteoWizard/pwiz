using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumClassFilters
    {
        private FilterPage _ms1FilterPage = new FilterPage("MS1", new FilterSpec[]
            {
                new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClassColumn.MsLevel)),
                    FilterPredicate.CreateFilterPredicate(FilterOperations.OP_EQUALS, 1))
            },
            SpectrumClassColumn.MS1.Select(col => col.PropertyPath));

        private FilterPage _ms2FilterPage = new FilterPage("MS2+", new[]
        {
            new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClassColumn.MsLevel)),
                FilterPredicate.CreateFilterPredicate(FilterOperations.OP_IS_GREATER_THAN, 1))
        }, SpectrumClassColumn.ALL.Select(col => col.PropertyPath));

        public SpectrumClassFilters(SrmDocument document)
        {
            Document = document;
        }

        public SrmDocument Document { get; }

        public FilterPages GetFilterPages(TransitionGroupDocNode transitionGroupDocNode)
        {
            var standardPages = ImmutableList.ValueOf(GetStandardFilterPages(new []{transitionGroupDocNode}));
            if (transitionGroupDocNode.SpectrumClassFilter.IsEmpty)
            {
                return new FilterPages(standardPages,
                    Enumerable.Repeat(ImmutableList<FilterSpec>.EMPTY, standardPages.Count));
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
            var clauses = new List<ImmutableList<FilterSpec>>();
            for (int i = 0; i < transitionGroupDocNode.SpectrumClassFilter.Clauses.Count; i++)
            {
                pages.Add(MakeGenericFilterPage(i));
                clauses.Add(transitionGroupDocNode.SpectrumClassFilter[i].FilterSpecs);
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

            return ImmutableList.Singleton(MakeGenericFilterPage(0));
        }

        public FilterPage MakeGenericFilterPage(int pageNumber)
        {
            return new FilterPage("Case " + (pageNumber + 1), ImmutableList<FilterSpec>.EMPTY,
                SpectrumClassColumn.ALL.Select(col => col.PropertyPath));
        }

        public FilterPages MatchFilterPages(IList<FilterPage> filterPages, SpectrumClassFilter filter)
        {
            if (filter.Clauses.Count != filterPages.Count)
            {
                return null;
            }

            var pageFilters = new List<ImmutableList<FilterSpec>>();
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
