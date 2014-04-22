using System;

namespace pwiz.Common.DataBinding.Attributes
{
    public class InvariantDisplayNameAttribute : Attribute
    {
        public InvariantDisplayNameAttribute(string displayName)
        {
            InvariantDisplayName = displayName;
        }
        public string InvariantDisplayName { get; private set; }
        public ColumnCaption ColumnCaption { get {return new ColumnCaption(InvariantDisplayName);} }
    }
}
