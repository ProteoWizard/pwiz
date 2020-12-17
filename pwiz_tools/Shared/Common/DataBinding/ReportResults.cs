using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    public class ReportResults : Immutable
    {
        public static readonly ReportResults EMPTY = new ReportResults(ImmutableList.Empty<RowItem>(), ItemProperties.EMPTY);
        public ReportResults(IEnumerable<RowItem> rowItems, IEnumerable<DataPropertyDescriptor> itemProperties) 
        {
            RowItems = ImmutableList.ValueOf(rowItems);
            ItemProperties = ItemProperties.FromList(itemProperties);
        }

        public ImmutableList<RowItem> RowItems { get; private set; }

        public virtual ReportResults ChangeRowItems(IEnumerable<RowItem> rowItems)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.RowItems = ImmutableList.ValueOf(rowItems);
            });
        }

        public int RowCount
        {
            get { return RowItems.Count; }
        }
        public ItemProperties ItemProperties
        {
            get;
            private set;
        }
    }
}
