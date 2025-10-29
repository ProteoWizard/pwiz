using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Lists;
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.Controls.Databinding
{
    public abstract class ReplicatePivotColumns
    {
        public static ReplicatePivotColumns FromItemProperties(ItemProperties itemProperties)
        {
            var rowType = GetRowType(itemProperties);
            if (DocumentViewTransformer.GetMappingForRowType(rowType) != null)
            {
                return new DocumentGridReplicatePivotColumns(itemProperties);
            }

            if (rowType == typeof(FoldChangeDetailRow))
            {
                return new FoldChangeReplicatePivotColumns(itemProperties);
            }

            return null;
        }


        public ReplicatePivotColumns(ItemProperties itemProperties)
        {
            RowType = GetRowType(itemProperties);
            ItemProperties = itemProperties;
        }

        public Type RowType { get; }
        public ItemProperties ItemProperties { get; }
        public PropertyPath ReplicatePropertyPath { get; protected set; }
        public PropertyPath ResultFilePropertyPath { get; protected set; }

        public IEnumerable<IGrouping<ResultKey, ColumnPropertyDescriptor>> GetReplicateColumnGroups()
        {
            return ItemProperties.OfType<ColumnPropertyDescriptor>().GroupBy(GetResultKey).Where(g => g.Key != null);
        }

        protected abstract ResultKey GetResultKey(PivotKey pivotKey);

        protected ResultKey GetResultKey(ColumnPropertyDescriptor columnPropertyDescriptor)
        {
            return GetResultKey(columnPropertyDescriptor.PivotKey);
        }

        public Replicate GetReplicate(ColumnPropertyDescriptor columnPropertyDescriptor)
        {
            return GetReplicate(columnPropertyDescriptor.DisplayColumn.DataSchema as SkylineDataSchema,
                columnPropertyDescriptor.PivotKey);
        }

        private Replicate GetReplicate(SkylineDataSchema skylineDataSchema, PivotKey pivotKey)
        {
            var resultKey = GetResultKey(pivotKey);
            if (resultKey == null)
            {
                return null;
            }

            var resultKeyWithoutFileIndex = new ResultKey(resultKey.ReplicateName, resultKey.ReplicateIndex, 0);
            Replicate replicate = null;
            skylineDataSchema?.ReplicateList.TryGetValue(resultKeyWithoutFileIndex, out replicate);
            return replicate;
        }

        public virtual bool IsConstantColumn(ColumnPropertyDescriptor columnPropertyDescriptor)
        {
            var propertyPath = columnPropertyDescriptor.DisplayColumn.PropertyPath;
            if (ReplicatePropertyPath != null && propertyPath.StartsWith(ReplicatePropertyPath))
            {
                return true;
            }

            if (ResultFilePropertyPath != null && propertyPath.StartsWith(ResultFilePropertyPath))
            {
                // If it is a result file column, then it is a constant if and only if the replicate has only one file in it
                var replicate = GetReplicate(columnPropertyDescriptor);
                return 1 == replicate?.Files.Count;
            }

            return false;
        }

        public bool HasConstantAndVariableColumns()
        {
            bool hasNonReplicateColumns = ItemProperties.OfType<ColumnPropertyDescriptor>().Any(p => GetResultKey(p) == null);
            bool hasConstant = false;
            bool hasVariable = false;
            foreach (var descriptor in GetReplicateColumnGroups().SelectMany(grouping => grouping))
            {
                hasConstant |= IsConstantColumn(descriptor);
                hasVariable |= !IsConstantColumn(descriptor);

                if (hasConstant && hasVariable && hasNonReplicateColumns)
                    return true;
            }
            return false;
        }

        private static Type GetRowType(ItemProperties itemProperties)
        {
            foreach (var property in itemProperties.OfType<ColumnPropertyDescriptor>())
            {
                var columnDescriptor = property.DisplayColumn.ColumnDescriptor;
                while (columnDescriptor?.Parent != null)
                {
                    columnDescriptor = columnDescriptor.Parent;
                }

                return columnDescriptor?.PropertyType ?? typeof(object);
            }

            return typeof(object);
        }

        /// <summary>
        /// For one of the columns for which <see cref="IsConstantColumn"/> is true, returns
        /// the value of that column.
        /// </summary>
        public object GetConstantColumnValue(ColumnPropertyDescriptor columnPropertyDescriptor)
        {
            return GetConstantColumnValue(columnPropertyDescriptor.DisplayColumn.ColumnDescriptor,
                columnPropertyDescriptor.PivotKey);
        }
        /// <summary>
        /// Returns whether a replicate column should be read-only or not.
        /// It will be read only if either the underlying column is read-only, or if the
        /// value of the parent is null so that there is no object on which to set the property value.
        /// </summary>
        public bool IsConstantColumnReadOnly(ColumnPropertyDescriptor columnPropertyDescriptor)
        {
            if (columnPropertyDescriptor.IsReadOnly)
            {
                return true;
            }
            var columnDescriptor = columnPropertyDescriptor.DisplayColumn.ColumnDescriptor;
            if (false != columnDescriptor.ReflectedPropertyDescriptor?.IsReadOnly)
            {
                return true;
            }

            var parentValue = GetConstantColumnParentValue(columnPropertyDescriptor);
            if (parentValue == null)
            {
                return true;
            }

            if (parentValue is ListItem listItem)
            {
                return listItem.GetRecord() is ListItem.OrphanRecordData;
            }
            return false;
        }

        public void SetConstantColumnValue(ColumnPropertyDescriptor columnPropertyDescriptor, object value)
        {
            var parentValue = GetConstantColumnParentValue(columnPropertyDescriptor);

            var reflectedPropertyDescriptor =
                columnPropertyDescriptor.DisplayColumn.ColumnDescriptor.ReflectedPropertyDescriptor;
            reflectedPropertyDescriptor.SetValue(parentValue, value);
        }

        private object GetConstantColumnParentValue(ColumnPropertyDescriptor columnPropertyDescriptor)
        {
            return GetConstantColumnValue(columnPropertyDescriptor.DisplayColumn.ColumnDescriptor.Parent,
                columnPropertyDescriptor.PivotKey);
        }

        private object GetConstantColumnValue(ColumnDescriptor columnDescriptor, PivotKey pivotKey)
        {
            if (columnDescriptor == null)
            {
                return null;
            }
            if (Equals(ReplicatePropertyPath, columnDescriptor.PropertyPath))
            {
                return GetReplicate(columnDescriptor.DataSchema as SkylineDataSchema, pivotKey);
            }

            if (Equals(ResultFilePropertyPath, columnDescriptor.PropertyPath))
            {
                var replicate = GetReplicate(columnDescriptor.DataSchema as SkylineDataSchema, pivotKey);
                if (replicate?.Files.Count == 1)
                {
                    return replicate.Files.First();
                }

                return null;
            }

            var parentColumnDescriptor = columnDescriptor.Parent;
            if (parentColumnDescriptor != null)
            {
                var reflected = columnDescriptor.ReflectedPropertyDescriptor;
                if (reflected != null)
                {
                    var parentValue = GetConstantColumnValue(parentColumnDescriptor, pivotKey);
                    if (parentValue != null)
                    {
                        return reflected.GetValue(parentValue);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// For rows from the DocumentGrid:
        /// <see cref="Protein"/>, <see cref="Peptide"/> <see cref="Precursor"/> <see cref="Transition"/>.
        /// </summary>
        private class DocumentGridReplicatePivotColumns : ReplicatePivotColumns
        {
            private static readonly PropertyPath PROPERTY_PATH_RESULT_KEY = PropertyPath.Root.Property(@"Results").LookupAllItems();
            public DocumentGridReplicatePivotColumns(ItemProperties itemProperties) : base(itemProperties)
            {
                ReplicatePropertyPath = DocumentViewTransformer.GetReplicatePropertyPath(RowType);
                ResultFilePropertyPath = DocumentViewTransformer.GetResultFilePropertyPath(RowType);
            }

            protected override ResultKey GetResultKey(PivotKey pivotKey)
            {
                return pivotKey?.FindValue(PROPERTY_PATH_RESULT_KEY) as ResultKey;
            }
        }


        /// <summary>
        /// For <see cref="FoldChangeDetailRow"/> rows.
        /// </summary>
        private class FoldChangeReplicatePivotColumns : ReplicatePivotColumns
        {
            private static readonly PropertyPath PROPERTY_PATH_REPLICATE_ABUNDANCES = PropertyPath.Root
                .Property(nameof(AbstractFoldChangeRow.ReplicateAbundances))
                .LookupAllItems();

            private static readonly PropertyPath PROPERTY_PATH_REPLICATE_ABUNDANCES_VALUES = PropertyPath.Root
                .Property(nameof(AbstractFoldChangeRow.ReplicateAbundances))
                .DictionaryValues();


            private static readonly PropertyPath PROPERTY_PATH_REPLICATE =
                PROPERTY_PATH_REPLICATE_ABUNDANCES_VALUES.Property(nameof(ReplicateRow.Replicate));

            private static readonly PropertyPath PROPERTY_PATH_REPLICATE_SAMPLE_IDENTITY =
                PROPERTY_PATH_REPLICATE_ABUNDANCES_VALUES.Property(nameof(ReplicateRow.ReplicateSampleIdentity));

            private static readonly PropertyPath PROPERTY_PATH_REPLICATE_GROUP =
                PROPERTY_PATH_REPLICATE_ABUNDANCES_VALUES.Property(nameof(ReplicateRow.ReplicateGroup));
            public FoldChangeReplicatePivotColumns(ItemProperties itemProperties) : base(itemProperties)
            {
                ReplicatePropertyPath = PROPERTY_PATH_REPLICATE;
            }

            protected override ResultKey GetResultKey(PivotKey pivotKey)
            {
                var replicate = pivotKey?.FindValue(PROPERTY_PATH_REPLICATE_ABUNDANCES) as Replicate;
                if (replicate == null)
                {
                    return null;
                }

                if (string.IsNullOrEmpty(replicate.Name))
                {
                    // When switching documents, the replicate might be invalid (Replicate.EMPTY_CHROMATOGRAM_SET)
                    return null;
                }

                return new ResultKey(replicate, 0);
            }

            public override bool IsConstantColumn(ColumnPropertyDescriptor columnPropertyDescriptor)
            {
                var propertyPath = columnPropertyDescriptor.DisplayColumn.PropertyPath;
                if (propertyPath.StartsWith(PROPERTY_PATH_REPLICATE_SAMPLE_IDENTITY) ||
                    propertyPath.StartsWith(PROPERTY_PATH_REPLICATE_GROUP))
                {
                    return true;
                }
                return base.IsConstantColumn(columnPropertyDescriptor);
            }
        }
    }
}
