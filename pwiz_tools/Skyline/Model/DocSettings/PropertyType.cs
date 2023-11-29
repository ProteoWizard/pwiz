using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Lists;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.DocSettings
{
    public class ListPropertyType
    {
        public const string LookupPrefix = "LOOKUP:";
        public static readonly ListPropertyType TEXT = new ListPropertyType(AnnotationDef.AnnotationType.text, null);
        public static readonly ListPropertyType NUMBER = new ListPropertyType(AnnotationDef.AnnotationType.number, null);
        public static readonly ListPropertyType TRUE_FALSE = new ListPropertyType(AnnotationDef.AnnotationType.true_false, null);
        private static readonly ImmutableList<ListPropertyType> ScalarPropertyTypes = ImmutableList.ValueOf(new[]
        {
            TEXT,
            NUMBER,
            TRUE_FALSE,
            new ListPropertyType(AnnotationDef.AnnotationType.value_list, null),
        });
        public ListPropertyType(AnnotationDef.AnnotationType annotationType, string lookup)
        {
            AnnotationType = annotationType;
            Lookup = lookup;
        }

        public AnnotationDef.AnnotationType AnnotationType { get; private set; }
        public string Lookup { get; private set; }

        public string Label
        {
            get
            {
                if (string.IsNullOrEmpty(Lookup))
                {
                    return GetAnnotationTypeName(AnnotationType);
                }
                return Resources.ListPropertyType_Label_Lookup__ + Lookup;
            }
        }

        public override string ToString()
        {
            return Label;
        }

        public ListPropertyType Self { get { return this; } }

        public string Key
        {
            get
            {
                if (string.IsNullOrEmpty(Label))
                {
                    return AnnotationType.ToString();
                }
                return LookupPrefix + Lookup;
            }
        }

        public static string GetAnnotationTypeName(AnnotationDef.AnnotationType annotationType)
        {
            switch (annotationType)
            {
                case AnnotationDef.AnnotationType.text:
                    return Resources.ListPropertyType_GetAnnotationTypeName_Text;
                case AnnotationDef.AnnotationType.number:
                    return Resources.ListPropertyType_GetAnnotationTypeName_Number;
                case AnnotationDef.AnnotationType.true_false:
                    return Resources.ListPropertyType_GetAnnotationTypeName_True_False;
                case AnnotationDef.AnnotationType.value_list:
                    return Resources.ListPropertyType_GetAnnotationTypeName_Value_List;
                default:
                    return annotationType.ToString();
            }
        }

        protected bool Equals(ListPropertyType other)
        {
            return string.Equals(Lookup, other.Lookup) && AnnotationType == other.AnnotationType;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ListPropertyType)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Lookup != null ? Lookup.GetHashCode() : 0) * 397) ^ (int) AnnotationType;
            }
        }

        public static IEnumerable<ListPropertyType> ListPropertyTypes(IEnumerable<ListDef> listDefs)
        {
            var propertyTypes = new List<ListPropertyType>(ScalarPropertyTypes);
            foreach (var listDef in listDefs)
            {
                var idColumn = listDef.IdPropertyDef;
                if (null != idColumn)
                {
                    propertyTypes.Add(new ListPropertyType(idColumn.Type, listDef.Name));
                }
            }
            return propertyTypes;
        }

        public static IEnumerable<ListPropertyType> ListPropertyTypes()
        {
            return ListPropertyTypes(Settings.Default.ListDefList.Select(list=>list.ListDef));
        }
    }
}
