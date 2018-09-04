using System;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    public class ViewGroupInfo : LabeledValues<string>
    {
        public ViewGroupInfo(string name) : this(name, null)
        {
            AllowGroupRename = true;
        }
        public ViewGroupInfo(string name, Func<String> getLabelFunc) : base (name, getLabelFunc)
        {
        }

        private ViewGroupInfo(ViewGroupInfo viewGroupInfo) : this(viewGroupInfo.Name, viewGroupInfo._getLabel)
        {
            AllowGroupRename = viewGroupInfo.AllowGroupRename;
        }

        public override string Label
        {
            get
            {
                if (_getLabel == null)
                    return Name;
                return _getLabel();
            }
        }

        public bool AllowGroupRename { get; private set; }

        public ViewGroupInfo ChangeAllowRename(bool allowRename)
        {
            return new ViewGroupInfo(this) {AllowGroupRename = allowRename};
        }

        public override string ToString()
        {
            return Label;
        }
    }
}
