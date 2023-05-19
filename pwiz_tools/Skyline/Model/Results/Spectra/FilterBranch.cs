using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class FilterBranch
    {
        public FilterBranch(IEnumerable<FilterSpec> discriminant, IEnumerable<SpectrumClassColumn> availableColumns)
        {
            Discriminant = ImmutableList.ValueOf(discriminant);
            AvailableColumns = ImmutableList.ValueOf(availableColumns);
        }
        public ImmutableList<FilterSpec> Discriminant { get; }
        public ImmutableList<SpectrumClassColumn> AvailableColumns { get; }
    }
}
