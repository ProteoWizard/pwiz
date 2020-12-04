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

        public ImmutableList<ColumnRef> RowHeaders { get; private set; }

        public ClusteringSpec ChangeRowHeaders(IEnumerable<ColumnRef> values)
        {
            return ChangeProp(ImClone(this), im => im.RowHeaders = ImmutableList.ValueOf(values));
        }

        public ImmutableList<ValueSpec> RowValues { get; private set; }

        public ClusteringSpec ChangeRowValues(IEnumerable<ValueSpec> values)
        {
            return ChangeProp(ImClone(this), im => im.RowValues = ImmutableList.ValueOf(values));
        }

        public ImmutableList<GroupSpec> ColumnGroups { get; private set; }

        public ClusteringSpec ChangeColumnGroups(IEnumerable<GroupSpec> values)
        {
            return ChangeProp(ImClone(this), im => im.ColumnGroups = ImmutableList.ValueOf(values));
        }

        public ImmutableList<ValueSpec> ColumnValues { get; private set; }

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

        public class GroupSpec : Immutable
        {
            public ImmutableList<ColumnRef> ColumnHeaders { get; private set; }

            public GroupSpec ChangeColumnHeaders(IEnumerable<ColumnRef> headers)
            {
                return ChangeProp(ImClone(this), im => im.ColumnHeaders = ImmutableList.ValueOf(headers));
            }
            public ImmutableList<ValueSpec> ColumnValues { get; private set; }

            public GroupSpec ChangeColumnValues(IEnumerable<ValueSpec> values)
            {
                return ChangeProp(ImClone(this), im => im.ColumnValues = ImmutableList.ValueOf(values));
            }

            protected bool Equals(GroupSpec other)
            {
                return ColumnHeaders.Equals(other.ColumnHeaders) && ColumnValues.Equals(other.ColumnValues);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((GroupSpec) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (ColumnHeaders.GetHashCode() * 397) ^ ColumnValues.GetHashCode();
                }
            }
        }

        public class ColumnRef
        {
            public ColumnRef(ColumnId columnId)
            {
                ColumnId = columnId.Name;
            }

            public ColumnRef(PropertyPath propertyPath)
            {
                PropertyPath = propertyPath;
            }

            public string ColumnId { get; private set; }

            public PropertyPath PropertyPath { get; private set; }

            protected bool Equals(ColumnRef other)
            {
                return ColumnId == other.ColumnId && Equals(PropertyPath, other.PropertyPath);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ColumnRef) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((ColumnId != null ? ColumnId.GetHashCode() : 0) * 397) ^ (PropertyPath != null ? PropertyPath.GetHashCode() : 0);
                }
            }
        }

        public class ValueSpec
        {
            public ValueSpec(ColumnRef columnRef, string transform)
            {
                Column = columnRef;
                Transform = transform;
            }
            public ColumnRef Column { get; private set; }
            public string Transform { get; private set; }

            protected bool Equals(ValueSpec other)
            {
                return Column.Equals(other.Column) && Transform == other.Transform;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ValueSpec) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Column.GetHashCode() * 397) ^ Transform.GetHashCode();
                }
            }
        }
    }
}
