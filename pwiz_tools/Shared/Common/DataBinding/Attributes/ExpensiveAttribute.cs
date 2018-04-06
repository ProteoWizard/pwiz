using System;

namespace pwiz.Common.DataBinding.Attributes
{
    /// <summary>
    /// Use this attribute to indicate that a property is expensive to calculate.
    /// Double-clicking on a column header to resize will be disabled for this property
    /// and all descendents.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ExpensiveAttribute : Attribute
    {
    }
}
