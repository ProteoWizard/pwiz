using System;

namespace pwiz.Common.DataBinding.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class HideWhenAttribute : Attribute
    {
        public Type AncestorOfType { get; set; }
    }
}
