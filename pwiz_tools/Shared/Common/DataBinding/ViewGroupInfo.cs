using System;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    public class ViewGroupInfo : NamedValues<string>
    {
        public ViewGroupInfo(string value) : this(value, null)
        {
            AllowGroupRename = true;
        }
        public ViewGroupInfo(string value, Func<String> getLabelFunc) : base (value, getLabelFunc)
        {
        }

        private ViewGroupInfo(ViewGroupInfo viewGroupInfo) : this(viewGroupInfo.Value, viewGroupInfo._getName)
        {
            AllowGroupRename = viewGroupInfo.AllowGroupRename;
        }

        public override string Name
        {
            get
            {
                if (_getName == null)
                    return Value;
                return _getName();
            }
        }

        public bool AllowGroupRename { get; private set; }

        public ViewGroupInfo ChangeAllowRename(bool allowRename)
        {
            return new ViewGroupInfo(this) {AllowGroupRename = allowRename};
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
