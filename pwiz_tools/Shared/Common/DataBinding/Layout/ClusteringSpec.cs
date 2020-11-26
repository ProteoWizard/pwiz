using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Layout
{
    public class ClusteringSpec : Immutable
    {
        public static readonly ClusteringSpec MINIMUM = new ClusteringSpec();
        public static readonly ClusteringSpec DEFAULT = new ClusteringSpec()
        {
            ClusterColumns = true,
            ClusterRows = true
        };
        public bool ClusterColumns { get; private set; }

        public ClusteringSpec ChangeClusterColumns(bool clusterColumns)
        {
            return ChangeProp(ImClone(this), im => im.ClusterColumns = clusterColumns);
        }

        public bool ClusterRows { get; private set; }

        public ClusteringSpec ChangeClusterRows(bool clusterRows)
        {
            return ChangeProp(ImClone(this), im => im.ClusterRows = clusterRows);
        }

        public ImmutableList<ValueSpec> RowHeaders { get; private set; }

        public ClusteringSpec ChangeRowHeaders(IEnumerable<ValueSpec> values)
        {
            return ChangeProp(ImClone(this), im => im.RowHeaders = ImmutableList.ValueOf(values));
        }

        public ImmutableList<ValueSpec> ColumnHeaders { get; private set; }

        public ClusteringSpec ChangeColumnHeaders(IEnumerable<ValueSpec> values)
        {
            return ChangeProp(ImClone(this), im => im.ColumnHeaders = ImmutableList.ValueOf(values));
        }


        protected bool Equals(ClusteringSpec other)
        {
            return ClusterColumns == other.ClusterColumns && ClusterRows == other.ClusterRows;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ClusteringSpec) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ClusterColumns.GetHashCode();
                hashCode = (hashCode * 397) ^ ClusterRows.GetHashCode();
                return hashCode;
            }
        }

    }

    public class RowClusteringSpec : Immutable
    {
        public ImmutableList<ValueSpec> Headers { get; private set; }
        public 
    }

    public class ValueSpec : Immutable
    {
        public ValueSpec(ColumnId columnId, PropertyPath propertyPath)
        {
            ColumnId = columnId;
            PropertyPath = propertyPath;
        }

        public ColumnId ColumnId { get; private set; }
        public PropertyPath PropertyPath { get; private set; }
    }
}
