using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;

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

            if (rowType == typeof(FoldChangeBindingSource.FoldChangeDetailRow))
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

        protected abstract ResultKey GetResultKey(ColumnPropertyDescriptor columnPropertyDescriptor);

        public Replicate GetReplicate(ColumnPropertyDescriptor columnPropertyDescriptor)
        {
            var resultKey = GetResultKey(columnPropertyDescriptor);
            if (resultKey == null)
            {
                return null;
            }

            var skylineDataSchema =
                columnPropertyDescriptor.DisplayColumn.ColumnDescriptor.DataSchema as SkylineDataSchema;
            Replicate replicate = null;
            skylineDataSchema?.ReplicateList.TryGetValue(resultKey, out replicate);
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
            bool hasConstant = false;
            bool hasVariable = false;
            foreach (var descriptor in GetReplicateColumnGroups().SelectMany(grouping => grouping))
            {
                hasConstant |= IsConstantColumn(descriptor);
                hasVariable |= !IsConstantColumn(descriptor);

                if (hasConstant && hasVariable)
                    return true;
            }
            return false;
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

            protected override ResultKey GetResultKey(ColumnPropertyDescriptor columnPropertyDescriptor)
            {
                return columnPropertyDescriptor.PivotKey?.FindValue(PROPERTY_PATH_RESULT_KEY) as ResultKey;
            }
        }


        /// <summary>
        /// For <see cref="FoldChangeBindingSource.FoldChangeDetailRow"/> rows.
        /// </summary>
        private class FoldChangeReplicatePivotColumns : ReplicatePivotColumns
        {
            private static readonly PropertyPath PROPERTY_PATH_REPLICATE_ABUNDANCES = PropertyPath.Root
                .Property(nameof(FoldChangeBindingSource.AbstractFoldChangeRow.ReplicateAbundances))
                .LookupAllItems();

            private static readonly PropertyPath PROPERTY_PATH_REPLICATE_ABUNDANCES_VALUES = PropertyPath.Root
                .Property(nameof(FoldChangeBindingSource.AbstractFoldChangeRow.ReplicateAbundances))
                .DictionaryValues();


            private static readonly PropertyPath PROPERTY_PATH_REPLICATE =
                PROPERTY_PATH_REPLICATE_ABUNDANCES_VALUES.Property(nameof(FoldChangeBindingSource.ReplicateRow.Replicate));

            private static readonly PropertyPath PROPERTY_PATH_REPLICATE_SAMPLE_IDENTITY =
                PROPERTY_PATH_REPLICATE_ABUNDANCES_VALUES.Property(nameof(FoldChangeBindingSource.ReplicateRow.ReplicateSampleIdentity));

            private static readonly PropertyPath PROPERTY_PATH_REPLICATE_GROUP =
                PROPERTY_PATH_REPLICATE_ABUNDANCES_VALUES.Property(nameof(FoldChangeBindingSource.ReplicateRow.ReplicateGroup));
            public FoldChangeReplicatePivotColumns(ItemProperties itemProperties) : base(itemProperties)
            {
                ReplicatePropertyPath = PROPERTY_PATH_REPLICATE;
            }

            protected override ResultKey GetResultKey(ColumnPropertyDescriptor columnPropertyDescriptor)
            {
                var replicate = columnPropertyDescriptor.PivotKey?.FindValue(PROPERTY_PATH_REPLICATE_ABUNDANCES) as Replicate;
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
