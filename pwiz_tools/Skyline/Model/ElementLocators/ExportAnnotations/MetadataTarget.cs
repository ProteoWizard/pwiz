using System;
using System.Globalization;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.ElementLocators.ExportAnnotations
{
    public abstract class MetadataTarget
    {
        protected MetadataTarget(PropertyPath propertyPath)
        {
            PropertyPath = propertyPath;
        }
        public PropertyPath PropertyPath { get; }

        public abstract string DisplayName { get; }

        public abstract string GetFormattedValue(CultureInfo cultureInfo, object component);

        public abstract void SetValue(CultureInfo cultureInfo, object component, string value);


        public override string ToString()
        {
            return DisplayName;
        }

        public class Property : MetadataTarget
        {
            private ImportablePropertyInfo _propertyInfo;

            public Property(ImportablePropertyInfo propertyInfo) : base (PropertyPath.Root.Property(propertyInfo.PropertyDescriptor.Name))
            {
                _propertyInfo = propertyInfo;
            }

            public override string DisplayName
            {
                get { return _propertyInfo.PropertyDescriptor.DisplayName; }
            }

            public override string GetFormattedValue(CultureInfo cultureInfo, object component)
            {
                return _propertyInfo.FormatPropertyValue(cultureInfo,
                    _propertyInfo.PropertyDescriptor.GetValue(component));
            }

            public override void SetValue(CultureInfo cultureInfo, object component, string strValue)
            {
                object value = _propertyInfo.ParsePropertyValue(cultureInfo, strValue);
                _propertyInfo.PropertyDescriptor.SetValue(component, value);
            }
        }

        public class Annotation : MetadataTarget
        {
            private AnnotationDef _annotationDef;
            public Annotation(AnnotationDef annotationDef) : base(
                PropertyPath.Root.Property(AnnotationDef.ANNOTATION_PREFIX + annotationDef.Name))
            {
                _annotationDef = annotationDef;
            }

            public override string DisplayName
            {
                get { return _annotationDef.Name; }
            }

            public override string GetFormattedValue(CultureInfo cultureInfo, object component)
            {
                SkylineObject skylineObject = (SkylineObject) component;
                object value = skylineObject.GetAnnotation(_annotationDef);
                if (value == null)
                {
                    return string.Empty;
                }

                var iFormattable = value as IFormattable;
                if (iFormattable != null)
                {
                    return iFormattable.ToString(null, cultureInfo);
                }

                return value.ToString();
            }

            public override void SetValue(CultureInfo cultureInfo, object component, string value)
            {
                throw new NotImplementedException();
            }
        }

        public class Chained : MetadataTarget
        {
            private Func<object, object> _componentMapper;
            private MetadataTarget _child;

            public Chained(PropertyPath propertyPath, Func<object, object> componentMapper, MetadataTarget child) :
                base(propertyPath.Concat(child.PropertyPath))
            {
                _componentMapper = componentMapper;
                _child = child;
            }

            public override string DisplayName
            {
                get { return _child.DisplayName; }
            }

            public override string GetFormattedValue(CultureInfo cultureInfo, object component)
            {
                var childComponent = _componentMapper(component);
                if (childComponent == null)
                {
                    return string.Empty;
                }

                return _child.GetFormattedValue(cultureInfo, childComponent);
            }

            public override void SetValue(CultureInfo cultureInfo, object component, string value)
            {
                var childComponent = _componentMapper(component);
                if (childComponent == null)
                {
                    throw new ArgumentNullException();
                }

                _child.SetValue(cultureInfo, childComponent, value);
            }
        }
    }
}
