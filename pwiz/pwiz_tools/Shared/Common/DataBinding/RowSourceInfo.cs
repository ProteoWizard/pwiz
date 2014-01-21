using System;
using System.Collections;
using System.Collections.Generic;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding
{
    public class RowSourceInfo
    {
        public RowSourceInfo(Type rowType, IEnumerable rows, IEnumerable<ViewInfo> views)
        {
            RowType = rowType;
            Rows = rows;
            Views = ImmutableList.ValueOf(views);
            DisplayName = rowType.Name;
            Name = rowType.FullName;
        }

        public RowSourceInfo(IEnumerable rows, ViewInfo viewInfo)
            : this(viewInfo.ParentColumn.PropertyType, rows, new[] {viewInfo})
        {
        }

        public string Name { get; private set; }
        public string DisplayName { get; private set; }
        public Type RowType { get; private set; }
        public IEnumerable Rows { get; private set; }
        public IList<ViewInfo> Views { get; private set; }
    }
}
