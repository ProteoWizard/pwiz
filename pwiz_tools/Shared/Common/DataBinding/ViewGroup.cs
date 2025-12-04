using System;
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    public struct ViewGroupId : IEquatable<ViewGroupId>
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

        public bool Equals(ViewGroupId other)
        {
            return _name == other._name;
        }

        public override bool Equals(object obj)
        {
            return obj is ViewGroupId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (_name != null ? _name.GetHashCode() : 0);
        }
    }

    public class ViewGroup : LabeledValues<string>
    {
        public static readonly ViewGroup BUILT_IN = new ViewGroup(string.Empty, ()=>Resources.ViewGroup_BUILT_IN_Built_In_Views);

        public ViewGroup(string name, Func<string> getLabelFunc) : base (name, getLabelFunc)
        {
            Id = new ViewGroupId(name);
        }

        public ViewGroupId Id { get; private set; }

        public override string ToString()
        {
            return Label;
        }
    }

    public struct ViewName : IEquatable<ViewName>
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

        public bool Equals(ViewName other)
        {
            return GroupId.Equals(other.GroupId) && Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            return obj is ViewName other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (GroupId.GetHashCode() * 397) ^ (Name != null ? Name.GetHashCode() : 0);
            }
        }
    }
}
