using System;
using System.ComponentModel;

namespace pwiz.Common.DataBinding
{
    public abstract class DataPropertyDescriptor : PropertyDescriptor
    {
        protected DataPropertyDescriptor(string name, IColumnCaption caption, DataSchemaLocalizer dataSchemaLocalizer, Attribute[] attrs) : base(name, attrs)
        {
            ColumnCaption = caption;
            DataSchemaLocalizer = dataSchemaLocalizer;
        }

        public void SetAttributes(Attribute[] newAttributes)
        {
            AttributeArray = newAttributes;
        }

        public IColumnCaption ColumnCaption { get; private set; }
        public DataSchemaLocalizer DataSchemaLocalizer { get; private set; }

        public override string DisplayName
        {
            get { return ColumnCaption.GetCaption(DataSchemaLocalizer); }
        }

        protected bool Equals(DataPropertyDescriptor other)
        {
            return base.Equals(other)
                   && Equals(ColumnCaption, other.ColumnCaption)
                   && Equals(DataSchemaLocalizer, other.DataSchemaLocalizer);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DataPropertyDescriptor) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (ColumnCaption != null ? ColumnCaption.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (DataSchemaLocalizer != null ? DataSchemaLocalizer.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
