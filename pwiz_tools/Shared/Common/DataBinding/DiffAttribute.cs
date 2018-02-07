using System;
using System.Runtime.CompilerServices;

namespace pwiz.Common.DataBinding
{
    public abstract class DiffAttributeBase : Attribute
    {
        protected DiffAttributeBase(string name)
        {
            PropertyName = name;
        }

        public string PropertyName { get; protected set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DiffAttribute : DiffAttributeBase
    {
        public DiffAttribute([CallerMemberName] string name = null) : base(name) { }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DiffParentAttribute : DiffAttributeBase
    {
        public DiffParentAttribute([CallerMemberName] string name = null) : base(name) { }
    }
}
