using System;

namespace pwiz.Common.DataBinding.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
    public class InvariantDisplayNameAttribute : InUiModesAttribute
    {
        public InvariantDisplayNameAttribute(string displayName)
        {
            InvariantDisplayName = displayName;
        }
        public string InvariantDisplayName { get; private set; }
        public ColumnCaption ColumnCaption { get {return new ColumnCaption(InvariantDisplayName);} }
    }
}
