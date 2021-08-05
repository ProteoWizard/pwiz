using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using pwiz.Common.DataBinding.Attributes;

namespace pwiz.Common.DataBinding.Internal
{
    public class IndexedPropertyDescriptor : DataPropertyDescriptor
    {
        private readonly Type _propertyType;
        private readonly PivotedColumnId _pivotedColumnId;
        public IndexedPropertyDescriptor(DataSchema dataSchema, int index, PropertyDescriptor originalPropertyDescriptor, IColumnCaption displayName)
            : this(dataSchema, index, originalPropertyDescriptor.PropertyType, displayName, null, GetAttributes(originalPropertyDescriptor))
        {
        }

        public IndexedPropertyDescriptor(DataSchema dataSchema, int index, Type propertyType, IColumnCaption displayName, PivotedColumnId pivotedColumnId, Attribute[] attributes)
            : base(@"property" + index, displayName, dataSchema.DataSchemaLocalizer, MergeAttributes(dataSchema, displayName, attributes))
        {
            PropertyIndex = index;
            _propertyType = propertyType;
            _pivotedColumnId = pivotedColumnId;
        }

        public int PropertyIndex { get; private set; }

        public override bool CanResetValue(object component)
        {
            return false;
        }

        public override Type ComponentType
        {
            get { return typeof(RowItem); }
        }

        public override object GetValue(object component)
        {
            var rowItem = component as RowItem;
            if (rowItem == null)
            {
                return null;
            }
            var list = rowItem.Value as IList<object>;
            if (list == null)
            {
                return null;
            }
            if (PropertyIndex < 0 || PropertyIndex >= list.Count)
            {
                return null;
            }
            return list[PropertyIndex];
        }

        public override bool IsReadOnly
        {
            get { return true; }
        }

        public override Type PropertyType
        {
            get { return _propertyType; }
        }

        public override void ResetValue(object component)
        {
            throw new InvalidOperationException();
        }

        public override void SetValue(object component, object value)
        {
            throw new InvalidOperationException();
        }

        public override bool ShouldSerializeValue(object component)
        {
            return false;
        }

        public override PivotedColumnId PivotedColumnId => _pivotedColumnId;
        
        private static Attribute[] GetAttributes(PropertyDescriptor propertyDescriptor)
        {
            return propertyDescriptor.Attributes.OfType<Attribute>().ToArray();
        }

        private static Attribute[] MergeAttributes(DataSchema dataSchema, IColumnCaption columnCaption,
            Attribute[] existingAttributes)
        {
            var overrideAttributes = new Attribute[]
            {
                new DisplayNameAttribute(columnCaption.GetCaption(dataSchema.DataSchemaLocalizer)),
                new ColumnCaptionAttribute(columnCaption)
            };
            return AttributeCollection.FromExisting(new AttributeCollection(existingAttributes.ToArray()), overrideAttributes)
                .Cast<Attribute>().ToArray();
        }
    }
}
