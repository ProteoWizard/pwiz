using System;
using pwiz.Common.Properties;

namespace pwiz.Common.DataBinding
{
    public struct ViewGroupId
    {
        private readonly string _name;
        public ViewGroupId(string name) : this()
        {
            _name = string.IsNullOrEmpty(name) ? null : name;
        }
        public string Name { get { return _name ?? string.Empty; } }
        public override string ToString()
        {
            return Name;
        }

        public ViewName ViewName(string viewName)
        {
            return new ViewName(this, viewName);
        }
    }

    public class ViewGroup
    {
        public static readonly ViewGroup BUILT_IN = new ViewGroup(string.Empty, ()=>Resources.ViewGroup_BUILT_IN_Built_In_Views);
        private readonly Func<string> _getLabelFunc;
        public ViewGroup(string name, Func<string> getLabelFunc)
        {
            Id = new ViewGroupId(name);
            _getLabelFunc = getLabelFunc;
        }

        public ViewGroupId Id { get; private set; }

        public string Label
        {
            get { return _getLabelFunc(); }
        }

        public override string ToString()
        {
            return Label;
        }
    }

    public struct ViewName
    {
        public ViewName(ViewGroupId groupId, string name) : this()
        {
            GroupId = groupId;
            Name = name;
        }

        public ViewGroupId GroupId { get; private set; }
        public String Name { get; private set; }
        public override string ToString()
        {
            return GroupId.ToString() + '.' + Name;
        }

        public static ViewName? Parse(string str)
        {
            if (str == null)
            {
                return null;
            }
            int ichDot = str.IndexOf('.');
            if (ichDot < 0)
            {
                return new ViewName(new ViewGroupId(str), null);
            }
            return new ViewName(new ViewGroupId(str.Substring(0, ichDot)), str.Substring(ichDot + 1));
        }
    }
}
