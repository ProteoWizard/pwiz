using System;
using System.Collections.Generic;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding
{
    public class RowSourceInfo
    {
        private IRowSource _rows;
        public RowSourceInfo(IRowSource rows, ViewInfo viewInfo)
            : this(viewInfo.ParentColumn.PropertyType, rows, new[] { viewInfo })
        {
        }

        public RowSourceInfo(Type rowType, IRowSource rows, IEnumerable<ViewInfo> views) : this(rowType, rows, views, rowType.FullName, rowType.Name)
        {
        }

        public RowSourceInfo(Type rowType, IRowSource rows, IEnumerable<ViewInfo> views, string fullName,
            string displayName)
        {
            RowType = rowType;
            _rows = rows;
            Views = ImmutableList.ValueOf(views);
            Name = fullName;
            DisplayName = displayName;
        }

        public string Name { get; private set; }
        public string DisplayName { get; private set; }
        public Type RowType { get; private set; }

        public IRowSource Rows
        {
            get
            {
                return _rows;
            }
        }
        public IList<ViewInfo> Views { get; private set; }
    }
}
