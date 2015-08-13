using System;
using System.Collections;
using System.Collections.Generic;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding
{
    public class RowSourceInfo
    {
        private IEnumerable _rows;
        private Func<IEnumerable> _rowProvider;
        public RowSourceInfo(Type rowType, IEnumerable rows, IEnumerable<ViewInfo> views)
        {
            RowType = rowType;
            _rows = rows;
            Views = ImmutableList.ValueOf(views);
            DisplayName = rowType.Name;
            Name = rowType.FullName;
        }

        public RowSourceInfo(IEnumerable rows, ViewInfo viewInfo)
            : this(viewInfo.ParentColumn.PropertyType, rows, new[] {viewInfo})
        {
        }

        public static RowSourceInfo DeferredRowSourceInfo<T>(Func<IEnumerable<T>> rowProvider, ViewInfo view)
        {
            return new RowSourceInfo(typeof(T), null, new[]{view})
            {
                _rowProvider = rowProvider
            };
        }

        public string Name { get; private set; }
        public string DisplayName { get; private set; }
        public Type RowType { get; private set; }

        public IEnumerable Rows
        {
            get
            {
                if (_rows == null)
                {
                    _rows = _rowProvider();
                }
                return _rows;
            }
        }
        public IList<ViewInfo> Views { get; private set; }
    }
}
