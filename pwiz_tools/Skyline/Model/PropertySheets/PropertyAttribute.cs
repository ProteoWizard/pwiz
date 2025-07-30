using System;
using System.Linq;

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
        public override string Category => PropertySheetResources.Category_FileProperties;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ChromatogramPropertyAttribute : PropertyAttribute
    {
        public override string Category => PropertySheetResources.Category_ChromatogramSet;
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

    [AttributeUsage(AttributeTargets.Property)]
    public class ContainsViewablePropertiesAttribute : Attribute
    {
        public Func<Type, bool> IsRelevantToType;

        public ContainsViewablePropertiesAttribute()
        {
            IsRelevantToType = (type) => true;
        }

        public ContainsViewablePropertiesAttribute(params Type[] relevantTypes)
        {
            IsRelevantToType = (type) => relevantTypes.Any(relevantType => relevantType.IsAssignableFrom(type));
        }
    }
}
