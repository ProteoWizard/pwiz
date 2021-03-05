using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.DataBinding;

namespace pwiz.Skyline.Model.Databinding
{
    public class DataGridType
    {
        private Func<string> _titleTemplateStringFunc;
        public static DataGridType DOCUMENT_GRID = new DataGridType(@"DocumentGrid", ()=>"Document Grid");
        public static DataGridType RESULTS_GRID = new DataGridType(@"ResultsGrid", ()=>"Results Grid");
        public static DataGridType GROUP_COMPARISON = new DataGridType(@"GroupComparison", ()=>"{0}");
        public static DataGridType LIST = new DataGridType(@"List", ()=>"List {0}");
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

        public DataGridType(string name, Func<string> titleTemplateStringFunc)
        {
            Name = name;
            _titleTemplateStringFunc = titleTemplateStringFunc;
        }

        public string Name { get; private set; }

        public string TitleTemplateString
        {
            get { return _titleTemplateStringFunc(); }
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
            return string.Format(DataGridType.TitleTemplateString, Name);
        }

        public string ToPersistedString()
        {
            return MakePersistentStringFromParts(DataGridType.Name, Name);
        }

        public static DataGridId FromPersistentString(string persistentString)
        {
            var parts = ParsePersistedStringParts(persistentString).ToList();
            if (parts.Count != 2)
            {
                return null;
            }

            var dataGridType = DataGridType.FromName(parts[0]);
            if (dataGridType == null)
            {
                return null;
            }
            return new DataGridId(dataGridType, parts[1]);
        }
        private const char PERSISTENT_SEPARATOR = '|';


        public static string MakePersistentStringFromParts(params string[] parts)
        {
            return string.Join(PERSISTENT_SEPARATOR.ToString(),
                parts.Select(part => Uri.EscapeDataString(part ?? string.Empty)));
        }

        public static IEnumerable<string> ParsePersistedStringParts(string persistentString)
        {
            return persistentString.Split(PERSISTENT_SEPARATOR).Select(part =>
            {
                var decoded = Uri.UnescapeDataString(part);
                return string.IsNullOrEmpty(decoded) ? null : decoded;
            });
        }
    }

    public class DataSourceId
    {
        public DataSourceId(DataGridId dataGridId, ViewName viewName, string layoutName)
        {
            DataGridId = dataGridId;
            ViewName = viewName;
        }
        public DataGridId DataGridId
        {
            get;
            private set;
        }

        public ViewName ViewName { get; private set; }
        public string LayoutName { get; private set; }

        public override string ToString()
        {
            string result = DataGridId.ToString();
            if (!string.IsNullOrEmpty(ViewName.Name))
            {
                result = result + ':' + ViewName.Name;
            }

            if (!string.IsNullOrEmpty(LayoutName))
            {
                result = result + '(' + LayoutName + ')';
            }

            return result;
        }

        private const char PERSISTENT_SEPARATOR = '|';
        public string ToPersistentString()
        {
            var parts = new[]
            {
                DataGridId.DataGridType.Name,
                DataGridId.Name,
                ViewName.GroupId.Name,
                ViewName.Name,
                LayoutName
            };
            return string.Join(PERSISTENT_SEPARATOR.ToString(), parts.Select(part => Uri.EscapeDataString(part ?? string.Empty)));
        }

        public static DataSourceId ParsePersistentString(string persistentString)
        {
            var parts = persistentString.Split(PERSISTENT_SEPARATOR).Select(part =>
            {
                var decoded = Uri.UnescapeDataString(part);
                return string.IsNullOrEmpty(decoded) ? null : decoded;
            }).ToList();
            if (parts.Count != 5)
            {
                return null;
            }

            var gridType = DataGridType.FromName(parts[0]);
            if (gridType == null)
            {
                return null;
            }
            var gridId = new DataGridId(gridType, parts[1]);
            var viewName = new ViewGroupId(parts[2]).ViewName(parts[3]);
            return new DataSourceId(gridId, viewName, parts[4]);
        }
    }
}
