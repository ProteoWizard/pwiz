using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Databinding
{
    public class ReplicatePivotColumns
    {
        private PropertyPath propertyPathResultsKey = PropertyPath.Root.Property(@"Results").LookupAllItems();
        public ReplicatePivotColumns(ItemProperties itemProperties)
        {
            RowType = GetRowType(itemProperties);
            ItemProperties = itemProperties;
            ReplicatePropertyPath = DocumentViewTransformer.GetReplicatePropertyPath(RowType);
            ResultFilePropertyPath = DocumentViewTransformer.GetResultFilesPropertyPath(RowType);
        }

        public Type RowType { get; }
        public ItemProperties ItemProperties { get; }
        public PropertyPath ReplicatePropertyPath { get; }
        public PropertyPath ResultFilePropertyPath { get; }

        public IEnumerable<IGrouping<ResultKey, ColumnPropertyDescriptor>> GetReplicateColumnGroups()
        {
            return ItemProperties.OfType<ColumnPropertyDescriptor>()
                .GroupBy(col => col.PivotKey?.FindValue(propertyPathResultsKey) as ResultKey)
                .Where(grouping => grouping.Key != null);
        }

        public bool IsPivoted()
        {
            return GetReplicateColumnGroups().Any();
        }

        public ResultFile GetResultFile(PivotKey pivotKey)
        {
            return (ResultFile) pivotKey.FindValue(ResultFilePropertyPath);
        }

        public bool IsReplicateColumn(ColumnPropertyDescriptor columnPropertyDescriptor)
        {
            return columnPropertyDescriptor.DisplayColumn.PropertyPath.StartsWith(ReplicatePropertyPath) || columnPropertyDescriptor.DisplayColumn.PropertyPath.StartsWith(ResultFilePropertyPath);
        }

        private static Type GetRowType(ItemProperties itemProperties)
        {
            foreach (var property in itemProperties.OfType<ColumnPropertyDescriptor>())
            {
                var columnDescriptor = property.DisplayColumn.ColumnDescriptor;
                while (columnDescriptor.Parent != null)
                {
                    columnDescriptor = columnDescriptor.Parent;
                }

                return columnDescriptor.PropertyType;
            }

            return typeof(object);
        }
    }
}
