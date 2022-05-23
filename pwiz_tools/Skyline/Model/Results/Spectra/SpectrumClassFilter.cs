using System;
using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumClassFilter : Immutable
    {
        public SpectrumClassFilter(IEnumerable<FilterSpec> filterSpecs)
        {
            FilterSpecs = ImmutableList.ValueOf(filterSpecs);
        }

        public ImmutableList<FilterSpec> FilterSpecs { get; }

        public Predicate<SpectrumMetadata> MakePredicate()
        {
            var dataSchema = new DataSchema();
            var clauses = new List<Predicate<SpectrumMetadata>>();
            foreach (var filterSpec in FilterSpecs)
            {
                var spectrumClassColumn = SpectrumClassColumn.FindColumn(filterSpec.ColumnId);
                if (spectrumClassColumn == null)
                {
                    throw new InvalidOperationException(string.Format("No such spectrum column {0}",
                        filterSpec.ColumnId));
                }

                var filterPredicate = filterSpec.Predicate.MakePredicate(dataSchema, spectrumClassColumn.ValueType);
                clauses.Add(spectrum=>filterPredicate(spectrumClassColumn.GetValue(spectrum)));
            }

            return spectrum =>
            {
                foreach (var clause in clauses)
                {
                    if (!clause(spectrum))
                    {
                        return false;
                    }
                }

                return true;
            };
        }
    }
}
