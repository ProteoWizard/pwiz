using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// PropertyDescriptor which uses a <see cref="ColumnDescriptor"/> to obtain the property value.
    /// </summary>
    public class ColumnPropertyDescriptor : PropertyDescriptor
    {
        public ColumnPropertyDescriptor(ColumnDescriptor columnDescriptor) : base(columnDescriptor.IdPath.ToString(), new Attribute[0])
        {
            ColumnDescriptor = columnDescriptor;
        }

        public ColumnDescriptor ColumnDescriptor { get; private set; }
        public override bool CanResetValue(object component)
        {
            return false;
        }

        public override object GetValue(object component)
        {
            return ColumnDescriptor.GetPropertyValue(component);
        }

        public override void ResetValue(object component)
        {
            throw new NotImplementedException();
        }

        public override void SetValue(object component, object value)
        {
            throw new NotImplementedException();
        }

        public override bool ShouldSerializeValue(object component)
        {
            throw new NotImplementedException();
        }

        public override Type ComponentType
        {
            get { return typeof(object); }
        }

        public override bool IsReadOnly
        {
            get { return true; }
        }

        public override Type PropertyType
        {
            get { return ColumnDescriptor.PropertyType; }
        }

        public override string DisplayName
        {
            get { return ColumnDescriptor.DisplayName; }
        }
    }
}
