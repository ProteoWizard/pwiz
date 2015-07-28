using System;
using pwiz.Common.DataBinding;

namespace pwiz.Topograph.ui.DataBinding
{
    public class TopographViewGroup : ViewGroup
    {
        public TopographViewGroup(Type rowType) : base(GetGroupId(rowType).Name, () => "Views")
        {
            RowType = rowType;
        }

        public Type RowType { get; private set; }

        public static ViewGroupId GetGroupId(Type rowType)
        {
            return new ViewGroupId(rowType.FullName);
        }

        public static ViewGroupId GetGroupId(ViewSpec viewSpec)
        {
            return new ViewGroupId(viewSpec.RowSource);
        }
    }
}
