using System;

namespace pwiz.Skyline.Model.PropertySheets
{
    #region Property Attributes - Used to flag properties to be included in property sheets

    [AttributeUsage(AttributeTargets.Property)]
    public abstract class PropertyAttribute : Attribute
    {
        public abstract string Category { get; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class FilePropertyAttribute : PropertyAttribute
    {
        public override string Category => PropertySheetResources.FileProperties;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ChromatogramPropertyAttribute : PropertyAttribute
    {
        public override string Category => PropertySheetResources.ChromatogramSet;
    }

    #endregion

    #region Property Type Attributes - Used to flag how a properties value is retrieved for the property sheet

    [AttributeUsage(AttributeTargets.Property)]
    public abstract class PropertyTypeAttribute : Attribute
    {
        public abstract Type Type { get; }
        public abstract object GetValue(object obj);
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class StringPropertyTypeAttribute : PropertyTypeAttribute
    {
        public override Type Type => typeof(string);
        public override object GetValue(object obj)
        {
            return obj?.ToString();
        }
    }

    #endregion

    #region Recursive property search

    // Searches for properties recursively but still displays them at top level in the property sheet.
    [AttributeUsage(AttributeTargets.Property)]
    public class ContainsViewablePropertiesAttribute : Attribute { }

    // Searches for properties recursively and displays them in a nested list view.
    [AttributeUsage(AttributeTargets.Property)]
    public class ListContainsViewablePropertiesAttribute : Attribute
    {
        public string Category { get; }

        public ListContainsViewablePropertiesAttribute(string category)
        {
            Category = category;
        }
    }

    #endregion

    #region Enables property editing in the property sheet

    [AttributeUsage(AttributeTargets.Property)]
    public class EditableInPropertySheetAttribute : Attribute { }

    #endregion
}
