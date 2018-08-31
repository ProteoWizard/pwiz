using System;

namespace pwiz.Common.DataBinding.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class HideWhenAttribute : Attribute
    {
        public Type AncestorOfType
        {
            get
            {
                if (AncestorsOfAnyOfTheseTypes != null && AncestorsOfAnyOfTheseTypes.Length > 0)
                {
                    return AncestorsOfAnyOfTheseTypes[0];
                }
                return null;
            }
            set { AncestorsOfAnyOfTheseTypes = new[] {value}; }
        }

        public Type[] AncestorsOfAnyOfTheseTypes { get; set; }
    }
}
