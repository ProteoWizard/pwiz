using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Controls;

namespace pwiz.Common.DataBinding
{
    public class RowItemValues
    {
        public static RowItemValues FromItemProperties<TProp>(Type propertyType, ICollection<TProp> itemProperties) where TProp : PropertyDescriptor
        {
            var columnDescriptors = ColumnDescriptorsWithType(propertyType, itemProperties);
            var pivotKeys = itemProperties.OfType<ColumnPropertyDescriptor>().Select(dpd => dpd.PivotKey).Distinct();
            var dataPropertyDescriptors = DataPropertyDescriptorsWithType(propertyType, itemProperties);
            return new RowItemValues(propertyType, columnDescriptors, pivotKeys, dataPropertyDescriptors);
        }

        public static RowItemValues Empty(Type propertyType)
        {
            return new RowItemValues(propertyType, ImmutableList.Empty<ColumnDescriptor>(),
                ImmutableList.Empty<PivotKey>(), ImmutableList.Empty<DataPropertyDescriptor>());
        }

        public static RowItemValues FromDataGridView(Type propertyType, BoundDataGridView dataGridView)
        {
            if (dataGridView == null)
            {
                return Empty(propertyType);
            }
            var bindingSource = dataGridView.DataSource as BindingListSource;
            if (bindingSource == null)
            {
                return Empty(propertyType);
            }

            return FromItemProperties(propertyType, bindingSource.ItemProperties);
        }

        private static IEnumerable<ColumnDescriptor> ColumnDescriptorsWithType(Type propertyType, IEnumerable<PropertyDescriptor> itemProperties)
        {
            var propertyPaths = new HashSet<PropertyPath>();
            foreach (var propertyDescriptor in itemProperties)
            {
                var columnPropertyDescriptor = propertyDescriptor as ColumnPropertyDescriptor;
                if (columnPropertyDescriptor == null)
                {
                    continue;
                }

                var columnDescriptor = columnPropertyDescriptor.DisplayColumn.ColumnDescriptor;
                while (columnDescriptor != null)
                {
                    if (!propertyPaths.Add(columnDescriptor.PropertyPath))
                    {
                        break;
                    }

                    if (propertyType.IsAssignableFrom(columnDescriptor.PropertyType))
                    {
                        yield return columnDescriptor;
                    }

                    columnDescriptor = columnDescriptor.Parent;
                }
            }
        }

        private static IEnumerable<DataPropertyDescriptor> DataPropertyDescriptorsWithType(Type propertyType,
            IEnumerable<PropertyDescriptor> itemProperties)
        {
            foreach (var propertyDescriptor in itemProperties.OfType<DataPropertyDescriptor>())
            {
                if (propertyDescriptor is ColumnPropertyDescriptor)
                {
                    continue;
                }

                if (propertyType.IsAssignableFrom(propertyDescriptor.PropertyType))
                {
                    yield return propertyDescriptor;
                }
            }
        }

        private RowItemValues(Type propertyType, IEnumerable<ColumnDescriptor> columnDescriptors, IEnumerable<PivotKey> pivotKeys, IEnumerable<DataPropertyDescriptor> dataPropertyDescriptors)
        {
            PropertyType = propertyType;
            ColumnDescriptors = ImmutableList.ValueOf(columnDescriptors);
            PivotKeys = ImmutableList.ValueOf(pivotKeys);
            DataPropertyDescriptors = ImmutableList.ValueOf(dataPropertyDescriptors);
        }

        public ImmutableList<ColumnDescriptor> ColumnDescriptors { get; private set; }
        public ImmutableList<DataPropertyDescriptor> DataPropertyDescriptors { get; private set; }

        public ImmutableList<PivotKey> PivotKeys { get; private set; }
        public Type PropertyType { get; private set; }

        public IEnumerable<object> GetRowValues(RowItem rowItem)
        {
            return AllRowValues(rowItem).Where(PropertyType.IsInstanceOfType);
        }

        private IEnumerable<object> AllRowValues(RowItem rowItem)
        {
            foreach (var columnDescriptor in ColumnDescriptors)
            {
                foreach (var pivotKey in PivotKeys)
                {
                    yield return columnDescriptor.GetPropertyValue(rowItem, pivotKey);
                }
            }

            foreach (var dataPropertyDescriptor in DataPropertyDescriptors)
            {
                yield return dataPropertyDescriptor.GetValue(rowItem);
            }
        }

        public IEnumerable<RowItem> GetSelectedRowItems(BoundDataGridView dataGridView)
        {
            var bindingSource = dataGridView.DataSource as BindingListSource;
            if (bindingSource == null)
            {
                return new RowItem[0];
            }

            if (dataGridView.SelectedRows.Count == 0)
            {
                var currentRow = bindingSource.Current as RowItem;
                if (currentRow == null)
                {
                    return new RowItem[0];
                }

                return new[] {currentRow};
            }

            return dataGridView.SelectedRows.Cast<DataGridViewRow>()
                .Select(row => (RowItem) bindingSource[row.Index]);
        }
    }
}
