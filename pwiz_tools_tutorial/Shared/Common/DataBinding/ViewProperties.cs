using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace pwiz.Common.DataBinding
{
    public class ViewProperties : PropertyDescriptorCollection
    {
        public const string ColumnNamePrefix = "COLUMN_";
        private const string DisplayColumnPrefix = "DISPLAYCOLUMN_";
        public ViewProperties(ViewInfo viewInfo, IDictionary<IdentifierPath, ICollection<PivotKey>> pivotKeys) 
            : this(viewInfo, new DisplayColumnProperties(viewInfo, pivotKeys))
        {
            
        }

        private ViewProperties(ViewInfo viewInfo, DisplayColumnProperties displayColumnProperties)
            : base(displayColumnProperties.PropertyDescriptorArray, true)
        {
            ViewInfo = viewInfo;
        }

        public ViewInfo ViewInfo
        {
            get; private set;
        }

        public override PropertyDescriptor Find(string name, bool ignoreCase)
        {
            if (!name.StartsWith(ColumnNamePrefix))
            {
                return base.Find(name, ignoreCase);
            }
            var identifierPath = IdentifierPath.Parse(name.Substring(ColumnNamePrefix.Length));
            PivotKey pivotKey;
            var columnDescriptor = FindColumn(identifierPath, out pivotKey);
            if (columnDescriptor == null)
            {
                return base.Find(name, ignoreCase);
            }
            return new ColumnPropertyDescriptor(new DisplayColumn(
                ViewInfo, new ColumnSpec(), columnDescriptor), name);
        }

        private ColumnDescriptor FindColumn(IdentifierPath identifierPath, out PivotKey pivotKey)
        {
            if (identifierPath.IsRoot)
            {
                pivotKey = null;
                return ViewInfo.ParentColumn;
            }
            var parentColumn = FindColumn(identifierPath.Parent, out pivotKey);
            if (parentColumn == null)
            {
                return null;
            }
            var column = parentColumn.ResolveChild(identifierPath.Name);
            if (column != null)
            {
                return column;
            }
            column = parentColumn.ResolveChild(null);
            if (column != null)
            {
                pivotKey = PivotKey.OfValues(pivotKey, IdentifierPath.Root, new[] {
                    new KeyValuePair<IdentifierPath, object>(column.IdPath, 
                        Convert.ChangeType(identifierPath.Name, column.CollectionInfo.KeyType))});
                return column;
            }
            return null;
        }

        class DisplayColumnProperties
        {
            public DisplayColumnProperties(ViewInfo viewInfo, IDictionary<IdentifierPath, ICollection<PivotKey>> pivotKeysByColumn)
            {
                var propertyDescriptors = new List<PropertyDescriptor>();
                var displayColumnsByKey = new Dictionary<PivotKey, List<DisplayColumn>>();
                var columnNames = new HashSet<string>();
                foreach (var displayColumn in viewInfo.DisplayColumns)
                {
                    if (displayColumn.ColumnSpec.Hidden)
                    {
                        continue;
                    }
                    ICollection<PivotKey> pivotKeys = GetPivotKeys(pivotKeysByColumn, displayColumn);
                    if (pivotKeys != null)
                    {
                        List<DisplayColumn> columns;
                        foreach (var value in pivotKeys)
                        {
                            if (!displayColumnsByKey.TryGetValue(value, out columns))
                            {
                                columns = new List<DisplayColumn>();
                                displayColumnsByKey.Add(value, columns);
                            }
                            columns.Add(displayColumn);
                        }
                    }
                    else
                    {
                        string propertyName = MakeUniqueName(columnNames, null,
                                                             displayColumn.IdentifierPath);
                        propertyDescriptors.Add(new ColumnPropertyDescriptor(displayColumn, propertyName, displayColumn.IdentifierPath, null));
                    }
                }
                var allPivotKeys = displayColumnsByKey.Keys.ToArray();
                Array.Sort(allPivotKeys, PivotKey.GetComparer(viewInfo.DataSchema));
                foreach (var pivotKey in allPivotKeys)
                {
                    foreach (var displayColumn in displayColumnsByKey[pivotKey])
                    {
                        var identifierPath = PivotKey.QualifyIdentifierPath(pivotKey, displayColumn.IdentifierPath);
                        var columnName = MakeUniqueName(columnNames, null, identifierPath);
                        propertyDescriptors.Add(new ColumnPropertyDescriptor(displayColumn, columnName, identifierPath, pivotKey));
                    }
                }
                PropertyDescriptorArray = propertyDescriptors.ToArray();
            }
            private ICollection<PivotKey> GetPivotKeys(IDictionary<IdentifierPath, ICollection<PivotKey>> pivotKeyDict, DisplayColumn displayColumn)
            {
                ICollection<PivotKey> result = null;
                IdentifierPath longestId = null;
                foreach (var entry in pivotKeyDict)
                {
                    if (longestId != null && longestId.Length > entry.Key.Length)
                    {
                        continue;
                    }
                    if (!displayColumn.IdentifierPath.StartsWith(entry.Key))
                    {
                        continue;
                    }
                    longestId = entry.Key;
                    result = entry.Value;
                }
                return result;
            }
            public PropertyDescriptor[] PropertyDescriptorArray { get; private set; }
        }
        static string MakeUniqueName(HashSet<string> columnNames, IAggregateFunction aggregateFunction, IdentifierPath identifierPath)
        {
            string baseName = DisplayColumnPrefix + identifierPath;

            if (aggregateFunction != null && AggregateFunctions.GroupBy != aggregateFunction)
            {
                baseName = aggregateFunction.Name + "_" + baseName;
            }
            var columnName = baseName;
            for (int index = 1; columnNames.Contains(columnName); index++)
            {
                columnName = baseName + identifierPath + index;
            }
            columnNames.Add(columnName);
            return columnName;
        }

    }
}
