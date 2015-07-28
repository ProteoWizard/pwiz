using System;

namespace pwiz.Common.DataBinding
{
    public class ViewGroupInfo
    {
        private readonly Func<String> _getLabelFunc;

        public ViewGroupInfo(string name) : this(name, null)
        {
            AllowGroupRename = true;
        }
        public ViewGroupInfo(string name, Func<String> getLabelFunc)
        {
            Name = name;
            _getLabelFunc = getLabelFunc;
        }

        private ViewGroupInfo(ViewGroupInfo viewGroupInfo)
        {
            Name = viewGroupInfo.Name;
            _getLabelFunc = viewGroupInfo._getLabelFunc;
            AllowGroupRename = viewGroupInfo.AllowGroupRename;
        }
        public string Name { get; private set; }
        public string Label { get { return _getLabelFunc == null ? Name : _getLabelFunc(); }}
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
