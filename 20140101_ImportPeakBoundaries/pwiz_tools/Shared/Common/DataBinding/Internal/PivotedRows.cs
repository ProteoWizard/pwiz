using System.Collections.Generic;
using System.ComponentModel;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Internal
{
    internal class PivotedRows
    {
        public PivotedRows(IEnumerable<RowItem> rowItems, PropertyDescriptorCollection itemProperties)
        {
            RowItems = ImmutableList.ValueOf(rowItems);
            ItemProperties = itemProperties;
        }

        public IList<RowItem> RowItems { get; private set; }
        public PropertyDescriptorCollection ItemProperties { get; private set; }
    }
}