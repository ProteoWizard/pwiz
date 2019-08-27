using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Internal;

namespace pwiz.Common.DataBinding
{
    public class ColumnSelector
    {
        public ColumnSelector(ColumnDescriptor rootColumn, PropertyPath propertyPath)
        {
            var viewSpec = new ViewSpec().SetColumns(ImmutableList.Singleton(new ColumnSpec(propertyPath)))
                .SetSublistId(propertyPath);
            ViewInfo = new ViewInfo(rootColumn, viewSpec);
            var displayColumn = ViewInfo.DisplayColumns[0];
            if (displayColumn.ColumnDescriptor != null && null != displayColumn.ColumnDescriptor.CollectionAncestor())
            {
                Pivoter = new Pivoter(ViewInfo);
            }
        }
        public ViewInfo ViewInfo { get; private set; }
        internal Pivoter Pivoter { get; private set; }

        public bool IsValid
        {
            get { return ViewInfo.DisplayColumns[0].ColumnDescriptor != null; }
        }

        public object GetSingleValue(object rootObject)
        {
            return ViewInfo.DisplayColumns[0].ColumnDescriptor.GetPropertyValue(new RowItem(rootObject), null);
        }

        public object AggregateValues(AggregateOperation aggregateOperation, object rootObject)
        {
            return aggregateOperation.CalculateValue(ViewInfo.DataSchema, GetAllValues(rootObject));
        }


        private IEnumerable<object> GetAllValues(object rootObject)
        {
            if (Pivoter == null)
            {
                return new[] {GetSingleValue(rootObject)};
            }
            var columnDescriptor = ViewInfo.DisplayColumns[0].ColumnDescriptor;
            var rowItem = new RowItem(rootObject);
            if (Pivoter == null)
            {

                return new[] {columnDescriptor.GetPropertyValue(rowItem, null)};
            }

            var pivotedRows = Pivoter.ExpandAndPivot(ViewInfo.DataSchema.QueryLock.CancellationToken, new[] {rowItem});
            return pivotedRows.RowItems.Select(item => columnDescriptor.GetPropertyValue(item, null));
        }
    }
}
