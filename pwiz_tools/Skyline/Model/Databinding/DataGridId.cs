using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding
{
    public class DataGridType
    {
        private Func<string> _titleFunc;
        public static DataGridType DOCUMENT_GRID = new DataGridType(@"DocumentGrid", ()=>"Document Grid");
        public static DataGridType RESULTS_GRID = new DataGridType(@"ResultsGrid", ()=>"Results Grid");
        public static DataGridType GROUP_COMPARISON = new DataGridType(@"GroupComparison", ()=>"Group Comparison");
        public static DataGridType LIST = new DataGridType(@"List", ()=>"List");
        public static DataGridType AUDIT_LOG = new DataGridType(@"AuditLog", ()=>"Audit Log");

        public static IEnumerable<DataGridType> All
        {
            get
            {
                yield return DOCUMENT_GRID;
                yield return RESULTS_GRID;
                yield return GROUP_COMPARISON;
                yield return LIST;
                yield return AUDIT_LOG;
            }
        }
        public static DataGridType FromName(string name)
        {
            return All.FirstOrDefault(type => type.Name == name);
        }

        public DataGridType(string name, Func<string> titleFunc)
        {
            Name = name;
            _titleFunc = titleFunc;
        }

        public string Name { get; }

        public string Title
        {
            get { return _titleFunc(); }
        }
    }

    public class DataGridId
    {
        public DataGridId(DataGridType type, string name)
        {
            DataGridType = type;
            Name = name;
        }

        public DataGridType DataGridType { get; private set; }
        public string Name { get; private set; }

        public override string ToString()
        {
            string title = DataGridType.Title;
            if (string.IsNullOrEmpty(Name))
            {
                return title;
            }

            return TextUtil.SpaceSeparate(title, Name);
        }

        public PersistentString ToPersistedString()
        {
            return PersistentString.FromParts(DataGridType.Name, Name);
        }

        public static DataGridId MakeDataGridId(string typeName, string instanceName)
        {
            var dataGridType = DataGridType.FromName(typeName);
            if (dataGridType == null)
            {
                return null;
            }
            return new DataGridId(dataGridType, instanceName);
        }

        public static DataGridId FromPersistentString(PersistentString persistentString, out PersistentString remainingParts)
        {
            remainingParts = PersistentString.EMPTY;
            if (persistentString.Parts.Count < 2)
            {
                return null;
            }
            var dataGridType = DataGridType.FromName(persistentString.Parts[0]);
            if (dataGridType == null)
            {
                return null;
            }

            remainingParts = persistentString.Skip(2);
            return new DataGridId(dataGridType, persistentString.Parts[1]);
        }

        protected bool Equals(DataGridId other)
        {
            return Equals(DataGridType, other.DataGridType) && Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DataGridId) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((DataGridType != null ? DataGridType.GetHashCode() : 0) * 397) ^ (Name != null ? Name.GetHashCode() : 0);
            }
        }
    }
}
