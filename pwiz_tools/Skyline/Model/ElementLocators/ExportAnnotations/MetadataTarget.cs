using System;
using System.ComponentModel;
using System.Globalization;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;

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

        public abstract bool IsImportable
        {
            get;
        }


        public override string ToString()
        {
            return DisplayName;
        }

        public class Property : MetadataTarget
        {
            private PropertyDescriptor _propertyDescriptor;
            private ImportablePropertyInfo _propertyInfo;

            public Property(ImportablePropertyInfo importablePropertyInfo) : this (importablePropertyInfo.PropertyDescriptor)
            {
                _propertyInfo = importablePropertyInfo;
            }

            public Property(PropertyDescriptor propertyDescriptor) : base (PropertyPath.Root.Property(propertyDescriptor.Name))
            {
                _propertyDescriptor = propertyDescriptor;
            }

            public override string DisplayName
            {
                get { return _propertyDescriptor.DisplayName; }
            }

            public override string GetFormattedValue(CultureInfo cultureInfo, object component)
            {
                var value = _propertyDescriptor.GetValue(component);
                if (_propertyInfo != null)
                {
                    return _propertyInfo.FormatPropertyValue(cultureInfo, value);
                }
                return FormatValue(cultureInfo, value);
            }

            public override void SetValue(CultureInfo cultureInfo, object component, string strValue)
            {
                object value = _propertyInfo.ParsePropertyValue(cultureInfo, strValue);
                _propertyInfo.PropertyDescriptor.SetValue(component, value);
            }

            public override bool IsImportable
            {
                get { return _propertyInfo != null; }
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
                return FormatValue(cultureInfo, value);
            }

            public override void SetValue(CultureInfo cultureInfo, object component, string value)
            {
                throw new NotImplementedException();
            }

            public override bool IsImportable
            {
                get { return true; }
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

            public override bool IsImportable
            {
                get { return _child.IsImportable; }
            }
        }

        public static string FormatValue(CultureInfo cultureInfo, object value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            if (value is double d)
            {
                return d.ToString(Formats.RoundTrip, cultureInfo);
            }
            return (string)Convert.ChangeType(value, typeof(string), cultureInfo);
        }
    }
}
