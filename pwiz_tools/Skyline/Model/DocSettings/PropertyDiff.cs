using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using pwiz.Common.DataBinding;

namespace pwiz.Skyline.Model.DocSettings
{
    public class Property
    {
        private readonly DiffAttributeBase diffAttribute;

        public Property(PropertyInfo propertyInfo, DiffAttributeBase diffAttribute)
        {
            PropertyInfo = propertyInfo;
            this.diffAttribute = diffAttribute;
        }

        public PropertyInfo PropertyInfo { get; private set; }

        public string Name
        {
            get { return diffAttribute.PropertyName ?? PropertyInfo.Name; }
        }

        public bool Diff
        {
            get { return diffAttribute is DiffAttribute; }
        }
    }

    public abstract class PropertyDiff
    {
        protected PropertyDiff(Property property, PropertyPath propertyPath, DateTime time)
        {
            Property = property;
            PropertyPath = propertyPath;
            Time = time;
        }

        public abstract override string ToString();

        public abstract List<AuditLogRow> CreateRows();

        public Property Property { get; protected set; }
        public PropertyPath PropertyPath { get; protected set; }
        public DateTime Time { get; protected set; }
    }

    public class PropertyValueChangedDiff : PropertyDiff
    {
        public PropertyValueChangedDiff(Property property, PropertyPath propertyPath, DateTime time, object oldValue, object newValue) : base(property, propertyPath, time)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        public override string ToString()
        {
            return string.Format("{0} changed from \"{1}\" to \"{2}\"", PropertyPath, OldValue, NewValue); // Not L10N
        }

        public override List<AuditLogRow> CreateRows()
        {
            return new List<AuditLogRow> { new AuditLogRow(PropertyPath, Time, OldValue, NewValue) };
        }

        public object OldValue { get; private set; }
        public object NewValue { get; private set; }
    }

    public abstract class CollectionDiff : PropertyDiff
    {
        protected CollectionDiff(Property property, PropertyPath propertyPath, DateTime time, List<KeyValuePair<object, object>> elements) : base(property, propertyPath, time)
        {
            Elements = elements;
        }


        public List<KeyValuePair<object, object>> Elements { get; protected set; }
    }

    public class CollectionElementsAddedDiff : CollectionDiff
    {
        public CollectionElementsAddedDiff(Property property, PropertyPath propertyPath, DateTime time,
            List<KeyValuePair<object, object>> elements)
            : base(property, propertyPath, time, elements)
        {
        }

        public override List<AuditLogRow> CreateRows()
        {
            return Elements.Select(e => new AuditLogRow(PropertyPath.LookupByKey(e.Key.ToString()), Time, null, e.Value)).ToList();
        }

        public override string ToString()
        {
            return string.Format("[{0}] were added to {1}", string.Join(", ", Elements), PropertyPath); // Not L10N
        }
    }

    public class CollectionElementsRemovedDiff : CollectionDiff
    {
        public CollectionElementsRemovedDiff(Property property, PropertyPath propertyPath, DateTime time,
            List<KeyValuePair<object, object>> elements)
            : base(property, propertyPath, time, elements)
        {
        }

        public override List<AuditLogRow> CreateRows()
        {
            return Elements.Select(e => new AuditLogRow(PropertyPath.LookupByKey(e.Key.ToString()), Time, e.Value, null)).ToList();
        }

        public override string ToString()
        {
            return string.Format("[{0}] were removed from {1}", string.Join(", ", Elements), PropertyPath); // Not L10N
        }
    }
}