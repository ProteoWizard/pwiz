using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class FilterPage
    {
        public FilterPage(string caption, IEnumerable<FilterSpec> discriminant,
            IEnumerable<PropertyPath> availableColumns)
        {
            Caption = caption;
            Discriminant = ImmutableList.ValueOf(discriminant);
            AvailableColumns = ImmutableList.ValueOf(availableColumns);
        }

        public string Caption { get; }
        public virtual ImmutableList<FilterSpec> Discriminant { get; }
        public virtual IEnumerable<PropertyPath> AvailableColumns { get; }

        public ImmutableList<FilterSpec> MatchDiscriminant(IEnumerable<FilterSpec> filterSpecs)
        {
            var discriminant = Discriminant.ToHashSet();
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
                return ImmutableList.ValueOf(remainder);
            }

            return null;
        }
    }
}
